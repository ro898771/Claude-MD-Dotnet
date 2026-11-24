---
name: Dino-Data_Format-Trace_Fmtrace_Munich
description: Format of FmTrace (.fmtrace) archives and their snp files. Use when reading or compiling FmTrace.
---

# FmTrace (`.fmtrace`) format

Reference: QuickMi2e `src/lib/event/Compile_fmtrace.py`, `src/lib/view/FmFilter_Dialog.py`, `configSetting/fbar.json`, `header_config.json`, and the samples in `dataset/fmtrace`. An FmTrace archive holds the S-parameter traces of the units of one lot, written by an FBAR VNA test, one file per unit and channel. It is a different file family from Tiger/Cntrace (`Dino-Data_Format-Trace_Cntrace_Seoul`): the data files are **tab-separated, one trace per file**, with a JSON line instead of `--- Section:` blocks.

## 1) The package

- A `.fmtrace` file is a **zip archive** with another name (the extension is matched in any letter case: `.fmtrace`, `.FMTRACE`). QuickMi2e copies it to a staging `.zip` and extracts it; the original is not touched.
- Inside, files sit side by side (no folders that matter; QuickMi2e flattens them):
  - the **data files**, `Unit<PID>_Chan<n>_<Product>_S<n>.<ext>`, e.g. `Unit46_Chan1_ENGF-8251-AP1-RF1_BE-PXI-KEYSIGHT_S1.snp`. `<ext>` is one of the extensions listed in `configSetting/fbar.json` (`snp`, `nf`, `pwr`, `h2`; `txt` is also listed);
  - one lot information file `<LOT>.txt`, e.g. `FT6421408244-E.txt` (section 3).
- Archive names look like `ENGF-8251-AP1-RF1_BE-PXI-KEYSIGHT-V0005_FT6421408244-E_1AEXTB5_20261022_021905_IP172.16.7.62_QAM2AT3.fmtrace` = `<Product>-<Version>_<LOT>_<SubLot>_<yyyyMMdd>_<HHmmss>_IP<tester ip>_<code>`.
- The unit counter restarts in every archive, so member names repeat from archive to archive. QuickMi2e therefore saves each data file as `<archive name>__<member name>` (two underscores), e.g. `...QAM2AT3__Unit46_Chan1_..._S1.snp`. The `.txt` keeps its own name.
- A source folder may also hold files that were already extracted (`.snp`, `.h2`, `.nf`, `.pwr`, `.txt` sitting directly in it); QuickMi2e takes them as they are and also unzips any `.fmtrace` found next to them.

## 2) Layout of a data file (`.snp`)

```
#<TAB>HZ<TAB>S<TAB>dB<TAB>R 50
!{"schema_version":"1.1.1","trace_name":"TRIGGER-CH1","date":"2026-10-22T02:23:44.773Z","lot_id":"FT6421408244-E","sublot_id":"1AEXTB5","z_conversion":{},"h2_power":{},"s2p_embedding":{"p1":"TROSSI_P1_VectorBlock_SOL_SPAR_InBlock.S2P","p2":"...","p3":"...","p4":"..."}}
!Freq<TAB>S11<TAB><TAB>S12<TAB><TAB>S13<TAB><TAB> ... S44<TAB><TAB>
100000000<TAB>-0.486328963774277<TAB>160.87625931339<TAB>-63.041324736355<TAB>174.313232885664<TAB> ...
150000000<TAB>...
```

- **Tab-separated**, lines end with CRLF, no BOM. A fixed layout by line number:
  1. **Option line** `# <freq unit> <parameter type> <format> R <z0>`: here `HZ`, `S`, `dB`, `R 50`. Only the first word (the frequency unit, upper-cased; `HZ` if missing) is used.
  2. **Metadata line** `!` followed by one JSON object. Used: `lot_id` (required) and `sublot_id`. Others seen: `schema_version`, `trace_name` (e.g. `TRIGGER-CH1`), `date` (ISO time of the test), `z_conversion`, `h2_power`, `s2p_embedding` (the embedded port files `p1`..`p4`). Some exporters (seen on `.h2` / `.nf` / `.pwr`) wrap the whole line in CSV-style quotes with the inner quotes doubled (`""`); remove the outer quotes and turn `""` back into `"` before parsing.
  3. **Parameter header** `!Freq` and then each parameter name followed by one empty tab slot (the slot of its second value). Take every second token after `!Freq`, drop empty ones and repeated names. In an `.snp` the names are `S<i><j>` (the sample has 16 for a 4-port trace); in `.h2` they are other names (e.g. `B1D2SAM`), so do not assume an S-parameter pattern.
  4. **Data rows** from line 4: frequency, then **two numbers per parameter**. A row has `1 + 2 x parameters` columns (33 for 16 parameters). Lines starting with `!` or `#` and blank lines are skipped. The sample has 2,151 rows from 100 MHz to 20 GHz.
