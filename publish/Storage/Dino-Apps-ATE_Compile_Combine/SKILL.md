---
name: Dino-Apps-ATE_Compile_Combine
description: Compiles ATE tester files and combines them on a chosen benchmark file. Use when asked to compile or combine.
---

# Instructions

Does what the Split-Spread tool's **Compile + Combine** button does (reference: `Split-Spread-Compiler\Split-Spread`, `Services\CompileWorker.cs`, `Services\Compiler.cs`, `Models\TesterData.cs`): splits every ATE tester file into Excel-safe pieces, then joins the units of all files under the parameters of one **benchmark** file. The input layout is the one in the `Dino-Data_Format-Raw_Data` skill (read it if a file does not parse).

1. **Ask for the directory path.** Ask in plain text, exactly: "Which directory holds the ATE tester files? Paste the full path." Wait for the answer; never guess a path. Strip surrounding quotes. If the folder does not exist, say so and ask again.
2. **List the input files.** Take the files directly inside that directory with the extension `.csv`, `.zip` or `.xlsx` (not sub-folders, and nothing inside an `Output` folder). Sort them in natural order (`file-2` before `file-10`). Print them numbered with their size:
   ```
   1. IP172.16.23.150_..._FF.csv   (35.2 MB)
   2. ...
   ```
   If there are none, report "no .csv/.zip/.xlsx files in <path>" and stop. Two files with the same name but a different extension (`X.csv` and `X.zip`) would write the same output: keep the first, tell the user the second is skipped.
