# Hand-off: backend / 002-us3 (T067-T068)

## What was built

- `src/StemStudio.Api/Detail/DetailService.cs` + `DetailEndpoints.cs`:
  - `POST /api/tracks/{id}/detail` (`startDetail`): 202 + `Location` + Track body when the track is
    completed and `detail.status` is none/failed/cancelled. It removes an old `detail/` folder (best effort)
    and queues a fresh phase (progress 0, no worker job). The `DetailSynchronizer` submits it, so an unreachable
    worker is not an error (contract: no 503). 409 "Only completed tracks can be split further." or
    "The detail phase is queued|running|completed; ...". 404 for an unknown track.
  - `POST /api/tracks/{id}/detail/cancel` (`cancelDetail`): 200 + Track body from queued/running. If a
    worker job exists it is cancelled in the worker first: worker 404 -> cancelled locally, worker 409
    (already finished) -> 409 "Detail phase already finished" with the worker detail, unreachable -> 503.
    In both error cases nothing changes. A pending submission (no job yet) is cancelled locally without a
    worker call. 409 when no phase is active.
- `TrackDeletionService`: 409 "Detail separation in progress" / "The detail separation of this track is
  queued or running; cancel it first." while the phase is active (FR-016). Otherwise the whole track folder,
  including `detail/`, is removed as before.
- `PendingDetailOperations` is now empty: every contract 1.2.0 operation is served.
- SIG: synchroniser, appliers, `DetailService` and `ApiComposition` units split so that all production
  units are <= 15 LOC.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T067 / T068 start, cancel, delete rule | 1c20f5f (20 of 24 selected tests red) | ae9c0dd |

## Files changed

- `src/StemStudio.Api/Detail/{DetailService,DetailEndpoints}.cs` (new), `Detail/{DetailJobApplier,DetailSynchronizer}.cs`,
  `Tracks/TrackDeletionService.cs`, `Sync/TrackJobApplier.cs`, `ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Detail/DetailEndpointTests.cs` (new),
  `tests/StemStudio.Api.Tests/Contract/BackendContractTests.StemsAndMixes.cs`

## Tests and coverage

```text
Api.Tests 357 passed   StemStudio.Api line 99.59 %  branch 97.82 %   StemStudio.Contracts 100 % / 100 %
Domain.Tests 239 passed (98.46 % / 97.54 %), Persistence.Tests 40 passed (98.59 % / 100 %) - unchanged
```

## Quality checks

```text
dotnet build -c Release 0 warnings; dotnet format --verify-no-changes exit 0
tools/quality/run_all.py: 395 files, 2886 units, 22 clones, 0 errors, 122 warnings -> exit 0
  (none in src/StemStudio.{Domain,Persistence,Api})
```

## Coordinator FYI (frontend hand-off), checked

1. DELETE returns 409 problem details with a readable `detail` while the detail phase is queued or running: yes.
2. startDetail/cancelDetail return the Track body: yes (asserted against the contract).
3. Mix duplicate/update keep `expanded` + `detailChannels`: duplicate copies them in the Domain (T027 tests).
   Update stores what the body sends. The endpoint round trip is covered in T077/T078.
4. An outcome entry for bass (`not-supported`) is passed through from the worker result: yes.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend (US4: T072-T073).
