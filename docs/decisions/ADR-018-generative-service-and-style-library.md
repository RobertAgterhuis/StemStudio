# ADR-018: Generative service, cross-container GPU lease and style library (feature 009)

- **Status**: Proposed (architect, 2026-11-15). Becomes Accepted at the start of feature 009,
  together with the constitution amendment in D9 (CTO/owner).
- **Context documents**: `specs/009-style-transfer/{spec.md, plan.md, research.md, data-model.md}`,
  `docs/research/roadmap-003-011.md` section 8, ADR-009 (model weights), ADR-011 (GPU vendor
  runtime), ADR-012 (GPU device policy), CTO brief 2026-11-15.

## Context

Feature 009 converts an imported song into another style with a generative model; feature 010
(ADR-019) generates songs from lyrics with the same model. The CTO chose **ACE-Step 1.5** (MIT
code, MIT weights) for both. ACE-Step pins `torch==2.10.0+cu128` and `transformers<4.58`; the
worker pins `torch==2.15.0` (cu132 in the GPU image), so the two cannot share a Python
environment. The model is impractical on a CPU (CTO decision: GPU-only). ADR-012 D3 deferred the
cross-service GPU scheduler "to the first feature that adds a second GPU service": that is this
feature. The spike (research.md S-01..S-08) measured the model on the reference RTX 5070 Ti.

## Decisions

### D1 A separate, optional "generator" service

A new Python 3.12 service `generator/` (package `stemstudio_generator`, FastAPI, own
`pyproject.toml` + `uv.lock`, own image `stemstudio/generator:local`) wraps the ACE-Step Python
API (`acestep.handler.AceStepHandler`, `acestep.inference.generate_music`) behind StemStudio's own
versioned contract `generator-api.openapi.yaml` (1.0.0 in 009). It does **not** expose ACE-Step's
own `acestep-api` server or Gradio UI. ACE-Step is installed as a pinned dependency (git commit
pin, MIT); no ACE-Step code is copied into the repository. The service has no host port (internal
network only), like the worker. It is started with an opt-in compose override
`docker-compose.generative.yml` that requires the GPU override
(`docker compose -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml up`).
The default stack is unchanged.

Rationale: own contract = contract tests, fakes in CI, our job model and GPU policy, no Gradio
surface; separate image = separate torch pin (ADR-012 rejected sharing).

### D2 GPU-only with a start-up probe, never failing start

The generator reuses the ADR-012 D2 probe logic (CUDA build, CUDA available, architecture in the
build, test kernel, NVML) as its own small `devices` module (the generator cannot import the
worker package; the probe is about 60 lines and is re-implemented against the same data-model
texts). Without a usable GPU the service still starts; `/health` reports
`ready: false, reason: "<probe reason>"`, and every generation request is refused with `409`
`GPU required`. There is no CPU fallback for generation (D9).

The API exposes `GET /api/generation/status`
(`notInstalled | unavailable(reason) | ready`, plus models present and their sizes). "Not
installed" = `StemStudio__GeneratorBaseUrl` not configured or `/health` unreachable for 2 s.
The UI disables generative controls with the reason and a help link.

### D3 One GPU step at a time across containers: shared file lease

The 003 in-process lease becomes a **cross-container lease**: an exclusive `fcntl.flock` on
`/gpu-lease/gpu.lock` in a shared named volume `gpu-lease`, mounted by the worker (GPU image)
and the generator. Measured on Docker Desktop/WSL2: a second container blocks until the first
releases (research.md S-07). Rules:

- The worker acquires the file lease inside `DeviceRunner` (after its thread lease) for every GPU
  step. It waits up to `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS` (default 900); on timeout the step runs
  on the CPU with the fallback reason "GPU busy with another StemStudio job" (003 FR-004 style).
- The generator acquires the lease per generation step (one ACE-Step call, one voice
  conversion). While waiting the job state is `waitingForGpu`. It never runs without the lease.
