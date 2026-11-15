# Hand-off: frontend / 007 US4 (export a range)

Tasks 007:T056 (red) and T057 (green).

## What was built

- `Components/Export/RangePicker.razor` (catalogue feature `export.range`, Advanced+), in the stem and mix dialogs (not
  the stem pack): `export-range-loop` "Only the loop region" exists only when the mixer has a loop region and fills
  start/end from it (unticking clears them); `export-range-start` (marker `export-range`, `HelpHint`) and
  `export-range-end` are `type="number"` inputs in seconds.
- `Export/RangeInput.cs`: empty/empty = whole song (`range: null`); otherwise both must be numbers
  (`IncompleteMessage`), `end - start >= 0.5` (`TooShortMessage`) and `0 <= start`, `end <= duration`
  ("The range must lie inside the song (0 to 3:33)."). Errors appear in `export-error` before any request.
- Help topic `tracks/export-range.md` (feature `export.range`).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T056 / T057 | 91c3e68 + 458e90c (build fix; 12 of 12 fail) | 39fd8fa |

## Tests and coverage

```text
Passed! - Failed: 0, Passed: 983, Total: 983
| StemStudio.Web | 98.8% line | 92.06% branch | 99.04% method |
```

## Quality checks

Build 0 warnings, format clean, `run_all.py` 0 errors (the `RangeInput.Validate` complexity warning is gone).

## Notes for qa

- The loop values are written with up to 3 decimals (invariant culture), so a 2.500 s loop is sent as 2.5.

## Next owner

frontend (007 US5), qa (007:T058).
