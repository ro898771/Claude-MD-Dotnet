---
name: Dino-Data_Format-Trace_Cntrace_Penang
description: Format of Tiger/Cntrace trace files, Penang test program. Use when reading or compiling these files.
---

# Tiger / Cntrace trace format - Penang

Reference: QuickMi2e (`src/lib/event/Compile_Tiger.py`, `configSetting/TP.json`, `header_config.json`). Same file family as the Seoul one; only the **channel label line** (and so the channel folder name) differs. Each Tiger skill is written to be read alone, so the shared sections are repeated.

## 1) The package

- A `.tiger` or `.cntrace` file is a **zip archive** with another name. QuickMi2e renames it to `.zip` and extracts it; inside is one `.cntracer` text file named like the archive. A `.cntracer` can also be given already extracted (QuickMi2e's source folder may mix `.tiger`, `.cntrace`, `.cntracer` and `.snxs`).
- Archive names are long and may hold spaces or dots (`...IP172.16.23.168...`): only the **trailing** `.cntracer` is stripped when naming outputs, never everything after the first dot.
- Read the text as UTF-8 with BOM (`utf-8-sig`). Fields are separated by a plain comma (no quoting). The files are big (4-11 MB, 15,000-42,000 lines).

## 2) Layout of the `.cntracer`

```
--- Global Info:,
Date,2026_11_17 15:45:22
SetupTime,2026_11_17 15:27:02
StartTime,...  FinishTime,  ProgramName,...  Lot,PT6461409372-E  SubLot,1A  Wafer,NA
TesterName,HT205T1_023_168  TesterType,...  Product,AFEM-8275-AP1  SpecName,<TSF>.csv  ...
--- Site details:,Head #1
Handler ID,HT205
Handler type,HandlerHontech7145_MQTT
LoadBoardName,NaN
--- Options:,
UnitsMode,normalized
--- ConditionName:,AFEM-8275-AP1_TCF_PROD_Rev005.xlsx
PcbLot,NA  AssemblyLot,MFG050430  FullPackageID,...  ContactorID,...  ClothoMode,1  TCFVer,15  TesterID,HT205-01  PID,109
<blank line>
MfgID_50430,ModuleID_30000209040496
*******************************************************
<channel label line>
Freq,S11_Mag,S11_Phase,S12_Mag,S12_Phase,...
10000000,-2.863,-179.436,-65.639,10.119,0,0,...
20000000,...
<channel label line of the next block>
...
```

(In the file every `key,value` is on its own line; they are shortened here.)

- **Meta sections**: a line starting `---` up to a colon opens a section (`Global Info`, `Site details`, `Options`, `ConditionName`; the name is stripped); the lines below are `key,value` until the next `---` line. Key names are matched case-insensitively (`SubLot` is read as `Sublot`). `Date` is `YYYY_MM_DD HH:MM:SS` (or `YYYY-MM-DD HH:MM:SS`).
- **ID line**: the line just above the `****` line holds `Key_value` pairs separated by commas, e.g. `MfgID_50430,ModuleID_30000209040496` (a `WaferID_...` can be there too). Parse each with `([a-zA-Z]+)_(-?\w+)`.
- **Separator**: the first line that contains `**` ends the meta part; channel blocks follow to the end of the file.
- **Channel block**: one *label line*, one *header row* (`Freq`, then `S<i><j>_Mag`, `S<i><j>_Phase` pairs), then data rows (frequency in Hz, then the values) until the next label line or the end of the file. S-parameters of unused ports are zero-filled. A data row can be shorter than the header: pad with empty (NaN). A header can end with an empty name: ignore empty names.
- A block is told apart from data rows only by its label line matching the **header regex** of the test program type (see below).

## 3) Output name of a compiled trace

`dataset/results/<channel folder>/<header label>-TN<number>.csv`, for example `results/TN1022_TR34_CH22_LMB[x]_x_INCPL2_OUTCPL2_CPLFWR/LOT[PT6461409372-E]_SL[1A]_MOD[30000209040169]_PID[253]_DateTime[20261117-161500-7708]-TN1022.csv`

- The csv holds exactly the block's header row and data rows (`Freq,S11_Mag,S11_Phase,...`), index column off.
- **Header label** = `KEY[value]` pieces joined by `_`, for the keys switched on in `configSetting/header_config.json` (section `Tiger_Cntrace_SNXS`; default on: `LOT`, `SL`, `MOD`, `PID`, `DateTime`). A value that is empty or `NA` is left out.

| Key | Read from |
|---|---|
| LOT / SL / TT | Global Info `Lot` / `Sublot` / `TesterName` |
| PROD | Global Info `Product` |
| HI / HT / LB | Site details `Handler ID` / `Handler type` / `LoadBoardName` |
| FP / CID / CM / TCFV | ConditionName `FullPackageID` / `ContactorID` / `ClothoMode` / `TCFVer` |
| ASM / PCB / PID | ConditionName `AssemblyLot` / `PcbLot` / `PID` |
| MFG / MOD / WF | the ID line `MfgID` / `ModuleID` / `WaferID` |
| DateTime | Global Info `Date` turned into `YYYYMMDD-HHMMSS`, then `-` and 4 digits of the current microseconds (so two files with the same `Date` do not collide) |

- In `PROD, HI, HT, LB, FP, CID, CM, TCFV` every `_` is replaced by `-` (the underscore separates the label pieces).
- `TN<number>` is read from the channel folder name; leading zeros are removed (`TN00001` -> `TN1`, `TN0120` -> `TN120`).
- One `.cntracer` = one unit with many channel blocks; units are told apart by the label (`PID`, `MOD`, `DateTime`).

## 4) Penang channel label line

- Header regex (`TP.json` > `Regex` > `Penang`): `(CH\d+)_([\w\-]+)`, applied with `re.match` to every line of the file: the line **starts with `CH<digits>_`** followed by letters, digits, `_` or `-`.
- Example of a Penang channel name (from QuickMi2e's README, the suffix of its html names): `CH1_B1_HPME_OUT-OUT4_TN103`. The label must contain `TN<digits>`: the output file name ends with it, and a block whose label has none fails to save.
- **Unlike Seoul, the label line is used whole** (nothing is cut at a `|`); only the TN zeros are removed (`TN0103` -> `TN103`). The whole label is the channel folder name, so it must be a valid Windows folder name.
- Everything else (meta sections, ID line, `****` line, header row, data rows, output name, header label) is exactly as in sections 1-3.

## 5) Seoul or Penang?

The test program type is one global setting (`configSetting/TP.json` `Default`, chosen with the TPType box in QuickMi2e and saved on compile). To tell by the file: the first label line after the `****` line starts with `CH<digits>_` -> **Penang**; if it starts with `TN<digits>` and holds a `|` -> Seoul (see `Dino-Data_Format-Tiger_Cntrace_Seoul`). A wrong choice stops the compile with "No channel data found using regex ...".

## Not verified

QuickMi2e's repository holds Seoul samples only. The Penang rules come from `TP.json`, `Compile_Tiger.py` and the README, not from a real Penang file: open one before relying on details such as which text follows the TN in the label line.

## Pitfalls

- Read the file as lines and cut the blocks by label line; do not build one table per line (QuickMi2e reads lines into a DataFrame and works with many threads).
- A `|` or `:` is not allowed in a Windows folder name: that is why the Seoul label is cut at `|`.
- Windows long paths: prefix with `\\?\` (QuickMi2e's `long_path` / `_long`); archive names are long.
- The Tiger flow reads `.cntracer` only. `.snxs` is a different, binary format: see `Dino-Data_Format-SNXS`.
