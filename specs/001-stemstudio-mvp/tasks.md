---

description: "Task list for the StemStudio MVP (one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: StemStudio MVP

**Input**: Design documents from `/specs/001-stemstudio-mvp/` (plan.md, spec.md, research.md,
data-model.md, quickstart.md, contracts/), constitution v1.0.0, ADR-001..ADR-007.

**Prerequisites**: plan.md, spec.md, data-model.md, contracts/ (all committed and reviewed).

**Tests**: REQUIRED. Constitution I is non-negotiable: within every owner sequence the test task
comes before the implementation task it covers (red -> green -> refactor), and every owner reports
>= 90 % line AND branch coverage in its hand-off.

**Organization**: tasks are grouped by user story (US1..US4) so each story is independently
testable; the [Execution plan per owner](#execution-plan-per-owner-parallel-wave) regroups the same
task IDs per agent for the single parallel wave (one git worktree per agent).

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: can run in parallel with the other [P] tasks of the same phase (different files, no
  dependency on an incomplete task). Tasks of *different owners* are always parallel in the wave.
- **[Story]**: US1..US4 from spec.md (Setup, Foundational, Polish and Integration have none).
- **[owner]**: the agent from `.claude/agents/` that executes the task; it may only write inside
  its owned paths (see the execution plan) plus `docs/handoffs/<owner>-<task>.md`.
- Paths are relative to the repository root `E:\PatricksSoundStudio`.
- **CTO** = the orchestrator agent (reviews hand-offs, merges, runs the integration wave).
- Test data: every owner keeps its fixtures and fakes in its own test subfolder (e.g.
  `worker/tests/analysis/`, `tests/StemStudio.Api.Tests/Worker/Examples/`); `tests/fixtures/` (qa)
  is only for E2E and the fixture generator.

## Checkpoint hand-offs (binding, ADR-008)

Every owner writes `docs/handoffs/<owner>-<checkpoint>.md` at each checkpoint it reaches, with
checkpoint one of `foundational` (Setup + Foundational), `us1`, `us2`, `us3`, `us4`, `polish`.
Each note contains red -> green evidence (the commit hash of the failing test and of the passing
implementation per task pair), the exact line/branch coverage of the owner's component and the
lint/SIG output. In the execution plan the marker `HO:<checkpoint>` shows where it is due; the
Polish tasks T111-T118 are the `polish` hand-offs.

## Cross-owner rule (binding)

No task may need another owner's *code* at runtime during the wave. Every cross-owner dependency
goes through a committed contract and is replaced by a fake/mock in the dependent owner's tests:

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`main.py`, `jobs/`) | ingest adapters, beat analyzer | `ports.py` protocols + factory signatures (T001, Appendix B) | in-test fakes injected through `create_app(settings, adapters=...)` |
| beat-analysis, stem-separation | audio with known tempo | none (self-contained) | synthetic signals generated in the test (`numpy` + `soundfile`), never `tests/fixtures/` |
| backend (`WorkerClient`, `JobSyncService`) | worker HTTP API | `worker-api.openapi.yaml` | stub `HttpMessageHandler` returning the contract examples (fake worker client) |
| frontend | backend HTTP API | `backend-api.openapi.yaml` + `StemStudio.Contracts` DTOs | stub `HttpMessageHandler` / fake `IStemStudioApiClient` |
| frontend | audio engine | `contracts/audio-engine.md` + `src/audio-engine/src/types.ts` | bUnit `JSInterop` (mocked `IJSRuntime` / `IJSObjectReference`) |
| audio-engine | browser audio, Blazor callbacks | Web Audio API, `DotNetObjectReference` in `types.ts` | hand-written `AudioContext` mock, fake `dotnetRef`, Vitest fake timers, stubbed `fetch` |
| qa (Playwright) | the running UI | UI test-id contract (Appendix A, T002) | nothing: suite is authored in the wave, executed in the integration wave |
| devops (images, CI) | build/test commands of every component | quickstart.md commands + Appendix B | builds run against the committed skeleton (green today) |
| quality-gate | source of all owners | constitution limits | synthetic sample files under `tools/quality/tests/samples/` |

---

## Phase 0: Contract fixes (architect, BEFORE the wave)

**Purpose**: close the contract gaps found while generating these tasks. The wave must not start
until T001-T003 are committed (they change only architect-owned skeleton/contract files).

- [x] T001 [architect] Add the worker composition contract: in `worker/src/stemstudio_worker/ports.py` add `UploadIntake` protocol (`accept(file_name: str, path: Path) -> SourceInfo`, raises `InvalidSourceError` for extensions other than `.mp3`/`.wav`/`.flac`; key `upload:<64 lower-hex sha256 of the bytes>`) and frozen dataclasses `IngestAdapters(resolver, upload_intake, fetcher, normalizer)` and `PipelineAdapters(ingest, separator, analyzer)`; add factory stubs raising `NotImplementedError` with final signatures: `ingest/__init__.py::create_ingest_adapters(settings: WorkerSettings) -> IngestAdapters`, `separation/__init__.py::create_separator(settings) -> Separator`, `analysis/__init__.py::create_beat_analyzer(settings) -> BeatAnalyzer`; skeleton test `worker/tests/test_ports.py` (FR-003, FR-009)
- [x] T002 [P] [architect] Publish the UI test-id contract from Appendix A as `specs/001-stemstudio-mvp/contracts/ui-test-ids.md` (v1.0.0) and link it from `contracts/audio-engine.md` "Testing" (FR-005, FR-011, FR-016)
- [x] T003 [P] [architect] Add anticipated test-only dependencies the owners cannot add themselves: worker dev group `jsonschema`, `pyyaml`, `types-PyYAML`, `types-jsonschema`, `pytest-timeout` in `worker/pyproject.toml` + `worker/uv.lock`; `YamlDotNet` (test projects only) in `Directory.Packages.props`; local tool `dotnet-ef` 10.0.15 in `dotnet-tools.json` (`dotnet tool restore`); record licences in `docs/handoffs/architect-plan-and-skeleton.md` ("Phase 0"). JsonSchema.Net is NOT allowed (v9 ships under the OSMF EULA, not OSI): .NET contract tests compare shapes with YamlDotNet + System.Text.Json
- [x] T129 [architect] Add binding URL test vectors `specs/001-stemstudio-mvp/contracts/source-url-vectors.json` (16 accepted forms with `sourceKey` and `canonicalUrl`: youtu.be, watch?v on www/bare/m/music hosts, /shorts/, `&list=` on a watch URL, open.spotify.com/track, /intl-xx/track/; 22 rejected forms: playlists, albums, artists, episodes, shows, other or look-alike hosts, bad schemes, missing ids); backend forwards the canonical URL; worker accepts the same set (I1)
- [x] T130 [P] [architect] Contracts 1.0.1 (worker + backend, `schemas.py` `CONTRACT_VERSION`): 200 MiB stated as 209,715,200 bytes (I2, quickstart V4 "200 MiB + 1 byte"); getStem 304, `ETag`/`Last-Modified`, 416 with `Content-Range: bytes */N` (I4); lists ordered by `updatedAt` descending (I10); field limits title/artist <= 500, stage <= 200, error <= 1000, duration > 0 and <= 900 (I11); six-stem result example and the model -> stem set rule (`htdemucs_6s`/`fake` = 6, `htdemucs` = 4) (I12); engine `stop()` always resets to 0 (I8); data model `uploads/<jobId>/input<ext>` (I7) and fixed error texts incl. "Audio is longer than 15 minutes" (I3)
- [x] T131 [P] [architect] Per-component coverage gate in `Directory.Build.props` (`Include=[StemStudio.X]*` per test project, Api.Tests also gates Contracts; `Threshold=90`, `ThresholdType=line,branch`), `CoverletOutputFormat=cobertura,opencover`, ADR-006 amendment incl. the `dotnet sonarscanner begin/end` invocation (K3, K4)
- [x] T132 [P] [architect] `eslint-plugin-jsdoc` 65.4.2 in `src/audio-engine` (`jsdoc/require-jsdoc` on exported functions, classes, interfaces, type aliases, interface methods) (K6)
- [x] T133 [P] [architect] plan.md: `src/StemStudio.Contracts` architect-owned and read-only for backend and frontend during the wave, DTO gaps come back as contract-fix requests (O1); Chromium + Firefox tested, Safari 17+ best effort, untested (U5)

**Checkpoint**: `dotnet build`, `uv run pytest`, `npm test` still green; wave may start.

---

## Phase 1: Setup (tooling per owner, no story)

