# Hand-off: frontend / 007 US5 (stem pack)

Tasks 007:T061 (red) and T062 (green).

## What was built

- Track page: `export-all-stems` "Save all stems" (catalogue feature `export.stem-pack`, Advanced+, marker
  `export-actions`) opens `ExportDialog` with `data-mode="stem-pack"`: format picker only (no name, no range),
  `export-include-detail` only when the track has split parents with kept detail stems.
- Submit -> `POST /api/assets/stem-packs` (`CreateStemPackRequest(trackId, format, includeDetail)`); confirmation
  "Saved N items to the library, one per stem. ..." with `export-saved-open` = `library?asset=<first item id>`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T061 / T062 | 1db9996 (3 of 4 fail) | 33dfbee |

## Tests and coverage

```text
Passed! - Failed: 0, Passed: 988, Total: 988
| StemStudio.Web | 99.15% line | 92.64% branch | 99.64% method |
```

## Quality checks

Build 0 warnings, format clean, `run_all.py` 0 errors.

## Next owner

frontend (007 polish T067), qa (007:T063).
