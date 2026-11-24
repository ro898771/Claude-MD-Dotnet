---
name: Dino-Data_Format-Spec_TSF
description: TSF (Test Specification File) csv format and limit refinement. Use when reading, editing or generating a TSF.
---

A TSF is one csv of `#SECTION ... #END` blocks. Fields are comma separated, no quoting needed (parameter names have no commas). The file starts with a UTF-8 BOM and every block is padded with trailing commas to the width of the widest row (49 columns for 22 soft bins).

## 1) Layout (in this order)

```
#HEADER                       TestMode,Production / Title / Date / Author / Description / SpecVersion   (Key,Value rows)
#END
#
#CONTROL_PARAMETERS           TotalBinYieldAlarmLimit, StopAfterBinLimitFail, StopAfterAnyParaLimitFail, StopRequiredUnitsCount,
                              ContinuousUnitsPass/FailAlarmLimit, MoveToQABinUnitsCount, MoveToQAHwBinNum, MoveToQABinUnitsLimit,
                              ExpediteLotTraceUnitsCount/Limit   (Key,Value rows)
#END
#
#HWBIN_DEFINITION            <hwbin>,<name>,<softbin>,<softbin>,...   e.g.  4,FAIL_RF,2,3,4,...,19   (which soft bins land in which hard bin)
#END
#
(blank line)
#SWBIN_DEFINITION            <softbin>,<name>,OR,<softbin>             e.g.  8,F_IL_AVG,OR,8      (1 = PASS ... last = FAIL)
#END
#
#SERIAL_DEFINITION
,,,,,1,,2,,3,,...,N           row of soft bin numbers, one over each Min/Max pair (starting at column 6)
TestNumber,TestParameter,ColumnDisplayFlag,ChartDisplayFlag,FailThresPercent,Min,Max,Min,Max,...   (a Min,Max pair per soft bin)
<n>,<parameter>,1,0,0,<min>,<max>,<min>,<max>,...        one row per parameter, to the line before the last #END
#END
```

Rows of SERIAL_DEFINITION: columns 1-5 are TestNumber, TestParameter, ColumnDisplayFlag, ChartDisplayFlag, FailThresPercent. From column 6 on, pair k (columns 6+2(k-1), 7+2(k-1)) is the **Min (LSL) and Max (USL) used for soft bin k**. `None` = no limit on that side.

## 2) How the soft-bin limits behave

- Soft bin 1 (PASS) holds the pass spec: its pair (columns 6, 7) is the parameter's LSL / USL.
- A parameter is repeated in the pairs of the soft bins that are evaluated **before** the soft bin it fails into; from its own fail bin onward the pairs are `None`. E.g. an ESD parameter (fail bin 21) has its limit in bins 1-20 and `None` in 21-22.
- In practice the limit is the same number in every non-`None` pair of a row. When changing a spec, **change every non-`None` Min and/or Max cell of the row**, never only bin 1.
- Rows with no limits at all (info columns, `M_TEST-CHECK...`) are all `None`.
- Companion rows such as `..._LErosLimit`, `..._UErosLimit`, `..._ErosFlag` are not normal specs (flag ±0.1, limits `None`): leave them alone unless asked.

## 3) Rules when producing a new revision

1. Never touch the first 60 lines (header, control parameters, HW bin, SW bin, SERIAL header rows) or columns 1-5 of any parameter row: soft bin definitions must stay byte-identical. Verify with `diff <(head -60 old) <(head -60 new)` and `cut -d, -f1-5` on the parameter rows.
2. Edit by splitting each parameter row on `,`, replacing only Min/Max cells, joining again. Keep BOM, the `\r\n`/`\n` ending the file already uses, the line count and the column count (49 for 22 bins).
3. Write a **new file** (Rev04 etc.); leave the old revision alone. Also write a change report csv (parameter, N, mean, sd, old LSL/USL/CPK, new LSL/USL/CPK).
4. Number format when the user wants fewer decimals (e.g. "2 decimal place for all"): format every limit with `0.00`, rounding **outward** (USL up, LSL down) using `decimal` arithmetic (not double: `0.07*100` is 7.000000000000001), so no limit ever tightens and CPK never drops under the target.

## 4) Raising failing parameters to a target CPK

- Failing = any unit outside its LSL / USL (blank cells skipped). In this tool: `ChartData.FailedParameters(data, parameters)`. Use `ChartData.StatsFor(data, p, excluded, offset:true)` for N / Mean / StdDev (sample sd, n-1) per TSF set.
- CPK = min((USL-mean)/3s, (mean-LSL)/3s). For target T: a side below T moves to USL = mean + 3T s, or LSL = mean - 3T s (CPK 2 -> 6 sigma, CPK 3 -> 9 sigma). A side already at or above T is left as it is. Never tighten. Skip parameters with no spread (s = 0).
- Take the "old" limits from the original TSF, not from a Spec file the data may already have been refreshed with.
- Ignore on request: parameters by pattern (e.g. names containing `GD`, `Eros`, a named `M_2DID-SN`); they keep their rows unchanged.
- This widens the spec to fit the data, it does not improve the process. Say so in the answer, give the biggest moves, and ask the limit owner to confirm.
- Specific edits the user asks for (e.g. `M_2DID-SN` USL = 11000) are applied on top, to every non-`None` Max cell of that row.

## 5) Combine-Spec_All.csv (the tool's limit file for a combined data set)

```
Parameter,<TSF file name> LSL,<TSF file name> USL[,<next TSF> LSL,<next TSF> USL ...]
<parameter>,<lsl or empty>,<usl or empty>
```

One LSL/USL column pair per TSF present in the data, named exactly as the data's SpecName (e.g. `ENGF-9272-AP1-RF1_TSF_Rev03.csv`; keep that name if the file should apply to data already loaded). Rows are the parameters that have a limit, in the data's order; write the soft-bin-1 Min as LSL and Max as USL, empty for `None`. UTF-8 with BOM. Copy it next to `Combine.csv` (Output/Combine/) and the tool reads it when a file is loaded or Load / Failed only is used.
