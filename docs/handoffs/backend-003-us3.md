# Hand-off: backend / 003-us3 (T040-T041)

## What was built

- `GET /api/device` (`getProcessingDevice`, tag `health`) lives in `src/StemStudio.Api/Device/`.
- `ProcessingDeviceService` (scoped) reads the worker's `GET /health` through
  `IWorkerClient.GetHealthAsync`, which is limited to 2 s. It maps `deviceInfo` to
  `ProcessingDeviceDto`:
  - `reachable: true` plus `kind`, `name`, `totalMemoryMiB` and `reason`;
  - on the CPU, `reason` is null unless the GPU was requested and could not be used;
  - `computeCapability` and `freeMemoryMiB` are not part of the backend contract.
- A `WorkerUnavailableException` (unreachable, > 2 s, 5xx, invalid body) or a `WorkerRequestException`
  (4xx) answers `200` with `ProcessingDeviceDto.Unreachable`.
- `ProcessingDeviceCache` (singleton, `TimeProvider`) keeps every answer, including an unreachable
  one, for 10 s (`ProcessingDeviceCache.Lifetime`).
- `PendingDetailOperations` is empty again. The served OpenAPI document lists exactly the backend 1.3.0
  operations.
- The coordinator's note: on a running stack the answer is `reachable: true`, with the worker device
  info passed through. This matches the implementation, so nothing changed.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T040 / T041 | 2c81d6c (9 tests failed at run time, the served-operations test included) | 0c954f6 |

## Files changed

- `src/StemStudio.Api/Device/{DeviceEndpoints,ProcessingDeviceService}.cs` (new), `ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Device/DeviceEndpointTests.cs` (new: GPU, CPU with reason, default CPU,
  unreachable, 4xx, 10 s cache with `FakeTimeProvider` (9.999 s cached, 10 s refetched), contract
  validation of reachable and unreachable bodies), `Contract/BackendContractTests.StemsAndMixes.cs`
  (unstaged `getProcessingDevice`)

## Tests and coverage

```text
Api.Tests 414 passed   StemStudio.Api line 99.63 %  branch 98.02 %   Contracts 100 % / 100 %
Domain / Persistence unchanged since us1 (98.54/97.67, 98.56/100)
```

## Quality checks

```text
dotnet format --verify-no-changes: exit 0; build 0 warnings
run_all.py: 510 files, 3994 units, 28 clones, 0 errors, 141 warnings -> exit 0 (backend src 0 warnings)
```

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend (`IStemStudioApiClient.GetDeviceAsync` against `GET /api/device`); backend T046 (polish).
