# Hand-off: backend / 002-us1 (T051-T054)

## What was built

- **Auto-queue** (FR-001): `TrackJobApplier` calls `Track.RequestDetail` right after `Track.Complete`, so
  the detail phase is `queued` (no worker job yet) in the same save that completes the track.
- **`DetailSynchronizer`** (`src/StemStudio.Api/Detail/`), run by `JobSyncService` on every tick after the
  standard track pass in the same scope: for each track from `ListDetailActiveAsync` it either submits a
  pending phase (`POST /v1/detail-jobs {standardJobId}`; unreachable -> stays pending and is retried next
  tick; 404/409 rejection -> phase failed with the worker's detail text) or polls `GET /v1/detail-jobs/{id}`.
  Saves per track; one broken track never stops the others; transitions are logged.
- **`DetailJobApplier`**: queued/running -> `ApplyDetailProgress` (stage, progress, current parent);
  failed -> worker error (blank -> default text); cancelled -> cancelled; 404 -> failed
  "interrupted by restart"; completed -> result, then every detail stem downloaded through
  `IDetailStemFileStore` (temp + rename), then `CompleteDetail`. A rejected download, a disk error or an
  invalid result fails the phase with a readable reason and deletes `detail/` (no partial stems offered);
  an unreachable worker during the download leaves the phase for the next tick. A create that returns
  `completed` (worker cache, SC-007) completes in the same pass.
- **Contract**: `TrackMapper` maps `Track.detail` via `DetailMapper` (`none` when never requested,
  `updatedAt` null then; detail stem URLs `/api/tracks/{id}/detail-stems/{parent}/{name}`);
  `GET /api/tracks/{id}/detail-stems/{parent}/{name}` (`getDetailStem`) shares the `PhysicalFile`
  range/ETag/304/416 processing and CORS of `getStem`, 404 unless the completed phase lists the stem and the
  file exists. `getDetailStem` removed from `PendingDetailOperations` (served-operations test green).

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T051 / T052 auto-queue + synchroniser | a2d1c8b | b050cef |
| T053 / T054 contract `Track.detail` + `getDetailStem` | 709259e (7 of 40 contract tests red) | 37e7a41 |

## Files changed

- `src/StemStudio.Api/Detail/{DetailJobApplier,DetailSynchronizer}.cs` (new),
  `src/StemStudio.Api/Tracks/{DetailMapper.cs (new),TrackMapper.cs,StemEndpoints.cs}`,
  `src/StemStudio.Api/Sync/{TrackJobApplier,JobSyncService}.cs`, `src/StemStudio.Api/ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Detail/DetailAutoQueueTests.cs` (new),
  `tests/StemStudio.Api.Tests/Contract/BackendContractTests.Detail.cs` (new),
  `tests/StemStudio.Api.Tests/Contract/BackendContractTests.StemsAndMixes.cs` (pending list),
  `tests/StemStudio.Api.Tests/TestSupport/SyncTestData.cs` (`DetailQueued`, `DetailCompleted`)

## Tests and coverage

```text
dotnet test tests/StemStudio.<X>.Tests -c Release -p:CollectCoverage=true   (gate 90 % line+branch)
  Domain.Tests       239 passed   StemStudio.Domain       line 98.46 %  branch 97.54 %
  Persistence.Tests   40 passed   StemStudio.Persistence  line 98.59 %  branch 100 %
  Api.Tests          334 passed   StemStudio.Api          line 99.78 %  branch 97.63 %
                                  StemStudio.Contracts    line 100 %    branch 100 %
```

## Quality checks

```text
dotnet build -c Release: 0 warnings; dotnet format --verify-no-changes: exit 0
tools/quality/run_all.py: 392 files, 2852 units, 21 clones, 0 errors, 125 warnings -> exit 0
  (no warnings in src/StemStudio.{Domain,Persistence,Api}; new warnings are unit-size/duplication in
   my test files, to be trimmed in T083)
```

## Dependencies added

None.

## Open issues / requests

- Behaviour visible to other owners: a track that just completed has `detail.status = queued`, so
  `DELETE /api/tracks/{id}` answers 409 until the phase is cancelled or finished (FR-016, implemented in
  T068). E2E tests that delete right after import must cancel the detail phase first (qa).
- Concurrency note: the sync loop and the cancel endpoint (T068) may both write `Tracks.DetailPhase`;
  the last writer wins and the next poll converges on the worker's state. No optimistic concurrency token
  was added (YAGNI for a single local user).

## Next owner

backend (US3: T067-T068).
