# Implementation Plan: StemStudio MVP

**Branch**: `001-stemstudio-mvp` | **Date**: 2026-11-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-stemstudio-mvp/spec.md`

**Note**: Produced by `/speckit-plan` (architect agent). Binding decisions come from the CTO brief
and are recorded in ADR-002 .. ADR-007 (`docs/decisions/`).

## Summary

A musician imports a song (YouTube URL, Spotify track URL resolved to a public YouTube match, or a
local MP3/WAV/FLAC upload). A Python worker downloads/normalises the audio, separates it with
Demucs `htdemucs_6s` (fallback `htdemucs`) into FLAC stems and detects the beat grid with librosa.
An ASP.NET Core Minimal API owns tracks, stems (served with HTTP Range + CORS) and saved mixes in
SQLite, and synchronises worker jobs by polling. A Blazor Web App (Interactive Server, Radzen)
provides import, progress, track list, mixer and mix library; a TypeScript Web Audio engine plays
all stems sample-locked from one `AudioContext` with gain/pan/mute/solo, post-fader peak meters,
seamless A-B looping and a look-ahead metronome scheduled on `AudioContext.currentTime`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.401, `net10.0`); Python 3.12 (worker, `>=3.12,<3.13`);
TypeScript 6.0 (audio engine, ES2022 module); Node 22 for tooling.

**Primary Dependencies**: ASP.NET Core 10 Minimal API, EF Core 10.0.15 + SQLite,
Microsoft.AspNetCore.OpenApi, Microsoft.Extensions.Http.Resilience; Blazor Web App + Radzen.Blazor
13.1.0; FastAPI 0.163 + uvicorn 0.23.2 + pydantic 2.14; Demucs 4.1.0 on torch 2.15.0 (CPU wheels);
librosa 1.0.1, soundfile 0.14, numpy 2.5; yt-dlp 2026.10.26; spotDL 4.6.0; ffmpeg (system binary).
Exact pins and licences: hand-off `docs/handoffs/architect-plan-and-skeleton.md`.

**Storage**: SQLite (`<DataDir>/stemstudio.db`, EF Core migrations) for tracks and mixes; local disk
for stems (`<DataDir>/tracks/<trackId>/stems/<name>.flac`); worker job state, cache and temporary
files under `STEMSTUDIO_DATA_DIR` (JSON files + FLAC). See [data-model.md](data-model.md).

**Testing**: xUnit v3 (VSTest runner) + Shouldly + NSubstitute + bUnit, coverlet (Cobertura);
pytest + pytest-cov (one `@pytest.mark.integration` test runs real Demucs); Vitest + v8 coverage with
a mocked `AudioContext`; Playwright (TypeScript) E2E for import -> separate -> mix -> save -> reload
with `STEMSTUDIO_SEPARATOR=fake`. SIG metrics via `tools/quality/` (lizard + jscpd). See
[research.md](research.md) R-08..R-10 and ADR-006.

**Target Platform**: Single local machine. Docker Compose on Linux containers (x86_64/aarch64) as
the primary run path; native run on Windows/Linux as documented alternative. Evergreen desktop
browsers (Chromium, Firefox, Safari 17+) with Web Audio and FLAC decoding.

**Project Type**: Web application: Blazor server-rendered UI + REST API + Python worker service +
browser-side TypeScript audio library.

**Performance Goals**: Stems aligned within 1 ms through seeks and 50 loop repetitions (SC-002);
metronome clicks within 1 ms of the beat grid (SC-003); control changes audible within 50 ms
(SC-004); re-import of a cached source ready within 2 s (SC-006); a 4-minute song processed on CPU
in "several minutes" with continuous progress.

**Constraints**: Upload <= 200 MiB, audio <= 15 min; one job at a time (FIFO); CPU by default
(`STEMSTUDIO_DEVICE=cpu|cuda|auto`); no DRM circumvention; no secrets; OSI licences only; SIG limits
(unit <= 15 LOC, CC <= 5, <= 4 params, no 6-line duplicates); coverage >= 90 % line and branch per
component; zero analyzer warnings.

**Scale/Scope**: 1 user, tens to a few hundred tracks, 4-6 stems per track (~25 MB FLAC per stem
for 4 min), 5 screens (import, progress, track list, mixer, mix library).

No NEEDS CLARIFICATION items remain: spec clarifications are settled by ADR-001, technology choices
by the CTO brief and [research.md](research.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch per component | Every agent works red-green-refactor; coverage gates: coverlet (.NET API, .NET UI), pytest-cov `fail_under = 90` + branch (worker), Vitest thresholds 90/90/90/90 (engine); CI fails below threshold (devops). Heavy tools behind ports (`worker/src/stemstudio_worker/ports.py`), exactly one `integration`-marked Demucs test, Playwright main-flow E2E. | PASS |
| II | SIG 4 stars | Limits encoded in tooling: ruff (`max-complexity 5`, `max-args 4`, `max-statements 15`), ESLint (`complexity 5`, `max-params 4`, `max-lines-per-function 30`), .NET analyzers + `tools/quality/` lizard/jscpd checks (quality-gate agent). Layers are one-way (see Project Structure); 6 components. | PASS |
| III | Contracts first | `contracts/worker-api.openapi.yaml`, `contracts/backend-api.openapi.yaml` (OpenAPI 3.1, Redocly-valid), `contracts/audio-engine.md` + `src/audio-engine/src/types.ts` written before implementation; `StemStudio.Contracts` DTOs and `schemas.py` mirror them; contract tests exist in the skeleton. All public APIs documented (CS1591 is an error, ruff `D` rules, TSDoc). | PASS |
| IV | Open source only, lawful use | All dependencies OSI-licensed (list in hand-off; copyleft GPL/LGPL items flagged for the CTO). FluentAssertions excluded (commercial licence since v8). Spotify: metadata via spotDL, audio from a public YouTube match only. UI notice required by FR-005 (frontend). | PASS |
| V | Simplicity and honesty | MVP scope only; polling instead of push; files + SQLite instead of servers; single worker thread. Stem limitation documented (FR-010). Web Audio timing on `AudioContext.currentTime` with look-ahead scheduler; timers only wake the scheduler. | PASS |
| - | Tech & security constraints | Preferred stack used unchanged; config from env/appsettings without secrets; URL host allow-lists and size limits in API and worker; subprocesses with argument lists; stem names validated against an enum (no path traversal). | PASS |
| - | Workflow & quality gates | Spec Kit flow followed; agents own disjoint paths; hand-off notes per task; Conventional Commits. | PASS |

**Post-design re-check (after Phase 1)**: PASS. The design adds no component, layer or dependency
beyond the constitution's stack. Two deliberate refinements are recorded rather than treated as
violations: Demucs 4.1.0 instead of 4.0.1 (newer upstream release without the torchaudio
dependency; ADR-003) and xUnit v3 run on VSTest instead of Microsoft.Testing.Platform to keep
coverlet (ADR-007).

## Project Structure

### Documentation (this feature)

```text
specs/001-stemstudio-mvp/
├── spec.md              # Feature specification (/speckit-specify, /speckit-clarify)
├── plan.md              # This file
├── research.md          # Phase 0: decisions, rationale, alternatives
├── data-model.md        # Phase 1: entities, validation, state machines, storage layout
├── quickstart.md        # Phase 1: run and validation guide
├── contracts/
│   ├── worker-api.openapi.yaml    # Worker HTTP API (OpenAPI 3.1)
│   ├── backend-api.openapi.yaml   # Backend HTTP API (OpenAPI 3.1)
│   ├── audio-engine.md            # JS interop contract (types in src/audio-engine/src/types.ts)
│   └── redocly.yaml               # Lint config for the OpenAPI files
├── checklists/
└── tasks.md             # Phase 2 (/speckit-tasks; not created by /speckit-plan)
```

### Source Code (repository root)

```text
StemStudio.slnx                      # .NET solution (src + tests)
global.json                          # SDK 10.0.401, rollForward latestFeature
Directory.Build.props                # net10.0, nullable, warnings-as-errors, analyzers, test defaults
Directory.Packages.props             # Central Package Management (all NuGet versions)
.editorconfig                        # C# style/analyzer rules (+ other file types)

