# Quickstart / validation: Style Transfer (feature 009)

Validation steps for the CTO after the integration wave. Q1-Q2 run anywhere (CI covers them);
Q3-Q9 need the reference machine (RTX 5070 Ti 16 GB, Docker Desktop WSL2, driver 610.88).

## Q1 Default stack unchanged (CPU, no generator)

```powershell
docker compose up -d --build --wait
```

- `GET http://localhost:8081/api/generation/status` -> `{"state":"notInstalled", ...}`.
- Track page: `track-restyle` is disabled; `generation-status-reason` says the generative service
  is not installed; the style library (`/styles`) lists >= 24 presets and user styles can be
  created. Full test suite green (SC-002).

## Q2 Fake generator stack (what CI and Playwright use)

```powershell
$env:STEMSTUDIO_SEPARATOR='fake'; $env:STEMSTUDIO_GENERATOR='fake'
docker compose -f docker-compose.yml -f docker-compose.generative.yml -f docker-compose.generative-fake.yml up -d --build --wait
npx playwright test --grep @generative
```

Repeat with `STEMSTUDIO_FAKE_GENERATOR_MODE=no-gpu` (controls disabled, `409 gpuRequired`),
`oom` (take still produced after the low-memory retry), `oom-twice` (failed, readable reason),
`busy` (state `waitingForGpu` then `failed gpuBusy` with a 5 s wait limit).

## Q3 GPU generative stack builds and reports ready

```powershell
docker compose -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml up -d --build --wait
docker compose exec generator python -c "import torch; print(torch.__version__, torch.cuda.get_arch_list())"
```

- torch `2.10.0+cu128`, `sm_120` in the arch list; `/api/generation/status` -> `ready`, models
  `present: false` before the first job, `downloadBytesNeeded` about 6.3 GB for 009 (turbo DiT,
  VAE, text encoder) or 10.1 GB for the full bundle.
- Record the generator image size (`docker image ls stemstudio/generator:local`).

## Q4 First restyle (downloads weights) and SC-001

Import "Hero of the Day" (or reuse the track). Restyle range 60-105 s with "Jazz trio", strength
Balanced, 1 take. Record: download time, cold job time (load + generate), then a second restyle
with "Synthwave" (warm). Sample VRAM during both:

```powershell
nvidia-smi --query-gpu=memory.used --format=csv,noheader -lms 500 > vram.log
```

Targets: warm <= 60 s, cold <= 3 min (excluding download). Then the whole song (4:22) <= 5 min.

## Q5 Labels and library

- The take is in the 007 library with the AI-generated badge; `take-details-*` shows style,
  strength, seed, source, range, model, device.
- `metaflac --list --block-type=VORBIS_COMMENT take-1.flac` (or `ffprobe`) shows
  `AI_GENERATED=true` and `GENERATOR=ACE-Step 1.5 (...)` (SC-005).

## Q6 Import as a track

Import the take; it is separated (GPU worker) and opens in the mixer; `track-origin-badge` and
`track-origin-source-link` are present; importing again returns the same track.

## Q7 GPU sharing

Start a detail separation of a song and, while it runs, a restyle: the restyle shows
"Waiting for the GPU" until the separation step releases the lease; `nvidia-smi` never shows both
models resident. Reverse order: a separation started during a restyle waits (or, after the wait
limit, falls back to the CPU with "GPU busy with another StemStudio job").

## Q8 Free-memory wait and OOM

Occupy about 10 GB of VRAM with another process (for example `ollama run` a large model, or
`docker run --rm --gpus all <torch image> python -c "import torch,time; x=torch.empty(10*2**30, dtype=torch.uint8, device='cuda'); time.sleep(900)"`),
start a restyle: state `waitingForGpu`; free the memory: the job starts. Keep it occupied past the
wait limit (set `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS=60` for the test): `failed` with
"GPU busy: needs X GB, Y GB free".

## Q9 Listening check (SC-004)

Owner: 5 presets x 2 songs (one rock, one acoustic); score "recognisably the source" and
"recognisably the style" per take; >= 7 of 10 both yes. Record in `docs/STATUS.md`.
