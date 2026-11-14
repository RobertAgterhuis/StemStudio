# Research: StemStudio MVP (Phase 0)

Each item: **Decision**, **Rationale**, **Alternatives considered**. Versions were resolved
against nuget.org, PyPI and npm on 2026-11-14 and verified by building/testing the skeleton.
Binding architecture choices come from the CTO brief; this file records why they hold.

## R-01 Stem separation model and library

- **Decision**: Demucs **4.1.0** (PyPI, MIT) with model `htdemucs_6s` (vocals, drums, bass, guitar,
  piano, other), fallback `htdemucs` (vocals, drums, bass, other) when the 6-stem model cannot be
  loaded. Load with `demucs.pretrained.get_model(name)` and run `demucs.apply.apply_model(model,
  mix, device=..., split=True, overlap=0.25, progress=False)`; we do our own audio I/O (soundfile).
  Weights download on first use: 4.1.0 tries the Hugging Face Hub first (`HF_HOME` cache), then the
  legacy `dl.fbaipublicfiles.com` URLs via `torch.hub` (`TORCH_HOME` cache). Docker must persist
  both cache directories in a volume; the integration test is skipped when weights are absent and
  no network is available.
- **Rationale**: Best open-source quality for music stems at the time of writing; the 6-stem
  variant is the only widely used open model with guitar and piano stems (quality of those two is
  noticeably lower; documented per FR-010). 4.1.0 (2026-07) drops the hard torchaudio dependency
  and the training-only dependencies, and was verified to import (`demucs.api`, `get_model`,
  `apply_model`) with torch 2.15.0+cpu on Python 3.12.13.
- **Alternatives**: Demucs 4.0.1 (brief's assumption; needs torchaudio, whose 2.9+ releases removed
  the I/O backends that `demucs.audio` uses, so the whole torch stack would have to be pinned back);
  Spleeter (TensorFlow, 2/4/5 stems, lower quality, unmaintained); Open-Unmix (4 stems only);
  BS-RoFormer/MDX community checkpoints (licences of weights unclear, heavier).

## R-02 PyTorch build and device

- **Decision**: `torch==2.15.0` from the PyTorch **CPU** index (`https://download.pytorch.org/whl/cpu`,
  declared as an explicit uv index in `worker/pyproject.toml`); no torchaudio. `STEMSTUDIO_DEVICE`
  = `cpu` (default) | `cuda` | `auto` (CUDA if `torch.cuda.is_available()`).
- **Rationale**: CPU wheels are ~10x smaller than CUDA wheels, keep the Docker image and CI fast,
  and work everywhere. The lock contains cp312 wheels for win_amd64, manylinux x86_64 and aarch64.
  GPU users can install a CUDA build locally without contract changes.
- **Alternatives**: default PyPI torch (bundles CUDA on Linux, ~2.5 GB); CUDA-by-default image
  (needs NVIDIA runtime, not available in CI).

## R-03 Python dependency set and compatibility

- **Decision**: Python 3.12 (`>=3.12,<3.13`), managed by uv 0.13 (`uv.lock` committed).
  fastapi 0.163, uvicorn[standard] 0.23.2, python-multipart 0.0.33, pydantic 2.14, yt-dlp
  2026.10.26, spotdl 4.6.0, soundfile 0.14, librosa 1.0.1, numpy 2.5; dev: pytest 9.2, pytest-cov
  7.1, httpx 0.28, ruff 0.17, mypy 2.4.
- **Rationale**: Python 3.12 is the newest version for which the whole set (numba/llvmlite for
  librosa, torch, spotdl) publishes wheels. spotdl 4.6 pins `uvicorn<0.24`, so uvicorn is pinned to
  0.23.2; FastAPI works with it. yt-dlp must stay current because YouTube changes frequently.
- **Alternatives**: Python 3.13/3.14 (system Python is 3.14; spotdl/numba wheels lag); pip +
  requirements.txt (no cross-platform lock); Poetry (slower, no first-class PyTorch index pinning).

## R-04 Audio acquisition and normalisation

- **Decision**: YouTube via yt-dlp (Python API, best audio-only format, no playlists). Spotify via
  spotDL metadata (title, artist, duration) plus a YouTube search match; audio always comes from
  YouTube. Uploads are accepted as MP3/WAV/FLAC (extension + ffmpeg probe). ffmpeg (system binary,
  invoked with an argument list) converts everything to 44.1 kHz stereo 16-bit PCM WAV; audio
  longer than 900 s is rejected. Source keys: `youtube:<videoId>`, `spotify:<trackId>`,
  `upload:<sha256>`; the worker cache is additionally keyed by the SHA-256 of the normalised WAV.
