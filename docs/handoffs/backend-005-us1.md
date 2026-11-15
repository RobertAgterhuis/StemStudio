# Hand-off: backend / 005-us1 (T026-T027)

## What was built

- `Api/Artwork/ArtworkFileStore.cs` (`IArtworkFileStore`, singleton) stores pictures under
  `data/tracks/<id>/artwork/{auto,user}/{cover,thumb}.jpg`.
  - Both JPEGs are written to temp files first, then both are renamed, and temp files never survive.
  - Paths are built only from the GUID and fixed names.
  - Deleting a track removes the whole `tracks/<id>` folder, which includes the artwork (FR-016, tested).
- `Api/Artwork/ArtworkDownloader.cs` downloads a picture only when `Track.AcceptsArtwork` says the precedence
  would take it, so the same hash is never fetched twice.
  - It downloads `cover` and `thumb` from the worker, stores them in `auto/`, then calls `Track.OfferArtwork`.
  - A worker `user` source, an invalid hash, a missing variant, an unreachable worker or a disk error is logged
    and leaves the track unchanged. The next poll tries again, and the import is never failed (FR-002).
- `Api/Sync/JobReportApplier.cs` (new) applies device, title/artist and the source picture on every standard poll.
  `TrackJobApplier` now takes it instead of `JobDeviceApplier`, which keeps it at 4 parameters.
- `GET /api/tracks/{trackId}/artwork/{variant}` (`getTrackArtwork`, `ArtworkService` + `ArtworkEndpoints`):
  - serves `image/jpeg` from `user/` for an own picture, else from `auto/`
  - `ETag` is the quoted version (first 12 hex characters), and `If-None-Match` gives 304 with the `ETag`
  - `Cache-Control: private, max-age=31536000, immutable` when `v` matches, else `no-cache`
  - 404 for an unknown track, a track without a picture, an unknown variant or a missing file
- `TrackMapper` maps `TrackDto.artwork` (`/api/tracks/{id}/artwork/{cover,thumb}?v=<version>`) and
  `TrackDto.metadata`.
- `getTrackArtwork` was removed from `PendingArtworkOperations`; the served OpenAPI document lists it.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T026 / T027 artwork sync + endpoint | 17b3224 (build fails: artwork store/endpoint missing) | f5845f3 |

## Files changed

- `src/StemStudio.Api/Artwork/{ArtworkFileStore,ArtworkDownloader,ArtworkService,ArtworkEndpoints}.cs` (new),
  `Sync/JobReportApplier.cs` (new), `Sync/TrackJobApplier.cs`, `Tracks/TrackMapper.cs`, `ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Artwork/{ArtworkSyncTests,ArtworkEndpointTests}.cs` (new),
  `Contract/BackendContractTests.StemsAndMixes.cs` (un-staged `getTrackArtwork`)

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  523 passed   StemStudio.Api line 99.27 %  branch 98.04 %   StemStudio.Contracts 100 % / 100 %
Domain (unchanged since foundational) 339 passed, line 98.78 % branch 97.04 %
Persistence (unchanged)                 60 passed, line 97.81 % branch 95.83 %
```

`getTrackArtwork` 200/304/404 responses are asserted with `ContractAssert` against backend 1.5.0.

## Quality checks

- Build has 0 warnings. `dotnet format --verify-no-changes` is clean.
- `sig_check.py src/StemStudio.Api`: 59 files, 250 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- **frontend**: picture URLs are relative API URLs, like the stems. The CORS policy already allows `GET` with
  `If-None-Match` from the web origin.

## Next owner

backend (US2: T037-T040); frontend T028/T029 can use `TrackDto.artwork`.
