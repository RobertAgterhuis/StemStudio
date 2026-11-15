# StemStudio

Practise along with your favourite songs. StemStudio imports a song (YouTube link, Spotify track
link or a local MP3/WAV/FLAC file), splits it into instrument stems with
[Demucs](https://github.com/adefossez/demucs), and gives you a mixer with a beat-synced
metronome so you can mute the guitar and play it yourself.

> **Personal practice only.** You are responsible for complying with copyright law and with the
> terms of service of YouTube and Spotify. StemStudio never circumvents DRM: for Spotify links it
> only reads track metadata and fetches a matching public recording from YouTube.

## Features (MVP)

- **Import** from YouTube, Spotify (metadata → YouTube match) or a local file (up to 200 MiB,
  15 minutes), with progress reporting and a per-source cache.
- **Stem separation** into vocals, drums, bass, guitar, piano and other (`htdemucs_6s`), with a
  fallback to 4 stems (`htdemucs`).
- **Mixer**: per stem a volume fader, pan, mute, solo and a post-fader peak meter; master volume;
  play/pause/stop/seek; A-B loop. All stems play sample-locked.
- **Metronome** on the detected beat grid (librosa) with accent on beat 1, its own volume, and
  manual BPM / offset / beats-per-bar correction. Clicks are scheduled on the Web Audio clock.
- **Mixes**: save, load, edit, duplicate and delete named mixes (all channel, master, metronome
  and loop settings).

## Quick start (Docker)

Requirements: Docker with Compose v2. Nothing else; no accounts or keys.

```bash
docker compose up --build
```

Open <http://localhost:8080>. The first real separation downloads the Demucs model weights
(about 50 MB for `htdemucs_6s`) into the `model-cache` volume; later runs are offline for the model.

Useful variants:

```bash
STEMSTUDIO_SEPARATOR=fake docker compose up --build   # instant fake "stems" (demo / tests)
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build   # NVIDIA GPU (see below)
docker compose down        # stop; data stays in the named volumes
docker compose down -v     # stop and delete all tracks, stems, mixes and the model cache
```

Ports: web `8080`, API `8081`. The worker is internal to the compose network.

### NVIDIA GPU (optional)

On a machine with an NVIDIA GPU (tested: RTX 5070 Ti 16 GB, Windows 11 + Docker Desktop/WSL2,
driver 610.88) the GPU variant separates about 8x faster (reference song 4:22: standard + detail
separation 3.7 min instead of 31.5 min on the CPU).

```bash
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
./scripts/gpu-smoke.ps1      # or scripts/gpu-smoke.sh: checks CUDA, ONNX Runtime and /health
```

- Requirements: a current NVIDIA driver on the host; on Windows nothing has to be installed inside
  WSL. The GPU image is ~11.5 GB (CPU image ~3.4 GB).
- The import page shows the processing device; each track shows where it was processed.
- One GPU-heavy step runs at a time. Before each step the free GPU memory is checked (the largest
  detail model needs ~10.6 GB); if another program (a game, Ollama) holds too much, that step runs
  on the CPU and the track page says why. A GPU out-of-memory error retries the step on the CPU.
- The GPU image contains NVIDIA's free but proprietary CUDA runtime libraries (system dependency,
  constitution 1.2.0); the default CPU image is fully open source.

## Running without Docker

