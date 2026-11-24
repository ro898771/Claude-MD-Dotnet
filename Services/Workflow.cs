using System.IO;
using System.Text;
using System.Text.Json;

namespace ClaudeMd.Services;

/// <summary>One input or output of a block: a name and a data type (a link only joins ports of the same type, or "Any").</summary>
public sealed record Port(string Name, string Type);

/// <summary>
/// What a skill takes in and gives out, read from block.json next to its SKILL.md:
/// { "inputs": [ { "name": "gu_files", "type": "GU_Files" } ], "outputs": [ { "name": "gu_rows", "type": "ParsedRows" } ] }.
/// A skill without block.json is one generic block: a single "input" and a single "output", both of type Any.
/// </summary>
public sealed class BlockContract
{
    public const string FileName = "block.json";
    public const string AnyType = "Any";

    public List<Port> Inputs { get; } = new();
    public List<Port> Outputs { get; } = new();
    public bool HasContract { get; private set; }
    public string Problem { get; private set; } = "";

    public static string PathFor(string skill) => Path.Combine(AppPaths.StorageDir, skill, FileName);
    public static bool Exists(string skill) => File.Exists(PathFor(skill));
    public static bool SkillExists(string skill) => File.Exists(Path.Combine(AppPaths.StorageDir, skill, SkillStore.SkillFile));

    public static BlockContract Load(string skill)
    {
        var c = new BlockContract();
        var path = PathFor(skill);
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                ReadPorts(doc.RootElement, "inputs", c.Inputs);
                ReadPorts(doc.RootElement, "outputs", c.Outputs);
                c.HasContract = true;
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
            {
                c.Problem = "block.json cannot be read: " + e.Message;
                c.Inputs.Clear(); c.Outputs.Clear();
            }
        }
        if (!c.HasContract)
        {
            c.Inputs.Add(new Port("input", AnyType));
            c.Outputs.Add(new Port("output", AnyType));
        }
        return c;
    }

    private static void ReadPorts(JsonElement root, string key, List<Port> into)
    {
        if (!root.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array) return;
        foreach (var p in array.EnumerateArray())
        {
            var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
            var type = p.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name) && !into.Any(x => x.Name == name!.Trim()))
                into.Add(new Port(name!.Trim(), string.IsNullOrWhiteSpace(type) ? AnyType : type!.Trim()));
        }
    }
}

