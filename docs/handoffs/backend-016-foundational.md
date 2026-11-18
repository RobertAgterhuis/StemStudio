# Hand-off: backend / 016 foundational (T039-T052)

Lane L4, branch `016-round-lane-l4` (from the 016 design head `7463ac54`). Not pushed, not merged.

## Model freeze (rule M)

The EF model of round 016 is **frozen** with migration `20261118055631_AddTrackAnalyses` (table `TrackAnalyses`, key
`(TrackId, Kind)`, FK to `Tracks` with cascade delete, index on `Status`; columns per data-model 7.1: `Reason`, `JobId`,
`Progress`, `OptionsJson`, `DocumentJson`, `Engine`, `DeviceJson`, `SourceFingerprint`, `EstimateSeconds`, `UpdatedAt`).
The 016 score additions (`ScoreFacts.Tablature/Part/ChordsIncluded`, `ScoreRecipe.Tablature/Part/IncludeChords`) live in the
existing JSON columns, so they need no migration. No further 016 migration is planned.

## Tasks (red -> green)

| Tasks | Red | Green |
|---|---|---|
| T039/T040 TrackAnalysis + AnalysisRules | `4b5f79ca` | `ddef02a5` |
| T041/T042 HarteLabel, ChordSpeller, KeyFormatter, ChordSheetBuilder, ChordProWriter (all 245 + 2211 + 90 vectors) | `4b5f79ca` | `ddef02a5` |
| T043/T044 CapoAdvisor (12 capoFixtures) | `4b5f79ca` | `ddef02a5` |
| T045/T046 InstrumentCatalogue, FamilyRule | `4b5f79ca` | `ddef02a5` |
| T047/T048 persistence + migration `AddTrackAnalyses` | `96c57730` | `b5355350` |
| T049/T050 `IWorkerAnalysisClient` (`WorkerClient.InstrumentAnalysis.cs`) | `d9e6849b` | `671f588e` |
| T051/T052 `TrackAnalysisService`, `AnalysisKindStrategies`, `AnalysisJobApplier`, `AnalysisAutoStarter` | `d9e6849b` | `671f588e` |
| SIG fix (5 parameter errors) + format | - | `ab375503`, `e80c885d` |

## Tests and coverage (coverlet gate, Release)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4165 passed | 99.09 % | 94.14 % |
| StemStudio.Persistence | 178 passed | 99.48 % | 95.12 % |
| StemStudio.Api (+ Contracts) | 3289 passed, 4 skipped (existing platform skips) | 98.49 % (Contracts 100 %) | 90.69 % (Contracts 100 %) |

Quality: `dotnet build StemStudio.slnx -c Release` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean;
`tools/quality/run_all.py` **PASS** (0 errors, 454 clones, jscpd step ran; the first run found 5 parameter errors in new code,
fixed in `ab375503`).

## Decisions

1. Source fingerprint = SHA-256 over sorted `channel=sha256(file)` lines of the input stem files in local storage (a missing
   file counts as `missing`). Staleness is checked when a standard separation or a detail split completes (new
   `IDetailCompletionHook`, run by `DetailSynchronizer` after the save, failures logged like `ITrackCompletionHook`).
2. Effective status: a never-run analysis whose prerequisite is missing reads `unavailable` with its reason ("Split the drums
   first.") in GET and in `Track.instrumentViews`, without writing a row; an automatic start with a missing prerequisite stores
   `unavailable`.
3. One `AnalysisGate` lock around check, insert and submission (LESSONS 023); the job applier polls the worker outside the
   lock and applies only when the stored job id is unchanged (a delete or re-run in between wins).
4. Worker results are checked against the contract limits (segments, regions, hits, frames, pitch classes, enums) before
   storing; an out-of-contract result fails the analysis ("unusable result"). Unreadable `DocumentJson` (an archive from
   outside) loads as no document.
5. Estimates read the worker `/health` `instrumentAnalysis` lane (engines, BTC model readiness) and the device; an older or
   unreachable worker gives the defaults (`btc`, `pyin`, CPU). Factors in `StemStudio:InstrumentViews:Estimates`.
6. Track domain rules make "no vocals stem" and "no harmonic stem" impossible for real tracks; the prerequisites are kept
   defensively (contract `409 no-vocals`), chords without a harmonic stem would be stored `unavailable`.
7. Auto rules: chords after the standard separation, drum hits after a detail split; stale chords/drum hits without edits run
   again; vocal pitch never starts on its own; a failed analysis is never retried automatically.

## Deviations

None from the contracts.

## Open items

- US1 endpoints (T064/T065, T072/T073) in progress.
- `AnalysisResultMapper` branch coverage is 74 %; more hostile-result cases planned for HO:polish.

## Security self-review

Worker paths: every input path is built from the track id and catalogue names (`RenderChannel.PathFor`), never from a
request; fingerprint files come from the stem stores (contained in the data directory); job ids are checked by the
`[0-9a-f]{32}` pattern before any URL is built (tests with `../../etc/passwd`). Worker results are bounded and validated
(hostile-result theory, 4,001 segments, 90,001 frames). Hostile `DocumentJson` (unknown discriminator, junk, deep nesting)
loads as null.

## Dependencies

None added.
