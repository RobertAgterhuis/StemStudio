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

## SoundTouch module (012)

The practice tempo needs `soundtouch.wasm` (SoundTouch 2.4.1, LGPL-2.1, plus StemStudio's ABI
shim). The **web image builds it itself**: an offline (`--network=none`) emsdk 6.0.13 stage,
pinned by digest in `third_party/soundtouch/soundtouch.lock`, verifies the release tarball's
SHA-256 and writes `/app/wwwroot/js/soundtouch/soundtouch.wasm` (served as
`application/wasm`) together with the LGPL folder `/third-party/soundtouch/` (`index.html`,
`COPYING.TXT`, `NOTICE.md`, `README.md`, the lock, the source tarball, the shim, the header,
both build scripts and `SHA256SUMS`).

Outside Docker (development, `scripts/dev.*`, unit tests) build it once:

```
scripts/build-soundtouch.sh                        # Docker, pinned emsdk image, offline compile
pwsh scripts/build-soundtouch.ps1                  # the same on Windows
scripts/build-soundtouch.sh --local                # an activated local emsdk 6.0.13 instead of Docker
scripts/build-soundtouch.sh --verify-reproducible  # build twice, fail unless the SHA-256 match
```

The output (`src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm`, about 30 KB, no
imports, 13 exports) is git-ignored. Without it the mixer still works; speed and transpose
then show *unavailable*. Two builds from the same lock are bit-identical (checked in CI).

**Replacing the module (LGPL-2.1 section 6):** rebuild SoundTouch (modified or not) with the
shim as described in `third_party/soundtouch/README.md` and mount the file over
`/app/wwwroot/js/soundtouch/soundtouch.wasm` in the web container. The processor checks
`st_abi_version() == 1`. `scripts/check-soundtouch-served.{sh,ps1} [BASE_URL]` verifies that a
running installation serves the module and the complete source package.

No Content-Security-Policy is set today; a CSP added later must allow `'wasm-unsafe-eval'`
for the worklet.

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

## Generative services

Features 009 and 010 add opt-in overrides; the default stack does not change:

```
docker compose -f docker-compose.yml -f docker-compose.gpu.yml \
  -f docker-compose.generative.yml up --build
```

This adds the `generator` (about 15 GB image) and `singer` (7 to 8 GB) services without host
ports, plus the volumes `generator-data`, `singer-data` and the shared `gpu-lease` volume.
`docker-compose.generative-fake.yml` runs a fake generator without torch or GPU for tests.
The weights are expected in the `model-cache` volume (`/models/acestep`, `/models/soulx`, each
with `REVISION` and `SHA256SUMS`); `scripts/generative-smoke.{sh,ps1}` checks the images and
the weights. Measured on the reference machine (RTX 5070 Ti 16 GB, weights present): a 45 s
song 50.6 s cold (VRAM +10.5 GB), a 30 s restyle 15.7 s (+4.8 GB), a 10 s voice conversion
25.5 s (+3.4 GB), all on the GPU without CPU fallback.

## Backups folder (013)

Backups are written by the API to a host folder that compose bind-mounts at `/backups` (only
into the `api` service). Create it **before** `docker compose up`, otherwise Docker creates it
owned by root:

```
mkdir -p backups
sudo chown 1654 backups          # Linux only: the API runs as uid 1654 (non-root)
docker compose up -d
```

