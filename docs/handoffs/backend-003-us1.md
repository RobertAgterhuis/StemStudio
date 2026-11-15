# Hand-off: backend / 003-us1 (T029-T030)

## What was built

- `Sync/JobDeviceApplier.cs` (new, singleton) converts the worker `WorkerJobDevice` to the domain
  `JobDevice`, then calls `Track.ApplyDevice` or `Track.ApplyDetailDevice`. When the domain rules
  reject the data, it logs a warning (`Track {id}: ignoring an invalid device ...`) and keeps the
  current device. The track is not failed.
- `TrackJobApplier` copies `job.Device` on every standard poll, before the status is applied. A
  completed job's device is therefore stored before `Complete` and kept afterwards.
  `DetailJobApplier` copies `job.Device` on every detail poll, submissions included. A 404 keeps the
  phase's device.
- `TrackMapper.ToDto(JobDevice?)` maps devices to `JobDeviceDto`. `TrackDto.Device` and
  `DetailPhaseDto.Device` are served.
- Refactor to stay within SIG limits:
  - `TrackJobApplier` now receives the stem and upload stores as one `TrackFiles` record (singleton).
    This keeps it at four constructor parameters.
  - `ApplyReported` and `ApplyDevice` helpers keep `ApplyAsync` within 15 LOC.
  - `ApiComposition.AddSyncServices` is split out of `AddApplicationServices`.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T029 / T030 device sync + served devices | 4a4a1f9 (6 of 7 tests failed at run time) | e7b299a |
| SIG refactor | - | e96ce43 |

## Files changed

- `src/StemStudio.Api/Sync/{JobDeviceApplier.cs (new),TrackJobApplier.cs}`, `Detail/DetailJobApplier.cs`,
  `Tracks/{TrackMapper,DetailMapper}.cs`, `ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Sync/DeviceSyncTests.cs` (new; `GET /api/tracks/{id}` is asserted
  against backend 1.3.0 `getTrack` with a GPU/fallback detail device and a CPU standard device)

## Tests and coverage

`dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true`:

```text
Domain.Tests       261 passed   StemStudio.Domain       line 98.54 %  branch 97.67 %
Persistence.Tests   48 passed   StemStudio.Persistence  line 98.56 %  branch 100 %
Api.Tests          403 passed   StemStudio.Api          line 99.62 %  branch 97.98 %
                                StemStudio.Contracts    line 100 %    branch 100 %
Web.Tests          284 passed
```

## Quality checks

```text
dotnet build -c Release: 0 Warning(s), 0 Error(s); dotnet format --verify-no-changes: exit 0
run_all.py: 506 files, 3971 units, 26 clones, 0 errors, 139 warnings -> exit 0
  backend src: 0 warnings; new test warnings: 2 boilerplate clones (usings/fixture header of DeviceSyncTests)
```

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend (T036 -> T037, US2). frontend can consume `TrackDto.Device` / `DetailPhaseDto.Device`.
