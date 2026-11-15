# Deployment

## Docker Compose (default, CPU)

Requirements: Docker with Compose v2. For real detail separation Docker needs at least 20 GB
of memory (a 4-minute song uses about 16 GB in the worker); check with
`docker info --format "{{.MemTotal}}"`.

```
docker compose up --build        # start; web on :8080, API on :8081
docker compose down              # stop, keep data
docker compose down -v           # stop and DELETE all data and the model cache
```

The compose project is called `stemstudio`. Every service uses `restart: unless-stopped`
and a healthcheck; the API waits for a healthy worker and the web app for a healthy API.

| Service | Image | Host port | Volumes | Healthcheck |
|---|---|---|---|---|
| worker | `stemstudio/worker:local` | none (`expose: 8000`) | `worker-data:/data`, `model-cache:/models` | `GET /health` on 8000 |
| api | `stemstudio/api:local` | 8081 | `api-data:/data` | `GET /health` on 8081 |
| web | `stemstudio/web:local` | 8080 | `web-keys` (data-protection keys) | `GET /health` on 8080 |

Kestrel serves directly without a reverse proxy. A proxy added later must accept request
bodies of at least 200 MiB plus multipart overhead. Containers run as non-root users (the
worker as uid/gid 10001).

## GPU variant

```
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

The override builds `stemstudio/worker:local-gpu` with CUDA wheels (`TORCH_VARIANT`, default
`cu132`, and `ORT_VARIANT=gpu`), sets `STEMSTUDIO_DEVICE=auto` and reserves one NVIDIA GPU
(`deploy.resources.reservations.devices`). Prerequisites: compute capability ≥ 7.5, a host
driver with CUDA 13.2 support (R580+), Docker Desktop on WSL2 without a Linux driver inside
WSL, or the NVIDIA Container Toolkit on Linux. Verify the host with
`docker run --rm --gpus all nvcr.io/nvidia/k8s/cuda-sample:nbody nbody -gpu -benchmark` and
StemStudio with `scripts/gpu-smoke.ps1` (or `.sh`). The CPU image is about 3.4 GB, the GPU
image about 11.5 GB.

## Generative services <span class="planned">In development</span>

Features 009 and 010 add opt-in overrides; the default stack does not change:

```
docker compose -f docker-compose.yml -f docker-compose.gpu.yml \
  -f docker-compose.generative.yml up --build
```

This adds the `generator` (about 15 GB image) and `singer` (7 to 8 GB) services without host
ports, plus the volumes `generator-data`, `singer-data` and the shared `gpu-lease` volume.
`docker-compose.generative-fake.yml` runs a fake generator without torch or GPU for tests.

## Non-Docker development start

Requirements: .NET SDK 10.0.401+, uv 0.13+ (installs Python 3.12), Node.js 22.22.2+, ffmpeg
and ffprobe on `PATH`, and Deno (needed by yt-dlp). `scripts/dev.ps1` (or `dev.sh`) builds
the TypeScript bundles and the solution and starts worker (127.0.0.1:8000), API (8081) and
web (8080) with data under `<repo>/data`. Switches: `-Fake`, `-SkipEngineBuild`,
`-SmokeTest` and `-TimeoutSeconds`.

## Configuration

ASP.NET Core settings use `__` as separator in environment variables
(`StemStudio__DataDir`). Delivered settings:

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_SEPARATOR` | worker | `demucs` | `demucs` or `fake` |
| `STEMSTUDIO_DEVICE` | worker | `cpu` (GPU image `auto`) | `cpu`, `cuda` or `auto` |
| `STEMSTUDIO_DATA_DIR` | worker | `/data` | Jobs, cache, temp files |
| `STEMSTUDIO_MODEL_DIR` | worker | `/models` | Model weights |
| `HF_HOME`, `TORCH_HOME` | worker | `/models/hf`, `/models/torch` | Model caches |
| `STEMSTUDIO_FAKE_DEVICE` | worker | `cpu` | Fake stack only: `cpu`, `gpu`, `gpu-oom` |
| `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS` | worker | `1` | Fake stack only |
| `STEMSTUDIO_TORCH_VARIANT` | build | `cu132` | GPU build argument |
| `STEMSTUDIO_MODEL_CACHE_DIR` | compose | — | Host folder for the model cache (`docker/compose.model-cache-bind.yml`) |
| `PYTORCH_CUDA_ALLOC_CONF` | worker | unset | Optional CUDA allocator tuning |
| `StemStudio__DataDir` | api | `/data` | SQLite database, uploads, stems |
| `StemStudio__WorkerBaseUrl` | api | `http://worker:8000` | Worker address |
| `StemStudio__WebOrigin` | api | `http://localhost:8080` | CORS origin for stem requests |
| `StemStudio__ApiBaseUrl` | web | `http://api:8081` | Server-side API calls |
| `StemStudio__PublicApiBaseUrl` | web | `http://localhost:8081` | Stem URLs used by the browser |
| `ASPNETCORE_HTTP_PORTS` | api, web | 8081, 8080 | Listening ports |
| `STEMSTUDIO_ONLINE_LOOKUPS` | worker | `on` | 005: `off` sends no request to MusicBrainz, the Cover Art Archive, Wikidata or Wikipedia; metadata jobs end as `disabled` |
| `STEMSTUDIO_CONTACT_URL` | worker | unset | 005: public page about your install, sent in the User-Agent. Must be `https://` with a host, no user info or `@` (so never an e-mail), at most 200 characters; an invalid value is replaced by the generic token and logged without the value |
| `STEMSTUDIO_WIKIPEDIA_LANGUAGES` | worker | `en` | 005: excerpt languages in order, comma list of 1-5 codes (`[a-z]{2,3}`); anything else stops the worker at start-up |
| `STEMSTUDIO_FAKE_ONLINE` | worker | `found` | 005, fake stack only: `found`, `not-found`, `failed`, `off` |
| `STEMSTUDIO_LIBRARY_DIR` | worker | unset (compose: `/library`) | 007: the API data volume mounted **read-only**; unset means renders fail with *"The library is not available to the worker"* |
| `STEMSTUDIO_RENDER_BLOCK_SECONDS` | worker | `10` | 007: render block length (> 0) |
| `StemStudio__MinFreeDiskBytes` | api | 1073741824 (1 GiB) | 007: exports are refused (409 *"Not enough disk space"*) while less is free on the data drive |