- [X] T004 [P] [quality-gate] Write failing tests for unit metrics in `tools/quality/tests/test_sig_check.py` using sample files `tools/quality/tests/samples/{ok,long_unit,complex_unit,many_params}.{cs,py,ts}`: warn when LOC > 15 or CC > 5, fail when LOC > 30, CC > 10 or params > 4
- [X] T005 [quality-gate] Implement `tools/quality/sig_check.py` (lizard API; CLI `sig_check.py [paths...]`, default = all source roots, excludes `bin/obj/node_modules/.venv/Migrations/*.g.cs/wwwroot/js/audio-engine.js`; exit 1 on hard breach; JSON + Markdown summary) and create `tools/quality/pyproject.toml` (pinned `lizard`, ruff with the worker's SIG limits, mypy --strict, pytest + pytest-cov `fail_under = 90` with branch coverage) (K2)
- [X] T006 [P] [quality-gate] Write failing tests for duplication and dependency direction in `tools/quality/tests/test_duplication.py` and `tools/quality/tests/test_dependency_check.py` (sample duplicated 6-line blocks; sample csproj graph with a forbidden `Web -> Persistence` reference; sample Python import `ingest -> separation`)
- [X] T007 [quality-gate] Implement `tools/quality/duplication.py` (wraps `npx jscpd@<pinned>` with `tools/quality/.jscpd.json`, `minLines: 6`, formats cs/py/ts/razor) and `tools/quality/dependency_check.py` (allowed .NET graph from plan.md: `Web -> Contracts`, `Api -> {Contracts, Domain, Persistence}`, `Persistence -> Domain`; worker: `api -> jobs -> ports`, `{ingest, separation, analysis}` import only `ports/schemas/config`, no cycles)
- [X] T008 [quality-gate] Implement the one-command entry `tools/quality/run_all.py` (sig_check + duplication + dependency_check, `--report docs/quality/SIG-REPORT.md`) with test `tools/quality/tests/test_run_all.py`
- [X] T009 [P] [quality-gate] Create `sonar-project.properties` (sources/tests per component; `sonar.cs.opencover.reportsPaths=tests/**/TestResults/coverage/coverage.opencover.xml`, `sonar.python.coverage.reportPaths=worker/coverage.xml`, `sonar.javascript.lcov.reportPaths=src/audio-engine/coverage/lcov.info`, exclusions as in T005) and document in its header the .NET flow `dotnet sonarscanner begin` -> `dotnet build` -> `dotnet test -p:CollectCoverage=true` -> `dotnet sonarscanner end` with the token only in `SONAR_TOKEN` (K4, ADR-006)
- [X] T010 [quality-gate] Run `run_all.py` on the skeleton and write the baseline `docs/quality/SIG-REPORT.md` (measured values, star estimate per SIG property, refactor-request section per owner, empty for now)
- [ ] T011 [P] [devops] Create `.dockerignore` and `docker/worker.Dockerfile` (python:3.12-slim, apt `ffmpeg`, uv `sync --frozen --no-dev`, CPU torch from lock, non-root user, `ENV HF_HOME=/models/hf TORCH_HOME=/models/torch STEMSTUDIO_DATA_DIR=/data`, `HEALTHCHECK` on `/health`)
- [ ] T012 [P] [devops] Create `docker/api.Dockerfile` (sdk:10.0 build/publish `src/StemStudio.Api`, aspnet:10.0 runtime, port 8081, `StemStudio__DataDir=/data`, non-root, healthcheck `/health`)
- [ ] T013 [P] [devops] Create `docker/web.Dockerfile` (node:22 stage `npm ci && npm run build` in `src/audio-engine`, then sdk:10.0 publish `src/StemStudio.Web`, aspnet:10.0 runtime, port 8080, healthcheck `/health`)
- [ ] T014 [devops] Create `docker-compose.yml`: services `worker` (internal only, no host port), `api` (8081), `web` (8080); volumes `worker-data:/data`, `api-data:/data`, `model-cache:/models` (HF_HOME/TORCH_HOME); `STEMSTUDIO_SEPARATOR=${STEMSTUDIO_SEPARATOR:-demucs}`, `STEMSTUDIO_DEVICE=${STEMSTUDIO_DEVICE:-cpu}`; `StemStudio__WorkerBaseUrl=http://worker:8000`, `StemStudio__WebOrigin=http://localhost:8080`, `StemStudio__ApiBaseUrl=http://api:8081`, `StemStudio__PublicApiBaseUrl=http://localhost:8081`; `depends_on: condition: service_healthy`; healthchecks with start_period (worker model load); no reverse proxy (Kestrel's upload limit from T070 applies directly; if a proxy is ever added its body limit must be >= 200 MiB + multipart overhead) (G1)
- [ ] T015 [P] [devops] Create `scripts/dev.ps1` and `scripts/dev.sh` (non-Docker path from quickstart.md: build engine bundle, start worker/api/web as three processes, `-Fake`/`--fake` sets `STEMSTUDIO_SEPARATOR=fake`, data under `data/`, stop all on Ctrl+C; no secrets) and `scripts/test-all.{ps1,sh}` (every component's lint + test + coverage command)
- [ ] T016 [devops] Create `.github/workflows/ci.yml` (push + pull_request): jobs `dotnet` (`dotnet tool restore`, build, `dotnet format --verify-no-changes`, `dotnet test StemStudio.slnx -p:CollectCoverage=true`; the per-component 90 % line+branch gate and Cobertura/OpenCover output come from `Directory.Build.props`, ADR-006), `worker` (`uv sync --frozen`, ruff check, ruff format --check, mypy, `pytest --cov --cov-branch --cov-report=xml`, fail_under 90), `audio-engine` (`npm ci`, lint, typecheck, `test:coverage` thresholds 90, build), `contracts` (Redocly 2.78.0 lint), `quality` (ruff, mypy --strict and pytest-cov >= 90 % for `tools/quality` and `tests/fixtures`, then `python tools/quality/run_all.py`) (K2), `e2e-lint` (`npm ci && npm run lint` in `e2e/`) (U6), `licences` (`uvx pip-licenses`, `dnx nuget-license`, `npx license-checker` with pinned versions -> artifact `licence-report` for T126) (K7), `e2e` (needs all; generate fixtures, `STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`, `npm ci && npm test` in `e2e/`, upload Playwright report, `docker compose down -v`); pinned action versions, least-privilege `permissions`
- [ ] T017 [P] [devops] Create `.github/workflows/e2e-real.yml` (`workflow_dispatch` only): job 1 `uv run pytest -m integration` in `worker/` with the cached model cache (`HF_HOME`/`TORCH_HOME`) and `CI=true`; job 2 compose with `STEMSTUDIO_SEPARATOR=demucs` and the `model-cache` volume, `npm run test:real` in `e2e/`; generous timeouts (K5)
- [ ] T018 [devops] Validate: `docker compose config -q`, `docker compose build` against the skeleton, `actionlint` on both workflows, `scripts/dev.ps1 -Fake` smoke (three `/health` endpoints return ok); record outputs in hand-off
- [ ] T019 [P] [qa] Write `docs/testing/TEST-STRATEGY.md`: test pyramid per component, fakes per port, the single `@pytest.mark.integration` Demucs test, coverage gates, E2E scope (main flow + rejects + mixer), real-Demucs run policy, fixture catalogue, retries=0 rule, traceability matrix acceptance scenario -> test file; YouTube/Spotify imports (V3) are a manual, network-dependent check, not automated; owners keep their fixtures in their own test subfolders (G6, O5)
- [ ] T020 [P] [qa] Write failing tests `tests/fixtures/tests/test_generate_fixtures.py` (files exist, 44.1 kHz stereo, durations, click onsets at the manifest's beat times within 1 sample, deterministic hash on re-run)
- [ ] T021 [qa] Implement `tests/fixtures/generate_fixtures.py` (run with `uv run --project worker python tests/fixtures/generate_fixtures.py`; numpy + soundfile, ffmpeg for MP3): `click_120bpm_4-4_12s.wav`, `click_90bpm_3-4_12s.wav`, `music_100bpm_20s.mp3` (tones + clicks), `music_100bpm_20s.flac`, `silence_5s.wav`, `corrupt.mp3`, `unsupported.ogg`; writes `tests/fixtures/audio/` (git-ignored via `tests/fixtures/audio/.gitignore`) and commits `tests/fixtures/manifest.json` (bpm, beatsPerBar, beat times, duration); add `upload_4mb.wav` (a few MB, for the streaming-upload E2E, G1); generator is ruff-clean and mypy --strict typed with config in `tests/fixtures/pyproject.toml`, tests >= 90 % line+branch (K2)
- [ ] T022 [qa] Scaffold Playwright in `e2e/` (`package.json` with pinned `@playwright/test`, scripts `test` = all except `@real`, `test:real` = `--grep @real`, `lint`; `playwright.config.ts` retries 0, `baseURL` from `E2E_BASE_URL` default `http://localhost:8080`, `E2E_API_URL` default `http://localhost:8081`, Chromium + Firefox projects; `tsconfig.json`; `eslint.config.js`; page objects in `e2e/pages/`)

---

## Phase 2: Foundational (blocking prerequisites inside each owner)

**Purpose**: domain model, persistence, clients and test doubles that every story of that owner
builds on. Tasks of different owners still run in parallel.

- [ ] T023 [P] [backend] Write failing data-driven tests `tests/StemStudio.Domain.Tests/Tracks/SourceKeyTests.cs` over every vector in `specs/001-stemstudio-mvp/contracts/source-url-vectors.json` (accepted -> `sourceKey` + `canonicalUrl`, rejected -> actionable error; file linked into the test project as content): hosts `youtube.com`, `www.youtube.com`, `m.youtube.com`, `music.youtube.com` with `/watch?v=<id>` or `/shorts/<id>`, `youtu.be/<id>` -> `youtube:<id>` (`[A-Za-z0-9_-]{11}`); `open.spotify.com` `/track/<id>` or `/intl-<xx>/track/<id>` -> `spotify:<id>` (`[A-Za-z0-9]{22}`); `watch?v=<id>&list=...` accepted as the single video; rejected: other hosts, non-http(s) schemes, `list=`-only or `/playlist`, `/album/`, `/artist/`, `/episode/`, `/show/`, missing ids; upload key `upload:<64 lower-hex sha256>` (FR-001, FR-002, FR-004, FR-009)
- [ ] T024 [backend] Implement `src/StemStudio.Domain/Tracks/SourceKey.cs` (+ `SourceKeyParseResult` with the key, the canonical URL and actionable messages) to pass T023
- [ ] T025 [P] [backend] Write failing tests `tests/StemStudio.Domain.Tests/Tracks/TrackTests.cs`: state machine "queued < downloading < separating < analysing < completed; skipped states are allowed", `Progress` int 0..100 "never decreases within one job", `completed forces 100`, `Error` "set iff Status == Failed", `DurationSeconds` "> 0 and <= 900 once completed", `Title` 1..500, `Artist` <= 500, `Stage` <= 200, `Error` <= 1000, retry "allowed only from failed", stems unique by name and ordered by `StemNames.SixStem`, BeatGrid `Bpm` 0..400, `BeatsPerBar` 1..12, `Confidence` 0..1, beats strictly ascending, downbeats subset (FR-006..FR-009a)
- [ ] T026 [backend] Implement `src/StemStudio.Domain/Tracks/{Track,Stem,BeatGrid}.cs` to pass T025 (no infrastructure references)
- [ ] T027 [P] [backend] Write failing tests `tests/StemStudio.Domain.Tests/Mixes/MixTests.cs`: `Name` "trimmed, 1..100 chars", `NormalizedName` = `Name.Trim().ToUpperInvariant()`, `MasterVolume` 0..1 default 1.0, ChannelSettings `Volume` 0..1 (default 0.8), `Pan` -1..1 (default 0), at most one entry per stem and "every Stem must exist on the track", Metronome `Enabled` default = `BeatGrid.Reliable`, `Volume` 0..1 (default 0.7), `BpmOverride` null or 30..300, `OffsetMs` -2000..2000, `BeatsPerBar` 1..12, Loop `Start >= 0`, `End > Start`, `End <= Track.DurationSeconds`; duplicate naming "<name> (copy)", "(copy 2)", ... truncated to <= 100 chars (FR-016, FR-017)
- [ ] T028 [backend] Implement `src/StemStudio.Domain/Mixes/{Mix,ChannelSettings,MetronomeSettings,LoopRegion,MixNaming}.cs` to pass T027
- [ ] T029 [P] [backend] Write failing tests `tests/StemStudio.Persistence.Tests/StemStudioDbContextTests.cs` (SQLite in-memory, plus one file-based SQLite round trip: save a track and a mix, dispose, read them back through a new `DbContext` with every setting equal, G2): unique index on `SourceKey`, unique (`TrackId`, `NormalizedName`), owned JSON `Stems`/`BeatGrid`/`Channels`/`Metronome`/`Loop` round-trip, timestamps stored as UTC `DateTime` and ordered by `UpdatedAt`, enums as strings, FK Mix->Track restrict
- [ ] T030 [backend] Implement EF configurations, `TrackRepository`, `MixRepository` in `src/StemStudio.Persistence/{Configurations,Repositories}/` and the initial migration `src/StemStudio.Persistence/Migrations/` (`dotnet ef migrations add Initial -p src/StemStudio.Persistence -s src/StemStudio.Api`) to pass T029
- [ ] T031 [P] [backend] Write failing contract tests `tests/StemStudio.Api.Tests/Worker/WorkerClientTests.cs` with a stub `HttpMessageHandler` serving the worker contract examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/*.json` (create 200/202 for JSON and for the multipart upload with part `file` streamed from disk (U4), get job, result, stem stream, 404/409 problems, connection refused -> `WorkerUnavailableException`)
- [ ] T032 [backend] Implement `src/StemStudio.Api/Worker/{IWorkerClient,WorkerClient,WorkerJob,WorkerJobResult}.cs` (typed `HttpClient`, `Microsoft.Extensions.Http.Resilience` with no retry on POST) and `src/StemStudio.Api/StemStudioApiOptions.cs` (`DataDir`, `WorkerBaseUrl`, `WebOrigin`, `JobPollIntervalMs`) to pass T031
- [ ] T033 [P] [backend] Write failing tests `tests/StemStudio.Api.Tests/Storage/StemFileStoreTests.cs` (temp dir; path `<DataDir>/tracks/<TrackId>/stems/<Name>.flac` derived from enum name only; write to temp file then rename; uploads `uploads/<sha256><ext>`; rejects unknown stem names)
- [ ] T034 [backend] Implement `src/StemStudio.Api/Storage/{IStemFileStore,StemFileStore,IUploadStore,UploadStore}.cs` to pass T033
- [ ] T035 [P] [stem-separation] Write failing tests `worker/tests/jobs/test_job_store.py`: `jobs/<jobId>.json` atomic replace, `jobId` = `uuid4().hex`, load/save round-trip of `schemas.Job` + internal fields, lookup by source key of non-failed jobs
- [ ] T036 [stem-separation] Implement `worker/src/stemstudio_worker/jobs/store.py` to pass T035
- [ ] T037 [P] [audio-engine] Create the test doubles `src/audio-engine/tests/mocks/audio-context.ts` (records `start(when, offset)`, `stop`, `loop/loopStart/loopEnd`, `setTargetAtTime`, pan values, analyser data, `decodeAudioData`), `tests/mocks/dotnet-ref.ts`, `tests/mocks/fetch.ts`, with self-tests in `tests/mocks/mocks.test.ts`
- [ ] T038 [P] [frontend] Write failing tests `tests/StemStudio.Web.Tests/Api/StemStudioApiClientTests.cs` (stub handler: every backend operation, ProblemDetails/ValidationProblemDetails mapped to `ApiError` with field errors, 409/503 distinct)
- [ ] T039 [frontend] Implement `src/StemStudio.Web/Api/{IStemStudioApiClient,StemStudioApiClient,ApiError}.cs` registered as typed client on `StemStudioWebOptions.ApiClientName`, to pass T038
- [ ] T040 [P] [frontend] Write failing tests `tests/StemStudio.Web.Tests/Audio/AudioEngineInteropTests.cs` (bUnit JSInterop: `import("./js/audio-engine.js")`, `createEngine`, `loadTrack` payload camelCase with absolute stem URLs from `PublicApiBaseUrl`, `setChannel`, `setMaster`, `setLoop`, `setMetronome`, `attachMeter`, `dispose`; `[JSInvokable] OnPositionChanged/OnPlaybackStateChanged` raise C# events; `JSException` surfaced as `AudioEngineException`; a drift test parses the `AudioEngine` interface in `src/audio-engine/src/types.ts` and asserts every method name the wrapper invokes exists there, U2)
- [ ] T041 [frontend] Implement `src/StemStudio.Web/Audio/{IAudioEngine,AudioEngineInterop,AudioEngineException}.cs` to pass T040
- [ ] T042 [P] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Layout/MainLayoutTests.cs` (nav links Import, Tracks, Mixes; body rendered)
- [ ] T135 [frontend] Implement `src/StemStudio.Web/Components/Layout/MainLayout.razor` (+ `NavMenu.razor`) to pass T042 (O3)

**Checkpoint**: each owner's foundation is green; story work proceeds inside the same worktree.

---

## Phase 3: User Story 1 - Import a song and get its stems (P1) MVP

**Goal**: YouTube/Spotify/upload import -> background job -> 6 (or 4) FLAC stems + beat grid,
with progress, cache reuse, rejects, failure + retry, restart handling.

**Independent test**: upload a short file, watch queued -> completed, verify stems, BPM, duration
(quickstart V1-V6).

### audio-ingest (tests first)

- [X] T043 [P] [US1] [audio-ingest] Write failing data-driven tests `worker/tests/ingest/test_url_resolver.py` over every vector in `specs/001-stemstudio-mvp/contracts/source-url-vectors.json` (same accept/reject set and keys as the backend): allow-listed hosts only, playlists/albums/artists/episodes/shows rejected, `watch?v=..&list=..` accepted as the video, canonical `https://www.youtube.com/watch?v=<id>`, keys `youtube:<id>`/`spotify:<id>`, Spotify resolved through a fake `SpotifyMetadataClient` + fake `YouTubeMatcher` with title and artist asserted to come from the Spotify client (not from the YouTube match) (A3), no match -> `SourceUnavailableError("No matching YouTube audio was found for this Spotify track.")` (FR-001, FR-002, FR-004)
- [X] T044 [US1] [audio-ingest] Implement `worker/src/stemstudio_worker/ingest/{urls.py,resolver.py,spotify.py}` (spotDL used for metadata + YouTube match only; never Spotify audio) to pass T043
- [X] T045 [P] [US1] [audio-ingest] Write failing tests `worker/tests/ingest/test_fetcher.py` with a fake `yt_dlp` factory: audio-only format, output inside `work_dir`, progress fractions non-decreasing, early `AudioTooLongError` when reported duration > 900 s, download errors -> `SourceUnavailableError` without secrets/stack traces
- [X] T046 [US1] [audio-ingest] Implement `worker/src/stemstudio_worker/ingest/fetcher.py` (`YtDlpFetcher`, injected `YoutubeDL` factory) to pass T045
- [X] T047 [P] [US1] [audio-ingest] Write failing tests `worker/tests/ingest/test_normalizer.py` with a fake subprocess runner: exact `ffmpeg`/`ffprobe` argument lists (no shell), 44.1 kHz stereo `pcm_s16le`, non-zero exit or no audio stream -> `AudioDecodeError`, duration > 900 s -> `AudioTooLongError("Audio is longer than 15 minutes")`, sha256 of the WAV (FR-003, FR-007)
- [X] T048 [US1] [audio-ingest] Implement `worker/src/stemstudio_worker/ingest/normalizer.py` (`FfmpegNormalizer`, injected `CommandRunner`) to pass T047
- [X] T049 [P] [US1] [audio-ingest] Write failing tests `worker/tests/ingest/test_upload_intake.py`: `.mp3/.wav/.flac` case-insensitive, other extensions -> `InvalidSourceError`, key `upload:<sha256 of bytes>`, title = file name without extension, file name never used as a path component (FR-003, FR-009)
- [X] T050 [US1] [audio-ingest] Implement `worker/src/stemstudio_worker/ingest/upload.py` (`UploadIntake`) to pass T049
- [X] T139 [P] [US1] [audio-ingest] Write failing tests `worker/tests/ingest/test_factory.py`: `create_ingest_adapters(settings)` returns an `IngestAdapters` whose members satisfy the four protocols and use the settings' limits (O3)
- [X] T140 [US1] [audio-ingest] Implement `create_ingest_adapters` in `worker/src/stemstudio_worker/ingest/__init__.py` to pass T139

### beat-analysis (tests first)

- [X] T051 [P] [US1] [beat-analysis] Create the synthetic-signal helpers `worker/tests/analysis/conftest.py` (click tracks at 60/90/120/150/180 BPM, 3/4 and 4/4 accents, music-like tone mix, silence, white noise; written with soundfile into `tmp_path`)
- [X] T052 [P] [US1] [beat-analysis] Write failing tests `worker/tests/analysis/test_librosa_analyzer.py`: BPM within +-1, beats within 30 ms of truth, beats ascending, downbeats a subset, `beatsPerBar` 4 for 4/4 and 3 for 3/4 accent patterns (default 4 when undetermined), `confidence` 0..1 (FR-007)
- [X] T053 [US1] [beat-analysis] Implement `worker/src/stemstudio_worker/analysis/{librosa_analyzer.py,tempo.py,downbeats.py}` (`LibrosaBeatAnalyzer` implementing `BeatAnalyzer`) to pass T052
- [X] T054 [P] [US1] [beat-analysis] Write failing tests `worker/tests/analysis/test_reliability.py`: silence, noise and clips shorter than a few beats return `bpm=0` or low confidence with `reliable=False` and never raise (edge case "no clear pulse")
- [X] T055 [US1] [beat-analysis] Implement `worker/src/stemstudio_worker/analysis/reliability.py` to pass T054
- [X] T141 [P] [US1] [beat-analysis] Write failing test `worker/tests/analysis/test_factory.py`: `create_beat_analyzer(settings)` returns a `BeatAnalyzer` (O3)
- [X] T142 [US1] [beat-analysis] Implement `create_beat_analyzer` in `worker/src/stemstudio_worker/analysis/__init__.py` to pass T141

### stem-separation (tests first)

- [ ] T056 [P] [US1] [stem-separation] Write failing tests `worker/tests/separation/test_fake_separator.py`: `FakeSeparator` writes 6 sample-aligned FLAC stems (44.1 kHz, stereo, 16-bit) from the input mix, model `fake`, progress reported (enables E2E with `STEMSTUDIO_SEPARATOR=fake`)
- [ ] T057 [US1] [stem-separation] Implement `worker/src/stemstudio_worker/separation/fake.py` to pass T056
- [ ] T058 [P] [US1] [stem-separation] Write failing tests `worker/tests/separation/test_demucs_separator.py` with an injected fake model loader/applier: tries `htdemucs_6s`, falls back to `htdemucs` when loading fails (4 stems: vocals, drums, bass, other), both fail -> `SeparationError`, device from settings (`auto` -> cuda if available), progress `fraction` non-decreasing, FLAC written via soundfile (no torchaudio); `create_separator` returns the fake or Demucs separator per `STEMSTUDIO_SEPARATOR` (FR-006, FR-009b)
- [ ] T059 [US1] [stem-separation] Implement `worker/src/stemstudio_worker/separation/{demucs_separator.py,flac_writer.py}` and `separation/__init__.py::create_separator` (`fake` | `demucs` by `STEMSTUDIO_SEPARATOR`) to pass T058
- [ ] T060 [US1] [stem-separation] Write the single `@pytest.mark.integration` test `worker/tests/separation/test_demucs_integration.py`: real Demucs on a 5-10 s synthetic clip generated in the test, asserts stem count, sample rate, length equality (sample-aligned); when `CI=true` a missing model or network FAILS the test instead of skipping it (K5) (Constitution I)
- [ ] T061 [P] [US1] [stem-separation] Write failing tests `worker/tests/jobs/test_runner.py` with fakes for every port: stage/progress ranges queued 0, downloading 0-10, separating `10 + 80 x fraction`, analysing 90-99, completed 100; stage texts "Queued", "Downloading audio", "Preparing audio", "Separating stems", "Analysing beats", "Completed", "Failed"; `WorkerError` message becomes `error`; unexpected exception -> "Unexpected error" + log; `work/<jobId>/` and `uploads/<jobId>/` removed when terminal; FIFO, one job at a time (FR-008)
- [ ] T062 [US1] [stem-separation] Implement `worker/src/stemstudio_worker/jobs/{runner.py,queue.py,progress.py}` (single worker thread, `queue.Queue`) to pass T061
- [ ] T063 [P] [US1] [stem-separation] Write failing tests `worker/tests/jobs/test_cache.py`: `cache/by-source/<key with ':' -> '_'>.json` hit -> queued -> completed without separation; `cache/by-audio/<audioSha256>/` hit after normalisation -> downloading -> completed; duplicate concurrent create for the same source key returns the existing non-failed job (FR-009, SC-006)
- [ ] T064 [US1] [stem-separation] Implement `worker/src/stemstudio_worker/jobs/cache.py` and wire it into the runner to pass T063
- [ ] T065 [P] [US1] [stem-separation] Write failing tests `worker/tests/jobs/test_reconcile.py`: on start-up every non-terminal job becomes `failed` "interrupted by restart" (FR-009a)
- [ ] T066 [US1] [stem-separation] Implement `worker/src/stemstudio_worker/jobs/reconcile.py` to pass T065
- [ ] T067 [P] [US1] [stem-separation] Write failing contract tests `worker/tests/api/test_jobs_api.py` (httpx `TestClient`, fake `PipelineAdapters`): every response validated with `jsonschema` against `specs/001-stemstudio-mvp/contracts/worker-api.openapi.yaml`; JSON create 200/202 + `Location`, multipart upload, 400 invalid source, 413 > 200 MiB (streamed size check), 415 wrong type, get 200/404, result 200/404/409, stem FLAC 200/404/409, problem+json bodies, `/health` (FR-003, FR-004, FR-008)
- [ ] T068 [US1] [stem-separation] Implement `worker/src/stemstudio_worker/api/{routes.py,problems.py,dependencies.py}` and composition root `worker/src/stemstudio_worker/main.py::create_app(settings=None, adapters=None)` (default adapters from the T001 factories; reconcile on start-up; start runner thread in lifespan) to pass T067

### backend (tests first)

- [ ] T069 [P] [US1] [backend] Write failing tests `tests/StemStudio.Api.Tests/Imports/ImportEndpointsTests.cs` (WebApplicationFactory, fake `IWorkerClient`, SQLite in-memory): `POST /api/imports` existing source -> 200 same track, new -> 202 Track(`queued`) + worker job, invalid URL/host/playlist -> 400 ValidationProblemDetails with actionable message and nothing persisted, worker unreachable -> 503 "processing service unavailable" and nothing persisted; the worker receives the canonical URL (I1); worker 400 -> 400 ValidationProblemDetails on `url` and nothing persisted; two parallel imports of the same key -> one worker call, both answers carry the same track (second 200) after the unique-index violation is caught (G5); `POST /api/imports/upload` boundary tests with a fake length-reporting stream (no 200 MB file on disk): exactly 209,715,200 bytes accepted, 209,715,201 -> 413 (G1), 415 non mp3/wav/flac, sha256 key, file stored under `uploads/`, title = file name without extension (FR-001..FR-004, FR-009, SC-007)
- [ ] T070 [US1] [backend] Implement `src/StemStudio.Api/Imports/{ImportEndpoints,ImportService}.cs` to pass T069: Kestrel `MaxRequestBodySize` and `FormOptions.MultipartBodyLengthLimit` = `ContractLimits.MaxUploadBytes` + multipart overhead (configured in `Program.cs`), file streamed to `uploads/` while hashing (never buffered in memory), exact size check -> 413, canonical URL forwarded, `DbUpdateException` on the `SourceKey` unique index caught -> existing track 200 (G1, G5)
- [ ] T071 [P] [US1] [backend] Write failing tests `tests/StemStudio.Api.Tests/Sync/JobSyncServiceTests.cs` (`FakeTimeProvider`, fake worker client, temp DataDir): polls non-terminal tracks every `JobPollIntervalMs`; forward-only status/stage/progress/metadata title+artist; on `completed` downloads result + every stem, writes files first then status `completed`; stem download failure -> `failed` readable reason; worker 404 -> `failed` "interrupted by restart"; worker unreachable -> state kept, retried next tick; the API's uploaded file `uploads/<sha256><ext>` is deleted once the track is `completed` (U3) (FR-008, FR-009a)
- [ ] T072 [US1] [backend] Implement `src/StemStudio.Api/Sync/{JobSyncService,TrackSynchronizer}.cs` (`BackgroundService`, `TimeProvider`; deletes the upload after completion, U3) to pass T071
- [ ] T073 [P] [US1] [backend] Write failing tests `tests/StemStudio.Api.Tests/Tracks/TrackEndpointsTests.cs`: `GET /api/tracks` (ordered by `UpdatedAt` desc), `GET /api/tracks/{id}` 200/404 with stems as relative URLs `/api/tracks/{trackId}/stems/{name}`, `POST /api/tracks/{id}/retry` only from `failed` -> `queued` with new `WorkerJobId`, upload retry 409 when the uploaded file is gone, URL-source retry re-sends the canonical URL, retry while the worker is unreachable -> 503 and the track stays `failed` (G7) (FR-008, FR-018)
- [ ] T074 [US1] [backend] Implement `src/StemStudio.Api/Tracks/{TrackEndpoints,TrackMapper}.cs` and wire everything in `src/StemStudio.Api/Program.cs` (options, DbContext + `Database.Migrate()` at start-up, CORS policy for `WebOrigin`, worker client, hosted service) to pass T073
- [ ] T075 [US1] [backend] Write `tests/StemStudio.Api.Tests/Contract/BackendContractTests.cs` for the imports and tracks operations: status codes and response bodies checked against `specs/001-stemstudio-mvp/contracts/backend-api.openapi.yaml` (YamlDotNet, T003) (Constitution III, O2)

### frontend (tests first)

- [ ] T076 [P] [US1] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Pages/ImportPageTests.cs`: notice text "for personal practice only ... responsible for complying with copyright and the platforms' terms of service" visible (FR-005), URL submit, file upload (client-side extension + 200 MiB check; a > 500 KB file is sent as a streamed `StreamContent` from `OpenReadStream(ContractLimits.MaxUploadBytes)`, asserted via the stub handler, G1), ProblemDetails messages shown (SC-007), 503 message, navigation to the track progress view on 200/202, FR-010 limitation help text
- [ ] T077 [US1] [frontend] Implement `src/StemStudio.Web/Components/Pages/Import.razor` (+ `.razor.cs`, route `/`, replaces `Home.razor`) with Radzen inputs and Appendix A test ids to pass T076; upload = `IBrowserFile.OpenReadStream(ContractLimits.MaxUploadBytes)` streamed into `StreamContent`, never buffered (G1)
- [ ] T078 [P] [US1] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Pages/TrackDetailPageTests.cs` (fake API client, `FakeTimeProvider`-driven `IPollingTimer`): polls ~1 s until terminal, progress bar 0-100 + stage text, failed shows reason + Retry button calling retry, completed shows stems, BPM, duration, model (4-stem fallback note) and "Open mixer" (FR-008, US1-4, US1-8)
- [ ] T079 [US1] [frontend] Implement `src/StemStudio.Web/Components/Pages/TrackDetail.razor` (+ `.razor.cs`, route `/tracks/{id:guid}`) and `src/StemStudio.Web/Services/{IPollingTimer,PollingTimer}.cs` to pass T078
- [ ] T080 [P] [US1] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Pages/TrackListPageTests.cs` (title, artist, status badge, duration, link to track detail / mixer) (FR-018)
- [ ] T136 [US1] [frontend] Implement `src/StemStudio.Web/Components/Pages/Tracks.razor` (+ code-behind, route `/tracks`) to pass T080 (O3)

### qa (authored in the wave, executed in the integration wave)

- [ ] T081 [P] [US1] [qa] Write `e2e/tests/import.spec.ts`: notice visible; upload `music_100bpm_20s.mp3` -> progress reaches completed with 6 stems (model `fake`) and BPM ~100; re-upload returns the same track within 2 s (SC-006); rejects for `unsupported.ogg`, a `vimeo.com` URL and a playlist URL show a reason and create no track (SC-007); `corrupt.mp3` upload ends `failed` with a readable reason and a visible `track-retry`; navigation `/tracks` -> track -> `open-mixer` works (G6)

**Checkpoint**: US1 is green in every owner's unit/contract tests; end-to-end proof follows in I-wave.

---

## Phase 4: User Story 2 - Mix the stems and play them in sync (P1) MVP

**Goal**: sample-locked multi-stem playback with gain/pan/mute/solo, meters, master, transport,
A-B loop. **Independent test**: quickstart V7-V8.

### audio-engine (tests first)

- [ ] T082 [P] [US2] [audio-engine] Write failing tests `src/audio-engine/tests/transport.test.ts`: `loadTrack` fetches + decodes every stem (error names the stem and HTTP status), returns duration; `play` resumes the context and starts every new source with the same `when = currentTime + startDelay` and same offset; position = `offset + (currentTime - when)`; pause/stop/seek semantics; end of track -> `stopped`, position 0; callbacks `OnPositionChanged` <= 10 Hz and after seek/pause/stop, `OnPlaybackStateChanged` on every change ; a scripted run of 50 loop wraps and 20 seeks asserts that every start call of one transport event uses the identical `when` and `offset` across all stems (G4) (FR-012, SC-002)
- [ ] T083 [US2] [audio-engine] Implement `src/audio-engine/src/{transport.ts,stem-loader.ts,callbacks.ts}` to pass T082
- [ ] T084 [P] [US2] [audio-engine] Write failing tests `src/audio-engine/tests/mixer.test.ts`: graph source -> gain -> panner -> analyser -> master -> master analyser -> destination; `gate = mute ? 0 : (anySolo && !solo ? 0 : 1)`; gain = volume x gate with `setTargetAtTime` <= 10 ms; pan -1..1; master volume; unknown stem -> Error (FR-011, SC-004, US2-3)
- [ ] T085 [US2] [audio-engine] Implement `src/audio-engine/src/{mixer.ts,channel.ts}` to pass T084
- [ ] T086 [P] [US2] [audio-engine] Write failing tests `src/audio-engine/tests/loop.test.ts`: `setLoop` validates `0 <= start < end <= duration` else throws `Error("Invalid loop region: ...")` and keeps the previous loop; enabled sets `loop/loopStart/loopEnd` on every running source; position outside region restarts at `start`; position wraps inside the region; disable clears `loop` (FR-013, US2-4, US2-5)
- [ ] T087 [US2] [audio-engine] Implement `src/audio-engine/src/loop.ts` to pass T086
- [ ] T088 [P] [US2] [audio-engine] Write failing tests `src/audio-engine/tests/meters.test.ts` (fake `requestAnimationFrame`, jsdom elements): peak -> dB clamp -60..0, `--meter-level` = (dB + 60) / 60 with ~20 dB/s decay, `data-peak-db` rounded, `data-clip="true"` for 1.5 s after `|x| >= 1.0`, frame loop stops when no meter is attached and on `dispose` (FR-011, ADR-001 #4)
- [ ] T089 [US2] [audio-engine] Implement `src/audio-engine/src/meters.ts` to pass T088
- [ ] T090 [US2] [audio-engine] Write failing tests `src/audio-engine/tests/engine.test.ts`: `createEngine` composes transport, mixer, loop and meters; `ENGINE_CONTRACT_VERSION = "1.0.0"`; every method throws after `dispose()`; `setMetronome` accepted and stored (scheduling arrives in T099) (I5)
- [ ] T137 [US2] [audio-engine] Implement `src/audio-engine/src/{engine.ts,index.ts}` to pass T090 and verify `npm run build` emits `src/StemStudio.Web/wwwroot/js/audio-engine.js` (O3)

### backend (tests first)

- [ ] T091 [P] [US2] [backend] Write failing tests `tests/StemStudio.Api.Tests/Tracks/StemEndpointTests.cs`: `GET /api/tracks/{id}/stems/{stemName}` 200 `audio/flac` + `Accept-Ranges: bytes` + `ETag`, `Range: bytes=0-1023` -> 206 + `Content-Range`, unsatisfiable -> 416, `If-None-Match`/`If-Modified-Since` -> 304, `ETag` + `Last-Modified` on 200/206/304, 416 carries `Content-Range: bytes */<size>` (I4), unknown track/stem or stem not on the track -> 404, CORS header for `WebOrigin` only (FR-009b, FR-012)
- [ ] T092 [US2] [backend] Implement `src/StemStudio.Api/Tracks/StemEndpoints.cs` (`Results.File` with `enableRangeProcessing`, path from `IStemFileStore`) to pass T091

### frontend (tests first)

- [ ] T093 [P] [US2] [frontend] Write failing tests `tests/StemStudio.Web.Tests/Mixer/MixerStateTests.cs` (pure C#): defaults fader 0.8, pan 0, master 1.0; solo/mute rules mirrored for display; `IsDirty` after any change, cleared after save/load; loop validation "B must be after A" / "outside the track" messages (US2-1, US2-5, FR-011a)
- [ ] T094 [US2] [frontend] Implement `src/StemStudio.Web/Mixer/{MixerState,LoopValidator}.cs` to pass T093
- [ ] T095 [P] [US2] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Mixer/MixerPageTests.cs` (fake API client, mocked `IAudioEngine`): one `ChannelStrip` per stem in `StemNames.SixStem` order, control changes call `setChannel`/`setMaster` immediately, transport play/pause/stop/seek, position display from `OnPositionChanged`, meters attached by element id, loop A/B set + enable + refusal message, unsaved-changes indicator, `NavigationLock` confirm when dirty (FR-011, FR-011a, FR-012, FR-013)
- [ ] T096 [US2] [frontend] Implement `src/StemStudio.Web/Components/Mixer/{Mixer.razor,ChannelStrip.razor,MasterSection.razor,TransportBar.razor,LoopControls.razor}` (+ code-behind; route `/tracks/{id:guid}/mixer`) with Appendix A test ids to pass T095

### qa

- [ ] T097 [P] [US2] [qa] Write `e2e/tests/mixer.spec.ts`: open the completed fixture track, strips for every stem with default 80 % / pan 0, play after a user gesture, `data-peak-db` on master meter rises above -60 while playing, mute/solo change meters, loop 10 s -> 20 s accepted, B <= A refused with a message; SC-004 timing: `page.evaluate` wraps the engine's `setChannel` to record `performance.now()`, the test asserts click -> engine call < 50 ms for fader, mute and solo (G3)

**Checkpoint**: US1 + US2 = the smallest viable product (unit level).

---

## Phase 5: User Story 3 - Practise with a beat-synced metronome (P2)

**Goal**: click on the detected grid, accent on beat 1, own volume/toggle, BPM/offset override +
reset, aligned after seek/loop/pause. **Independent test**: quickstart V9.

- [ ] T098 [P] [US3] [audio-engine] Write failing tests `src/audio-engine/tests/metronome.test.ts` (fake timers + mock context): timer only wakes the scheduler; every click in `[now, now + scheduleAhead)` started with `start(exactContextTime)` computed from `currentTime`, transport anchor and grid (never from the timer); detected grid `beats[i] + offsetMs/1000`, accent on downbeats; override grid `t0 + k * 60/bpm + offsetMs/1000` with `t0 = downbeats[0] ?? beats[0] ?? 0`, accent every `beatsPerBar`; loop wrap maps the remainder to `loopStart`; seek/pause/loop change cancels future clicks and reschedules; volume change immediate and independent of master; unreliable grid without override is silent; every scheduled click within 1 ms of its beat (FR-014, FR-015, SC-003)
- [ ] T099 [US3] [audio-engine] Implement `src/audio-engine/src/metronome/{scheduler.ts,grid.ts,clicks.ts}` (pre-rendered accent/normal buffers, bypasses master) and wire `setMetronome` into the engine from T137 (wiring only) to pass T098 (I5)
- [ ] T100 [P] [US3] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Mixer/MetronomePanelTests.cs`: toggle (disabled by default when `BeatGrid.Reliable` is false), volume, BPM override 30-300, offset -2000..+2000 ms, beats per bar 1..12, out-of-range input refused with message, Reset restores detected grid, each change calls `setMetronome` and marks the mixer dirty (FR-014)
- [ ] T101 [US3] [frontend] Implement `src/StemStudio.Web/Components/Mixer/MetronomePanel.razor` (+ code-behind) to pass T100

**Checkpoint**: US3 complete at unit level; audible alignment verified manually in I-wave (V9).

---

## Phase 6: User Story 4 - Save, load and manage mixes (P2)

**Goal**: save/save-as/load/update/duplicate/delete mixes; names unique per track.
**Independent test**: quickstart V10.

- [ ] T102 [P] [US4] [backend] Write failing tests `tests/StemStudio.Api.Tests/Mixes/MixEndpointsTests.cs`: `GET /api/mixes[?trackId=]` summaries with `trackTitle`, `updatedAt` desc; `POST /api/mixes` 201 + Location, 400 ValidationProblemDetails with JSON-path keys (`settings.metronome.bpmOverride`, `settings.loop.end`, `settings.name`), unknown stem or stem not on the track -> 400, track not completed -> 400, unknown track -> 404 (A1), duplicate name (case-insensitive, trimmed) -> 409; `GET/PUT/DELETE /api/mixes/{id}` (PUT changes `UpdatedAt`, 409 on rename clash, 404); `POST /api/mixes/{id}/duplicate` -> "<name> (copy)", "(copy 2)" (FR-016, FR-017, SC-005)
- [ ] T103 [US4] [backend] Implement `src/StemStudio.Api/Mixes/{MixEndpoints,MixService,MixMapper,MixValidator}.cs` to pass T102
- [ ] T134 [US4] [backend] Extend `tests/StemStudio.Api.Tests/Contract/BackendContractTests.cs` to every operation (stems from T092, mixes from T103): status codes, headers and bodies against `backend-api.openapi.yaml` 1.0.1, and the served `/openapi/v1.json` lists exactly the contract's operationIds (O2)
- [ ] T104 [P] [US4] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Mixer/SaveMixTests.cs`: Save on an existing mix -> PUT, "Save as" dialog 1-100 chars -> POST, 409 shows "name is taken", success clears dirty; loading `?mixId=` restores channels, master, metronome, loop exactly and pushes them to the engine (US4-1..US4-3, US4-6, SC-005)
- [ ] T105 [US4] [frontend] Implement `src/StemStudio.Web/Components/Mixer/SaveMixDialog.razor` and mix loading in `Mixer.razor.cs` to pass T104
- [ ] T106 [P] [US4] [frontend] Write failing bUnit tests `tests/StemStudio.Web.Tests/Pages/MixLibraryPageTests.cs` (list with track title + last modified, load -> mixer, duplicate, rename with 409 message, delete with confirm dialog; track remains) (US4-4, US4-5, FR-017)
- [ ] T138 [US4] [frontend] Implement `src/StemStudio.Web/Components/Pages/Mixes.razor` (+ code-behind, route `/mixes`) to pass T106 (O3)
- [ ] T107 [US4] [qa] Write `e2e/tests/main-flow.spec.ts` (the constitution's main flow): import(upload fixture) -> wait completed -> open mixer -> change fader, pan, mute, solo, metronome BPM + offset, loop -> save "Practice" -> reload page -> load "Practice" -> every control shows the saved value; save-as "Practice 2", duplicate, delete (SC-001, SC-005)
- [ ] T108 [US4] [qa] Tag a variant of the main flow `@real` in `e2e/tests/main-flow.real.spec.ts` (expects model `htdemucs_6s` or `htdemucs`, long timeout, runs only via `npm run test:real`)

---

## Phase 7: Polish (inside the wave, per owner)

- [ ] T109 [P] [quality-gate] Write `docs/quality/LINT-CONFIG.md` mapping each SIG limit to the enforcing rule (ruff C90/PLR, ESLint complexity/max-params/max-lines-per-function, .NET analyzers CA1502/CA1506 settings, lizard thresholds) and list config gaps as requests to the architect (configs are architect-owned); hand-off `docs/handoffs/quality-gate-polish.md` (checkpoint hand-off, see ADR-008)
- [ ] T110 [P] [qa] Audit test evidence: for every owner's hand-off check test-first commits, fakes instead of network/subprocess, the single integration marker, coverage numbers; write `docs/testing/AUDIT.md` with findings per owner
- [ ] T111 [P] [backend] Structured logging (job sync transitions, worker errors without stack traces to clients) in `src/StemStudio.Api/Sync/` and `Imports/`, with log assertions in existing tests; hand-off `docs/handoffs/backend-polish.md` (checkpoint hand-off, see ADR-008)
- [ ] T112 [P] [frontend] Accessibility pass (labels/aria for faders, pan, buttons; keyboard space = play/pause) with bUnit assertions; hand-off `docs/handoffs/frontend-polish.md` (checkpoint hand-off, see ADR-008)
- [ ] T113 [P] [audio-engine] TSDoc on every export, hand-off `docs/handoffs/audio-engine-polish.md` (checkpoint hand-off, see ADR-008)
- [X] T114 [P] [audio-ingest] Docstrings + hand-off `docs/handoffs/audio-ingest-polish.md` (checkpoint hand-off, see ADR-008) (coverage via `uv run pytest --cov=stemstudio_worker.ingest tests/ingest`)
- [X] T115 [P] [beat-analysis] Docstrings + hand-off `docs/handoffs/beat-analysis-polish.md` (checkpoint hand-off, see ADR-008) (measured BPM/beat errors per synthetic tempo)
- [ ] T116 [P] [stem-separation] Docstrings + hand-off `docs/handoffs/stem-separation-polish.md` (checkpoint hand-off, see ADR-008) (unit coverage + integration test output, model download size/time)
- [ ] T117 [P] [devops] Hand-off `docs/handoffs/devops-polish.md` (checkpoint hand-off, see ADR-008) (image sizes, compose config output, CI job list)
- [ ] T118 [P] [qa] Hand-off `docs/handoffs/qa-polish.md` (checkpoint hand-off, see ADR-008) (fixture generator coverage, suite inventory, audit summary)

---

## Integration wave (CTO, after all wave hand-offs are accepted)

Sequential unless marked [P]. Owners named in brackets fix their own findings in their worktree.

- [ ] T119 [CTO] Merge worktrees into `001-stemstudio-mvp` in dependency order: quality-gate, devops, audio-ingest, beat-analysis, stem-separation, backend, audio-engine, frontend, qa; resolve conflicts only in CTO-owned files, send anything else back to the owner
- [ ] T120 [CTO] Full local verification on the merged branch: `scripts/test-all.ps1` (all builds, `dotnet format`, ruff/mypy, ESLint/tsc, every test suite) and coverage >= 90 % line+branch per component (.NET Api, .NET Web, worker, audio engine); worker coverage now measured on the whole `stemstudio_worker` package; record the output of `uv run pytest -m integration` (real Demucs, K5)
- [ ] T121 [quality-gate] Run `python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md` on the merged code; issue refactor requests per owner; re-run until no hard breach
- [ ] T122 [qa] Generate fixtures, `STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`, run `npm test` in `e2e/` with retries=0 (import, mixer, main flow) on Chromium and Firefox, including one streamed upload of `upload_4mb.wav` (G1) and the persistence check: save a mix, `docker compose restart api web`, reload and load the mix with every setting equal (G2, SC-005); file defects per owner
- [ ] T123 [qa] Real-Demucs run: `docker compose up` with `STEMSTUDIO_SEPARATOR=demucs`, `npm run test:real`; record processing time of the 20 s fixture and a 4-minute song, model used, model-cache reuse on second start
- [ ] T124 [CTO] Manual quickstart validation V1-V10 (including V5 worker down -> 503, V6 restart -> "interrupted by restart" + Retry, V9 audible metronome alignment, SC-002 stem alignment after 50 loop repetitions). SC-002 method: unit evidence from T082 plus a browser check in which `page.evaluate` renders two stems of the fixture through an `OfflineAudioContext` with the engine's start/loop parameters and compares the sample offsets of the outputs (no Vitest: jsdom has no `OfflineAudioContext`); document the result in `docs/MVP-REPORT.md` (G4)
- [ ] T125 [P] [CTO] Push the branch and confirm `.github/workflows/ci.yml` is green (all jobs incl. e2e); trigger `e2e-real.yml` once
- [ ] T126 [P] [CTO] Consolidate `docs/THIRD-PARTY-LICENSES.md` from every hand-off (NuGet, Python incl. flagged GPL/LGPL transitive packages, npm, Docker base images, ffmpeg) and the known-limitations section (FR-010 stem limitation, unofficial Spotify-client ToS risk)
- [ ] T127 [CTO] Write `README.md` (one-command start, non-Docker path, limitations, legal notice) and `docs/MVP-REPORT.md` (scope delivered per FR/SC, coverage table, SIG report summary, E2E evidence, open issues, backlog: mix export first)
- [ ] T128 [CTO] Update `docs/STATUS.md`, run `/speckit-analyze` for drift between spec, plan, tasks and code, and tag the MVP commit

---

## Execution plan per owner (parallel wave)

All owners start simultaneously after Phase 0, each in its own git worktree from the Phase 0
commit. Order inside a sequence is binding (test task before its implementation task).

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| audio-ingest | T043 -> T044 -> T045 -> T046 -> T047 -> T048 -> T049 -> T050 -> T139 -> T140 -> HO:us1 -> T114 (HO:polish) | `worker/src/stemstudio_worker/ingest/**`, `worker/tests/ingest/**` | fake `yt_dlp` factory, fake spotDL metadata client + YouTube matcher, fake `CommandRunner` for ffmpeg/ffprobe; real files only in `tmp_path` |
| beat-analysis | T051 -> T052 -> T053 -> T054 -> T055 -> T141 -> T142 -> HO:us1 -> T115 (HO:polish) | `worker/src/stemstudio_worker/analysis/**`, `worker/tests/analysis/**` | none needed (synthetic audio generated in tests; librosa runs for real) |
| stem-separation | T035 -> T036 -> HO:foundational -> T056 -> T057 -> T058 -> T059 -> T060 -> T061 -> T062 -> T063 -> T064 -> T065 -> T066 -> T067 -> T068 -> HO:us1 -> T116 (HO:polish) | `worker/src/stemstudio_worker/{separation,jobs,api}/**`, `worker/src/stemstudio_worker/main.py`, `worker/tests/{separation,jobs,api}/**` | fake `PipelineAdapters` / `IngestAdapters` (all ports), fake Demucs loader/applier, fake clock; T001 factories for ingest/analysis are NOT called in unit tests |
| backend | T023 -> T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> T032 -> T033 -> T034 -> HO:foundational -> T069 -> T070 -> T071 -> T072 -> T073 -> T074 -> T075 -> HO:us1 -> T091 -> T092 -> HO:us2 -> T102 -> T103 -> T134 -> HO:us4 -> T111 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**` (Contracts read-only: architect-owned, plan.md O1), `tests/StemStudio.{Domain,Persistence,Api}.Tests/**` | fake worker client / stub `HttpMessageHandler` with worker contract examples, `FakeTimeProvider`, SQLite in-memory, temp DataDir |
| audio-engine | T037 -> HO:foundational -> T082 -> T083 -> T084 -> T085 -> T086 -> T087 -> T088 -> T089 -> T090 -> T137 -> HO:us2 -> T098 -> T099 -> HO:us3 -> T113 (HO:polish) | `src/audio-engine/**` except `src/types.ts`, `package.json`, configs (architect) | mocked `AudioContext`, fake `dotnetRef`, stubbed `fetch`, fake timers + `requestAnimationFrame`, jsdom elements |
| frontend | T038 -> T039 -> T040 -> T041 -> T042 -> T135 -> HO:foundational -> T076 -> T077 -> T078 -> T079 -> T080 -> T136 -> HO:us1 -> T093 -> T094 -> T095 -> T096 -> HO:us2 -> T100 -> T101 -> HO:us3 -> T104 -> T105 -> T106 -> T138 -> HO:us4 -> T112 (HO:polish) | `src/StemStudio.Web/**` (not `wwwroot/js/audio-engine.js`), `tests/StemStudio.Web.Tests/**` | mocked `IJSRuntime`/`IJSObjectReference` (bUnit JSInterop), fake `IStemStudioApiClient` or stub `HttpMessageHandler`, fake `IPollingTimer`, bUnit `NavigationManager` |
| qa | T019 -> T020 -> T021 -> T022 -> HO:foundational -> T081 -> HO:us1 -> T097 -> HO:us2 -> T107 -> T108 -> HO:us4 -> T110 -> T118 (HO:polish; then T122, T123 in I-wave) | `e2e/**`, `tests/fixtures/**`, `docs/testing/**` | Playwright `page.route` only to self-test page objects; the real suite runs against compose in I-wave |
| quality-gate | T004 -> T005 -> T006 -> T007 -> T008 -> T009 -> T010 -> HO:foundational -> T109 (HO:polish; then T121 in I-wave) | `tools/quality/**`, `sonar-project.properties`, `docs/quality/**` | synthetic samples in `tools/quality/tests/samples/`; fake `subprocess` for jscpd in unit tests |
| devops | T011 -> T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> HO:foundational -> T117 (HO:polish) | `docker/**`, `docker-compose.yml`, `.github/**`, `scripts/**`, `.dockerignore` | runs against the green skeleton; no other owner's code |
| architect | T001, T002, T003, T129-T133 (done, before the wave; later only contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |

Coverage evidence per owner: backend `dotnet test tests/StemStudio.{Domain,Persistence,Api}.Tests
-p:CollectCoverage=true` (the per-component 90 % line+branch gate is built into
`Directory.Build.props`; Api.Tests also gates Contracts); frontend the same on
`tests/StemStudio.Web.Tests`; worker owners `uv run pytest --cov=stemstudio_worker.<package>
--cov-branch tests/<package>` (stem-separation: `jobs`, `separation`, `api`, `main`); audio-engine
`npm run test:coverage`; quality-gate `pytest --cov=tools/quality --cov-branch tools/quality/tests`;
qa `pytest --cov --cov-branch tests/fixtures/tests` for the generator (>= 90 %); devops: no measurable code (validation
outputs instead).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T068 stem-separation `main.py` | audio-ingest and beat-analysis implementations | factory signatures from T001; unit tests inject fakes; real wiring checked in T120/T122 |
| T052/T060 analysis + Demucs tests | qa fixtures in `tests/fixtures/` | synthetic audio generated inside the owner's own tests |
| T075/T134 backend contract test, T067 worker contract test | a YAML/JSON-schema library in architect-owned dependency files | T003 adds the test-only packages before the wave |
| T081/T097/T107 Playwright | frontend markup | UI test-id contract (T002, Appendix A); execution moved to T122 |
| T013 web image, T016 CI engine job | the engine bundle | built from source inside the image/CI (`npm run build`), skeleton bundle is enough for the wave |
| T016 CI `quality` and `e2e` jobs | quality-gate and qa scripts | Appendix B command interface; jobs go green only after T119 |

---

## Dependencies & Execution Order

- **Phase 0** (architect) blocks the wave.
- **Wave**: all owners in parallel; inside an owner: Setup -> Foundational -> US1 -> US2 -> US3 ->
  US4 -> Polish (sequence column above). No owner waits for another owner.
- **User stories**: US1 and US2 are both P1 (MVP); US2's mixer is testable with a seeded completed
  track (backend tests) or a fake API client (frontend), so it does not wait for US1 code. US3
  extends the engine and mixer of US2; US4 extends the mixer of US2 and the persistence of Phase 2.
- **Engine order (I5)**: core engine T090/T137 before the metronome T098/T099.
- **Integration wave**: T119 -> T120 -> T121 -> T122 -> T123 -> T124; T125/T126 in parallel;
  T127 -> T128 last.

## Parallel example (wave start)

```text
Agent audio-ingest:    T043 test_url_resolver.py
Agent beat-analysis:   T051 conftest.py synthetic signals
Agent stem-separation: T035 test_job_store.py
Agent backend:         T023 SourceKeyTests.cs
Agent audio-engine:    T037 tests/mocks/audio-context.ts
Agent frontend:        T038 StemStudioApiClientTests.cs
Agent qa:              T019 TEST-STRATEGY.md
Agent quality-gate:    T004 test_sig_check.py
Agent devops:          T011 worker.Dockerfile
```

## Implementation strategy

1. Phase 0 contract fixes (architect) -> commit.
2. One parallel wave; each owner delivers US1 + US2 first (MVP), then US3 and US4, then polish and
   a hand-off. The CTO reviews hand-offs as they arrive.
3. Integration wave: merge, full verification, SIG report, E2E (fake, then real Demucs), reports.
4. MVP = US1 + US2 green end to end; US3 + US4 complete the release.

---

## Definition of Done checklist (mirrors the constitution)

Per task and for the MVP:

- [ ] Red-green-refactor followed: the failing test was committed or shown before the implementation (I)
- [ ] All tests green; coverage >= 90 % line AND branch per component (.NET Api, .NET Web, worker, audio engine), exact numbers in the hand-off; CI fails below the threshold (I)
- [ ] Heavy tools (Demucs, librosa where slow, yt-dlp, spotDL, ffmpeg) behind ports with fakes; exactly one `@pytest.mark.integration` test; Playwright main flow green with retries=0 (I)
- [ ] SIG limits: unit <= 15 LOC (never > 30), CC <= 5 (never > 10), <= 4 parameters, no duplicated block >= 6 lines, one-way dependencies; `tools/quality/run_all.py` passes (II)
- [ ] Implementation matches the contracts; contract tests pass; no breaking change without ADR + version bump; every public API documented (XML docs, docstrings, TSDoc) (III)
- [ ] Only OSI-licensed dependencies, each listed with version, licence and URL in the hand-off; no DRM circumvention; legal notice in the UI (IV)
- [ ] MVP scope only; limitations documented (FR-010); audible timing only from `AudioContext.currentTime` (V)
- [ ] No secrets in files or command lines; URLs/files validated (allow-listed hosts, size limits, no path traversal); subprocess argument arrays, never a shell (Security)
- [ ] .NET analyzers with warnings as errors + `dotnet format --verify-no-changes`; ruff + ruff format + mypy --strict; ESLint + `tsc --noEmit` clean
- [ ] Hand-off note `docs/handoffs/<agent>-<task>.md` written; `docs/STATUS.md` updated by the CTO; Conventional Commit with the Co-Authored-By trailer

---

## Appendix A: UI test-id contract (binding for frontend and qa; published by T002)

The authoritative copy is [contracts/ui-test-ids.md](contracts/ui-test-ids.md); if the two differ, the contract file wins.


| Screen | `data-testid` |
|---|---|
| Import (`/`) | `legal-notice`, `import-url-input`, `import-url-submit`, `import-file-input`, `import-error`, `stem-limitation-help` |
| Track detail (`/tracks/{id}`) | `track-title`, `track-status`, `track-stage`, `track-progress` (attribute `aria-valuenow` 0-100), `track-error`, `track-retry`, `track-bpm`, `track-duration`, `track-model`, `track-stem-<name>`, `open-mixer` |
| Track list (`/tracks`) | `track-row-<trackId>`, `track-row-status-<trackId>` |
| Mixer (`/tracks/{id}/mixer`) | `strip-<stem>`, `fader-<stem>`, `pan-<stem>`, `mute-<stem>`, `solo-<stem>`, `meter-<stem>` and `meter-master` (engine writes `data-peak-db`, `data-clip`), `master-fader`, `transport-play`, `transport-pause`, `transport-stop`, `transport-seek`, `transport-position`, `loop-start`, `loop-end`, `loop-toggle`, `loop-error`, `unsaved-indicator`, `metronome-toggle`, `metronome-volume`, `metronome-bpm`, `metronome-offset`, `metronome-beats-per-bar`, `metronome-reset`, `metronome-error`, `mix-save`, `mix-save-as`, `mix-name-input`, `mix-save-confirm`, `mix-error` |
| Mix library (`/mixes`) | `mix-row-<mixId>`, `mix-load-<mixId>`, `mix-duplicate-<mixId>`, `mix-rename-<mixId>`, `mix-delete-<mixId>`, `confirm-dialog-ok`, `confirm-dialog-cancel` |

Fader/pan/volume inputs expose their numeric value via `aria-valuenow` (fader 0-100, pan -100..100).

## Appendix B: Command and code interfaces between owners

| Interface | Provider | Consumers | Definition |
|---|---|---|---|
| Worker adapter factories | audio-ingest, beat-analysis, stem-separation | stem-separation `main.py` | `create_ingest_adapters(settings)`, `create_beat_analyzer(settings)`, `create_separator(settings)` (T001) |
| Fake separator | stem-separation | devops, qa | `STEMSTUDIO_SEPARATOR=fake` -> model `fake`, 6 stems |
| Engine bundle | audio-engine | frontend, devops | `npm run build` in `src/audio-engine` -> `src/StemStudio.Web/wwwroot/js/audio-engine.js` |
| Quality gate | quality-gate | devops (CI), CTO | `python tools/quality/run_all.py [--report <md>]`, exit 0 = pass; deps `pip install -r tools/quality/requirements.txt`, Node for jscpd |
| Fixtures | qa | devops (CI), qa | `uv run --project worker python tests/fixtures/generate_fixtures.py` -> `tests/fixtures/audio/`, `tests/fixtures/manifest.json` |
| E2E suite | qa | devops (CI), CTO | in `e2e/`: `npm ci`, `npm test` (fake), `npm run test:real`; env `E2E_BASE_URL`, `E2E_API_URL` |
| Stack start | devops | qa, CTO | `STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`; native `scripts/dev.ps1 -Fake` / `scripts/dev.sh --fake` |
| Health | worker, api, web | devops healthchecks | `GET /health` on 8000, 8081, 8080 |
