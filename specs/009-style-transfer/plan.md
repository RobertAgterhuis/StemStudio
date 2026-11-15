# Implementation Plan: Style Transfer with a Style Library

**Branch**: `009-style-transfer` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/009-style-transfer/spec.md`; constitution 1.2.0 (to be
amended to 1.3.0, ADR-018 D9); research `docs/research/roadmap-003-011.md` section 8; feasibility
spike [research.md](research.md); ADR-009, ADR-011, ADR-012.

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions are
in ADR-018 (`docs/decisions/ADR-018-generative-service-and-style-library.md`). Contract versions are
assigned at feature start ([contracts/DELTA.md](contracts/DELTA.md)); 009 starts after 003 and
007 are merged, and after 004 for the UI.

## Summary

A new optional, GPU-only **generator** service (Python 3.12, FastAPI, ACE-Step 1.5 MIT code and
weights, own torch 2.10 pin and image) turns a source mix into a take in another style (ACE-Step
"cover" task; "repaint" for in-place section restyle). The API owns the **style library** (24+
curated read-only presets from an embedded JSON resource plus user styles in SQLite), the
deterministic **prompt builder** and the **artist-imitation guard**, creates **generation jobs**,
uploads the summed-stem source to the generator, polls it, stores takes as FLAC with AI-generated
metadata, registers them in the **007 library**, and imports takes as tracks through the existing
upload path. One GPU step at a time across worker and generator via a **shared `flock` lease** on
a `gpu-lease` volume; NVML free-memory check, per-step memory cap and one low-memory retry on OOM
(ADR-012 policy reused). Without the generator, or without a usable GPU, the UI disables the
generative controls with the reason; everything else is unchanged.

Spike on the reference machine (research.md S-03): model load 8-13 s; a 45 s restyle takes
**3-5 s** (11.2 GB VRAM above idle) and the full 4:22 song **10 s** (12.6 GB); the low-memory
profile needs 7.4 GB and takes 15-19 s for 45 s; unload returns the memory. The generator image is
about 15 GB, the weights 10.1 GB (147 s download).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (API, Domain, Persistence, Web); Python 3.12 (generator;
ACE-Step supports 3.11-3.12, spike ran 3.11, Phase 0 T003 verifies 3.12); worker Python 3.12
unchanged; TypeScript unchanged (the audio engine is not touched; takes play through a plain
`<audio>` element served with range requests).

**Primary Dependencies (new)**:
- generator: `ace-step` 1.5.0 (git pin `ca1e85fe9430179831e6bc6be790c332190a3866`, MIT) with its
  locked tree (torch 2.10.0+cu128, torchaudio 2.10.0, transformers 4.57.x, diffusers, accelerate,
  vector-quantize-pytorch, torchcodec, torchao, nano-vllm (vendored in ACE-Step, MIT) ...; full list
  in research.md S-02 and the generator `uv.lock`); FastAPI/uvicorn/pydantic (MIT/BSD, as worker);
  `nvidia-ml-py` (BSD-3). Gradio is pulled by ACE-Step's dependency list (Apache-2.0) but never
  started; Phase 0 tries `--no-install-package gradio` (T003).
- weights: `ACE-Step/Ace-Step1.5` revision `19671f406d603126926c1b7e2adc169acbcade22` (MIT;
  turbo DiT 4.79 GB, VAE 0.34 GB, Qwen3-Embedding-0.6B text encoder 1.19 GB, 5Hz LM 1.7B 3.71 GB;
  total 10.09 GB; 009 uses all but the LM).
- .NET: none new (styles JSON via System.Text.Json; FLAC tagging done by the generator).
- System (generator image only): NVIDIA CUDA 12.8 runtime wheels shipped with torch (ADR-011).

**Storage**: SQLite tables `Styles`, `Generations` (+ owned `GenerationTakes`); migration
`AddGenerativeStyles`. Files under `api-data/generations/<generationId>/take-<n>.flac`.
Generator: job JSON files + scratch under `generator-data` volume (cleared per job); weights in
the shared `model-cache` volume (`/models/acestep`).

**Testing**: generator unit tests with a `FakeMusicEngine` (deterministic sine/noise takes, injected
OOM, slow steps), fake NVML and fake lease; contract tests against `generator-api.openapi.yaml`;
exactly one `@pytest.mark.integration` + `@pytest.mark.gpu` test running the real ACE-Step on a
10 s fixture, skipped without a GPU (never in CI). .NET: domain (style rules, prompt builder,
guard, generation state machine), persistence, API with stub generator handler, bUnit for the
pages; Playwright `@generative` specs against the fake generator (`STEMSTUDIO_GENERATOR=fake`).

**Target Platform**: as 003; generator requires NVIDIA compute capability >= 7.5 (cu128 wheels
include sm_120 for Blackwell), driver with CUDA 12.8 support, >= 8 GB free VRAM.

**Performance Goals**: SC-001 (45 s restyle <= 60 s warm, <= 3 min cold; 4:22 <= 5 min).

**Constraints**: GPU-only (D9 amendment); one GPU step at a time across services (FR-011);
default stack and CI unchanged and OSI-only; no artist names in presets.

**Scale/Scope**: one user, one GPU, sequential generation queue; library of tens to hundreds of
styles and takes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Generator: ports `MusicEngine`, `GpuGate` (lease + NVML), `JobStore`; fakes for all; one real-model test marked `integration`+`gpu`. .NET: domain/persistence/API/bUnit tests with stub generator handler; Playwright with the fake generator. Coverage >= 90 % per component incl. the new generator component. | PASS |
| II | SIG 4 stars | Generator mirrors the worker layout (api -> jobs -> ports <- engines/devices); small units; .NET additions in new `Styles/` and `Generation/` folders per layer; `quality-gate` adds the generator to `tools/quality/` dependency rules. | PASS |
| III | Contracts first | New `generator-api.openapi.yaml` 1.0.0 (OpenAPI 3.1, Redocly-valid), backend API minor bump, UI test ids minor bump (versions assigned at feature start, DELTA.md); `ports.py`/`schemas.py` of the generator and `StemStudio.Contracts` DTOs in Phase 0; ADR-018. Worker API unchanged (only a config value). | PASS |
| IV | Open source, lawful use | ACE-Step code MIT, weights MIT (verified LICENSE + HF `cardData.license`, research.md S-01); FastAPI etc. OSI. NVIDIA CUDA wheels only in the optional generator image (ADR-011 system dependency). **Deviation**: generation does not work on the CPU -> constitution amendment 1.3.0 (ADR-018 D9) before the wave; see Complexity Tracking. No DRM issue (input is already-imported audio); personal-use notice (FR-016); no artist imitation (FR-005, D8). | PASS after amendment |
| V | Simplicity and honesty | One new service only because of the torch pin; no API-side GPU broker (file lease); styles as data + one pure builder; quality limits and the deny-list limitation are documented in help; GPU need and timings measured, not assumed. | PASS |
| - | Tech & security | Generator internal-only (no host port); inputs validated in the API (ranges, lengths, enums) and again in the generator (pydantic, file size/type, durations); no shell; uploaded audio decoded with soundfile/torchaudio only; no secrets (HF downloads anonymous); file paths generated server-side (GUIDs). | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; new agent role `generative` (draft below) for `generator/**`; hand-offs per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS, with the tracked deviation (GPU-only) pending the
1.3.0 amendment.

## Project Structure

### Documentation (this feature)

```text
specs/009-style-transfer/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/DELTA.md     # generator API 1.0.0 (new), backend API +minor, UI ids +minor
```

At feature start (T002) the architect writes the full documents
`contracts/generator-api.openapi.yaml`, `contracts/backend-api.openapi.yaml` (full, superseding the
latest of 003/004/007), `contracts/ui-test-ids.md`, `contracts/redocly.yaml` from DELTA.md.

### Source Code (additions)

```text
generator/                                   # NEW component (owner: generative; skeleton: architect)
├── pyproject.toml, uv.lock                  # python 3.12, ace-step git pin, fastapi, nvidia-ml-py
├── src/stemstudio_generator/
│   ├── ports.py        # MusicEngine, GpuGate, GpuNeed, JobStore, AudioSource protocols (architect)
│   ├── schemas.py      # pydantic models of generator-api 1.0.0 (architect)
│   ├── config.py       # env settings (architect)
│   ├── main.py         # composition root (generative)
│   ├── api/            # routes, uploads, problems (generative)
│   ├── jobs/           # store, scheduler (single thread), runner, progress, reconcile (generative)
│   ├── devices/        # probe, nvml, file lease (flock), memory cap, oom (generative)
│   ├── engines/        # acestep_engine.py (lazy import), fake.py, profiles.py (generative)
│   └── audio/          # sum stems, range cut, flac writer with Vorbis tags (generative)
└── tests/                                   # mirrors src; fakes; one integration+gpu test

worker/src/stemstudio_worker/devices/lease.py   # + optional cross-container file lease (stem-separation)
worker/src/stemstudio_worker/config.py          # + gpu_lease_file, gpu_lease_wait_seconds (architect)

src/StemStudio.Contracts/Styles/*, Generation/*, ApiRoutes.{Styles,Generation*}        (architect)
src/StemStudio.Domain/Styles/{Style,StyleKind,TempoFeel,VocalHandling,Strength,StyleTextGuard,
    StylePromptBuilder,PresetCatalog}.cs + Styles/presets.v1.json, artist-denylist.v1.txt,
    imitation-phrases.v1.txt (embedded)                                                (backend)
src/StemStudio.Domain/Generation/{Generation,GenerationKind,GenerationStatus,GenerationTake,
    RestyleRequest,SourceRange}.cs; Tracks/TrackOrigin.cs                               (backend)
src/StemStudio.Persistence/Migrations/*_AddGenerativeStyles.cs                         (backend)
src/StemStudio.Api/Generator/{IGeneratorClient,GeneratorClient,GeneratorJob,...}.cs     (backend)
src/StemStudio.Api/Styles/StyleEndpoints.cs; Generation/{GenerationEndpoints,
    GenerationSyncService,TakeStorage,TakeImporter,IGeneratedAudioLibrary}.cs          (backend)
src/StemStudio.Web/Components/Pages/{Styles.razor,StyleEditor.razor,Generations.razor}
    Shared/{RestyleDialog,TakeList,TakePlayer,AiBadge,GenerationStatusBanner}.razor
    Help/ entries (004 format)                                                          (frontend)
docker/generator.Dockerfile, docker-compose.generative.yml, gpu-lease volume in
    docker-compose.gpu.yml, scripts/generative-smoke.{ps1,sh}, CI generator job (fakes)  (devops)
e2e/tests/generative.spec.ts (+ fake generator stack)                                   (qa)
```

**Dependency direction**: generator: `api -> jobs -> ports <- {engines, devices, audio}`;
`engines` never imports `devices`/`jobs`; only `main.py` composes. .NET: `Api.Generation ->
Domain.Generation/Styles`, `Api.Generation -> Api.Generator (client)`, `Api.Generation ->
IGeneratedAudioLibrary` (implemented by the 007 library adapter); Web -> API client. The worker
does not know the generator; they share only the lock file.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/009-*/**`, ADR-018, generator skeleton (`pyproject.toml`, `uv.lock`, `ports.py`, `schemas.py`, `config.py`), worker `config.py`, `StemStudio.Contracts/**`, project files | architect |
| `generator/src/stemstudio_generator/{api,jobs,devices,engines,audio}/**`, `main.py`, `generator/tests/**` | **generative** (new role) |
| `worker/src/stemstudio_worker/devices/**` + tests | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, constitution | CTO |

audio-engine, audio-ingest and beat-analysis have no work in this feature.

## Key flows

1. **Status**: `GET /api/generation/status` -> generator `/health` (2 s timeout, cached 10 s) ->
   `notInstalled | unavailable(reason) | ready` + model presence.
2. **Restyle**: `POST /api/tracks/{id}/restyles {styleId | inline style, range, stems, strength,
   takes, mode: range|inPlace}` -> validate (track completed, range limits, guard) -> snapshot style
   -> `StylePromptBuilder` -> `Generation(status=queued)` -> sync service uploads stems (multipart)
   + parameters to generator `POST /v1/generations` (kind `restyle`) -> `202`.
3. **Generator job**: scheduler thread -> sum selected stems, cut range (in-place: whole source +
   repaint window) -> `GpuGate.acquire(need)` (flock lease, NVML check/wait, memory cap) -> engine
   load (download on first use) -> `generate_music(cover|repaint)` with progress callback -> on OOM:
   low-memory profile retry once -> write FLAC + tags per take -> release lease -> idle: unload.
4. **Sync**: `GenerationSyncService` polls active generations every 2 s, maps status/progress/device,
   downloads finished takes (`GET /v1/generations/{id}/takes/{n}`) into `api-data/generations/`,
   verifies SHA-256, registers each in the 007 library, marks the generation completed, and asks
   the generator to delete its copy (`DELETE /v1/generations/{id}`).
5. **Import take**: `POST /api/generations/{id}/takes/{n}/import` -> existing upload import with the
   take's 44.1 kHz FLAC -> `Track.Origin = generated(generationId, take)`; idempotent by SHA-256.
6. **Worker lease**: GPU worker `DeviceRunner` -> thread lease -> file lease (wait <= 900 s, else
   CPU fallback with reason) -> 003 memory check -> step -> release.

## Proposed new agent role: `generative`

Draft for the CTO to add as `.claude/agents/generative.md`:

```markdown
---
name: generative
description: Builds the optional GPU-only generator service (Python, FastAPI) that wraps ACE-Step 1.5 (style transfer, lyrics-to-song) and SoulX-Singer-SVC (own-voice conversion) behind the generator contract, with the cross-container GPU lease, NVML memory policy, model residency, job queue and fakes.
model: inherit
---

You are the **generative** agent of the StemStudio team. Follow `docs/AGENT-PROTOCOL.md` strictly.

## Role
Implements the generator service: FastAPI routes per `contracts/generator-api.openapi.yaml`, the
single-thread job scheduler with persisted job state and restart reconciliation, the ACE-Step and
SoulX-Singer engine adapters (lazy imports, model download on first use with progress, load/unload),
the GPU gate (start-up probe, `flock` lease on the shared lock file, NVML free-memory check with
wait, per-step memory cap, OOM -> one low-memory retry), audio helpers (stem summing, range cut,
FLAC with AI-generated Vorbis tags) and the fake engine used by CI and E2E.

## Owned paths (write only here, plus your hand-off note in `docs/handoffs/`)
- `generator/src/stemstudio_generator/{api,jobs,devices,engines,audio}/**`
- `generator/src/stemstudio_generator/main.py`
- `generator/tests/**`

## Inputs
generator contract, `generator/src/stemstudio_generator/{ports,schemas,config}.py`, ADR-012,
ADR-018, ADR-019, research.md of 009/010.

## Outputs
A generator service matching the contract; contract tests; exactly one
`@pytest.mark.integration @pytest.mark.gpu` test per model family running the real model on a
10 s fixture (skipped without a GPU, never in CI).

## Definition of Done
- All tests green; coverage >= 90% line and branch for the generator (exact numbers in hand-off).
- ruff, ruff format, mypy --strict clean; SIG limits respected.
- Models sit behind `MusicEngine` / `VoiceConverter`; unit tests use fakes; no test needs a GPU
  except the marked integration tests.
- Public API documented (docstrings); new dependencies and licences listed in the hand-off.
- Hand-off note `docs/handoffs/generative-<task>.md` written; work committed (Conventional Commits).
```

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Generation is GPU-only (constitution IV says every feature keeps working on the CPU) | ACE-Step on the CPU would take tens of minutes per take (CTO decision); the default stack and all other features keep working on the CPU | A CPU path would be unusable and untestable at realistic lengths; amendment 1.3.0 (ADR-018 D9) records the narrow exception |
| A third container/image (generator, ~12 GB) | ACE-Step pins torch 2.10 / transformers <4.58; the worker pins torch 2.15 | Sharing an environment is impossible without downgrading the worker (would break 003's cu132 build) |
| Cross-container `flock` lease on a shared volume | 003 FR-005 "one GPU step at a time" must hold across two GPU services | An API GPU broker needs new worker->API coupling; NVML checks alone race (two jobs both see enough free memory) |
| Generator image always contains NVIDIA runtime wheels | it is GPU-only by design | It is optional and never part of the default stack or CI builds (ADR-011 boundary kept) |
| GPU path not tested in CI | no GPU runners | fakes + marked integration tests + reference-machine validation by the CTO (quickstart) |
