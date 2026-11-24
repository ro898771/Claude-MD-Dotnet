---
name: Dino-Data_Format-ATE_GU
description: Format of the GU (.gucal) zip and the GUQC result csv files. Use when reading, parsing or summarising GU files.
---

# GU file format (as produced and read by GUQC)

Reference implementation: `Software_Downloaded\GUQC-V-4.3.0.0_A-RoeyYee` (`src/main.py`, `src/lib/event/gu_summary.py`). The per-parameter table layout is the same one described in the `Dino-Data_Format-Raw_Data` skill; this skill covers the GU package and the extra GU-only files.

## 1) The GU package (`.gucal` = a zip)

- A GU run is saved as `<name>.gucal`. It is a normal zip: rename to `.zip` (GUQC does this automatically, also one folder level down) and open with a zip library.
- Name pattern: `IP<tester-ip>_<yyyy-MM-dd>_<HH.mm.ss>_<TestPlan>_CorrVrfy_<XX>_<code>.zip`, e.g. `IP172.16.23.150_2026-05-11_11.05.59_AFEM-8288-AP1-QA-RF1_BE-PXI-NI_v0012_GU_Verification_CorrVrfy_FF_CP3WYH5.zip`.
- `<XX>` is the status, found with the regex `_(PP|PF|FP|FF)_`: first letter = Correlation, second letter = Verification, `P` = passed, `F` = failed.

Files inside the zip are found by a keyword in the file name **and** the sub-folder they sit in:

| Keyword | Zip sub-folder | Notes |
|---|---|---|
| `GuRefFinalData` | `1_RefDataAnalysis` | Reference device final data. **No** `--- Global Info:` header. |
| `GuCorrRawData` | `3_CorrAnalysis` | Raw readings of the correlation run, one `PID-xxx` row per device. |
| `GuCorrFactor` | `3_CorrAnalysis` | Skip entries containing `NoDemoOffset`. One aggregate row `999`. |
| `GuRawData` | `4_VerifyAnalysis` | Raw readings of the verification run, `PID-xxx` rows. |
| `GuVrfyData` | `4_VerifyAnalysis` | Verification data, `PID-xxx` rows. |
| `GuCorrCoeff` | `4_VerifyAnalysis` | Skip entries containing `LooseDemo`. One aggregate row `999`. |
| `GuVrfyError` | `4_VerifyAnalysis` | Verification error per device, `PID-xxx` rows, with limits. |
| `GuLogPrintout` | anywhere | Text log, source of the failure list (section 4). |

If the sub-folder match fails, GuCorrFactor and GuCorrCoeff fall back to any entry that contains the keyword.

## 2) The wide csv layout (all `Gu*` files except the log)

Same shape for every file: **columns = test parameters, rows = devices**.

```
--- Global Info:
Date,2026_5_11 11:5:59
StartTime,2026_5_11 11:5:8
FinishTime,2026_5_11 11:5:59
TestPlanVersion,1
Product,AFEM-8288-AP1-RF1
TestPlan,AFEM-8288-AP1-QA-RF1_BE-PXI-NI_v0012_GU_Verification.cs
Lot,GUCAL
Sublot,1AQA
Wafer,NA
TesterName,HT199T1_023_150
TesterIPaddress,172.16.23.150
Operator,A0001
Handler ID,HT199
LoadBoardName,LB-8286-0302-017
ContactorID,89QN-0.285-G9MP-2508-462-015
InstrumentInfo,HSDIO0 = NI PXIe-6570 ...;Vbatt.0 = ...
<blank line>
Parameter,SBIN,HBIN,DIE_X,DIE_Y,SITE,TIME,TOTAL_TESTS,LOT_ID,WAFER_ID,M_Temp_SelfCalFlag,...
Test#,,,,,,,,,,1,2,3,...
Unit,,,,,,,,,,degC,degC,...
HighL,,,,,,,,,,999999,999999,...
LowL,,,,,,,,,,-999999,-999999,...
PID-11004,,,,,1,,,,,0,-1.69921875,...
PID-11005,,,,,1,,,,,0,-0.88235293,...
<blank lines>
#GU Correlation Summary,FAILED
#GU Verification Summary,FAILED
```

