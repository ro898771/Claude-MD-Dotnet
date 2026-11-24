using System.IO;
using System.Text;
using System.Text.Json;

namespace ClaudeMd.Services;

public sealed record SkillEntry(string Name, string Description);

/// <summary>Reads/writes the Storage folder (one sub-folder per skill, each with a SKILL.md) and the sync target.</summary>
public static class SkillStore
{
    public const string SkillFile = "SKILL.md";
    private static readonly UTF8Encoding Utf8 = new(false);

    public static List<SkillEntry> Load(string storageDir)
    {
        var list = new List<SkillEntry>();
        if (!Directory.Exists(storageDir)) return list;
        foreach (var dir in Directory.GetDirectories(storageDir).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            list.Add(new SkillEntry(Path.GetFileName(dir), ReadDescription(Path.Combine(dir, SkillFile))));
        return list;
    }

    public static string ReadDescription(string skillPath)
    {
        if (!File.Exists(skillPath)) return "";
        var inFront = false;
        foreach (var line in File.ReadAllText(skillPath, Utf8).Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (l.Trim() == "---") { if (!inFront) { inFront = true; continue; } break; }
            if (inFront && l.StartsWith("description:")) return l[(l.IndexOf(':') + 1)..].Trim();
        }
        return "";
    }

    public static void WriteDescription(string skillPath, string description)
    {
        var lines = File.ReadAllText(skillPath, Utf8).Replace("\r\n", "\n").Split('\n').ToList();
        var inFront = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() == "---") { if (!inFront) { inFront = true; continue; } break; }
            if (inFront && lines[i].StartsWith("description:")) { lines[i] = "description: " + description; break; }
        }
        File.WriteAllText(skillPath, string.Join("\n", lines).TrimEnd('\n') + "\n", Utf8);
    }

    /// <summary>Claude Code's personal skills folder: the default sync target.</summary>
    public static string ClaudeSkillsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");

    public static string LoadDefaultSyncPath()
    {
        try
        {
            if (!File.Exists(AppPaths.PathJson)) return ClaudeSkillsDir;
            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.PathJson, Utf8));
            var p = doc.RootElement.TryGetProperty("default_path", out var v) ? v.GetString() ?? "" : "";
            return p.Length > 0 ? p : ClaudeSkillsDir;
        }
        catch (Exception e) when (e is IOException or JsonException) { return ClaudeSkillsDir; }
    }

    /// <summary>
    /// Claude Code lists a skill in the "/" menu by the "name:" in its frontmatter, not by folder name.
    /// Sets that name to the folder name (e.g. "Dino-Dev-add-winpath") so "/Dino" finds every skill.
    /// </summary>
    public static int NormalizeNames(string storageDir)
    {
        var changed = 0;
        if (!Directory.Exists(storageDir)) return 0;
        foreach (var dir in Directory.GetDirectories(storageDir))
        {
            var path = Path.Combine(dir, SkillFile);
            if (!File.Exists(path)) continue;
            var want = Path.GetFileName(dir);
            var text = File.ReadAllText(path, Utf8);
            var nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var inFront = false;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---") { if (!inFront) { inFront = true; continue; } break; }
                if (inFront && lines[i].StartsWith("name:"))
                {
                    if (lines[i][5..].Trim() != want)
                    {
                        lines[i] = "name: " + want;
                        File.WriteAllText(path, string.Join(nl, lines), Utf8);
                        changed++;
                    }
                    break;
                }
            }
        }
        return changed;
    }

    public static void SaveDefaultSyncPath(string path)
    {
        Directory.CreateDirectory(AppPaths.SettingDir);
        File.WriteAllText(AppPaths.PathJson, JsonSerializer.Serialize(new { default_path = path }, new JsonSerializerOptions { WriteIndented = true }), Utf8);
    }

    /// <summary>Appends ".../skills" to the target when missing.</summary>
    public static string ResolveTarget(string target)
        => Path.GetFileName(target.TrimEnd('\\', '/')).Equals("skills", StringComparison.OrdinalIgnoreCase) ? target : Path.Combine(target, "skills");

    /// <summary>Copies every new/changed Storage file into the target. Returns the final folder.</summary>
    public static (string Folder, int Copied) Sync(string storageDir, string target)
    {
        target = ResolveTarget(target);
        Directory.CreateDirectory(target);
        NormalizeNames(storageDir);
        var copied = 0;
        foreach (var rel in PendingFiles(storageDir, target))
        {
            var dst = Path.Combine(target, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(Path.Combine(storageDir, rel), dst, true);
            copied++;
        }
        return (target, copied);
    }

    /// <summary>Number of Storage files that are missing or different in the target.</summary>
    public static int CountPending(string storageDir, string target)
    {
        if (target.Length == 0) return 0;
        NormalizeNames(storageDir);
        return PendingFiles(storageDir, ResolveTarget(target)).Count();
    }

    private static IEnumerable<string> PendingFiles(string storageDir, string target)
    {
        if (!Directory.Exists(storageDir)) yield break;
        foreach (var file in Directory.EnumerateFiles(storageDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(storageDir, file);
            var dst = Path.Combine(target, rel);
            if (!File.Exists(dst) || !SameContent(file, dst)) yield return rel;
        }
    }

    private static bool SameContent(string a, string b)
    {
        try
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
        catch (IOException) { return false; }
    }
}