src/
├── StemStudio.Domain/               # Pure domain: Track, Stem, BeatGrid, Mix (+ChannelSettings,
│   └── Tracks/                      #   MetronomeSettings, LoopRegion), SourceKey, TrackStatus, rules
├── StemStudio.Contracts/            # HTTP DTOs (records) shared by Api and Web; no dependencies
│   ├── ApiRoutes.cs, ContractLimits.cs, StemNames.cs
│   ├── Health/  Importing/  Tracks/  Mixes/
├── StemStudio.Persistence/          # EF Core SQLite DbContext, configurations, migrations, repositories
├── StemStudio.Api/                  # Minimal API: endpoints, worker client, file storage, JobSyncService
├── StemStudio.Web/                  # Blazor Web App (Interactive Server) + Radzen
│   ├── Components/{App,Routes,_Imports}.razor, Layout/, Pages/
│   └── wwwroot/js/audio-engine.js   # build output of src/audio-engine (git-ignored)
└── audio-engine/                    # TypeScript Web Audio engine (Vite lib, Vitest, ESLint)
    ├── src/types.ts                 # binding interop contract
    ├── src/index.ts                 # createEngine (implemented by the audio-engine agent)
    └── tests/

tests/
├── StemStudio.Domain.Tests/         # xUnit
├── StemStudio.Persistence.Tests/    # xUnit + SQLite in-memory
├── StemStudio.Api.Tests/            # xUnit + WebApplicationFactory, contract tests
├── StemStudio.Web.Tests/            # xUnit + bUnit
└── coverlet.runsettings             # shared coverage include/exclude

