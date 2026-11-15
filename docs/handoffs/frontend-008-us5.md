# Hand-off: frontend / 008 US5 (a part of the song or a library item)

Tasks 008:T068 (red) and T069 (green).

## What was built

- **Library**:
  - `asset-score-<id>` ("Sheet music", aria-label "Sheet music of <name>") on ready **audio** items, shown at every
    level. Score items and unready items have no such action.
  - It opens `ScoreDialog` with `ScoreTarget.ForAsset` (`data-source="asset"`, subject `the library item "<name>"`,
    no suggested tempo).
  - After a submit the list reloads at once and polls the new queued item (`OnSaved`).
- **Range** (Advanced+, feature `score.range`):
  - `score-range-start`/`score-range-end` in seconds (marker `sheet-music`), sent as `RangeDto`.
  - Both empty means the whole source. They are checked by `RangeInput`, which now takes a minimum: 0.5 s for
    exports (unchanged text), 2 s for sheet music ("at least 2 s long"); the range must lie inside the source
    duration from the options.
  - The fields are the shared `Components/Shared/RangeFields`, also used by the 007 export range.
- **Track page**: closing the dialog reloads the track and polls it (a drums split may have been queued in US4).
- **Refactor**: `Components/ModalDialog` is the base of `ExportDialog`, `ScoreDialog` and `CorrectMetadataDialog`
  (capture the opener, focus the first element, return the focus). This removes the production clone `run_all.py`
  reported between the correction and the sheet music dialog. The 005/007 dialog tests are unchanged and green.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T068 / T069 | b93220f (11 failing) | 6174442 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1226, Total: 1226
| StemStudio.Web | 99.53% line | 92.85% branch | 99.9% method |
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `tools/quality/run_all.py`: 1311 files, **0 errors**, exit 0. There are no duplication findings in Web production
  code.

## Dependencies added

None.

## Notes for qa

- For a raw single-channel export the backend suggests a profile; for a mixed export the dialog shows "Choose an
  instrument" and requires a choice.
- The score item's library source shows the provenance track (`asset-source-<id>`). The audio item is recorded in
  `provenance.sourceAssetIds`, which the UI does not show.

## Next owner

frontend (008 polish T075); qa T070.
