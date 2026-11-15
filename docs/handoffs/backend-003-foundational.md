# Hand-off: backend / 003-foundational (T017-T022)

## What was built

- **Domain** (`src/StemStudio.Domain/Tracks/`): `ProcessingDeviceKind` (Cpu, Gpu) and the immutable
  `JobDevice` record. Its constructor enforces data-model 3.1: the name is null on the CPU and at most
  200 characters, the reason is at most 500 characters, and the reason is set exactly when `CpuFallback`
  is true. Breaking a rule throws `DomainValidationException`, so invalid worker data never reaches a
  track. `Track.Device` / `Track.ApplyDevice` (`Track.Device.cs`) copies the device only while the
  track is not terminal, keeps it after completion or failure, and `Retry` clears it.
  `DetailPhase.Device` / `Track.ApplyDetailDevice` copies the device only while the phase is queued
  or running. Completion, failure and cancellation keep it, and `RequestDetail` starts a fresh phase
  with no device. Applying an unchanged device does not touch `UpdatedAt`.
- **Persistence**: `Tracks.Device` is a nullable JSON text column through the `JobDeviceJson` value
  converter. The kind is stored by name, and record equality drives change tracking.
  `DetailPhase.Device` lives inside the existing `DetailPhase` JSON column. The migration
  `20261115115115_AddJobDevice` adds only a nullable `TEXT` column. It was generated with the
  `dotnet-ef` local tool, and its CRLF line endings were normalised to LF.
- **Worker client**: `WorkerJobDevice`, `WorkerDeviceInfo` and `WorkerHealth` live in
  `Worker/WorkerDevice.cs`. `WorkerJob.Device` and `WorkerDetailJob.Device` are null for 1.1.0
  bodies. `IWorkerClient.GetHealthAsync` (`GET /health`) is limited to `WorkerTimeouts.Health` (2 s,
  retries included) by a `TimeProvider`-driven `CancellationTokenSource`, so the timeout can be
  tested with `FakeTimeProvider`. A timeout, 5xx, transport failure or missing `deviceInfo` throws
  `WorkerUnavailableException`. `WorkerClient` now takes a `TimeProvider`, and `AddWorkerClient`
  registers `TimeProvider.System` with `TryAdd`.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T017 / T018 domain job device | 4d26254 | 6794cb0 |
| T019 / T020 persistence + migration | c6cf037 | 32510a8 |
| T021 / T022 worker device + health client | 0043783 | 4ebe500 |
| SIG refactor + JobDeviceJson coverage tests | - | adb7fea |

- T017 and T021 failed at compile time because the tested members did not exist yet.
- T019 failed at run time. `Track.Device` was not mapped, so the EF model could not be built.
- Side effect: T018 (6794cb0) left the persistence tests red until T020 (32510a8), because the new
  domain property had no mapping yet.

## Files changed

- `src/StemStudio.Domain/Tracks/{JobDevice,ProcessingDeviceKind,Track.Device}.cs` (new),
  `Track.cs` (retry clears the device), `Detail/DetailPhase.cs` (`Device`)
- `src/StemStudio.Persistence/Configurations/{JobDeviceJson.cs (new),TrackConfiguration.cs}`,
  `Migrations/20261115115115_AddJobDevice{,.Designer}.cs`, `Migrations/StemStudioDbContextModelSnapshot.cs`
- `src/StemStudio.Api/Worker/{WorkerDevice.cs (new),WorkerJob,WorkerDetailJob,IWorkerClient,WorkerClient,WorkerTimeouts,WorkerClientRegistration}.cs`
- Tests: `tests/StemStudio.Domain.Tests/Tracks/JobDeviceTests.cs`,
  `tests/StemStudio.Persistence.Tests/{JobDevicePersistenceTests,JobDeviceJsonTests}.cs` (new),
  `SqliteTestDatabase.cs`, `TestData.cs`, `DetailPersistenceTests.cs` (shared helpers),
  `tests/StemStudio.Api.Tests/Worker/WorkerDeviceClientTests.cs` (new; the health examples are read
  straight from the linked worker-api 1.2.0 YAML), `WorkerClientTests.cs`, `LoggingTests.cs`
  (constructor), `TestSupport/FakeWorkerClient.cs` (`OnGetHealth`, `HealthCalls`, `Health(...)`)

## Tests and coverage

`dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true` (coverlet gates on):

```text
Domain.Tests       261 passed   StemStudio.Domain       line 98.54 %  branch 97.67 %
Persistence.Tests   48 passed   StemStudio.Persistence  line 98.56 %  branch 100 %
Api.Tests          396 passed   StemStudio.Api          line 99.61 %  branch 97.92 %
                                StemStudio.Contracts    line 100 %    branch 100 %
Web.Tests          284 passed   (frontend, untouched)
```

The test that migrates a 002 database is file-based SQLite. It migrates to
`20261115091608_AddMixDetailSettings`, inserts a 002 row with a running `DetailPhase` JSON, then
migrates to the latest version. Both devices load as null, and no migrations are pending. The existing
001 migration test still passes.

## Quality checks

```text
dotnet build StemStudio.slnx -c Release        0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes   exit 0
uv run --project tools/quality python tools/quality/run_all.py
  504 files, 3947 units, 24 clones, 1 exemptions, 0 errors, 137 warnings -> exit 0
  new warnings: 2 test unit-size warnings (JobDevicePersistenceTests, 18 and 20 LOC); production code 0
```

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend (T029 -> T030, US1).