Set `STEMSTUDIO_BACKUP_HOST_DIR` to use another folder, for example an external drive. On
Docker Desktop for Windows and macOS the bind mount is writable without a `chown`. The API never
changes host permissions; when the folder is missing or read-only the Backups tab says so with
the fix. Check a folder with `scripts/check-backup-folder.{sh,ps1}`. Archives are plain ZIP
files (`stemstudio-backup-<UTC date>-<time>.zip`) that can be copied elsewhere and back; they
are **not encrypted**.

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
| `STEMSTUDIO_ALIGN_MODEL` | worker | `turbo` | 006: Whisper model for lyrics alignment, `turbo` (large-v3-turbo, 1.6 GB, downloaded into `/models/whisper` at the first alignment) or `small` |
| `STEMSTUDIO_ALIGN_AUTO` | worker | `gpu` | 006: when plain lyrics are aligned without a click: `gpu` (only on a GPU worker), `always`, `never` |
| `STEMSTUDIO_FAKE_LYRICS` | worker | `word` | 006, fake stack only: `word`, `line`, `plain`, `instrumental`, `not-found`, `failed` |
| `STEMSTUDIO_TRANSCRIBER` | worker | `real` (fake with the fake separator) | 008: transcription engine |
| `STEMSTUDIO_TRANSCRIBE_THREADS` | worker | `8` | 008: intra-op threads of the transcribe lane (1-64) |
| `StemStudio__ScoreEstimates__*` | api | see below | 008: seconds per audio second of the estimate shown in the score dialog |
| `STEMSTUDIO_BACKUP_HOST_DIR` | compose | `./backups` | 013: host folder bind-mounted at `/backups`, in the API only; must exist and be writable by uid 1654 on Linux |
| `StemStudio__BackupDir` | api | compose `/backups` | 013: where archives are written and read |
| `StemStudio__BackupHostDirHint` | api | compose `${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}` | 013: host path shown to the user (display only) |
| `StemStudio__LibraryCare__MinFreeMarginBytes` | api | 67108864 (64 MiB) | 013: free space above the estimate before a backup starts |
| `StemStudio__LibraryCare__StopBelowFreeBytes` | api | 268435456 (256 MiB) | 013: a running backup or restore stops below this free space |
| `StemStudio__LibraryCare__StorageCacheSeconds` | api | `30` | 013: cache of the storage overview |
| `StemStudio__LibraryCare__CleanupPlanMinutes` | api | `10` | 013: validity of a clean-up preview |
| `StemStudio__LibraryCare__ThrottleBytesPerSecond` | api | `0` (off) | 013, **test stacks only** (`docker-compose.e2e.yml`): rate-limits archive streams so E2E can observe a running job |
| `StemStudio:LibraryBackfillBatchSize` (`StemStudio__LibraryBackfillBatchSize`) | api | `500` | 022: items filed per transaction by the background filing of existing items; it runs 2 s after start and again after a restore |
| `StemStudio__LyricAssist__MaxInputTokensPerAssist`, `…__MaxOutputTokensPerAssist` | api | `20000`, `4000` | 023: Claude token caps per assist (a positive integer; anything else keeps the default); usage is shown in the pane |
| `StemStudio__LyricAssist__BlockLines` | api | `8` | 023: lyric lines per model block (1-40); the event stream also reads `StemStudio__LyricAssist__StreamIdleSeconds` (`1800`) |
| `StemStudio__LyricAssist__BlockTimeoutSeconds` | api | `60` local, `90` Claude | 023: per block; a block that times out is skipped with a note |
| `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` | api | unset | 023, **test stacks only** (backend `fake`): a file or a folder of scripted model answers (`<title-slug>-<kind>.json`, else `<kind>.json`); `docker-compose.e2e.yml` mounts `e2e/lyric-scripts/` read-only at `/lyric-scripts` and sets it |

Online lookups (005) identify themselves with the User-Agent
`StemStudio/<worker version> ( <STEMSTUDIO_CONTACT_URL> )`, or
`StemStudio/<worker version> ( local-practice-tool; contact not configured )` when no contact
URL is set. Check the read-only library mount with `scripts/check-library-mount.{ps1,sh}`. In
the non-Docker path `scripts/dev.{ps1,sh}` points `STEMSTUDIO_LIBRARY_DIR` at the API data
folder; the worker only reads it, but the file system does not enforce that there.

