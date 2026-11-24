---
name: Dino-Data_Format-ATE_Data
description: ATE tester CSV format reference (GU correlation and raw data). Use when parsing or writing these files.
---

Both file types in this project share the same layout. A parser should be built once and reused for both.

## 1) Meta header block(s)

The top of the file has one or more sections. Each section starts with a marker line and is followed by `SubHeader,Value` rows until the next marker or a blank line:

```
--- Global Info:
Date,2026_9_10 17:2:12
StartTime,2026_9_10 17:0:27
FinishTime,2026_9_10 17:2:12
TestPlanVersion,1
Product,ENGR-8303-AP1-RF1
...
```

Raw tester files can have several such sections back to back, e.g. `--- Global Info:`, `--- Site details:`, `--- Options:`, `--- ConditionName:`. GU correlation files typically only have `--- Global Info:`.

There is also a `#CF` row just before the parameter table — **do not ignore it**, it carries the GU correction factor (offset) per parameter:

```
#CF,ENGR-8303-AP1-RF1_PROTO1_A5A_CF_Rev0003c.csv,,,,,,,,,,,,,,,,,,,...
```

- First cell is literally `#CF`; the rest of the row aligns column-by-column with the `Parameter` row, one GU Offset value per parameter (same alignment as `Unit`/`HighL`/`LowL` below).
- When no correction is in effect for a run, the cell right after `#CF` instead holds the correction-factor definition filename (e.g. `ENGR-8303-AP1-RF1_PROTO1_A5A_CF_Rev0003c.csv`) and the remaining per-parameter cells are blank (offset = 0/none).
- **Formula:** the measured value shown in the data rows is `Value = Old Value + GU Offset`, i.e. the raw measurement plus this row's per-parameter offset. `Old Value` is the pre-correction reading; `GU Offset` is this row's value for that parameter.

## 2) Parameter table header rows

Right after the meta header (and the `#CF` row, if present), a fixed set of rows describes every measured parameter/column:

| Row label | Meaning |
|---|---|
| `Parameter` | Column names — first cell is literally `Parameter`, the rest are the parameter/test names (this is the header row for the data table). |
| `Test#` or `Tests#` | Test sequence number in the test flow for each parameter (blank for non-test columns like `SBIN`, `DIE_X`, `SITE`, etc.). |
| `Patterns` | Optional, only seen in raw tester files — usually blank, can be ignored. |
| `Unit` | Engineering unit for each parameter (e.g. `degC`, `Sec`, `x`, or blank). |
| `HighL` | Upper spec limit (USL) for each parameter. |
| `LowL` | Lower spec limit (LSL) for each parameter. |

All of these rows (and `#CF` above) align column-by-column with the `Parameter` row.

## 3) Data rows

After the limit rows, each remaining row is one measured unit: first cell is the **Unit ID** (e.g. `PID-93`, `PID-1`), and the rest of the row is the measured value for each parameter column, aligned to the `Parameter` header. These values are already GU-offset-corrected per the `#CF` formula above (`Value = Old Value + GU Offset`).

```
PID-93,,,,,1,,,,,0,26.046875,21.7647058823529,...
PID-94,,,,,1,,,,,0,26.109375,24.1176470588235,...
```

Data rows continue until a blank line or a footer comment line (starting with `#`).

## 4) Optional footer

GU correlation files end with summary comment lines after the data:

```
#GU Correlation Summary,PASSED
#GU Verification Summary,FAILED
```

## Parsing approach

1. Read the file line by line (or via `csv.reader`) until a row's first cell equals `Parameter` — everything above that is meta header; split each header line on the first comma into `(key, value)`, grouping by the most recent `--- <Section>:` marker. Capture the `#CF` row separately (don't treat it as a discardable comment) — keep it aligned to columns like the other spec rows.
2. The `Parameter` row gives the column names for the data table.
3. The next rows (`Test#`/`Tests#`, optional `Patterns`, `Unit`, `HighL`, `LowL`) plus the `#CF` row captured in step 1 are per-column metadata — build a small spec table indexed by parameter name with columns `test_seq`, `unit`, `usl`, `lsl`, `gu_offset`.
4. All subsequent rows up to the next blank line or a row whose first cell starts with `#` (other than `#CF`, already consumed) are data rows, keyed by Unit ID (first cell), with one already-offset-corrected value per parameter column (`Value = Old Value + GU Offset`, per `gu_offset` from step 3).
5. File type (GU vs raw tester data) is determined by filename: `GuCorrRawData_*.csv` → GU; anything else (including the `.csv` packaged inside the tester's `.zip`) → raw tester data. The table structure itself is identical either way.
