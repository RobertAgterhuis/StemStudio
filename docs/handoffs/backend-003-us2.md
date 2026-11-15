# Hand-off: backend / 003-us2 (T036-T037)

## What was built

`tests/StemStudio.Api.Tests/Sync/DeviceFallbackTests.cs` covers three cases end to end through the API:

- The worker 1.2.0 `RunningDetailJob` example is parsed by the real `WorkerClient`, synced, and served.
  Its `fallbackReason` reaches `DetailPhaseDto.Device.FallbackReason`, with `kind = gpu` and
  `cpuFallback = true`.
- A worker job without `device` (1.1.0 shape) keeps `TrackDto.Device` null.
- `POST /api/tracks/{id}/retry` on a failed track that had a GPU device answers 202 with
  `device: null` and stores null.

T037 needed no production change. The behaviour was already implemented in T018 (`Retry` clears the
device), T022 (an absent `device` becomes null) and T030 (the device sync), as in 002 T072/T073. A
follow-up refactor moved the synchroniser passes into `ApiFactory.SyncTracksAsync` and
`ApiFactory.SyncDetailAsync` (test duplication).

## Red -> green evidence

| Task pair | Test commit | Implementation commit |
|---|---|---|
| T036 / T037 | 7af80bf (green at once, no production change needed) | - |
| test refactor | - | 88fe5c2 |

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true`:

```text
Api.Tests 406 passed   StemStudio.Api line 99.62 %  branch 97.98 %   Contracts 100 % / 100 %
Domain (unchanged since us1)       line 98.54 %  branch 97.67 %
Persistence (unchanged since us1)  line 98.56 %  branch 100 %
```

## Quality checks

```text
dotnet format --verify-no-changes: exit 0; build 0 warnings
run_all.py: 507 files, 3977 units, 27 clones, 0 errors, 140 warnings -> exit 0 (backend src 0 warnings)
```

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend (T040 -> T041, US3).