worker/                              # Python 3.12, uv-managed, package stemstudio_worker
├── pyproject.toml, uv.lock
├── src/stemstudio_worker/
│   ├── ports.py                     # Protocols: SourceResolver, AudioFetcher, AudioNormalizer,
│   │                                #   Separator, BeatAnalyzer, ProgressCallback (+ errors, value types)
│   ├── schemas.py                   # pydantic v2 models = worker contract
│   ├── config.py                    # env settings (STEMSTUDIO_DATA_DIR, _SEPARATOR, _DEVICE)
│   ├── main.py                      # create_app() factory
│   ├── api/  jobs/  ingest/  separation/  analysis/
└── tests/{ingest,separation,analysis,jobs,api}/ + top-level skeleton tests

e2e/                                 # Playwright (TypeScript) - qa agent
tools/quality/                       # sig_check.py (lizard, jscpd), Sonar config - quality-gate agent
docker/, docker-compose.yml, scripts/, .github/workflows/   # devops agent
docs/decisions/ADR-*.md, docs/handoffs/*.md
```

**Dependency direction** (no cycles; enforced by project references and the quality gate):

```text
Web ──> Contracts <── Api ──> Domain <── Persistence
                       └────> Persistence
Api ──HTTP──> worker (worker-api.openapi.yaml)
Browser audio engine ──HTTP──> Api stems endpoint; Web ──JS interop──> audio engine (types.ts)
worker: api -> jobs -> ports <- {ingest, separation, analysis}; everything -> schemas, config
```

**Structure Decision**: Web application with three runtime services (web 8080, api 8081, worker
8000 internal) plus one browser library. .NET code is split into five projects so that the domain
and the HTTP contract have no infrastructure dependencies and Web cannot reach persistence. Each
agent owns disjoint folders (see `.claude/agents/*.md`).

### Ownership map

| Path | Owner |
|---|---|
| `specs/**`, `docs/decisions/ADR-002+`, skeleton files | architect |
| `src/StemStudio.{Domain,Persistence,Api}`, matching tests | backend |
| `src/StemStudio.Web`, `tests/StemStudio.Web.Tests` | frontend |
| `src/audio-engine/**` (except `src/types.ts`, configs) | audio-engine |
| `worker/src/stemstudio_worker/ingest/**` | audio-ingest |
| `worker/src/stemstudio_worker/{separation,jobs,api,main.py}` | stem-separation |
| `worker/src/stemstudio_worker/analysis/**` | beat-analysis |
| `e2e/**`, shared fixtures | qa |
| `tools/quality/**`, `sonar-project.properties` | quality-gate |
| `docker/**`, `docker-compose.yml`, `.github/**`, `scripts/**` | devops |

## Key flows (summary; details in contracts and data-model)

1. **Import**: Web -> `POST /api/imports` | `/api/imports/upload` -> Api validates, derives the
   SourceKey; existing track -> `200`; else Track(`queued`) + worker `POST /v1/jobs` -> `202`.
   Worker unreachable -> `503`, nothing persisted.
2. **Sync**: `JobSyncService` (BackgroundService, `TimeProvider`-driven, ~1 s) polls
   `GET /v1/jobs/{id}` for non-terminal tracks; maps status/stage/progress/metadata; on `completed`
   downloads `GET /v1/jobs/{id}/result` and every stem to `<DataDir>/tracks/<id>/stems/`, then marks
   the track `completed` (atomic: files first, status last). Worker `404` for a non-terminal
   track -> `failed` "interrupted by restart".
3. **Progress**: Web polls `GET /api/tracks/{id}` ~1 s until terminal.
4. **Playback**: engine fetches `GET /api/tracks/{id}/stems/{name}` directly from the public API
   base URL (CORS for the web origin, Range, ETag), decodes FLAC, plays sample-locked.
5. **Mixes**: CRUD + duplicate; name unique per track (case-insensitive) -> `409` on conflict.

## Complexity Tracking

No constitution violations. Deliberate refinements of the brief (not violations):

| Refinement | Why | Simpler alternative rejected because |
|---|---|---|
| Demucs 4.1.0 (brief assumed 4.0.1) | 4.1.0 (PyPI, 2026-07) removes the torchaudio dependency and works with torch 2.15 on Python 3.12; ships `demucs.api` | 4.0.1 hard-depends on torchaudio (2.9+ dropped the I/O backends `demucs.audio` calls) and on training-only packages; 4.1.0 needs neither and was verified to import and expose `get_model`/`apply_model` with torch 2.15.0+cpu |
| xUnit v3 on VSTest (`IsTestingPlatformApplication=false`) | Keeps `coverlet.collector`/`coverlet.msbuild` (brief requirement) working on .NET 10 SDK | MTP mode needs `coverlet.MTP` and different CLI switches; can be adopted later via ADR |