- **Rationale**: Constitution IV (no DRM circumvention); single canonical format for Demucs and
  librosa; two cache keys make re-import instant (SC-006) and avoid re-separating identical audio
  from different URLs.
- **Alternatives**: Spotify audio streams (DRM, forbidden); pydub/audioread (wrap ffmpeg anyway);
  caching by URL (fragile, ADR-001 #2). Note: spotDL 4.6 resolves metadata through `spotipyfree` /
  `spotapi` (unofficial web-API clients, no credentials needed); this touches metadata only.

## R-05 Stem format and delivery

- **Decision**: FLAC 44.1 kHz stereo 16-bit (ADR-001 #1), written with soundfile. The API stores
  stems on local disk and serves them with `Results.File(..., enableRangeProcessing: true)`
  (`audio/flac`, ETag/Last-Modified); the browser engine fetches them directly from the API
  (CORS policy allowing the configured web origin for `GET`). Details: ADR-004.
- **Rationale**: Lossless and padding-free, so all stems stay sample-aligned; native
  `decodeAudioData` support in all target browsers; about half the size of WAV. Direct fetch keeps
  megabytes of audio out of the Blazor SignalR circuit.
- **Alternatives**: MP3/AAC (encoder delay/padding breaks alignment), Opus (Safari gaps), WAV
  (2x size), streaming through Blazor interop (huge SignalR messages), proxying stems through the
  Web app (extra hop, no benefit on localhost).

## R-06 Backend shape

- **Decision**: ASP.NET Core 10 Minimal API; EF Core 10.0.15 with SQLite; owned types mapped with
  `OwnsOne/OwnsMany(...).ToJson()` for channels, metronome, loop, stems and beat grid; unique index
  on `Track.SourceKey` and on `(Mix.TrackId, Mix.NormalizedName)`; RFC 9457 problem details via
  `AddProblemDetails()`; typed `HttpClient` for the worker with
  `Microsoft.Extensions.Http.Resilience` (timeouts/retries for idempotent GETs only); a
  `BackgroundService` (`JobSyncService`) using `TimeProvider` for testable polling.
- **Rationale**: Smallest moving parts for a single-user local app; JSON columns keep the mix
  aggregate in one row (save/load is one statement, SC-005); `TimeProvider` + `FakeTimeProvider`
  makes the sync loop deterministic in tests.
- **Alternatives**: Controllers (more ceremony); separate tables for channels (more joins, no query
  need); push notifications/SignalR from worker (more coupling than 1 s polling; spec mandates
  polling). SQLite caveat: EF Core cannot translate `ORDER BY`/comparisons on `DateTimeOffset`
  columns; store timestamps as UTC `DateTime` or via a value converter to ticks (data-model.md).

## R-07 Frontend render mode and component library

- **Decision**: Blazor Web App with global **Interactive Server** render mode (`<Routes
  @rendermode="InteractiveServer" />`), Radzen.Blazor 13.1.0 (MIT) with the Material theme; Web
  calls the API server-side through a named `HttpClient` (`StemStudio:ApiBaseUrl`) and hands the
  browser-facing base URL (`StemStudio:PublicApiBaseUrl`) to the audio engine.
- **Rationale**: Server mode keeps all C# on the server (no WASM download, simple debugging); the
  only latency-critical part (audio) runs in the browser engine anyway; position updates are
  throttled to 10 Hz and meters never cross interop, so the SignalR circuit stays light.
- **Alternatives**: WebAssembly/Auto modes (larger download, duplicated HTTP client config, no
  benefit for a local single-user app); MudBlazor/FluentUI (Radzen is the mandated stack).

## R-08 Audio engine techniques

- **Decision**: One `AudioContext`; per stem `AudioBufferSourceNode -> GainNode -> StereoPannerNode
  -> AnalyserNode -> master GainNode`; all sources started with one shared `when` and offset;
  looping with `loop/loopStart/loopEnd`; metronome with a look-ahead scheduler (timer wakes, clicks
  scheduled with `start(t)` on the audio clock) and pre-rendered click buffers; meters drawn by the
  engine via `requestAnimationFrame` into DOM elements registered by id. Build with Vite 8 library
  mode, test with Vitest 5 + jsdom + hand-written AudioContext mock, lint with ESLint 10 +
  typescript-eslint 8.72 (strict type-checked). TypeScript pinned to 6.0.3 because
  typescript-eslint 8.72 supports `<6.1`.
- **Rationale**: The only way to meet SC-002/SC-003 in a browser is to schedule everything on the
  audio clock; decoded buffers make seeks and loops sample-exact. Details: ADR-005 and
  `contracts/audio-engine.md`.
- **Alternatives**: `<audio>` elements per stem (drift, no sample lock), `MediaElementSource`
  (same), AudioWorklet mixer (more complex, unnecessary for 6 stems), Tone.js (extra abstraction;
  its transport also uses look-ahead scheduling - not needed for our narrow needs), TypeScript 7
  (native compiler; not yet supported by typescript-eslint).

## R-09 Testing and coverage tooling

- **Decision**: .NET: xUnit v3 4.0.3 executed through VSTest (`xunit.runner.visualstudio` 4.0.1,
  `Microsoft.NET.Test.Sdk` 18.12.0, `IsTestingPlatformApplication=false`), Shouldly 4.3.0
  (BSD-3), NSubstitute 6.2.0 (BSD-3), bUnit 2.15.2, `Microsoft.AspNetCore.Mvc.Testing`,
  `Microsoft.Extensions.TimeProvider.Testing`; coverage with coverlet 10.1.0 (`coverlet.collector`
  via `--collect "XPlat Code Coverage"` + `tests/coverlet.runsettings`, or `coverlet.msbuild` via
  `-p:CollectCoverage=true` with thresholds). Python: pytest 9 + pytest-cov (`fail_under = 90`,
  branch). TS: Vitest v8 coverage with 90 % thresholds. E2E: Playwright (TypeScript) against
  docker compose with `STEMSTUDIO_SEPARATOR=fake`.
- **Rationale**: Constitution I mandates xUnit + coverlet + bUnit, pytest-cov, Vitest and
  Playwright. xUnit v3 4.x defaults to Microsoft.Testing.Platform, under which the .NET 10 SDK
  refuses the VSTest target; opting the test projects out keeps the coverlet collector/msbuild
  integrations the CI expects (verified: `dotnet test` and both coverage modes work).
  FluentAssertions is excluded because v8+ requires a paid licence for commercial use.
- **Alternatives**: xUnit v2 2.9.3 (in maintenance; bUnit 2 and new analyzers target v3); MTP +
  `coverlet.MTP` (viable later, different CLI switches; ADR-007); Moq (SponsorLink controversy);
  FluentAssertions (licence).

## R-10 Maintainability metrics (SIG)

- **Decision**: `tools/quality/sig_check.py` (quality-gate agent) uses **lizard** (unit size,
  cyclomatic complexity, parameter count for C#, Python and TypeScript) and **jscpd** (duplicated
  blocks >= 6 lines across languages); dependency direction checked from project references and
  Python imports. Linters enforce the same limits locally (ruff C90/PLR, ESLint complexity/
  max-params/max-lines-per-function, .NET analyzers). SonarQube-compatible
  `sonar-project.properties` reads the Cobertura/lcov reports.
- **Rationale**: Open-source, multi-language, CI-friendly approximations of the SIG maintainability
  model; the paid Sigrid service is not required for the gate.
- **Alternatives**: Sigrid (commercial); SonarQube only (no SIG-style unit interfacing metric,
  server needed); radon (Python only).

## R-11 Job processing model

- **Decision**: Worker runs one in-process worker thread consuming a FIFO `queue.Queue`; job state
  is a JSON file per job (`jobs/<jobId>.json`, atomic write via temp file + rename) and is updated
  at each stage; on start-up all non-terminal jobs become `failed` ("interrupted by restart"). The
  API polls every ~1 s. Concurrent imports of the same source key return the existing job.
- **Rationale**: Spec FR-008/FR-009a and the "one job at a time" assumption; no broker, no DB in
  the worker; restart semantics are honest (ADR-001 #3).
- **Alternatives**: Celery/RQ + Redis (extra services), asyncio task in the event loop (Demucs is
  CPU-bound and would block the API), multiprocessing pool (memory heavy for torch models).

## R-12 Configuration and ports

- **Decision**: Ports web 8080, api 8081, worker 8000 (internal in compose). API config section
  `StemStudio`: `DataDir`, `WorkerBaseUrl`, `WebOrigin`, `JobPollIntervalMs`. Web: `ApiBaseUrl`,
  `PublicApiBaseUrl`. Worker env: `STEMSTUDIO_DATA_DIR`, `STEMSTUDIO_SEPARATOR` (`demucs`|`fake`),
  `STEMSTUDIO_DEVICE` (`cpu`|`cuda`|`auto`). All overridable by environment variables
  (`StemStudio__DataDir`, ...). No secrets exist.
- **Rationale**: Constitution security constraints; one-command compose start; identical config
  model for Docker and native runs.
- **Alternatives**: `.env` files committed (secrets risk, not needed), service discovery (overkill).