Online lookups (005) identify themselves with the User-Agent
`StemStudio/<worker version> ( <STEMSTUDIO_CONTACT_URL> )`, or
`StemStudio/<worker version> ( local-practice-tool; contact not configured )` when no contact
URL is set. Check the read-only library mount with `scripts/check-library-mount.{ps1,sh}`. In
the non-Docker path `scripts/dev.{ps1,sh}` points `STEMSTUDIO_LIBRARY_DIR` at the API data
folder; the worker only reads it, but the file system does not enforce that there.

Planned settings of later features <span class="planned">In development</span>:

| Variable | Feature | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_ALIGN_MODEL`, `STEMSTUDIO_ALIGN_AUTO` | 006 | `turbo`, `gpu` | Lyrics alignment model and when it runs |
| `STEMSTUDIO_TRANSCRIBER`, `STEMSTUDIO_TRANSCRIBE_THREADS` | 008 | `real`, `8` | Transcription engine and threads |
| `StemStudio__GeneratorBaseUrl`, `StemStudio__SingerBaseUrl` | 009, 010 | unset | Unset means "not installed" |
| `STEMSTUDIO_GPU_LEASE_FILE` | 009 | `/gpu-lease/gpu.lock` | Cross-container GPU lease |
| `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS` | 009 | `900` | Worker wait before CPU fallback |
| `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS` | 009 | `600` | Generator wait for free VRAM |
| `STEMSTUDIO_AGENT_BACKEND` | 011 | `ollama` | `ollama`, `claude`, `none` (`fake` in tests) |
| `STEMSTUDIO_OLLAMA_URL` | 011 | `http://host.docker.internal:11434` | Ollama on the host |
| `ANTHROPIC_API_KEY` | 011 | unset | Enables the optional Claude backend; see below |

Variables named `STEMSTUDIO_FAKE_*` are for the fake test stack only.

## Secrets: ANTHROPIC_API_KEY

StemStudio has exactly one secret, and only when the optional Claude backend of the agent is
used. The rules (constitution "Optional paid AI service exception", ADR-020):

- The key is read **only from the process environment** of the API. Export it in the shell
  session that starts compose; compose passes it through with a bare `- ANTHROPIC_API_KEY`
  entry.
- **Never** put it in a file: not in `.env`, compose files, `appsettings*.json`, scripts,
  the repository or command-line arguments. Prefer a secret store of your operating system
  or a key vault that injects it into the environment.
- The API only reports whether a key is present. It never stores, logs or returns the key; a
  sentinel-key test checks this.
- Without the key Claude cannot be selected and StemStudio works fully with Ollama. CI never
  calls Claude.
