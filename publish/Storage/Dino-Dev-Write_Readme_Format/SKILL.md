---
name: Dino-Dev-Write_Readme_Format
description: Writes README.md in a fixed 4-section template. Use when creating or updating a README.
---

# Instructions

Write `README.md` using exactly these headers, in this order, separated by a `---` horizontal rule between each section. Do not add other top-level sections (no Requirements, Setup, Usage, License, etc.) unless the user explicitly asks for them in the same request — this template is intentionally minimal.

```markdown
# {Project Name}

## Overview

{2-4 sentence paragraph: what the project does, who it's for, and why it's useful. No marketing fluff.}

- **Developer:** {name}
- **Author:** {name}
- **Platforms:** {e.g. Windows, macOS, Linux, or "Cross-platform"}
- **{4th metadata line, pick whichever fits the project}:** {value}

---

## Objective

{1-2 sentences: the single problem this project solves or goal it serves.}

---

## Feature List

- **{Feature Name}** – {one sentence: what it does and why it matters. One bullet per real, shipped feature — no roadmap/planned items.}

---

## Update List

1) {change}
2) {change}
```

## Filling it in

- **Project Name**: from `pyproject.toml` (`[project].name`), `package.json` (`name`), or the repo/folder name if neither exists.
- **Developer / Author**: default both to `git config user.name` (run it, don't guess). If the two roles are genuinely different people, ask.
- **Platforms**: infer from the code — OS-specific scripts (`.cmd`/`.ps1` vs `.sh`), GUI toolkit (e.g. Tkinter on Windows), or dependencies. Don't guess "Cross-platform" if there's OS-specific code.
- **4th metadata line**: whichever single fact best orients a new reader to what the project works with or on — e.g. "Supported File Formats" for a file/data tool, "Tech Stack" for a web app, "Supported Languages" for a linter/CLI. Pick one; don't add more than one extra line.
- **Feature List**: derive from actually reading the source/entry points, not from assumptions. Each bullet should be a capability a user can point to in the code.
- **Update List**:
  - Brand-new project: `v1.0.0.0` / `1) Initial release.`
  - Existing project with history: ask the user for the version number, then summarize the meaningful changes since the last README update as numbered, past-tense, one-line items (mirror the style of `git log` subjects, not diff noise).
- Keep the Overview and Objective paragraphs short — if a sentence doesn't help a first-time reader decide whether to keep reading, cut it.