The score estimates (008) are `duration × factor + 5 s` (+ 60 s while the piano model is not
downloaded). The defaults were set from the reference-machine measurements of the integration
run (RTX 5070 Ti, 8 CPU threads): `BasicPitchCpu` 0.05, `PianoCrnnCpu` 0.45, `PianoCrnnGpu` 0.2,
`DrumOnsets` 0.02 s/s. On a slower CPU raise `StemStudio__ScoreEstimates__PianoCrnnCpu`. The
worker image installs the Leipzig music font (OFL-1.1) for Verovio; check it with
`scripts/check-worker-fonts.{sh,ps1}`.

Settings of the generative services (009, 010):

| Variable | Feature | Default | Meaning |
|---|---|---|---|
| `StemStudio__GeneratorBaseUrl`, `StemStudio__SingerBaseUrl` | 009, 010 | unset | Unset means "not installed" |
| `STEMSTUDIO_GPU_LEASE_FILE` | 009 | `/gpu-lease/gpu.lock` | Cross-container GPU lease |
| `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS` | 009 | `900` | Worker wait before CPU fallback |
| `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS` | 009 | `600` | Generator wait for free VRAM |

Settings of the AI composer agent (011, API only; compose maps the shell variables to the
`StemStudio__Agent__*` keys of the API):

| Variable | Feature | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_AGENT_BACKEND` (`StemStudio__Agent__Backend`) | 011 | `ollama` | Initial backend `ollama`, `claude` or `none` while no settings row exists; the Settings page changes it later. `fake` (tests only) always wins over the stored row |
| `STEMSTUDIO_OLLAMA_URL` (`StemStudio__Agent__Ollama__BaseUrl`) | 011 | `http://host.docker.internal:11434` (compose), `http://localhost:11434/` (without Docker) | Ollama on the host; compose adds `host-gateway` for Linux engines |
| `ANTHROPIC_API_KEY` | 011 | unset | Claude key from the environment; wins over a key stored in Settings; see below |
| `StemStudio__DataProtection__KeyRingDir` | 011 claude-key | compose `/keys`; Windows `%LOCALAPPDATA%\StemStudio\keys`; Linux/macOS `$XDG_DATA_HOME/stemstudio/keys` or `~/.local/share/stemstudio/keys` | Key ring that encrypts the stored Claude key; see "Claude key storage" |
| `StemStudio__Agent__KeyEntry__InternalCaller` | 011 claude-key | compose `web`; empty without Docker | Host name of the Web app, whose addresses may store or delete the key besides loopback |
| `StemStudio__Agent__LoopbackBaseUrl` | 011 | `http://127.0.0.1:{HTTP_PORTS}` | Where the agent calls the API's own endpoints |
| `STEMSTUDIO_AGENT_FAKE_SCRIPT` | 011 | empty | E2E only, with backend `fake`: scripted model turns from `e2e/agent-scripts/` (mounted read-only at `/scripts`); Git Bash users prefix `MSYS_NO_PATHCONV=1` |

### Ollama for the agent (011, optional)

Ollama runs on the host, not in compose; StemStudio does not install, start or update it and
never pulls models. Reference machine: Ollama 0.40.16 on Windows 11, RTX 5070 Ti.

```
ollama pull qwen3.5:9b      # default model, ~6.6 GB (Q4_K_M), tool calling
ollama pull qwen3.5:4b      # fallback while a GPU job holds the GPU, ~3.3 GB
curl http://localhost:11434/api/tags
```

The API checks `GET /api/version`, `GET /api/tags` and the model's `tools` capability
(`POST /api/show`); a model that is not pulled or cannot call tools is refused in Settings
with `422` and the pull command. Turns use `num_ctx` 16,384; before a GPU-heavy job the API
asks Ollama to unload the model (`keep_alive: 0`, chapter 4) and agent turns use the fallback
model on the CPU while the job runs. Without Ollama the app works and the agent panel shows
*"Local AI not reachable"* with the fix.

### Lyric assistant (023)