- The lock file only exists in GPU mode; without the volume (`STEMSTUDIO_GPU_LEASE_FILE` unset) the
  worker keeps the 003 behaviour, so the CPU stack and CI are unchanged.
- A crashed holder releases the lock automatically (kernel releases `flock` on process exit).
- No API-side `GpuScheduler`: the lease gives FR-011 without a new orchestrator; priorities
  between services are FIFO by arrival at the lock (documented limitation). The agent LLM (011)
  will decide whether it needs priorities.

### D4 Free-memory check, memory cap, OOM handling (generator)

Before each step, after acquiring the lease, the generator reads NVML free memory and compares it
with the step's `GpuNeed` (measured NVML peak minus idle + 1,024 MiB margin; research.md S-03:
standard cover profile 13.6 GB for a full song, low-memory profile 8.4 GB). It **chooses the
profile by free memory**: standard if it fits, else the low-memory profile (DiT/VAE/text encoder
offloaded to the CPU; measured about 4x slower) if that fits, else (another program such as a game
or Ollama holds memory) the job releases the lease, stays `waitingForGpu` and re-checks every 10 s
for up to 10 minutes, then fails "GPU busy: needs X GB, Y GB free". When it fits it sets
`torch.cuda.set_per_process_memory_fraction((free - 256 MiB) / total)` (ADR-012 D4: turns the
WSL2 shared-memory spill into an OOM error). On `torch.cuda.OutOfMemoryError` it empties the cache
and retries the step **once with the low-memory profile** (DiT/VAE CPU offload on, language model
unloaded, batch 1); a second OOM fails the job with "Not enough GPU memory for this request
(try a shorter range or 1 take)".

### D5 Model residency and downloads

Models are loaded on the first job and released (handlers dropped, `torch.cuda.empty_cache()`)
when the generator's queue is empty, so Demucs, games or Ollama get the memory back. Weights are
downloaded on first use into the shared `model-cache` volume (`/models/acestep`), pinned by
Hugging Face revision; SHA-256 values are recorded in `docs/THIRD-PARTY-LICENSES.md` (ADR-009 rule
1). The download is a job phase (`downloadingModel` with byte progress). The API offers
`GET /api/generation/status` sizes so the UI can warn before the first ~10 GB download.

### D6 Model profile

Default DiT `acestep-v15-turbo` (2B, 8 steps, shift 3.0) for covers and songs; language model
`acestep-5Hz-lm-1.7B` (MIT) loaded **only for song generation** (010; covers ignore it, so 009
never loads it), with ACE-Step's PyTorch (`pt`) LM backend: the vLLM backend was faster but kept
its VRAM after unload and refused a 3-minute song on 16 GB (010 research.md S-04). bf16 as
ACE-Step selects on CUDA; INT8 quantisation and `torch.compile` off by default (not needed on
16 GB; not measured). The XL (4B) model
and the 4B LM are not offered (VRAM, research.md). The profile is fixed in the generator's
config; the UI exposes only musical parameters.

### D7 Style library = data + deterministic prompt builder (in the API)

Styles live in the API (domain + SQLite), not in the generator. Curated presets are a versioned
JSON resource in `StemStudio.Domain` (`Styles/presets.v1.json`, embedded), seeded read-only at
start (upsert by stable preset id). User styles are rows. A pure `StylePromptBuilder` turns a style
plus request options into the ACE-Step caption (at most 512 characters) and parameters
(`audioCoverStrength` from the 5-step strength scale 0.85/0.7/0.55/0.4/0.25, `inferenceSteps`
8, `shift` 3.0, seed). The generator receives only the final caption and parameters, so the
generator contract stays model-level and the library logic is unit-tested in .NET.

### D8 Guardrail against artist imitation