Requirements: .NET SDK 10.0.401+, [uv](https://docs.astral.sh/uv/) (installs Python 3.12),
Node.js 22, and ffmpeg/ffprobe on `PATH` (plus [Deno](https://deno.com) for yt-dlp's YouTube
support).

```powershell
./scripts/dev.ps1            # Windows: builds the audio engine, starts worker, API and web
./scripts/dev.ps1 -Fake      # same, with the fake separator
```

```bash
./scripts/dev.sh             # Linux/macOS equivalent (--fake for the fake separator)
```

Manual start, configuration variables and validation scenarios are in
[`specs/001-stemstudio-mvp/quickstart.md`](specs/001-stemstudio-mvp/quickstart.md).

## Architecture

```text
Browser ──Blazor Server (SignalR)──► StemStudio.Web (:8080)  Blazor + Radzen UI
   │                                     │ typed HttpClient
   │ fetch FLAC stems (CORS, Range)      ▼
   └──────────────────────────────► StemStudio.Api (:8081)   ASP.NET Core Minimal API
   Web Audio engine (TypeScript)        │  EF Core + SQLite, file storage, JobSyncService
                                        │  HTTP + polling (OpenAPI contract)
                                        ▼
                                  stemstudio_worker (:8000) Python 3.12 / FastAPI
                                  yt-dlp · spotDL · ffmpeg · Demucs · librosa
```

| Component | Path | Responsibility |
|---|---|---|
| Domain | `src/StemStudio.Domain` | Tracks, stems, beat grid, mixes, source keys, validation (no infrastructure) |
| Contracts | `src/StemStudio.Contracts` | DTOs shared by API and Web |
| Persistence | `src/StemStudio.Persistence` | EF Core SQLite, migrations, repositories |
| API | `src/StemStudio.Api` | HTTP endpoints, worker client, job sync, stem serving |
| Web | `src/StemStudio.Web` | Blazor Web App (Interactive Server) with Radzen |
| Audio engine | `src/audio-engine` | Web Audio playback, mixer, meters, loop, metronome (JS interop) |
| Worker | `worker/` | Ingest, separation, beat analysis, job queue |

Dependencies flow one way (Web → Contracts; Api → Persistence → Domain; Api → Contracts); this is
checked automatically by `tools/quality`. Contracts live in
[`specs/001-stemstudio-mvp/contracts/`](specs/001-stemstudio-mvp/contracts/) (OpenAPI 3.1 for the
API and the worker, plus the audio-engine interop contract). Design decisions are recorded in
[`docs/decisions/`](docs/decisions/).

## Development and quality gates

```powershell
./scripts/test-all.ps1       # everything CI runs: build, lint, tests + coverage, SIG gate, e2e lint
```

- Coverage gate: at least 90% line and branch per component (.NET via coverlet, Python via
  pytest-cov, TypeScript via Vitest).
- Maintainability (SIG-style limits): `uv run --project tools/quality python tools/quality/run_all.py`.
  The report is in [`docs/quality/SIG-REPORT.md`](docs/quality/SIG-REPORT.md).
- End-to-end: Playwright in [`e2e/`](e2e/) against the compose stack
  (see [`docs/testing/TEST-STRATEGY.md`](docs/testing/TEST-STRATEGY.md)).
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml); SonarQube settings in
  `sonar-project.properties` (the analysis runs only when a `SONAR_TOKEN` secret is configured).

The project was built with GitHub Spec Kit: the constitution, specification, plan and tasks are
in [`.specify/memory/constitution.md`](.specify/memory/constitution.md) and
[`specs/001-stemstudio-mvp/`](specs/001-stemstudio-mvp/).

## Known limitations

- **Stem granularity.** Current open-source models separate at most six stems (vocals, drums,
  bass, guitar, piano, other). Separating every instrument individually (for example two guitars,
  or keys versus synth) is not reliably possible; such parts end up together in one stem,
  usually "other". The quality of the guitar and piano stems varies between songs.
- **Processing time.** Separation runs on the CPU by default; a 4-minute song can take several
  minutes. Jobs are processed one at a time.
- **Beat detection** assumes a mostly constant tempo. Songs with tempo changes, rubato or a strong
  backbeat may need manual BPM/offset correction; downbeats can land on the wrong beat.
- **Spotify** links rely on spotDL, which uses unofficial Spotify web clients for metadata, and on
  finding a matching YouTube recording; a track without a match is rejected at import. Playlists,
  albums and podcasts are not supported.
- **YouTube** downloads depend on yt-dlp keeping up with YouTube changes; update the worker image
  when downloads start failing.
- **Browsers**: tested on Chromium and Firefox; Safari is best effort.
- **Single user**, local only: no accounts, no authentication. Do not expose the ports to the
  internet.
- Exporting a mix to an audio file is not part of the MVP (first backlog item).
- **Detail separation** (drums -> kick/snare/toms/hihat/cymbals, vocals -> lead/backing,
  guitar/piano/other -> instrument families) is experimental: quality varies per song, stems
  leak into each other, organ is part of "keys", and on the CPU it takes about 20 minutes for a
  4-minute song (first use downloads ~1.9 GB of models; Docker needs >= 20 GB memory).

## Licences

> **Model weights: local, personal, non-commercial use only.** The pretrained separation models
> (Demucs `htdemucs_6s`/`htdemucs` and the detail models MDX23C DrumSep, UVR MDX-Net KARA 2 and
> MVSep Mega 53-stem) have non-commercial, unclear or no stated licences. They are downloaded on
> first use and are never part of this repository. Commercial use and redistributing images or
> volumes that contain them are not covered. Detail separation credits Ultimate Vocal Remover
> (UVR) and its developers, and ZFTurbo's Music-Source-Separation-Training.

StemStudio's code and libraries are open source only; see
[`docs/THIRD-PARTY-LICENSES.md`](docs/THIRD-PARTY-LICENSES.md), including the GPL/LGPL components
pulled in by spotDL, Demucs and librosa.