public sealed class WfNode
{
    public string Id { get; set; } = "";
    public string Skill { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class WfLink
{
    public string FromNode { get; set; } = "";
    public string FromPort { get; set; } = "";
    public string ToNode { get; set; } = "";
    public string ToPort { get; set; } = "";
}

/// <summary>The saved workflow (workflow.json): the blocks with their place on the canvas, and the links between ports.</summary>
public sealed class WfFile
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public List<WfNode> Nodes { get; set; } = new();
    public List<WfLink> Links { get; set; } = new();

    public WfFile Clone() => new()
    {
        Version = Version, Name = Name,
        Nodes = Nodes.Select(n => new WfNode { Id = n.Id, Skill = n.Skill, X = n.X, Y = n.Y }).ToList(),
        Links = Links.Select(l => new WfLink { FromNode = l.FromNode, FromPort = l.FromPort, ToNode = l.ToNode, ToPort = l.ToPort }).ToList(),
    };
}

/// <summary>Level is PASS, INFO, WARN or FAIL. Only FAIL blocks generating.</summary>
public sealed record Finding(string Level, string Message);

public static class WorkflowRules
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Two ports can be linked when their types are equal (any letter case) or one of them is "Any".</summary>
    public static bool TypesMatch(string a, string b) =>
        a.Equals(BlockContract.AnyType, StringComparison.OrdinalIgnoreCase) || b.Equals(BlockContract.AnyType, StringComparison.OrdinalIgnoreCase)
        || a.Equals(b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The blocks in run order (a block comes after every block that feeds it; ties by top-to-bottom, then left-to-right). Null when the links form a loop.</summary>
    public static List<WfNode>? Order(WfFile wf)
    {
        var byId = wf.Nodes.ToDictionary(n => n.Id);
        var inDegree = wf.Nodes.ToDictionary(n => n.Id, _ => 0);
        var links = wf.Links.Where(l => byId.ContainsKey(l.FromNode) && byId.ContainsKey(l.ToNode)).ToList();
        foreach (var l in links) inDegree[l.ToNode]++;

        var ready = wf.Nodes.Where(n => inDegree[n.Id] == 0).ToList();
        var result = new List<WfNode>();
        while (ready.Count > 0)
        {
            ready = ready.OrderBy(n => n.Y).ThenBy(n => n.X).ToList();
            var next = ready[0]; ready.RemoveAt(0);
            result.Add(next);
            foreach (var l in links.Where(l => l.FromNode == next.Id))
                if (--inDegree[l.ToNode] == 0) ready.Add(byId[l.ToNode]);
        }
        return result.Count == wf.Nodes.Count ? result : null;
    }

    public static List<Finding> Validate(WfFile wf)
    {
        var f = new List<Finding>();
        if (wf.Nodes.Count == 0) { f.Add(new("FAIL", "The canvas has no blocks.")); return f; }
        f.Add(new("PASS", $"{wf.Nodes.Count} block(s), {wf.Links.Count} link(s)."));

        var contracts = new Dictionary<string, BlockContract>();
        foreach (var n in wf.Nodes)
        {
            var c = BlockContract.Load(n.Skill);
            contracts[n.Id] = c;
            if (!BlockContract.SkillExists(n.Skill)) f.Add(new("FAIL", $"Block {n.Id}: the skill '{n.Skill}' is not in Storage."));
            else if (c.Problem.Length > 0) f.Add(new("WARN", $"{n.Skill}: {c.Problem}"));
            else if (!c.HasContract) f.Add(new("WARN", $"{n.Skill} has no block.json: it is one generic block (input / output of type Any)."));
        }

        foreach (var l in wf.Links)
        {
            var from = wf.Nodes.FirstOrDefault(n => n.Id == l.FromNode);
            var to = wf.Nodes.FirstOrDefault(n => n.Id == l.ToNode);
            var label = $"{l.FromNode}.{l.FromPort} -> {l.ToNode}.{l.ToPort}";
            if (from is null || to is null) { f.Add(new("FAIL", $"Link {label}: a block of this link is not on the canvas.")); continue; }
            var op = contracts[from.Id].Outputs.FirstOrDefault(p => p.Name == l.FromPort);
            var ip = contracts[to.Id].Inputs.FirstOrDefault(p => p.Name == l.ToPort);
            if (op is null) { f.Add(new("FAIL", $"Link {label}: '{from.Skill}' has no output '{l.FromPort}' (block.json changed?).")); continue; }
            if (ip is null) { f.Add(new("FAIL", $"Link {label}: '{to.Skill}' has no input '{l.ToPort}' (block.json changed?).")); continue; }
            if (!TypesMatch(op.Type, ip.Type)) f.Add(new("FAIL", $"Link {label}: type {op.Type} does not match {ip.Type}."));
        }

        foreach (var g in wf.Links.GroupBy(l => (l.ToNode, l.ToPort)).Where(g => g.Count() > 1))
            f.Add(new("FAIL", $"Input {g.Key.ToNode}.{g.Key.ToPort} has {g.Count()} links; an input takes only one."));

        if (Order(wf) is null) f.Add(new("FAIL", "The links form a loop. A workflow must run from start to end without going back."));
        else f.Add(new("PASS", "No loops: the blocks have a run order."));

        if (wf.Nodes.Count > 1)
            foreach (var n in wf.Nodes.Where(n => !wf.Links.Any(l => l.FromNode == n.Id || l.ToNode == n.Id)))
                f.Add(new("WARN", $"{n.Skill} ({n.Id}) is not linked to any other block."));

        foreach (var n in wf.Nodes)
            foreach (var p in contracts[n.Id].Inputs.Where(p => !wf.Links.Any(l => l.ToNode == n.Id && l.ToPort == p.Name)))
                f.Add(new("INFO", $"Input '{p.Name}' ({p.Type}) of {n.Skill} is not linked: the user is asked for it when the workflow runs."));

        foreach (var n in wf.Nodes)
            foreach (var p in contracts[n.Id].Outputs.Where(p => !wf.Links.Any(l => l.FromNode == n.Id && l.FromPort == p.Name)))
                f.Add(new("INFO", $"Output '{p.Name}' ({p.Type}) of {n.Skill} is a final result of the workflow."));

        if (wf.Nodes.Count > 8) f.Add(new("WARN", "More than 8 blocks: every skill is loaded when the workflow runs, so a long chain uses a lot of context."));
        return f;
    }

    /// <summary>"Dino-Dev-Add_Winpath" -> "Add_Winpath".</summary>
    public static string ShortName(string skill) => MainWindow.Split(skill).Child;

    public static string DefaultDescription(WfFile wf)
    {
        var order = Order(wf) ?? wf.Nodes;
        var names = order.Select(n => ShortName(n.Skill)).ToList();
        string list = names.Count switch
        {
            0 => "no skills",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
        };
        var text = $"Runs {list} in order. Use when asked to run this workflow.";
        if (text.Length > SkillRules.DescriptionSoftMax && names.Count > 2)
            text = $"Runs {names[0]} to {names[^1]} in order ({names.Count} skills). Use when asked to run this workflow.";
        return text;
    }

    /// <summary>
    /// The block.json of the generated workflow, so it can be a block itself: its inputs are the inputs nobody feeds (asked from the user),
    /// its outputs are the outputs nobody uses (the final results). A port name used twice gets the skill's short name in front.
    /// </summary>
    public static string BuildBlockJson(WfFile wf)
    {
        var order = Order(wf) ?? wf.Nodes;
        var contracts = wf.Nodes.ToDictionary(n => n.Id, n => BlockContract.Load(n.Skill));
        var ins = order.SelectMany(n => contracts[n.Id].Inputs.Where(p => !wf.Links.Any(l => l.ToNode == n.Id && l.ToPort == p.Name)).Select(p => (Node: n, Port: p))).ToList();
        var outs = order.SelectMany(n => contracts[n.Id].Outputs.Where(p => !wf.Links.Any(l => l.FromNode == n.Id && l.FromPort == p.Name)).Select(p => (Node: n, Port: p))).ToList();
        object Ports(List<(WfNode Node, Port Port)> all) => all
            .Select(x => new { name = all.Count(y => y.Port.Name == x.Port.Name) > 1 ? $"{ShortName(x.Node.Skill)}_{x.Port.Name}" : x.Port.Name, type = x.Port.Type }).ToList();
        return JsonSerializer.Serialize(new { inputs = Ports(ins), outputs = Ports(outs) }, Json);
    }

    /// <summary>The SKILL.md of the workflow: the skills to load, in order, and what to pass from one to the next.</summary>
    public static string BuildSkillMd(WfFile wf, string folder, string description)
    {
        var order = Order(wf) ?? throw new InvalidOperationException("The workflow has a loop.");
        var contracts = wf.Nodes.ToDictionary(n => n.Id, n => BlockContract.Load(n.Skill));
        bool Linked(WfNode n, string port) => wf.Links.Any(l => l.ToNode == n.Id && l.ToPort == port);

        var userInputs = order.SelectMany(n => contracts[n.Id].Inputs.Where(p => !Linked(n, p.Name)).Select(p => (Node: n, Port: p))).ToList();
        int first = userInputs.Count > 0 ? 2 : 1;
        var step = order.Select((n, i) => (n, no: first + i)).ToDictionary(x => x.n.Id, x => x.no);
        string Skill(string nodeId) => wf.Nodes.First(n => n.Id == nodeId).Skill;

        var sb = new StringBuilder();
        sb.Append("---\n").Append("name: ").Append(folder).Append('\n').Append("description: ").Append(description).Append("\n---\n\n");
        sb.Append("# Instructions\n\n");
        sb.Append($"A workflow of {order.Count} skill(s): {string.Join(" -> ", order.Select(n => "`" + n.Skill + "`"))}. ");
        sb.Append("It only chains these skills; their rules are in the skills themselves. Run the steps in this order, load each skill with the Skill tool, follow it, and give its result to the next step as written. Do not skip a step.\n\n");

        int no = 1;
        if (userInputs.Count > 0)
        {
            sb.Append($"{no++}. **Ask the user for the inputs**, all at once, and wait for the answers (never guess a path or a file):\n");
            foreach (var (node, port) in userInputs)
                sb.Append($"   - `{port.Name}` ({port.Type}) - used by step {step[node.Id]}, `{node.Skill}`\n");
        }

        foreach (var node in order)
        {
            var c = contracts[node.Id];
            sb.Append($"{no++}. **Run `{node.Skill}`** (load the skill, then follow it).\n");
            foreach (var p in c.Inputs)
            {
                var link = wf.Links.FirstOrDefault(l => l.ToNode == node.Id && l.ToPort == p.Name);
                var source = link is null ? "the user's answer in step 1"
                    : $"output `{link.FromPort}` of step {step[link.FromNode]} (`{Skill(link.FromNode)}`)";
                sb.Append($"   - Input `{p.Name}` ({p.Type}): {source}.\n");
            }
            foreach (var p in c.Outputs)
            {
                var targets = wf.Links.Where(l => l.FromNode == node.Id && l.FromPort == p.Name)
                    .Select(l => $"step {step[l.ToNode]} (`{Skill(l.ToNode)}`) input `{l.ToPort}`").ToList();
                sb.Append($"   - Output `{p.Name}` ({p.Type}): {(targets.Count > 0 ? "keep it for " + string.Join("; ", targets) : "final result, include it in the report")}.\n");
            }
            sb.Append("   - If it fails: stop here, say which step and why, and do not run the later steps.\n");
        }

        sb.Append($"{no}. **Report**: for every step say what ran and what it produced; give each final result (name, type, where it is saved); list any warning or skipped step.\n\n");

        if (wf.Links.Count > 0)
        {
            sb.Append("## Data passed between steps\n\n| From | To | Type |\n|---|---|---|\n");
            foreach (var l in wf.Links.OrderBy(l => step[l.FromNode]).ThenBy(l => step[l.ToNode]))
            {
                var type = contracts[l.FromNode].Outputs.FirstOrDefault(p => p.Name == l.FromPort)?.Type ?? "";
                sb.Append($"| step {step[l.FromNode]} `{l.FromPort}` | step {step[l.ToNode]} `{l.ToPort}` | {type} |\n");
            }
            sb.Append('\n');
        }

        sb.Append("## Not done\n\n");
        sb.Append("- No step beyond the list above, and no change to the user's input files.\n");
        sb.Append("- The rules of each skill are not repeated here: if a skill changes, this workflow follows the new version.\n");
        sb.Append("- Built with the Skill Wire Frame of Claude MD; the wiring is saved in `workflow.json` next to this file.\n");
        return sb.ToString();
    }
}
