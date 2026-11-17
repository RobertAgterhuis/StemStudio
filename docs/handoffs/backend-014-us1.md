# Backend 014 - HO:us1 (lane L3)

Tasks T054-T055 (+ the `replaceSections` endpoint, tested in T070 next). Commit 43808de5.

## What is in

- `GET/PUT /api/tracks/{id}/sections`, `POST .../sections/detect` (`src/StemStudio.Api/Sections/`, namespace
  `StemStudio.Api.SongSections` - `StemStudio.Api.Sections` would shadow a `Sections` member used by existing contract
  tests). Problems: `track-not-ready`, `sections-edited`, `detection-running`, `sections-changed` (409),
  `worker-unavailable` (503), 422 field errors; all with `type = https://stemstudio.local/problems/<code>`.
- One `SectionDetectionGate` lock around check + submit + save (four parallel detects -> one job, tested).
- `ITrackCompletionHook` (Sync): `TrackSynchronizer` runs the hooks after a separation completed and was saved; a failing
  hook is logged, the track stays completed. `SectionDetectTrigger` submits when the set is missing or `none` and
  `StemStudio:Practice:SectionDetectOnComplete` is on; a worker failure leaves no row (status `none`).
- `SectionJobApplier` in the `JobSyncService` pass: running -> detecting, completed -> new sections (revision + 1),
  failed/cancelled -> reason, lost job -> "The detection was interrupted; try again."; the worker job is deleted after
  applying. Retrying a failed separation keeps the sections.
- `startBar` = number of downbeats up to the section start (0 = pickup), only with a grid.
- Contract: `PracticeFlowOperations` (served) / `PendingPracticeFlowOperations` in `BackendContractTests.PracticeFlow.cs`;
  `BackendContractTests.cs` line 214 spreads both (ownership given by the orchestrator).

## Tests and coverage (Api full suite with coverage)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Api (+Contracts) | 2995 passed, 4 skipped | 98.14 % | 91.1 % |
| new 014 Api files (Sections, Practice registration, Sync changes, worker practice) | - | 94.4 % | 91.4 % |

Domain and Persistence unchanged since HO:foundational (98.98/94, 99.32/94.73). `dotnet format` clean, build 0 warnings.

## Decisions

- Field errors use the request paths (`sections[1].startSeconds`); unknown kinds from the worker map to `other`,
  unknown warning codes and snaps are dropped.
- E2E part: see `qa-014-us1.md`.
