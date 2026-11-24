using System.Text;

namespace ClaudeMd.Services;

/// <summary>Result of checking a SKILL.md: blocking Errors, advisory Warnings, the Fixes applied automatically, and the corrected text.</summary>
public sealed record SkillCheck(List<string> Errors, List<string> Warnings, List<string> Fixes, string FixedText)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>The rules of the Dino-Dev-skill-format skill (section 1-3), applied to a SKILL.md before it is uploaded.</summary>
public static class SkillRules
{
    public const int DescriptionSoftMax = 110;

    public static SkillCheck Check(string text, string folder)
    {
        var errors = new List<string>(); var warnings = new List<string>(); var fixes = new List<string>();

        // folder = Dino-{Category}-{Application}; the application part never holds a '-'
        var parts = folder.Split('-', 3);
        if (parts.Length < 3 || parts[0] != "Dino" || parts[1].Length == 0 || parts[2].Length == 0)
            errors.Add($"The folder '{folder}' must be Dino-{{Category}}-{{Application}}.");
        else if (parts[2].Contains('-') || parts[2].Contains(' '))
            errors.Add($"The application name '{parts[2]}' must not contain '-' or spaces: join the words with '_' (e.g. {parts[2].Replace('-', '_').Replace(' ', '_')}).");

        if (text.Length > 0 && text[0] == '﻿') { text = text[1..]; fixes.Add("Removed the UTF-8 BOM."); }
        var crlf = text.Contains("\r\n");
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

        if (lines[0].Trim() != "---")
        {
            errors.Add("The file must start with a '---' frontmatter line.");
            return new SkillCheck(errors, warnings, fixes, text);
        }
        var end = lines.FindIndex(1, l => l.Trim() == "---");
        if (end < 0)
        {
            errors.Add("The frontmatter has no closing '---' line.");
            return new SkillCheck(errors, warnings, fixes, text);
        }

        // name = folder name
        var nameIdx = lines.FindIndex(1, end - 1, l => l.StartsWith("name:"));
        if (nameIdx < 0) { lines.Insert(1, "name: " + folder); end++; fixes.Add($"Added 'name: {folder}'."); }
        else if (lines[nameIdx][5..].Trim() != folder)
        {
            fixes.Add($"Changed name '{lines[nameIdx][5..].Trim()}' to '{folder}' (name must equal the folder name).");
            lines[nameIdx] = "name: " + folder;
        }

        // description = one single line
        var descIdx = lines.FindIndex(1, end - 1, l => l.StartsWith("description:"));
        if (descIdx < 0) errors.Add("The frontmatter has no 'description:' line.");
        else
        {
            var value = lines[descIdx]["description:".Length..];
            var trimmed = value.Trim();
            if (trimmed.Length == 0) errors.Add("'description:' is empty (a YAML block like '>-' or '|' is not allowed - write the text on the same line).");
            else if (trimmed[0] is '>' or '|') errors.Add($"'description:' uses a YAML block ('{trimmed[0]}'). Write the text as ONE line.");
            else
            {
                if (value != " " + trimmed) { lines[descIdx] = "description: " + trimmed; fixes.Add("Trimmed spaces around the description."); }
                if (descIdx + 1 < end && (lines[descIdx + 1].StartsWith(' ') || lines[descIdx + 1].StartsWith('\t')) && lines[descIdx + 1].Trim().Length > 0)
                    errors.Add("'description:' is wrapped over several lines. It must be ONE single line.");
                if (trimmed.Length > DescriptionSoftMax)
                    warnings.Add($"The description is {trimmed.Length} characters; the format asks for about 100 so the table stays readable.");
                if (!trimmed.Contains("Use when", StringComparison.OrdinalIgnoreCase))
                    warnings.Add("The description should end with a 'Use when ...' clause.");
            }
        }

        var extra = lines.Skip(1).Take(end - 1).Select(l => l.Split(':')[0].Trim())
            .Where(k => k.Length > 0 && k is not ("name" or "description") && !char.IsWhiteSpace(k[0])).Distinct().ToList();
        if (extra.Count > 0) warnings.Add("Extra frontmatter keys (only 'name' and 'description' are expected): " + string.Join(", ", extra));

        // body
        var body = lines.Skip(end + 1).ToList();
        var first = body.FirstOrDefault(l => l.Trim().Length > 0);
        if (first is null) errors.Add("The file has no content after the frontmatter.");
        else
        {
            if (!first.StartsWith("# ")) warnings.Add("The body should start with a '# Instructions' (or '# <Title>') heading.");
            if (lines[end + 1 < lines.Count ? end + 1 : end].Trim().Length > 0) { lines.Insert(end + 1, ""); fixes.Add("Added a blank line after the closing '---'."); }
        }

        var nl = crlf ? "\r\n" : "\n";
        var result = string.Join(nl, lines).TrimEnd('\r', '\n') + nl;
        if (!errors.Any() && result != text && fixes.Count == 0) fixes.Add("Normalised the ending to a single newline.");
        return new SkillCheck(errors, warnings, fixes, result);
    }

    public static readonly UTF8Encoding Utf8NoBom = new(false);
}
