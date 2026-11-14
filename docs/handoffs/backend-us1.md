# Hand-off: backend / us1

## What was built

- **Imports** (`src/StemStudio.Api/Importing/`, namespace renamed from `Imports` because of CA1716):
  - `POST /api/imports` (`importFromUrl`): URL validated by the Domain parser; existing key -> 200; otherwise the
    track is inserted **first** (status queued), then the worker job is created with the **canonical URL**
    (I1), then the job ID is saved -> 202 + `Location`. A worker 400 -> 400 validation problem on `url` with the
    worker's detail; worker unreachable/5xx -> 503 "Processing service unavailable"; in both cases the inserted
    track is removed again, so nothing is persisted.
  - Parallel imports (G5): the second request either finds the inserted track or hits the `SourceKey` unique
    index (`DuplicateKeyException`) and answers 200 with the winner, so the worker is called once.
  - `POST /api/imports/upload` (`importFromUpload`): `UploadRequestReader` reads the `file` part with
    `MultipartReader` straight from the request body (no form buffering), `ImportService` checks the extension
    (415), `UploadStore` streams to `uploads/<sha256><ext>` while hashing (413 above 209,715,200 bytes, 400 for
    an empty file), key `upload:<sha256>`, title = file name without extension. The kept file is streamed to the
    worker. A re-upload of an existing source answers 200; the new copy is deleted unless it is exactly the file
    the (non-completed) track still uses (so a re-upload restores a missing retry source). Kestrel
    `MaxRequestBodySize` and `FormOptions.MultipartBodyLengthLimit` are `ContractLimits.MaxUploadBytes` + 1 MiB
    (`UploadLimits`); a Kestrel `BadHttpRequestException` 413 also maps to the 413 problem.
- **Job sync** (`src/StemStudio.Api/Sync/`): `JobSyncService` (`BackgroundService`, `PeriodicTimer` on the injected
  `TimeProvider`, created in `StartAsync` so no tick is missed; each tick runs in a new scope and a failing tick
  is logged, not fatal), `TrackSynchronizer` (per-track isolation, worker unreachable -> state kept), and
  `TrackJobApplier` (forward-only progress/stage/metadata; worker 404 -> failed "interrupted by restart"; worker
  failed -> its error or "Processing failed."; completed -> result + every stem written via temp file + rename,
  then status completed; stem/result problems -> failed with a readable reason without paths; model -> stem-set
  check through the Domain; the API upload copy is deleted after completion, U3).
- **Tracks** (`src/StemStudio.Api/Tracks/`): `GET /api/tracks` (`listTracks`, `updatedAt` desc), `GET
  /api/tracks/{trackId}` (`getTrack`, stems as relative URLs), `POST /api/tracks/{trackId}/retry` (`retryTrack`:
  only from failed (409 otherwise), upload retry 409 when the file is gone, URL retry re-sends the canonical URL,
  worker unreachable -> 503 and the track stays failed (G7), worker 4xx -> 409 "Retry rejected").
- **Composition** (`ApiComposition`, `Program.cs`): options, `DataDirectory`, SQLite (`ConnectionStrings:StemStudio`
  or `<DataDir>/stemstudio.db`) with `Database.MigrateAsync()` at start-up, CORS policy `web` (GET from
  `StemStudio:WebOrigin`, exposes `Content-Range`, `Accept-Ranges`, `ETag`, `Last-Modified`, `Content-Length`),
  problem details (every framework 400 gets an `errors` map, `BadHttpRequestException` keeps its status),
  OpenAPI document, worker client, hosted service.
- **Contract tests** (`tests/StemStudio.Api.Tests/Contract/`): `OpenApiContract` (YAML -> `JsonNode` with
  YamlDotNet), `SchemaValidator` (OpenAPI 3.1 subset used by the contract), `ContractAssert` (declared status,
  declared headers, media type and body schema). Every imports/tracks/health status code reachable in tests is
  checked; a self-test proves the validator rejects extra properties, missing required fields, bad enums,
  ranges, patterns, formats and oneOf mismatches.

## Red -> green evidence (commit hashes)

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T069 / T070 imports | d7f103a | 8a90ab5 |
| T071 / T072 job sync | e55ab70 | 8dee709 |
| T073 / T074 tracks + wiring | 95a78e5 | c431fde |
| T075 contract tests (test-only task) | 5bc72b5 | (no production change needed) |

## Files changed

- `src/StemStudio.Api/{Program,ApiComposition,ApiProblems,ProblemDetailsCustomizer}.cs`
- `src/StemStudio.Api/Importing/{ImportEndpoints,ImportService,UploadRequestReader,UploadLimits}.cs`
- `src/StemStudio.Api/Sync/{ITrackSynchronizer,TrackSynchronizer,TrackJobApplier,JobSyncService}.cs`
- `src/StemStudio.Api/Tracks/{TrackEndpoints,TrackService,TrackMapper}.cs`
- `tests/StemStudio.Api.Tests/{HealthEndpointTests,CompositionTests}.cs`, `TestSupport/{ApiFactory,FakeWorkerClient,SyncTestData}.cs`,
  `Importing/*`, `Sync/*`, `Tracks/TrackEndpointsTests.cs`, `Contract/*`
- `specs/001-stemstudio-mvp/tasks.md` (T069-T075 ticked)

## Tests and coverage

`dotnet test tests/StemStudio.<X>.Tests -p:CollectCoverage=true` (90 % line AND branch gate):

| Project | Passed / Failed | Line | Branch | Method |
|---|---|---|---|---|
| StemStudio.Domain.Tests | 173 / 0 | 97.73 % | 97.72 % | 96.96 % |
| StemStudio.Persistence.Tests | 21 / 0 | 99.02 % | 100 % | 97.29 % |
| StemStudio.Api.Tests (Api) | 161 / 0 | 99.7 % | 97.61 % | 100 % |
| StemStudio.Api.Tests (Contracts) | (same run) | 93.33 % | 100 % | 88.88 % |

Test hosts: `ApiFactory` (`WebApplicationFactory<Program>`) per test with a temp `DataDir` and therefore a
**file-based** SQLite database, the `FakeWorkerClient` and a `FakeTimeProvider` (the hosted service only ticks when
a test advances the clock). File SQLite instead of in-memory is deliberate: the parallel-import test needs real
concurrent connections, which a single shared in-memory connection cannot provide. The worker-client contract
tests still use the stub `HttpMessageHandler` with the worker examples.

## Quality checks

- `dotnet build StemStudio.slnx --no-incremental`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/sig_check.py` not available in this worktree; units kept within the limits by construction
  (largest units ~16 LOC: `ImportService.StartAsync`, `TrackJobApplier.CompleteAsync`).

## Dependencies added

None.

## Open issues / requests

1. If a client disconnects between the track insert and the worker call, a queued track without a worker job can
   remain (it is never polled because it has no job ID); the user can re-import (200, same track) but cannot retry
   it (not failed). Low risk on a single-user local app; a later improvement would fail such tracks after a timeout.
2. `importFromUpload` is mapped with `HttpRequest` (streamed), so the generated `/openapi/v1.json` does not describe
   the multipart body; the YAML contract remains the source of truth.

## Next owner

backend (self): US2 (T091-T092). CTO: review of this checkpoint.
