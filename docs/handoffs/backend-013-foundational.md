# Hand-off: backend / 013 foundational (T013-T024)

Branch `013-lane-backend` (from `013-library-care` 7253cf37). Red -> green commits per task pair.

## What was built

- **Domain `LibraryCare/`** (T013/T014): `LibraryLayout` (the binding data-model 3.3 patterns, `Match`/`IsAllowed`,
  archive entry rules `IsSafeEntryName`/`ClassifyEntry`, `ToEntryName`/`ToDataPath`), `StorageClassifier` + `LibraryIndex`
  (layout + `*.tmp`, unreferenced uploads, rows missing -> `leftovers`; `library-care/staging|previous` rows; other
  library-care bookkeeping is not counted), `BackupCategory`/`StorageCategory`/`BackupCategories.ForAsset` (recordings by
  the provenance origin *name*, so 015 needs no change here), `Regenerability`, `CleanupKind`/`CleanupRules`.
  `LayoutAllowListTests.Allowed` now calls `LibraryLayout.IsAllowed` (architect note).
- **Consent** (T015/T016): `ConsentStatement.Known(version)`, `ConsentProof`, `ConsentProofRules` (`Check` in the
  data-model 5.4 order, `IsComplete`, `IsStatementValid`, `Reason`).
- **Gate** (T017/T018): `Api/LibraryCare/Gate/` - `LibraryGate` (Open/BackupRunning/Maintenance, in-flight counter,
  `WaitForIdleAsync`, `WaitWhileMaintenanceAsync`), `RequireGateOpen()` on the ten 001-011 operations of data-model 5.2,
  `UseLibraryGate()` middleware (503 `library-maintenance` + `Retry-After: 5`; `GET /api/library-care/status` and
  `GET /api/library-jobs/{id}` pass; `/health` is outside `/api`), `PeriodicSyncService` skips ticks and
  `AgentRunExecutor` waits while in maintenance. `GateProblems.Typed` builds every library-care problem
  (`type` = `https://stemstudio.local/problems/<code>`).
- **Jobs** (T019/T020): `Api/LibraryCare/Jobs/` - `LibraryJobRunner` (one job at a time, live progress, ETA from a
  10 s moving average after 5 s, cancel, `LibraryJobException` codes, unexpected -> `io-error` without the exception text),
  `LibraryJobStore` (JSON records, last 50 by `createdAt`, report written through `LibraryJobDto.Report` so `type` is
  emitted), `LibraryJobRecovery` (start-up: queued/running -> failed `interrupted`, `.partial` and staging removed),
  `LibraryCarePaths`, `ArchiveNames`. Options `StemStudio:BackupDir`, `BackupHostDirHint`, `LibraryCare:*` (data-model 1).
- **Detail** (T021/T022): `DetailPhase.FilesRemovedAt`, `Track.RemoveDetailFiles`, `Track.HasDetailFiles`,
  `CanStartDetail` true for a completed phase with removed files; the mark is kept while re-split and cleared on
  completion; `DetailPhaseDto.FilesRemovedAt` mapped (omitted when null). No migration (JSON column, absent = null).
- **Worker client** (T023/T024): `GetStorageAsync` (2 s total, `WorkerTimeouts.Storage`), `CleanStorageAsync`,
  `RehydrateJobAsync`; models in `Worker/WorkerStorage.cs`; the 011 GPU decorator forwards them; `FakeWorkerClient` gained
  `OnGetStorage`/`OnCleanStorage`/`OnRehydrate`. Health now uses the same `SendWithinAsync` helper.

## Files changed

`src/StemStudio.Domain/LibraryCare/*`, `Domain/Voices/VoiceConsent.cs`, `Domain/Tracks/{Track.Detail.cs, Detail/DetailPhase.cs}`,
`src/StemStudio.Api/LibraryCare/{Gate,Jobs}/*`, `LibraryCare/LibraryCareRegistration.cs`, `LibraryCareOptions.cs`,
`StemStudioApiOptions.cs`, `ApiComposition.cs`, `Sync/PeriodicSyncService.cs`, `Agent/AgentRunExecutor.cs`, the gated
`*Endpoints.cs`, `Tracks/DetailMapper.cs`, `Worker/{IWorkerClient, WorkerClient, WorkerClient.Storage, WorkerStorage,
WorkerTimeouts}.cs`, `Agent/Gpu/GpuReleasingWorkerClient.cs`; tests under `tests/StemStudio.{Domain,Persistence,Api}.Tests`
(`LibraryCare/*`, `Tracks/DetailFilesRemovedTests.cs`, `DetailPhaseJsonTests.cs`, `Detail/DetailEndpointTests.cs`,
`Worker/WorkerClientStorageTests.cs`, `Agent/GpuCoordinationTests.cs`, `TestSupport/FakeWorkerClient.cs`).

## Tests and coverage

```text
dotnet test tests/StemStudio.{Domain,Persistence,Api}.Tests -c Release -p:CollectCoverage=true
  Domain.Tests       934 passed                 Domain       98.96 % line / 95.11 % branch
  Persistence.Tests  121 passed                 Persistence  99.01 % / 95 %
  Api.Tests         1876 passed, 2 skipped      Api          98.91 % / 91.72 %, Contracts 100 % / 100 %
dotnet build: 0 warnings; dotnet format --verify-no-changes: clean (after `dotnet format`)
```

## Quality checks

`tools/quality/run_all.py` (main-repo venv): 0 errors after splitting `GateProblems.Typed` (parameters); warnings only
(unit size 16-23 lines in `LibraryJobRunner`, `LibraryJobContext.Report`, `LibraryJobStore`, `StorageClassifier.Classify`,
`LibraryGate.WaitForIdleAsync`; refactor in T066).

## Dependencies added

None.

## Docs impact

Setup: `StemStudio__BackupDir`, `StemStudio__BackupHostDirHint`, `StemStudio__LibraryCare__*` (data-model 1). No
user-visible change yet.

## Open issues / requests

- Recordings (015): no `AssetOrigin.Recording` member exists; the category is derived from the origin name, tests use a
  fixture value. 015 adds the enum member, nothing else changes.

## Next owner

backend (US1); frontend can use the gate problems and the job JSON shape now.