- No BOM. The first line is `--- Global Info:` (except `GuRefFinalData`, which starts with a blank line and goes straight to `Parameter`).
- Meta block: `key,value` lines. `InstrumentInfo` is one long `;`-separated cell. `StartTime` can be empty in `GuVrfyData`.
- The first 10 columns are bookkeeping (`SBIN,HBIN,DIE_X,DIE_Y,SITE,TIME,TOTAL_TESTS,LOT_ID,WAFER_ID`); real parameters start at `M_Temp_SelfCalFlag`. Parameter names start with `M_` (measurement/meta), `PT_` or `PR_` (test results). `SITE` holds the site number (1).
- Column-descriptor rows are exactly `Parameter`, `Test#`, `Unit`, `HighL`, `LowL`. The rows are about 4800 columns wide, so stream them; do not print them.
- Data row: first cell is the device id and decides the row type:
  - `PID-<n>`: one device (e.g. `PID-11004`).
  - `999` (bare number, no `PID-`): the aggregate row of `GuCorrFactor` and `GuCorrCoeff` (one value per parameter, not per device).
  - A data row is `PID-...` or a first cell starting with a digit (GUQC's `is_data_row`).
- Footer: `#GU Correlation Summary,PASSED|FAILED` and `#GU Verification Summary,PASSED|FAILED`. Stop reading data at the first row whose first cell starts with `#`.

What each file means in the values:

| File | Value rows | Limits (HighL/LowL) |
|---|---|---|
| GuRefFinalData | `PID-n` final reference value | real limits (e.g. `1 / -1`) |
| GuCorrRawData / GuRawData | `PID-n` raw reading | real limits (e.g. `1 / -1`) |
| GuVrfyData | `PID-n` verification value | real limits (e.g. `1 / -1`) |
| GuCorrCoeff | `999` = coefficient per parameter | real limits (e.g. `1 / -1`) |
| GuCorrFactor | `999` = correction factor to apply (0 for `M_Temp_*`) | mostly placeholder `999999 / -999999`, only a few real (35 of 4785 in the sample) |
| GuVrfyError | `PID-n` = verification error after correction | real limits for about a third of the parameters, the rest placeholder `9999999 / -9999999` |

A limit of `999999` / `9999999` (either sign) in GuCorrFactor / GuVrfyError is a "no limit" placeholder, not a real spec.

## 3) Pass / fail rule

GUCQC marks a value as failed (`1`) when `value > HighL` or `value < LowL`, else `0`; an empty or non numeric value gives no flag. Applied to the `999` value of `GuCorrFactor` and to every device value of `GuVrfyError` (a `Cumm_PF` column sums the device flags per parameter).

## 4) GuLogPrintout failure lines

Only lines after `Messages logged during GU Calibration` count. The `GU Device #<n> being run` lines give the device ids for the corr-factor lines that follow.

```
*** Failed GU Verification limits, site <s>, Device <id>, test <n>, <param>, LowL: <x>, measure error: <e>, HighL: <y>
*** Failed GU Corr-factor limits, site <s>, test <n>, <param>, LowL: <x>, calfactor: <c>, HighL: <y>
```

Parse them with these regexes (the param name can contain spaces, so use a lazy group):

```
\*\*\* Failed GU Verification limits,\s*site\s*(\S+),\s*Device\s*(\S+),\s*test\s*(\S+),\s*(.+?),\s*LowL:\s*(\S+),\s*measure error:\s*(\S+),\s*HighL:\s*([^,\s]+)
\*\*\* Failed GU Corr-factor limits,\s*site\s*(\S+),\s*test\s*(\S+),\s*(.+?),\s*LowL:\s*(\S+),\s*calfactor:\s*(\S+),\s*HighL:\s*([^,\s]+)
```

A corr-factor line has no Device; use the `#<id>` of the `GU Device #n being run` lines seen before it (joined with commas, e.g. `"#11004,#11005"`).

## 5) GUQC outputs (the `result/` folder)

GUQC deletes and recreates `result/` on every run and reads every `.zip` (and `.gucal`) in the chosen folder and its direct sub-folders.

| Output | Content |
|---|---|
| `GuCorrFactor/`, `GuVrfyError/`, `Corr_GuCorrRawData/`, `Vry_GuRawData/`, `GuRefFinalData/`, `GuVrfyData/`, `GuCorrCoeff/` | The extracted file of every zip, copied unchanged (flat). |
| `<Name>_ALL_CONCAT.csv` (`Corr_GuCorrRawData`, `Vry_GuRawData`, `GuCorrFactor`, `GuVrfyError`, `GuRefFinalData`, `GuVrfyData`, `GuCorrCoeff`) | All files stacked. Columns `TesterName,ZipFile` are put in front. The `Parameter/Test#/Unit/HighL/LowL` rows come from the first file only; every `PID-`/numeric data row is appended. Meta block and footer are dropped. In `GuCorrFactor` and `GuCorrCoeff`, a `999` id is replaced by the quoted real ids, `"PID-#11004,#11005,..."`, taken from `GuCorrRawData` / `GuRefFinalData`. |
| `GuLog_FailedSummary.csv` | Columns `TesterName,TesterIPaddress,Date,FinishTime,Product,Sublot,ZipFile,FailType,Site,Device,TestNum,ParamName,LowL,MeasureError,HighL`. The file only holds the text `All Pass` when no line failed. |
| `1GuSummary/<zip-name>_Summary.csv` | One per zip, see section 6. |
| `Plot/` | HTML box plots of the failed parameters, plus `Summary.html`. |

## 6) `1GuSummary` layout (transposed: one row per parameter)

- Row 1: block names, row 2: column names. Blocks in this fixed left-to-right order (a block that is missing in the zip is left out): `GuRefFinalData, GuCorrRawData, GuCorrFactor, GuRawData, GuCorrCoeff, GuVrfyError`.
- Columns per block (`pids` = sorted `PID-` ids found in RefFinalData/CorrRawData/RawData/VrfyError):
  - GuRefFinalData, GuCorrRawData, GuRawData: `Parameter, Test#, <PID-...>`.
  - GuCorrFactor: `Parameter, Test#, HighL, LowL, PID-999[CF], PID-999[CF]_PF`.
  - GuCorrCoeff: `Parameter, Test#, PID-999`.
  - GuVrfyError: `Parameter, Test#, HighL, LowL, <PID-...>, <PID-..._PF...>, Cumm_PF`.
- Then one row per parameter (the `SBIN, HBIN, ..., M_Temp_...` list), then 11 footer rows in each block: `GU_Filename, GU_Date, GU_TestPlan, GU_TesterName, GU_LoadBoardName, GU_ContractorID, GU_InstrumentInfo, GU_GuBatchID, GU_Status, GU_DataType, GU_ID`. `GU_ID` is `<GuBatchID>_<PID>` (`1_PID-11004`, `1_PID-999[CF]`).
- `GU_Date` is the meta `Date` with the first two `_` turned into `/` (`2026/5/11 11:5:59`). `GU_ContractorID` reads the meta key `ContactorID` (spelled that way in the file).
- `GuBatchID` is a counter: zips with the exact same set of `PID-` ids share one id (1, 2, 3 ... in the order they are processed).

## 7) Parsing steps

1. Open the file; if line 1 is `--- Global Info:`, read `key,value` lines (use `csv.reader`) until a row whose first cell is a descriptor name. Otherwise there is no meta.
2. Read the descriptor rows `Parameter, Test#, Unit, HighL, LowL` into lists (drop the first cell), aligned by index.
3. Read data rows keyed by the first cell (`PID-...` or `999`) until a row starting with `#`; skip blank lines.
4. For any file, index values by `Parameter[i]`; a parameter name is unique per file and the same list is used for all seven files of one zip.
5. Read the PASSED/FAILED footer lines as the run result.

## 8) Pitfalls

- Data rows are very wide (about 4800 columns). Never print a whole row; slice it.
- The device ids differ between files only by the prefix: `PID-11004` in a data row, `#11004` in the log.
- Fields may be quoted when they contain commas (the concat step adds `"PID-#a,#b"`). Always use a csv reader, never `split(",")`, for these.
- Decode with `utf-8` and `errors="replace"`.
- Windows long paths: GUQC wraps every path with `long_path()` (`winpath.py`, adds the `\\?\` prefix) because zip names are long.
