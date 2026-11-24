# Claude MD

## Overview

Claude MD is a Windows desktop tool for keeping Claude Code skills in one place. It lists, edits and checks the `SKILL.md` files kept in a Storage folder, copies them into Claude Code's skills folder, uploads them to a shared Box zip, and links skills together as blocks to generate new workflow skills. It is for engineers who write and share Claude skills.

- **Developer:** Roey Yee
- **Author:** Roey Yee
- **Platforms:** Windows (WPF, .NET 9; `build.bat` publishes a single self-contained `Claude-MD.exe`)
- **Supported File Formats:** `SKILL.md` (skills), `block.json` (a skill's inputs and outputs), `workflow.json` (a saved wiring)

---

## Objective

Keep one tidy, correctly formatted set of Claude skills, and get them into Claude Code and the team's shared copy with one click.

---

## Feature List

- **Skill Storage** – Lists every skill folder of `Storage` as `Dino-{Category}-{Application}`, grouped by category in an explorer tree and a table, with search, a `SKILL.md` viewer, and an explorer you can drag wider or narrower.
- **Edit descriptions** – Edit the one-line `description:` of any skill in the table and Save it back into its `SKILL.md`.
- **Sync to Claude Code** – Copies the skills into `~/.claude/skills` (or any folder you choose), only the files that differ, so `/Dino...` shows them in Claude Code. The button shows how many files are waiting and refreshes by itself, and a skill's `name:` is set to its folder name so the slash command matches.
- **Skill format check** – Checks a `SKILL.md` against the skill format before it is uploaded or generated: frontmatter, a one-line description with a "Use when", the `Dino-{Category}-{Application}` folder name (words joined with `_`, no `-` or spaces), and fixes small slips such as the `name:` line.
- **Upload Skill to Box** – Updates an existing skill, or adds a new one (category, name and file), inside `v1.0.0.0.zip` on Box as a new version of the same file. It needs `Box.Automation.WSD.dll` in `lib/` to build (see `lib/README.md`).
- **Skill Wire Frame** – A canvas where each skill is a block, with inputs on the left and outputs on the right read from its `block.json`. Drag from an output dot to an input dot to link two blocks; only matching types link, and loops are refused. Skills without a `block.json` are marked with a warning and act as one generic block.
- **Run Tests** – Checks the wiring (links, types, loops, missing skills, unlinked blocks) and that the generated skill passes the format check.
- **Generate Skill** – Turns the wiring into one new `Dino-Apps-{Name}` skill that runs the linked skills in order and passes each result on, saved with its `workflow.json` and a `block.json` so it can be a block itself.
- **Save and open wirings** – Save the canvas as a `.json` file, open it again, or load a sample built from your own skills.
- **One-file build** – `build.bat` publishes one self-contained `Claude-MD.exe` that runs on another Windows PC without Visual Studio or a .NET install.

---

## Update List

v1.0.0.0

1) Initial release.