3. **Ask which file is the benchmark.** Say: "Which file is the benchmark? Its parameters, their order, limits and CF are kept; the units of the other files are joined under them." Wait for a number or a file name (use AskUserQuestion when there are 4 files or fewer). Ask again on an invalid answer. With a single file, it is the benchmark automatically - say so and go on.
4. **Use these defaults without asking** (only change them when the user said so): at most 16,383 parameter columns per output file (Excel's limit), outputs in `<directory>\Output`, every unit kept (no sampling). Tell the user the defaults in one line before starting.
5. **Parse every input file** (section "Reading a file"). One unreadable file does not stop the job: skip it, remember the reason, go on. If the benchmark itself cannot be read, stop and report why.
6. **Compile each file** into `<directory>\Output\<source>\` (section "Compile output"). `<source>` is the file name without extension.
7. **Combine** into `<directory>\Output\Combine\` (section "Combine output"), starting with the benchmark's units, then the other files in the listed order.
8. **Golden Eagle headers (optional).** Only when `GoldenEagleConsole.exe` is at hand (the user named it, or it is in a `GoldenEagle-v*` folder next to Split-Spread): run it with its own folder as the working directory, on the benchmark file, writing next to `Combine.csv`:
   ```
   GoldenEagleConsole.exe /y "<benchmark.csv or .zip>" "<directory>\Output\Combine\GE_Headers.csv"
   ```
   It takes a `.csv` or `.zip` only (not `.xlsx`). If it is missing or fails, skip it and say `GE_Headers.csv` was not created.
9. **Check and report.** Re-read the headers of the files written. The row count of `Combine` must equal the sum of the units of all files read; the first row of every piece must be `Parameter`. Report: files read and skipped (with reasons), parameters of the benchmark, units per file and in Combine, the output files written with their folder, and the "Combine note" if any file lacked parameters (section "Combine output"). Offer to open the Output folder.

## Reading a file

- `.csv`: UTF-8 (a BOM is fine), read with a CSV reader (quoted fields can hold commas), never `split(",")`. `.zip`: open the `.csv` (else `.xlsx`) entry whose name matches the zip's name; otherwise the first `.csv`, then the first `.xlsx`; the zip's `.txt` summary is not data. `.xlsx`: first sheet, same row layout.
- Meta block: every row before the row whose first cell is `Parameter`. A `--- <Name>:` row starts a section (the first section is `Global Info`); other rows are `key,value`. Keep the `Global Info` keys `SubLot` (always upper case), `TesterName`, `SpecName` and `Lot`. The `#CF` row is kept separately.
- Header rows, aligned column by column with the `Parameter` row: `Test#` (or `Tests#`), `Patterns` (ignore), `Unit`, `HighL` (the USL), `LowL` (the LSL). The first cell is the row label.
- Parameter columns: skip a blank name; when a name repeats keep only the first.
- Data rows follow: the first cell is the Unit ID. Skip rows with an empty first cell; a row whose first cell starts with `#` is a footer line (not a unit, not written out).
- Numbers are read as floats; an empty cell is missing. A column that holds any non-numeric text (for example `LOT_ID`) is a **text column** and keeps its text. A column of all whole numbers is an **integer column** and is written without `.0`.
- CF: the `#CF` cell of a parameter looks like `(+) 0.139133335` (an offset to add) or `(*) 0.98` (a factor to multiply by). Keep the cell exactly as written. A blank cell, or the CF definition file name sitting right after `#CF`, means no CF.

## Compile output

For each file, in `Output\<source>\`:

| File | Content |
|---|---|
| `<source>.csv`, or `<source>-1.csv`, `-2.csv` ... | The parameters cut into pieces of at most the columns-per-file number. Rows: `Parameter`, `Test#`, `Unit`, `HighL`, `LowL`, then one row per unit (Unit ID first). No meta block, no `#CF` row, no footer. |
| `<source>-All.csv` | Every parameter in one file. Only written when the source was split into 2 or more pieces. |
| `<source>-Spec.csv` | `Parameter,LSL,USL` for every parameter; a missing limit is blank. Limits that are "no limit" placeholders (USL of 1e120 or more, LSL of -1e120 or less) are left blank. |
| `<source>-CF.csv` | `Parameter,CF` only for the parameters that have a CF, the CF text as written. Not written when none has one. |

- Write UTF-8 **with BOM**, line ends `\r\n`.
- Number text like Python: a whole float gets `.0` (`26.0`), others use the shortest exact form (`26.046875`), a missing number is `nan`, an integer column stays `26`, a text column keeps its text. A field with a comma, quote or line break is quoted.
- Delete older `<source>-<n>.csv` pieces of the same source left by an earlier run with another piece count, and the old `-All.csv` when this run does not need one.

## Combine output

In `Output\Combine\` (source name `Combine`). The **benchmark** decides the parameters: their names, order, `Test#`, `Unit`, limits and CF are the benchmark's.

1. Units: the benchmark's first, then every other file in the listed order. Keep every unit; the same Unit ID (`PID-1`) in several files stays several rows.
2. A parameter that a file does not have gets an **empty** cell (not `nan`) for that file's units. Parameters only other files have are dropped. Count them per file for the report: "Combine note: N of M other file(s) lack some of the benchmark's P parameter(s) - those cells are left empty (file: count; ...)".
3. A column is text when any file has text in it; it is an integer column only when it is one in every file. A blank `LOT_ID` takes that file's `Lot` from `Global Info`.
4. Four info columns go right after the Unit ID, in this order, filled per row from the row's own file: `SubLot`, `TesterName`, `SpecName` (all from `Global Info`) and `SourceFile` (the file name without extension). If the benchmark already has a parameter of that name, name the info column `<name> (Global Info)`.
5. Pieces: cut like a normal file, but with `columns-per-file - 4` parameters per piece, and **repeat the 4 info columns at the front of every piece**. Files: `Combine.csv` or `Combine-1.csv`, `-2.csv` ..., and `Combine-All.csv` when there are 2 or more pieces.
6. Because every file has its own CF and TSF (`SpecName`), the single CF and Spec files are not written for Combine. Instead:
   - `Combine-CF_All.csv`: header `Parameter` plus one column per input file (the file name); a row for each parameter that has a CF in any file, with the CF text as written in that file. Not written when no file has a CF.
   - `Combine-Spec_All.csv`: header `Parameter` plus `<SpecName> LSL`, `<SpecName> USL` for each distinct SpecName (files with the same SpecName share one set of limits, taken from the first file of that SpecName); a row for each parameter with a limit in any SpecName.
   - Delete the single `Combine-CF.csv`, `Combine-Spec.csv` of an earlier run.

## Not done

- No plots, grouping, PowerPoint, Reject/Pareto, N-Sampling or "Update" (appending new units to an earlier Output); say so if the user asks and point to Split-Spread.exe.
- Never change or delete an input file. Only the `Output` folder is written (and its stale pieces cleaned as above).
- If an output file cannot be written (usually it is open in Excel), report "close <file> and run again" and go on with the others.
- Very wide files are tens of MB each and are held in memory: when memory is short, tell the user to compile fewer files at a time instead of failing silently.
