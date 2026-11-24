---
name: Dino-Dev-Add_Winpath
description: Adds a winpath.py long-path helper to a Python project. Use on 'path too long' errors.
---

# Instructions

1. Find the project's source directory (the folder that holds its `.py` modules — typically `src/`, otherwise the project root). Create `winpath.py` there with exactly this content:

```python
import os

LONG_PATH_PREFIX = "\\\\?\\"   # this is the 4-character string:  \\?\


def long_path(path: str) -> str:
    r"""
    Return a path Windows can open even if it's longer than 260 characters.

    Windows blocks paths over MAX_PATH (260 chars) unless they start with
    the \\?\ marker, which tells it to skip the old path parser.
    That marker only works on absolute paths.
    """
    if path.startswith(LONG_PATH_PREFIX):
        return path                                  # already prefixed, leave it alone
    return LONG_PATH_PREFIX + os.path.abspath(path)  # make absolute, then prefix
```

2. Wire it into every file-system read/write boundary in the codebase — dropping the file in without using it fixes nothing. For each site, wrap the final string handed to the OS-level call in `long_path(str(path))`:
   - `open(path, ...)` → `open(long_path(str(path)), ...)`
   - `Path.write_text(...)` / `Path.read_text(...)` → switch to `open(long_path(str(path)), ...)` since `Path` methods don't accept a raw prefixed string
   - Library calls that take a path (`openpyxl.load_workbook(path)`, `wb.save(path)`, `pandas.read_csv(path)`, etc.) → pass `long_path(str(path))` instead of `path`
   - `Path.mkdir(...)` → `os.makedirs(long_path(str(path)), exist_ok=True)` (add `import os` if not already present)

3. Do **not** prefix `Path` objects used purely for directory traversal, globbing (`rglob`, `glob`, `iterdir`), or relative-path bookkeeping (`relative_to`, dict keys, display strings) — only wrap the final string at the actual read/write/save/mkdir call. Prefixing earlier can break glob matching and relative-path comparisons.

4. Only touch the call sites that plausibly see long or deeply nested paths (e.g. anything that walks a user-supplied root folder, or writes into it) — no need to wrap trivial, fixed-length internal paths.

5. Briefly note in the project's README or CLAUDE.md (if one exists) that `winpath.py` exists and why, so future edits keep using it at new I/O sites instead of reintroducing raw path calls.