A `StyleTextGuard` (domain) rejects style texts (name, description, tags, free caption) that
contain an imitation phrase (versioned list: "in the style of", "sounds like", "sound like",
"voice of", "imitate", "impersonat", "cover by", "like <Name>" patterns, Dutch equivalents "in de
stijl van", "klinkt als", "stem van") or an artist name from (a) the artist/title metadata of the
user's own tracks and (b) a curated deny-list resource (`Styles/artist-denylist.v1.txt`, a few
hundred widely known artists; case- and diacritics-insensitive, whole-word). A preset unit test
asserts no preset matches either list. Limitation (documented): a deny-list is never complete;
the guard is a safety net plus the visible rule, not a guarantee.

### D9 Constitution amendment: GPU-only generative features

Constitution 1.2.0 principle IV says "every feature MUST keep working on the CPU". Generative
features (009, 010) are GPU-only by CTO decision. Proposed amendment **1.3.0 (MINOR)** to the GPU
vendor runtime exception: "Optional generative features MAY require an NVIDIA GPU when CPU
inference is impractical, provided (a) they run in a separate opt-in service, (b) the default
stack does not include that service and stays fully open source, (c) the UI disables them with the
reason, and (d) every other feature keeps working on the CPU." The generator image is GPU-only and
therefore always contains the NVIDIA CUDA runtime wheels (system dependency, ADR-011); it is never
built in CI (CI builds and tests the generator package with fakes only). Executed by the CTO with
`/speckit-constitution` before the 009 wave (tasks.md T001).

### D10 Results in the 007 library; import as track

The API stores takes as FLAC (24-bit, 48 kHz as produced by ACE-Step, plus a 44.1 kHz copy for
import) under `api-data/generations/<generationId>/` and registers each take in the 007
export/asset library through an adapter port `IGeneratedAudioLibrary` (bound to the 007 service at
feature start; only "register file with kind `generated`, title, AI flag, provenance" is assumed).
File metadata: Vorbis comments `AI_GENERATED=true`, `GENERATOR=ACE-Step 1.5 (<revision>)`,
`STEMSTUDIO_GENERATION=<id>`, `COMMENT=AI-generated with StemStudio`. "Import as track" reuses the
upload import path (source key `upload:<sha256>`), so the worker contract is unchanged; the track
gets `Origin = generated` with the generation and take ids.

## Consequences

- Third image (generator, about 11-13 GB, research.md S-02), GPU-only and opt-in; about 10 GB of
  weights on first use (ACE-Step 1.5 bundle) plus 3 GB for 010's voice conversion (ADR-019).
- The worker GPU image gains the lease volume and one config value; its behaviour without
  contention is unchanged. With contention a separation step may wait for a generation (seconds to
  a few minutes) instead of running in parallel.
- CI never runs ACE-Step; correctness of the real path is validated on the reference machine
  (quickstart Q3-Q8) by the CTO, like 003.
- A new agent role `generative` owns `generator/**` (plan.md).

## Alternatives rejected

| Alternative | Why not |
|---|---|
| ACE-Step inside the worker | torch 2.10 vs 2.15 pin conflict (ADR-012); transformers pin conflict |
| Run ACE-Step's own `acestep-api` server unchanged and call it from the API | no contract of ours, no fakes, no GPU lease/NVML policy, task polling API with its own semantics; Gradio and extra endpoints exposed |
| YuE2 | 24 GB VRAM default, CC BY-NC weights (CTO decision) |
| CPU fallback for generation | impractically slow (CTO decision); constitution amended instead (D9) |
| API-side `GpuScheduler` brokering both services | needs the worker to ask the API before each step (new coupling and contract); the file lease achieves the same with one shared volume |
| Styles inside the generator | library logic would sit in a GPU-only service that is often not installed; the library must be browsable and editable on CPU-only installs |
| Free-text prompts only, no library | the owner asked for a library to apply |
| Artist presets ("in the style of X") | ethics rule (research 8.3); also legally sensitive |