The lyric assistant needs no new container and no download. Its dictionaries are **vendored in the
API image** under `/app/LyricAssist/Dictionaries` (7.7 MB: OpenTaal nl 2.20.23, SCOWL en_US and
en_GB 2026.02.25, CMUdict; exact versions, licences and SHA-256 in `PROVENANCE.md` and
`manifest.json` next to them) and are read from disk, never fetched at run time. CI fails when
the dictionaries in the image exceed 15 MB (`scripts/check-api-image-size.{sh,ps1}`). The model
layer uses the agent backend of Settings (Ollama on the host, `qwen3.5:9b`; Claude only when
selected and after the notice in Settings or the pane); nothing else leaves the machine. While a
heavy GPU job holds the GPU the lyric model is **unavailable** (no CPU fallback model for lyrics),
the spelling check, syllables and rhyme finder keep working. In the fake stack, the
`api` service reads `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` (a folder mounted read-only; the worker
has no access to it); the variable only has an effect with `STEMSTUDIO_AGENT_BACKEND=fake`.

Variables named `STEMSTUDIO_FAKE_*` are for the fake test stack only.

## Secrets: the Claude key

StemStudio has exactly one secret, and only when the optional Claude backend of the agent is
used. It has two sources (constitution 1.4.0, ADR-020, ADR-035):

- **Settings** (Composer agent -> Claude API key): the user pastes the key on the computer that
  runs StemStudio (`http://localhost:8080`); the API stores it encrypted (see "Claude key
  storage").
- **`ANTHROPIC_API_KEY`** in the process environment of the API. Export it in the shell session
  that starts compose; compose passes it through with a bare `- ANTHROPIC_API_KEY` entry. It
  **wins** over a stored key (the stored key is kept and shown as "not used").
- **Never** put the key in a file: not in `.env`, compose files, `appsettings*.json`, scripts,
  the repository or command-line arguments.
- Both sources pass the same format check (prefix `sk-ant-`, 40-256 letters, digits, `-`, `_`;
  never an Admin API key); a malformed environment value is not used and the status says why.
- The API reports only the source, the state, the last four characters and the save time. It
  never logs or returns the key; sentinel-key tests search every response, log line at Trace,
  event and database file for it.
- Without a usable key Claude cannot be selected and StemStudio works fully with Ollama. CI never
  calls Claude, and Anthropic is contacted only by a Claude run or an explicit **Test key**.

### Claude key storage (011 follow-up claude-key)

- The key is encrypted with ASP.NET Core Data Protection (purpose
  `StemStudio.Agent.ClaudeApiKey.v1`, application name `StemStudio.Api`); table `AgentSecrets`
  holds only the ciphertext, the last four characters and the save time.
- **Docker**: the key ring is the volume `stemstudio_api-keys` at `/keys`, mounted only into
  `api`, directory `0700` for the app user. **Never copy it together with `api-data` or a
  backup**: whoever has both can decrypt the key. `docker compose down -v`,
  `docker volume rm stemstudio_api-keys` or a restore on another computer makes the stored key
  unreadable by design: Settings shows *"cannot be read on this installation"* and the user enters
  the key again. `scripts/check-key-volume.{sh,ps1}` checks the compose set-up (volume only in
  `api`, `/keys` `0700`, the variables, `ANTHROPIC_API_KEY` a bare pass-through).
- **Without Docker**: a per-user directory, `%LOCALAPPDATA%\StemStudio\keys` on Windows (the
  ring is protected with DPAPI for the Windows user), `$XDG_DATA_HOME/stemstudio/keys` or
  `~/.local/share/stemstudio/keys` on Linux/macOS (directory `0700`, not encrypted at rest).
  `scripts/dev.*` print it at start; override it with `StemStudio__DataProtection__KeyRingDir`.
- The API refuses a key ring inside the library, the backups, the app folder or a git work tree
  (paths are normalised; case-insensitive except on Linux) and then disables key storage with
  `503 key-store-unavailable`; the API still starts and `ANTHROPIC_API_KEY` still works.
- Moving the library to another computer without the key ring makes the stored key unreadable
  by design; nothing else breaks.
