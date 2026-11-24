---
name: Dino-Dev-Skill_Format
description: Standard format for writing a SKILL.md. Use when creating or fixing a skill.
---

# Instructions

A skill is a folder `Dino-{Category}-{Application}/` holding one `SKILL.md`. Claude MD (the Storage manager) lists every folder in `Storage/`, groups them by `{Category}`, shows `{Application}` as the skill name and reads the `description:` line of the frontmatter, so the format below must be followed exactly.

## 1. Folder name and file
- Folder name = skill name = `Dino-{Category}-{Application}`, put in `Storage/Dino-{Category}-{Application}/SKILL.md`.
  - `Dino` is fixed.
  - `{Category}` is one of `Dev`, `Data_Format` or `Apps` (the groups of Claude MD, and the choices of its Upload Skill button). A new category is only added by agreement; `Data_Format` is the only one with an underscore.
  - `{Application}` is what the skill is about. Write each word with a capital first letter and **join the words with `_`** (underscore): `Add_Winpath`, `Raw_Data`, `Tiger_Cntrace_Seoul`, `ATE_Compile_And_Combine`. A well-known name or acronym stays as one word (`GU`, `TSF`, `SNXS`, `TouchStone`). Letters, digits and `_` only. **Never a `-` (hyphen) and never a space** inside the application name: Claude MD cuts the name at the first `-` after `Dino-` to find the category, so a second `-` is confusing, and a space ends the slash command (`/Dino-Dev-Add Winpath` would stop at the space).
  - Wrong: `Dino-Dev-add-winpath`, `Dino-Dev-AddWinpath`, `Dino-Dev-Add Winpath`, `Dino-Data_Format-Raw-Data`. Right: `Dino-Dev-Add_Winpath`, `Dino-Data_Format-Raw_Data`.
- The file is named exactly `SKILL.md`. UTF-8 without BOM, ending with one newline.
- Extra files (templates, scripts) go in sub-folders next to `SKILL.md` and are referenced by relative path.
- When another skill is named in a body, write its full folder name (`Dino-Data_Format-Raw_Data`).

## 2. Frontmatter (first lines, between two `---` lines)
```
---
name: Dino-{Category}-{Application}
description: <ONE line: what it does. Use when ...>
---
```
- `name` equals the folder name exactly (same capitals); it is also the slash command, e.g. `/Dino-Dev-Add_Winpath`.
- `description` is **one single line**. Never use a YAML block (`>-`, `|`) or wrap it over several lines - the app shows `>-` instead of the text. No line breaks, no leading/trailing spaces.
- Write the description as: what the skill does (1 sentence) + `Use when <trigger phrases the user would say>.` Keep it SHORT: one sentence of what + one short "Use when ..." clause, max about 100 characters, so the UI table stays readable. No file names, lists or examples in it - put those in the body.
- No other frontmatter keys unless really needed.

## 3. Body
1. Blank line after the closing `---`, then a top heading: `# Instructions` (or `# <Title>` for long guides).
2. Write numbered steps in the order Claude must do them; one action per step, imperative voice ("Create", "Run", "Ask").
3. Give exact commands, paths and file contents in fenced code blocks; say what to do when a step fails.
4. Say what is deliberately NOT done (scope limits), and what to report to the user at the end.
5. Do not repeat the description, and do not include personal data, passwords or tokens.

## 4. Template
```
---
name: Dino-Dev-My_Application
description: Does X for Y. Use when the user asks to do X or mentions Y.
---

# Instructions

1. First step, with the exact command:
   ```
   command here
   ```
2. Second step.
3. Report: summarise what was created or changed.
```

## 5. Check before finishing
- The folder is `Dino-{Category}-{Application}`: category is `Dev`, `Data_Format` or `Apps`, and the application words are joined with `_` (no `-`, no spaces).
- `name` equals the folder name, `description:` is one line.
- Open Claude MD and restart it: the skill appears under its category in the table with its description (not `>-`), not under "Other" (a sign that the folder name has no `Dino-{Category}-` start).
- Click Sync to copy it to `C:\Users\<user>\.claude\skills`.
