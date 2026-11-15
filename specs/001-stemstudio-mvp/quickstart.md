# Quickstart and validation guide: StemStudio MVP

How to build, test and run StemStudio and how to validate the user stories end to end.
Contracts: [contracts/](contracts/). Entities and states: [data-model.md](data-model.md).

## Prerequisites

| Tool | Version | Used by |
|---|---|---|
| .NET SDK | 10.0.401 (see `global.json`, roll-forward to later 10.0.x feature bands) | Api, Web, tests |
| uv | >= 0.13 (installs Python 3.12 itself) | worker |
| Node.js + npm | 22.22.2+ (LTS 22) | audio engine, Playwright, Redocly |
| ffmpeg | any recent build on `PATH` | worker |
| Docker + Compose | 29+ | one-command run, E2E |

No accounts, keys or secrets are needed.

## Build and test (every component)

```bash
# .NET (zero warnings expected; analyzers run as errors)
dotnet build StemStudio.slnx
dotnet test StemStudio.slnx
dotnet test StemStudio.slnx --collect "XPlat Code Coverage"          # Cobertura per test project
dotnet test StemStudio.slnx -p:CollectCoverage=true   # per-component gate: 90 % line+branch (Directory.Build.props, ADR-006)
dotnet format StemStudio.slnx --verify-no-changes

# Worker
cd worker
uv sync                                   # creates .venv with Python 3.12 from uv.lock
uv run pytest --cov                       # unit tests, fails below 90 % (line+branch)
uv run pytest -m integration              # the single real-Demucs test (slow, downloads weights)
uv run ruff check . && uv run ruff format --check . && uv run mypy src tests

# Audio engine
cd src/audio-engine
npm ci
npm test && npm run test:coverage && npm run lint && npm run typecheck
npm run build                             # -> src/StemStudio.Web/wwwroot/js/audio-engine.js

# Contracts
npx @redocly/cli@2.78.0 lint --config specs/001-stemstudio-mvp/contracts/redocly.yaml \
  specs/001-stemstudio-mvp/contracts/worker-api.openapi.yaml \
  specs/001-stemstudio-mvp/contracts/backend-api.openapi.yaml
```

## Run without Docker (three terminals)

```bash
# 1. worker on :8000 (use STEMSTUDIO_SEPARATOR=fake for a fast smoke run)
cd worker && STEMSTUDIO_DATA_DIR=../data/worker uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000

# 2. API on :8081
dotnet run --project src/StemStudio.Api --launch-profile http

# 3. audio engine bundle, then Web on :8080
(cd src/audio-engine && npm run build) && dotnet run --project src/StemStudio.Web --launch-profile http
```

Configuration (environment variables override `appsettings.json`):

| Setting | Default | Meaning |
|---|---|---|
| `StemStudio__DataDir` (Api) | `../../data/api` | SQLite DB, uploads, stems |
| `StemStudio__WorkerBaseUrl` (Api) | `http://localhost:8000` | worker API |
| `StemStudio__WebOrigin` (Api) | `http://localhost:8080` | CORS origin for stem fetches |
| `StemStudio__ApiBaseUrl` (Web) | `http://localhost:8081` | server-side API calls |
| `StemStudio__PublicApiBaseUrl` (Web) | `http://localhost:8081` | browser-side stem URLs |
| `STEMSTUDIO_DATA_DIR` (worker) | `data/worker` | jobs, cache, temp |
| `STEMSTUDIO_SEPARATOR` (worker) | `demucs` | `demucs` or `fake` |
| `STEMSTUDIO_DEVICE` (worker) | `cpu` | `cpu`, `cuda` or `auto` |

## Run with Docker (devops deliverable)

```bash
docker compose up --build        # web http://localhost:8080, api http://localhost:8081
```

## Validation scenarios

Health first: `GET http://localhost:8000/health`, `GET http://localhost:8081/health`,
`GET http://localhost:8080/health` all return `{"status":"ok",...}`.

| # | Story | Steps | Expected |
|---|---|---|---|
| V1 | US1 upload | Open `/`, read the practice/copyright notice, upload a 10-30 s MP3 | 202; progress moves queued -> downloading -> separating -> analysing -> completed with stage texts; 6 stems (4 if fallback), BPM and duration shown |
| V2 | US1 cache | Upload the same file again | 200 with the same track id, ready within 2 s (SC-006), no new worker job |
| V3 | US1 YouTube/Spotify | Import `https://youtu.be/<id>`, then a `open.spotify.com/track/<id>` URL | title (and artist for Spotify) from source metadata; Spotify audio comes from a YouTube match |
| V4 | US1 rejects | Submit a playlist URL, a `vimeo.com` URL, a `.ogg` file, a file of 200 MiB + 1 byte (209,715,201 bytes) | 400/415/413 problem details with an actionable message; no track created |
| V5 | US1 failure | Stop the worker, import a URL | 503 "processing service unavailable"; existing tracks still play |
| V6 | US1 restart | Restart the stack during "Separating stems" | the track shows failed "interrupted by restart"; Retry re-queues it |
| V7 | US2 mixer | Open a completed track; mute guitar, solo drums+bass, pan, move faders | one strip per stem (default 80 %, pan 0), meters move, only soloed unmuted stems audible, changes audible < 50 ms |
| V8 | US2 loop | Set A=10 s, B=20 s, enable loop, let it repeat; try B <= A | seamless repeat, stems stay aligned; invalid region refused with a reason |
| V9 | US3 metronome | Enable click, change volume, set BPM 120 and offset +50 ms, reset; seek and loop | accent on beat 1, clicks follow the grid / override, stay aligned after seek/loop |
| V10 | US4 mixes | Save "Practice", reload the page, load it; save-as "Practice 2"; duplicate; rename to an existing name; delete | all settings restored exactly; "(copy)" created; 409 name taken message; delete leaves track and stems |

Automated coverage of V1, V7, V10 (main flow) is the Playwright suite in `e2e/` running against
compose with `STEMSTUDIO_SEPARATOR=fake` (owned by qa).
