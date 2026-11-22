# Hand-off: backend / 016 US3 (T086, T087)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen; no deviations. No EF model change.

## Tasks (red -> green)

| Task | Red | Green |
|---|---|---|
| T086/T087 `getDrumHits`, `detectDrumHits`, `deleteDrumHits`: GET with `unavailable` "Split the drums first.", 409 `drums-not-split`/`analysis-running`, 503, auto start after the drums split, stale after a new split, DELETE cancels the job; ids un-staged | `a7a9199d` | `ae06bc60` |

## Tests and coverage (coverlet gate, Release, full run at `ae06bc60`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4198 passed | 99.13 % | 94.19 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3356 passed, 4 skipped (existing platform skips) | 98.44 % | 90.70 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: build 0 warnings; `dotnet format --verify-no-changes` clean; `tools/quality/run_all.py` PASS (0 errors, 460 clones, so jscpd ran).

## Decisions

1. New `PlainAnalysisEndpoints.MapPlainAnalysis<TDto>(PlainAnalysisRoute<TDto>)` maps GET/POST/DELETE for an analysis without a request body. It reuses `AnalysisEndpoints.GetAsync/StartAsync/DeleteAsync` and the default run options. Drum hits use it now, and the reference melody (T096) will use it too, so the two endpoint groups share one mapping and are not copied.
2. Problem `type` of the instrument-view endpoints is the URI form `https://stemstudio.local/problems/<code>` (`PracticeFlowProblems.Conflict`, as the US1 chord endpoints). The score endpoints keep the 008 bare codes. The T086 tests first asserted the bare code and were corrected in the green commit.

## Security self-review (US3)

No new untrusted input: the routes take only the `trackId` GUID (route constraint). The worker drum-hit results are already bounded by the foundational applier.

## Dependencies

None added.
