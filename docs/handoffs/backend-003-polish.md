# Hand-off: backend / 003-polish (T046) and feature 003 summary

## What was built (whole feature, backend lane)

| Checkpoint | Tasks | Note |
|---|---|---|
| foundational | T017-T022 | `docs/handoffs/backend-003-foundational.md` |
| us1 | T029-T030 | `docs/handoffs/backend-003-us1.md` |
| us2 | T036-T037 | `docs/handoffs/backend-003-us2.md` |
| us3 | T040-T041 | `docs/handoffs/backend-003-us3.md` |
| polish | T046 | this note |

- **Domain**: `JobDevice` value object and `ProcessingDeviceKind`; `Track.Device` and
  `DetailPhase.Device` with their rules (data-model 3.1).
- **Persistence**: `Tracks.Device` JSON column and migration `20261115115115_AddJobDevice`. 001 and
  002 databases migrate and load with null devices.
- **API**:
  - The worker client parses `device` and has `GetHealthAsync`, limited to 2 s.
  - `JobDeviceApplier` copies the device on every standard and detail poll. Invalid worker data is
    logged and ignored.
  - `TrackDto.Device` and `DetailPhaseDto.Device` are served.
  - `GET /api/device` is served, with a 10 s cache and `reachable: false` when the worker cannot
    be asked.
  - Every backend 1.3.0 operation is served, and `PendingDetailOperations` is empty.

Polish:
- `ApiTestBase` is a shared fixture (factory, client, fake worker, Ct) used by the new device tests and
  `DetailEndpointTests`.
- `ApiFactory.SyncTracksAsync` and `ApiFactory.SyncDetailAsync` are shared synchroniser passes.
- The device persistence tests were split under 15 LOC.
- Production code has 0 SIG warnings.
- XML docs cover every new public type and member (CS1591 is an error).

## Red -> green evidence (all pairs)

| Task pair | Red | Green |
|---|---|---|
| T017/T018 | 4d26254 | 6794cb0 |
| T019/T020 | c6cf037 | 32510a8 |
| T021/T022 | 0043783 | 4ebe500 |
| SIG refactor + JobDeviceJson tests | - | adb7fea |
| T029/T030 | 4a4a1f9 | e7b299a |
| SIG refactor | - | e96ce43 |
| T036/T037 | 7af80bf (green at once, no production change needed) | - |
| test refactor | - | 88fe5c2 |
| T040/T041 | 2c81d6c | 0c954f6 |
| T046 | - | f83eb9a |

## Tests and coverage (final, `dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true`)

```text
Domain.Tests       261 passed   StemStudio.Domain       line 98.54 %  branch 97.67 %
Persistence.Tests   48 passed   StemStudio.Persistence  line 98.56 %  branch 100 %
Api.Tests          414 passed   StemStudio.Api          line 99.63 %  branch 98.02 %
                                StemStudio.Contracts    line 100 %    branch 100 %
Web.Tests          284 passed   (frontend, untouched)
coverlet gates (90 % line+branch per component): all pass
```

## Quality checks

```text
dotnet build StemStudio.slnx -c Release                0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes      exit 0
uv run --project tools/quality python tools/quality/run_all.py
  511 files, 3992 units, 25 clones, 1 exemptions, 0 errors, 136 warnings -> exit 0
  (baseline at 399a81a: 0 errors, 135 warnings; backend src 0 warnings; remaining backend-test
   warnings: a 10-line using-directive clone in DeviceSyncTests/DetailEndpointTests and an 8-line
   clone between the architect's DeviceContractTests and DetailContractTests)
```

## Dependencies added

None. No new NuGet packages; `dotnet-ef` 10.0.15 was already a local tool.

## Deviations

- `WorkerClient` takes a `TimeProvider` as a third constructor parameter, so the 2 s health timeout
  can be tested with `FakeTimeProvider`. `AddWorkerClient` registers `TimeProvider.System` with
  `TryAdd`.
- `TrackJobApplier` receives the stem and upload stores as one `TrackFiles` record (SIG 4-parameter
  limit after `JobDeviceApplier` was added).
- `GET /api/device` caches every answer for 10 s, including `reachable: false`. A worker that is still
  starting can therefore show "unknown" for up to 10 s.
- `WorkerDeviceInfo` uses init properties instead of a positional record (6 parameters would break the
  SIG limit). The corresponding T021 test assertions were adjusted in the green commit 4ebe500.
- T018 (6794cb0) briefly left the persistence suite red until T020 (32510a8), because the new domain
  property had no EF mapping yet.

## Open issues / requests

None. No contract problems were found.

## Next owner

CTO (T051 integration wave).
