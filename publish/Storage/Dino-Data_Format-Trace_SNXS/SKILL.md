---
name: Dino-Data_Format-Trace_SNXS
description: Format of the binary .snxs S-parameter container. Use when reading, decoding or compiling SNXS files.
---

# SNXS (`.snxs`) binary format

Reference: QuickMi2e `src/lib/event/Snxs_Engine.py` (decoder of the SNXFormat.Core classes `SNXDecoder`, `SNXFormatConstants`, `SNXFormatTypes`), `Compile_Snxs.py`, `docs/SNXMetaInformation.md`. An SNXS file is the compact, binary form of what a Tiger/Cntrace file holds as text: the same unit information plus S-parameters of many traces. All numbers are **little-endian**.

## 1) Container

| Offset | Size | Content |
|---|---|---|
| 0 | 4 | `53 4E 58 53` = `SNX` + `0x53` ('S'). Anything else: "Invalid SNXS signature". Smaller than 8 bytes: too short. |
| 4 | 4 | `uint32` entry count N |
| 8 | 16 x N | Index table, one record per entry: `uint64` offset of its SNX data, `uint32` size, `uint8` port count, 3 reserved bytes |
| next | 4 | `int32` common meta size M |
| next | M | Common meta (section 2); absent when M is 0 |
| then, per entry | | `int32` basename length + basename (UTF-8), `int32` correction length + correction (UTF-8), then the SNX data (`size` bytes). When the stream position is not the index `offset`, the index offset wins. |

Check `offset + size` against the file length for every entry.

## 2) Common meta

`int32` field count, then per field: `int32` name length, name (UTF-8), `int32` value length, value (UTF-8). Stop at the first record that does not fit. The meta describes the unit (shared by all entries):

`Date, ProgramName, Lot, TesterName, Product, ExecRevision, SpecName, HostIpAddress, ContactorID, InstrumentInfo, PID, MFGID, MODID, WAFERID` (14 properties of the C# `SNXMetaInformation`; keys are matched case-insensitively). The code also has a helper that would take a meta key `TN`, `Channel`, `Unit` or `Folder` as the folder name, but the compile flow does not use it: the folder comes from each entry's basename (section 4).

## 3) One SNX entry (the data blob)

| Offset | Size | Content |
|---|---|---|
| 0 | 3 | `SNX` |
| 3 | 1 | format version: `0` or `1` |
| 4 | 4 | `uint32` encoded frequency count F |
| 8 | 5 x F | frequency entries |
| after | 5 x (nf x P x P) | S-parameter entries, P = port count, nf = number of decoded frequencies |

**Frequency entry** (5 bytes = a 40-bit integer): bits 0-37 value in Hz, bit 38 *step flag*, bit 39 sign (negative). A normal entry appends one frequency. An entry with the step flag is a **count** C (needs C >= 3 and two earlier entries): it removes the last two frequencies (start, stop) and puts C evenly spaced values from start to stop inclusive (`start + step * j`, step = (stop - start) / (C - 1)). So nf is usually larger than F.

**S-parameter entry** (5 bytes = a 40-bit integer), in the order frequency -> row r -> column c (r, c = 1..P, row first): the low 19 bits = phase, `value / 1000` degrees; the next 21 bits (bits 19-39) = magnitude in dB: version 0: `value / 1000 - 1000`; version 1: `value / 4000 - 256`.

## 4) What QuickMi2e writes

- One csv per entry: `results/<folder>/<header label>-TN<n>.csv`. `<folder>` = the entry's basename with leading TN zeros removed (`TN0007...` -> `TN7...`); `<n>` = the digits of the first `TN<digits>` in it.
- **Header label**: built from the meta exactly as for Tiger files (`KEY[value]` pieces joined by `_`, keys chosen in `configSetting/header_config.json`, default `LOT`, `SL`, `MOD`, `PID`, `DateTime`). Meta -> key: `Date` -> DateTime (`YYYYMMDD-HHMMSS-` + 4 digits), `Lot` -> LOT, `Product` -> PROD, `TesterName` -> TT, `ContactorID` -> CID, `PID`, `MFGID` -> MFG, `MODID` -> MOD, `WAFERID` -> WF; `AssemblyLot`, `PcbLot`, `SubLot`, `HandlerID`, `HandlerType`, `LoadBoardName`, `FullPackageID`, `ClothoMode`, `TCFVer` are mapped too but the C# class does not carry them yet, so those keys stay empty and are left out. In `PROD`, `CID` and the other mapped text keys `_` becomes `-`.
- Columns: `freq[HZ]`, then for each S-parameter in order `dB:S11`, `ang:S11`, `dB:S12`, `ang:S12`, ... (the TouchStone layout, see `Dino-Data_Format-TouchStone`), unlike the Tiger csv (`Freq`, `S11_Mag`, `S11_Phase`).
- Mixed folders: a source folder with `.snxs` and `.tiger`/`.cntrace` files is compiled by both flows; each writes its own channel folders into the same `results`.

## Not verified

QuickMi2e's repository holds no `.snxs` sample. The layout above is read from the decoder code; check it against a real file (signature, a known unit's `PID`, the first frequencies) before trusting a new writer or reader.

## Pitfalls

- Read the whole file as bytes and slice; entries can be large and the offsets are absolute.
- Do not assume one entry per file: one `.snxs` holds many traces (index table), all with the unit's common meta.
- Windows long paths need the `\\?\` prefix.
