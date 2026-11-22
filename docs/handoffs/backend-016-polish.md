# Hand-off: backend / 016 polish (T133) and lane L4 summary

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen (backend-api 1.15.0, worker 1.9.0); no deviations.
EF model: one migration `AddTrackAnalyses` (T048, `b5355350`); no model or snapshot change since then (Rule M).

## T133

| Commit | Content |
|---|---|
| `7e122e88` | SIG soft-limit refactor of the 016 units (splits only, no behaviour change) and hostile worker-result tests for `AnalysisResultMapper` (the open US1 item) |

The orchestrator's list (L3 `lanes-sig.txt`, L4 section) was re-checked against the current `run_all.py` output for every production file this round changed. All of those units are now at or under 15 LOC and McCabe 5, with one exception. The exception is `ApiComposition.AddGenerationServices` (28 LOC), which is feature 009 code that 016 did not touch, so I left it alone to avoid unrelated churn. The growth in `ApiComposition` (`AddStemStudioApi`, `UseStemStudioApiAsync`, `MapEndpoints`) is solved with `AddStorage`, `AddFeatureModules`, `PrepareStorageAsync`, `PrepareDatabaseAsync` and grouped endpoint maps. The instrument views register and map themselves in `InstrumentViewsRegistration`. The vocal-pitch compression comes in through an `IStartupFilter`, so `ApiComposition` gained no 016 line.

The hostile result tests cover:
- an unknown engine;
- an empty segment, a non-Harte label, an unknown quality;
- a root/bass outside 0..11, a confidence outside 0..1;
- an unknown mode, a bad alternative, a bad region;
- unknown drum pieces, negative times, strength > 1;
- a wrong hop, frames > 12,700 cents, an unknown melody source, a voiced ratio > 1;
- 90,001 frames;
- a result of another kind;
- warning-text clipping (300) and unknown warning codes dropped.

No product bug was found.

## Tests and coverage (coverlet gate, Release, full run at `7e122e88`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4219 passed | 99.16 % | 94.17 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3420 passed, 4 skipped (existing platform skips) | 98.56 % | 91.02 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: `dotnet build StemStudio.slnx -c Release` 0 warnings, 0 errors; `dotnet format StemStudio.slnx --verify-no-changes` clean;
`tools/quality/run_all.py` PASS (0 errors, 469 clones reported, so jscpd ran).

## Lane L4 checkpoints (all in `docs/handoffs/`)

`backend-016-foundational.md`, `backend-016-us1.md`, `backend-016-us2.md` (T079/T080), `backend-016-us3.md` (T086/T087), `backend-016-us4.md` (T095/T096),
`backend-016-us5.md` (T108/T109), `backend-016-us7.md` (T116/T117), `backend-016-us8.md` (T123/T124), this file (T133).

## Requests for the orchestrator (collected)

1. Contract gap: `getKey` and `getCapoSuggestions` declare no 422, but the API refuses an out-of-range `transpose` and an unknown `instrument` with a 422 validation problem, as `getChordSheet` does. Please add `'422': ValidationProblem` in the next contract revision.
2. Contract-driven choice to confirm: section profiles (`strings`/`brass`/`woodwinds`) are offered only for their own stems, added to the five 008 profiles, because `allowedProfiles`/`limits` have `maxItems: 6` (US5 decision 1).

## Security self-review (whole lane, 016)

Untrusted inputs and how each is handled:
- **Query values** (`channel`, `transpose`, `capo`, `instrument`): pattern- or range-checked, and the `channel` SQL filter is parameterised.
- **Score request enums**: bound by the JSON binder.
- **Chord edit labels**: checked against the Harte pattern.
- **ChordPro and file names**: sanitised (US1).
- **Worker results**: bounded and refused outside the contract (hostile tests above).
- **Response compression**: limited to the non-secret melody GET.

There is no free-text path into SQL, a file path or a header.

## Dependencies

None added in the whole lane.