- The two numbers of a parameter follow the format of the option line. With `dB` they are the magnitude in dB and the phase in degrees.

## 3) The lot file `<LOT>.txt`

`key=value` per line, e.g.:

```
DATE_START=20261022   TIME_START=022344   ENTITY=NA   SOURCE=FBAR   GROUP=N-PLEXER
PRODUCT_TAG=ENGF-8251-AP1-RF1_BE-PXI-KEYSIGHT   LOT_NUMBER=FT6421408244-E_1AEXTB5   WAFER_ID=NA
OPERATOR_NAME=1   TESTER_NAME=FBAR   TESTER_HOST_NAME=EIS194-02   HANLDER_NAME=EIS194   TEST_PLAN_VERSION=1
EmbedS2PFile-Port1=...S2P  (Port1..Port4)   INSTRUMENT_INFO=VNA=M9804A A.19.10.09*MY58100803
CALSUB_DATE=yyyyMMdd   CALSUB_TIME=HHmmss   2DID_READBACK_YIELD=97.83
```

(every pair is on its own line in the file; `HANLDER_NAME` is spelled so in the file). Only `TESTER_HOST_NAME` is used (the tester name `TT` of the output name); `NA` or a missing file means no tester name. The file is found by `<lot_id>.txt` where `lot_id` is the one in the JSON line of the data file.

## 4) What QuickMi2e does with it

Choose the file type FmTrace, then the compile mode of the FmTrace dialog. Each mode first clears (or backs up) `dataset/results`:

| Mode | Which archives and members | Extracted into `dataset/fmtrace/` |
|---|---|---|
| Default | every member whose extension is in `fbar.json` | one folder per extension: `snp/`, `nf/`, `pwr/`, `h2/`, `txt/` |
| Channel (`Chan<N>`) | members whose own name contains `Chan<N>` (any case); `.txt` always | `channel<N>/` and `txt/` |
| SubString | **all** members of the archives whose **archive name** contains the text (any case) | `substring-<text>/` and `txt/` |
| Regex | all members of the archives whose archive name matches the regex (`re.search`, any case) | `regex/` and `txt/` |

Then every data file in the chosen folders is converted to csv (`snp`, `nf`, `h2`, `pwr` in Default mode; the mode's folder otherwise; `txt` is never converted) into `dataset/results/<folder>/<header label>.csv`.

- **Columns:** `freq[<unit>]` (e.g. `freq[HZ]`), then for every parameter `<name>_Mag` and `<name>_Phase`, in the file's order, numbers as read. The first value of every pair is called `_Mag` even when the option line says `dB`: the value is not converted.
- **Header label** = `KEY[value]` pieces joined by `_`, for the keys switched on in `configSetting/header_config.json`, section `FmTrace` (default on: `LOT`, `PID`, `DateTime`; off: `SL`, `TT`, `WF`). The pieces follow the order of the keys in that section (`LOT, SL, TT, WF, PID, DateTime`); when the section's `Default` is true, the fixed set `LOT, SL, TT, PID, WF, DateTime` is used instead of the ticked keys. An empty value is left out, and `WF` is never filled for FmTrace, so it never shows. Example: `LOT[FT6421408244-E]_PID[46]_DateTime[20261104_101530_123].csv`.

| Key | Read from |
|---|---|
| LOT / SL | `lot_id` / `sublot_id` of the JSON line |
| TT | `TESTER_HOST_NAME` of `<lot_id>.txt` |
| PID | the digits after `Unit` in the **file name** (`Unit46` -> `46`) |
| DateTime | **the time of the compile**, `yyyyMMdd_HHmmss_mmm`, not the test time (the test time is `date` in the JSON line) |

- A parameter group of the result is chosen later by the folder: `results/snp`, `results/h2`, ... (QuickMi2e's batch and plot steps take the folder name as the "Channel Group"; a name like `snp#1` is a chunk of `snp`).

## Not verified

- QuickMi2e's repository has `.snp` samples only (49 files, all `Chan1`, one trace `TRIGGER-CH1`, 4 ports). The rules for `.h2`, `.nf` and `.pwr` come from the code and its comments, not from real files.
- Nothing in the repository names a "Munich" site: this skill is named by the request. Check that a Munich file has the same line layout (option line, JSON line, header line) before relying on it.

## Pitfalls

- A data file is **skipped** (a warning in the log, no csv) when: line 2 has no JSON with `lot_id`; there are fewer than 4 lines; there are no parameter names; there are no data rows; or the first data row does not have exactly `1 + 2 x parameters` columns.
- The files are tab-separated, not comma-separated: never `split(",")`.
- Two units with the same `LOT`, `PID` and compile millisecond would give the same csv name; the millisecond makes that rare, not impossible.
- A data file name without `Unit<digits>` gives no `PID` in the output name.
- Windows long paths: prefix with `\\?\` (QuickMi2e's `long_path`); archive names plus the `__` prefix get long.
