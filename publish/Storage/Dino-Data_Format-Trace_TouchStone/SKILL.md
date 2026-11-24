---
name: Dino-Data_Format-Trace_TouchStone
description: Format of TouchStone .sNp S-parameter files. Use when reading or compiling TouchStone files.
---

# TouchStone (`.s1p`, `.s2p`, `.s3p` ...) format

Reference: QuickMi2e `src/lib/event/Compile_TouchStone.py` and `Process_Stone.py`. A TouchStone file is the standard text S-parameter file of a VNA: one file = one trace. QuickMi2e reads only files directly inside the chosen folder (no sub-folders) whose name matches `^(.*)\.(s\d+p)$` (the digits are the port count: `.s2p` = 2 ports). The part before the extension is the **unit / trace name**.

## 1) Lines of the file

| First character | Meaning |
|---|---|
| `!` | Comment. May name the S-parameters (see section 2). |
| `#` | Option line: `# <freq unit> S <format> R <z0>`, e.g. `# HZ S DB R 50`. It has to hold exactly 6 words: `#`, unit (`HZ`, `KHZ`, `MHZ`, `GHZ`), `S`, format (`DB`, `RI` or `MA`), `R`, reference impedance. |
| anything else | Data: frequency, then 2 values per S-parameter. |

Without a `#` line QuickMi2e assumes `HZ` and `DB`.

- Formats: `DB` = dB magnitude + angle (degrees); `RI` = real + imaginary; `MA` = linear magnitude + angle.
- **Data layout**: per frequency `1 + 2 x P x P` numbers (P = ports; for `.s2p` 9). A frequency may be **wrapped over several lines** (typical for 3 or more ports): QuickMi2e collects the numbers of consecutive lines until it has `1 + 2 x P x P`, then starts the next frequency. Lines that start with `!` or `#`, blank lines and `freq...` header lines are skipped.

## 2) S-parameter names

The columns after the frequency are named by the S-parameters found in the comments: a name matches `S\d+(_)?\d+` (`S11`, `S21`, `S1_2`). The names come from `!` comment lines, and a line that starts with `freq` (optionally `!freq`) resets the list and takes the names after the word `freq` (`!freq S11 S21 S12 S22`). The names are taken in file order, duplicates dropped; each name owns two columns (value 1, value 2). If no names are found, no data is produced for the file (QuickMi2e once asked for them on the console; do not rely on that).

## 3) What QuickMi2e writes

`results/<unit>/<unit>.csv` (the unit name = the file name without `.sNp`), index column off, columns:

`freq[HZ]` (the unit of the `#` line, upper case), `dB:S11`, `ang:S11`, `dB:S21`, `ang:S21`, ...

- `DB`: copied as is. `RI`: `dB = 20 x log10(hypot(Re, Im))`, `ang = atan2(Im, Re)` in degrees. `MA`: `dB = 20 x log10(Mag)`, angle copied.
- A column that is zero in every row is written as empty (NaN).
- After all files: two extra folders `All files` and `All Files-CFG` collect a copy of every compiled csv (a name that already exists gets its folder name as a prefix). They are not channels: the plot step skips them.
- The plot step groups the csv files by the channel number found in the **file name** (`CH<digits>`, case-insensitive); a csv without `CH<n>` in its name is ignored. Name the TouchStone files accordingly (e.g. `...CH3...s2p`).

## Pitfalls

- A `# ... MA ...` file: QuickMi2e names the columns `mag:`/`ang:` but its conversion reads `Mag:`/`Ang:`, so `MA` files may fail there; check it before relying on `MA`. `DB` and `RI` follow the paths above.
- A frequency that is not numeric stops the file (all values are converted with `to_numeric`).
- The `.snp` extension with a letter (`.snp`, as in the FmTrace files) is not matched by this flow; `.snxs` is another format (see `Dino-Data_Format-SNXS`).
- Windows long paths need the `\\?\` prefix.
