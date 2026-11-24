---
name: Dino-Data_Format-Trace_Scat
description: Format of Scat (.scat) calibration archives. Use when reading or compiling Scat files.
---

# Scat (`.scat`) format

Reference: QuickMi2e `src/lib/event/Compile_Scat.py`, `Directory.rename_and_unzip_scat`, `Process_Cal.py` (its plotting), and the User Manual slides "Standardization - Merge During Compilation in Scat" and "PowerPoint generator for Scat". A Scat file holds the S-parameter data **and the spec values** of one calibration unit, so no TSF is needed.

## 1) The package

- A `.scat` file is a **zip archive** with another name. QuickMi2e renames it to `.zip` and extracts it into its own folder named after the file (`<extract dir>/<scat name>/`).
- The csv files must be at the **top level** of the archive: only `.csv` files directly inside that folder are read (a csv inside a sub-folder of the zip is not found).
- The same folder (`dataset/cntrace/zip`) provides the **spec choice**: the `.zip` names (without extension) listed there are offered as "spec versions" in the Scat dialog.

## 2) The csv files inside

File name pattern (regex `(\w+)_(\w+)_(\w+)\.csv`, `re.match` on the name):

`<Channel>_<S-parameter>_<rest>.csv`, e.g. `CH1_S11_something.csv`

- group 1 = **channel** (`CH1`), group 2 = **S-parameter** (`S11`). Because `\w` also matches `_`, the first group takes as many `_` parts as it can while leaving two more parts; keep a channel name without `_` to stay unambiguous.
- A file whose name does not match is skipped with a warning.

## 3) What QuickMi2e writes (merge during compilation)

Every matching csv is **moved** (not copied) and renamed:

`results/<Channel>/<scat name>_<S-parameter>.csv`, e.g. `results/CH1/<scat name>_S11.csv`

So one folder per channel collects the same S-parameter of every Scat file; the extracted work folder is emptied afterwards.

## 4) Content of a csv (as the plot step reads it)

- A table with a header row. The frequency column is `Freq` (or `freq[HZ]`).
- One column named exactly like the S-parameter in the file name (`S11`), holding the trace values (dB). The plot takes the S-parameter from the **last `_` part of the result file name**, so keep the name `..._<S-parameter>.csv`.
- The **spec** file: the result file whose name contains the chosen spec version is read as the spec table; it needs columns `USL` and `LSL` (same length as the frequency column; a blank or `0` limit is not drawn). The spec lines are drawn over every trace of that channel and S-parameter.
- Plot title: `<Channel>_<S-parameter>`, or the `Plot Title` of the batch file row where `S-Parameter` and `Channel Number` match.

## Not verified

QuickMi2e's repository holds no `.scat` sample. The package and file-name rules are read from the compile code; the csv columns are read from the plot code. Open a real `.scat` (rename to `.zip`) before relying on more.

## Pitfalls

- The move empties the extracted copy; keep the original `.scat` files.
- Two `.scat` files with the same name give the same result names and overwrite each other.
- Windows long paths need the `\\?\` prefix.
- Scat data cannot be plotted together with Tiger/Cntrace data in one run: QuickMi2e's plotting depends on the selected FileType.
