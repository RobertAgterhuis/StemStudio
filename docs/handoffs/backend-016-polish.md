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

## Review fixes (independent code review + security review, after `ad78d827`)

Commits: `e25d34c0` (red tests, compile seams `IStemFingerprinter` and `WorkerClient.MaxAnalysisResponseBytes` only), `90d27fa4` (fixes), `a2f3bbb7` (test helpers within SIG limits). No EF model change, no migration, no new dependency.

| Finding | Fix | Tests |
|---|---|---|
| M3 estimate of a never-run analysis | `TrackAnalysisService.GetAsync` adds the estimate a run would get (`AnalysisRunFactory.EstimateAsync`) for status `none` and for `unavailable` when the inputs exist; `EstimateSeconds` on the wire is nullable and unrestricted in 1.15.0, so no contract change | `AnalysisReviewFixTests.M3_*` (900 s melody: none + 37 s; unsplit drums: null; stored unavailable after the split returned: 10 s) |
| M4 a refused job read starves the sync pass | a `WorkerRequestException` on the job read counts as a job the worker no longer serves -> `failed` with `JobLost` (data-model 7.2); the delete after the finish is best-effort (logged), like `CancelQuietlyAsync` | `M4_ARefusedJobRead_*`, `M4_ARefusedDeleteAfterTheFinish_*` |
| L1 tunings for the suggested profile only | `tablature.tunings` lists the tunings of every allowed guitar/bass profile, the suggested instrument's first; `available`, `defaultTuning` and `capoAllowed` stay per suggested profile (their contract descriptions say so) | `TablatureScoreTests.Options_OfferTheTuningsOfEveryAllowedGuitarOrBassProfile_SuggestedFirst` (12 for guitar/bass/vocals/piano, 0 for drums) |
| L3 drum hits stay ready when their inputs vanish | yes, it can happen: cleanup/backup scrub removes the detail files (`RemoveDetailFiles`), a redone split may keep no drum stems (`NotReliable`). `RefreshStalenessLockedAsync` now marks a ready analysis without a plan stale with "The stems this analysis was made from are gone. Split the drums first." (`TrackAnalysis.MarkStale`); with `AutoDrumHits` on, the auto rule then stores `unavailable` (data-model 7.2 "prerequisite missing") | `L3_*`, Domain `StaleWithAReason_*` |
| L4 process-wide analysis lock | `AnalysisGate` locks per `(track, kind)` (one semaphore per key, kept for the process lifetime); check + insert + submit stay under one lock per analysis | `L4_AChordEditOnTrackB_IsNotBlockedByASlowStartOnTrackA` (slow fingerprinter fake), `L4_TwoStartsOfOneTrackAndKind_StillQueueOneJob` |
| S-M1 integer enum values | `ScoreRecipeRules` checks tuning, layout, instrument and clef with `Enum.IsDefined` first (422 field errors `tablature.tuning`, `tablature.layout`, `part.instrument`, `part.clef`); `ScoreNotationWire.FromWire` accepts only the exact wire name (so `instrument=0/99/-1` is 422) | `TablatureScoreTests.UndefinedCatalogueValues_*`, `KeyEndpointsTests.OutOfRangeQueries_Are422`, Domain `ValuesOutsideTheCatalogue_*` |
| S-L2 `Math.Abs(int.MinValue)` | range pattern | `transpose=-2147483648` -> 422 |
| S-L3 results outside worker 1.9.0 | key regions `startMs >= 0`, `endMs > startMs`; range `1..12700` and `low <= high`; `hopMs` must be 10 and `voicedRatio` present | `AnalysisResultMapperHostileTests` (9 new cases) |
| S-L4 2 GB response buffer | the `IWorkerAnalysisClient` HttpClient sets `MaxResponseContentBufferSize = 8 MiB`; the largest contract result (40,000 drum hits) is about 3 MB | `WorkerAnalysisClientSizeTests` (max result passes, 50 MB valid JSON -> `WorkerUnavailableException`) |

Decisions and notes:
- S-M1: `allowIntegerValues: false` is **not** applied. The converters are `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` attributes in `StemStudio.Contracts` (frozen), and an options-level override would answer integers with 400 instead of the requested 422 field errors. `Enum.IsDefined` covers every path that reaches a catalogue lookup.
- S-L4 residual: an oversized body is a `WorkerUnavailableException`, so the sync pass stops for that tick and retries the same job next tick (as for an unreachable worker). Only a worker that breaks its contract can cause this.
- L3: the cleanup/backup scrub itself calls no completion hook, so a ready drum chart stays `ready` while its files are removed. It becomes stale at the next detail completion (above). Its stored document still renders.

Contract questions for the orchestrator:
1. data-model 7.1 says `EstimateSeconds` is "shown while queued/running"; with M3 the wire also carries it for `none`/`unavailable` (the stored column is unchanged). Please align the data-model text.
2. `ScoreOptions.tablature` cannot express per-profile `available`/`capoAllowed`; the UI must derive the instrument from the tuning id prefix (`guitar-`/`bass-`) after a profile switch. A per-profile shape would need a contract revision.

Tests and coverage (coverlet gate, Release, at `90d27fa4`; `a2f3bbb7` only reshapes test helpers, the InstrumentViews namespace re-run green):

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4221 passed | 99.16 % | 94.18 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3449 passed, 4 skipped (existing platform skips) | 98.55 % | 91.00 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: `dotnet build StemStudio.slnx -c Release` 0 warnings, 0 errors; `dotnet format StemStudio.slnx --verify-no-changes` clean; `run_all.py` PASS (0 errors, 469 clones, so jscpd ran), with no new warning in a changed unit.

Security self-review: the diff touches worker data (result bounds, body size), request enums and query values. Each has hostile tests above. No new path from a request into SQL, a file path or a header.
