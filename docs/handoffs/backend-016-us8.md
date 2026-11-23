# Hand-off: backend / 016 US8 (T123, T124)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen. No EF model change. Every 016 operation is now served (`PendingInstrumentViewOperations` is empty).

## Tasks (red -> green)

| Task | Red | Green |
|---|---|---|
| T123/T124 `getCapoSuggestions?transpose`: up to three capos 0..7 ranked by `CapoAdvisor` (open share minus fret penalty), shape key, `fewOpenShapes`, the `original-shapes` shortcut, 409 `chords-not-ready`, 404, 422 on transpose; the best capo on the sheet and in ChordPro shows the open shapes and `{capo: n}`; un-staged | `bb00e58f` | `e664ff58` |

## Tests and coverage (coverlet gate, Release, full run at `e664ff58`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4219 passed | 99.16 % | 94.21 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3396 passed, 4 skipped (existing platform skips) | 98.55 % | 90.82 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: build 0 warnings; `dotnet format --verify-no-changes` clean; `tools/quality/run_all.py` PASS (0 errors, 469 clones, so jscpd ran).

## Decisions

1. The timed chords are the segments with a root and a quality ("N" is left out), with the duration `endMs - startMs`. Shape keys are named in the chart's key; a chart without a key uses C major. This case is tested but not seen with the real engines, which always report a key.
2. The suggestions use the ready or stale chart, like the sheet and ChordPro (`ChordService.FindReadyAsync`).

## Requests for the orchestrator

- Same contract gap as US7: `getCapoSuggestions` declares no 422, but an out-of-range `transpose` is refused with a 422 validation problem (as `getChordSheet`). Please add it in the next contract revision.

## Security self-review (US8)

The only input is `transpose` (an int, range-checked). The output has no user text.

## Dependencies

None added.
