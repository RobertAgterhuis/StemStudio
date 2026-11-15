# Hand-off: backend / 007-us1 (T035-T036)

## What was built

- `POST /api/assets/audio-exports` (`createAudioExport`, `Assets/ExportService`). The request is checked in this
  order:
  1. `ExportRequestRules`: format (bit depth 16/24 for FLAC/WAV only; MP3 bit rate 192/256/320 only), name, and
     exactly one of `mix`/`mixId` in mode `mix`. Violations -> 422.
  2. `ExportPlanner`: unknown track or mix -> 404. Track not completed -> 409 `track-not-ready`. Mix rules ->
     422 `mix.*`. Planner errors -> 422 `channels[i]`. No audible channel -> 422 `nothing-to-export`.
  3. `DiskSpaceGuard` (`IDiskSpace`, `StemStudio:MinFreeDiskBytes`, default 1 GiB): too little space -> 409
     `insufficient-disk-space`.
  4. The item is created with the default name and contents and the job is submitted with `AssetSubmitter`.
     Unreachable worker -> 503 and nothing is stored. Worker 400/404 (missing file) -> 409 `source-unavailable`.

  Success -> 202 `Asset` with `Location`. Format defaults: 16-bit for one raw channel, else 24; MP3 at 320 kbps. A
  raw single channel is sent with `dither: false` (bit-identical path). File tags are title, artist and contents.
- `AssetSynchronizer` runs as the fifth pass of the `JobSyncService` tick, with `AssetJobApplier`:

  | Worker job | Item |
  |---|---|
  | queued | stays queued |
  | running | `rendering` with the progress |
  | completed | file copied with a hash to `assets/<id>/audio.<ext>` and checked against the worker SHA-256, then `ready` with files and audio facts (incl. `appliedGainDb`); the worker job is then deleted (best effort) |
  | failed / cancelled | `failed` with the worker error |
  | 404 | `failed` "The processing service lost this job; try again" |
  | hash mismatch, download rejection, disk error, unexpected type | `failed`; no file is kept |
  | unreachable | unchanged |

- `GET /api/assets/{assetId}` (`getAsset`). `trackAvailable` is computed with
  `ITrackRepository.ExistingIdsAsync`.
- `GET /api/assets/{assetId}/files/{role}` (`getAssetFile`), plus `HEAD`, which is mapped separately and left out of
  the OpenAPI description:
  - `PhysicalFile` with Range/206, ETag (the quoted SHA-256) and 304
  - `Content-Disposition: attachment; filename="<ascii>"; filename*=UTF-8''<percent-encoded>`, or `inline` with
    `inline=true`
  - 404 when the item is not ready, the role is unknown or the file is missing
- `createAudioExport`, `getAsset` and `getAssetFile` were removed from `PendingAssetOperations`.
- Shared helpers: `TrackPass` became generic (track, detail, metadata and asset passes). `HeaderResult` replaces the
  artwork-only `CacheControlledResult`.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T035 / T036 raw export + sync + file | 7f1f4c9 (build fails: service/routes missing) | d43c344 |

`AssetSyncFailureTests` (failure paths, hosted-service pass, store guards) was added in d43c344 together with the
implementation, as hardening tests for branch coverage.

## Tests and coverage

`dotnet test StemStudio.slnx -p:CollectCoverage=true` exits 0:

```text
Domain 389 passed       line 98.46 %  branch 96.57 %
Persistence 65 passed   line 98.13 %  branch 92.85 %
Web 332 passed (untouched)
Api 631 passed          line 98.23 %  branch 91.66 %   Contracts 100 / 100
```

`ContractAssert` checks:
- `createAudioExport`: 202, 404, 409 (three types), 422, 503
- `getAsset`: 200 ready/failed, 404
- `getAssetFile`: 200, 206, 304, 404

## Quality checks

- `dotnet format --verify-no-changes` is clean.
- `sig_check.py src/StemStudio.Api`: 84 files, 378 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Deviations

- `StemStudio:AssetSyncIntervalMs` (data-model 1) is not a separate setting. Assets are polled in the existing
  `JobSyncService` tick (`JobPollIntervalMs`, default 1 s), so one timer and one scope serve every pass. data-model 1
  says "reuses the job-sync pattern", and the default value is the same.
- A worker 400/404 when a render is created (a stem file is missing or the path is refused) is answered with 409
  `source-unavailable`. `createAudioExport`'s `ExportConflict` lists only `track-not-ready` and
  `insufficient-disk-space`; the problem schema accepts it (tests pass). **Request to architect**: list
  `source-unavailable` under `ExportConflict` as well.

## Next owner

backend (US2: T042/T043).
