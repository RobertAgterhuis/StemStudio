# Implementation Plan: GPU Acceleration

**Branch**: `003-gpu-acceleration` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/003-gpu-acceleration/spec.md`; constitution 1.2.0 (GPU
vendor runtime exception, ADR-011); research `docs/research/roadmap-003-011.md` section 3 (there
called "004").

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions
are in ADR-012 (`docs/decisions/ADR-012-gpu-device-policy.md`); measurements in
[research.md](research.md).

## Summary

An opt-in GPU worker image (torch 2.15.0+cu132, onnxruntime-gpu 1.32.0) runs Demucs and the three
detail models on an NVIDIA GPU; the default image, the default start command and CI stay CPU-only
and unchanged. A start-up probe decides the worker's device once (CPU with a reason when no
usable GPU). A `DeviceRunner` port wraps every model step: one GPU step at a time (lease), an
NVML free-memory check against the step's measured need (CPU when it does not fit), a per-step
torch memory fraction that turns the WSL2 shared-memory spill into a real out-of-memory error,
and one CPU retry on GPU out-of-memory. Each standard and detail job records where it ran
(`JobDevice`); the backend stores it on the track and the detail phase, `GET /api/device`
reports the worker device, and the UI shows "GPU (<name>)" or "CPU" plus a fallback notice.

Measured on the reference machine (research.md G-02): the reference song (4:22) needs
**3.7 min** for standard + detail separation on the GPU (cold models; 2.2 min warm) versus
31.5 min on the CPU (**8.5x**, SC-001 >= 5x met), with identical per-parent outcomes (SC-003);
peak VRAM 13.2 GB NVML-used during Mega 53-stem.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0, engine untouched).

**Primary Dependencies (new)**: worker `nvidia-ml-py` 13.615.71 (BSD-3-Clause, pure Python NVML
binding; locked, both images). GPU image only (not in `uv.lock`, installed by build arguments):
`torch==2.15.0+cu132` (BSD-3) with its NVIDIA CUDA 13.2 runtime wheels (cuBLAS, cuDNN 9.26,
cuFFT, cuSPARSE(Lt), cuSOLVER, NCCL, NVSHMEM, nvJitLink, NVRTC, CUPTI, cuFile, NVTX, CCCL, plus
cuRAND 10.4.2.66 for onnxruntime; NVIDIA licence, system dependency per ADR-011), `triton` 3.9
(MIT), `onnxruntime-gpu==1.32.0` (MIT). No new .NET or npm
packages.

**Storage**: SQLite `Tracks.Device` JSON column (migration `AddJobDevice`); `DetailPhase.Device`
inside the existing `DetailPhase` JSON. Worker job JSON files gain the optional `device` field.

**Testing**: as 002. The device runner, probe and OOM fallback are tested with fake probes,
fake NVML readers and fake model steps (no GPU in CI). No new `@pytest.mark.integration` test:
the existing Demucs and detail integration tests run on whatever `STEMSTUDIO_DEVICE` selects;
the GPU path is validated on the reference machine (quickstart G3-G9, CTO). Playwright `@gpu`
specs run against the fake stack with `STEMSTUDIO_FAKE_DEVICE=gpu|gpu-oom`.

**Target Platform**: unchanged; GPU option for NVIDIA compute capability >= 7.5 with a CUDA 13.2
capable driver, on Docker Desktop (WSL2) or Linux with the NVIDIA Container Toolkit.

**Performance Goals**: SC-001 standard + detail on the GPU >= 5x faster than the CPU baseline
(31.5 min for 4:22 -> <= 6.3 min); measured 3.7 min cold.

**Constraints**: CPU image and CI free of NVIDIA runtime wheels (FR-008); one GPU step at a time
(FR-005); Mega 53-stem needs 10.6 GB of free VRAM (`GpuNeed`, research.md G-04), so on GPUs or
desktops with less free memory that step runs on the CPU.

**Scale/Scope**: one GPU, one worker; image variant + device policy + device record + display.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Device runner, probe, lease, memory check, OOM retry and the `merge_device` fold are unit-tested with fakes (no GPU); backend device mapping/persistence/`/api/device` with stub handlers; bUnit for the device display; Playwright `@gpu` with the fake device. Torch/NVML/onnxruntime calls sit behind small adapters (like `demucs_runtime.py`) tested with monkeypatched modules. No new real-model test. | PASS |
| II | SIG 4 stars | New worker package `devices/` (probe, nvml reader, lease, runner) of small units; backend changes in existing `Sync/`, `Detail/`, `Worker/` folders plus one endpoint; one small Razor component. | PASS |
| III | Contracts first | Worker API 1.2.0, backend API 1.3.0 (both additive, Redocly-valid OpenAPI 3.1), UI test ids 1.2.0, engine 2.0.0 unchanged; `schemas.py`, `ports.py` (`DeviceRunner`, `GpuNeed`, `DeviceUse`, `merge_device`), `config.py`, `StemStudio.Contracts` follow in Phase 0. ADR-012 records the design. | PASS |
| IV | Open source, lawful use | New locked library `nvidia-ml-py` is BSD-3 (OSI). The NVIDIA CUDA runtime wheels are allowed only in the optional GPU image as a system dependency (**constitution 1.2.0 GPU vendor runtime exception, ADR-011**): not in `uv.lock`, not in the CPU image, not in CI (CI guard, quickstart G2); `docs/THIRD-PARTY-LICENSES.md` lists them under "System dependencies (GPU variant only)". Every feature keeps working on the CPU (FR-003, FR-004). | PASS |
| V | Simplicity and honesty | One Dockerfile with build arguments, one compose override (already existed, fixed); policy inside the existing worker (no GPU broker service, YAGNI); no fp16/chunk tricks. Fallbacks are shown to the user with their reason; speed and VRAM measured, not assumed; the WSL2 spill behaviour is documented. | PASS |
| - | Tech & security | Stack unchanged; no secrets; no user input reaches device code; subprocesses unchanged (NVML via the Python binding, not `nvidia-smi`). | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-off per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS; one tracked refinement (Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/003-gpu-acceleration/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── worker-api.openapi.yaml    # 1.2.0 (full document; supersedes 002's 1.1.0)
    ├── backend-api.openapi.yaml   # 1.3.0 (full document; supersedes 002's 1.2.0)
    ├── ui-test-ids.md             # 1.2.0
    └── redocly.yaml
```

The audio-engine contract stays `specs/002-detail-separation/contracts/audio-engine.md` (2.0.0,
unchanged). Contract tests switch to the 003 documents in Phase 0 (worker tests, `StemStudio.Api.Tests`
links); CI `CONTRACTS_DIR` and `scripts/test-all.*` switch in the wave (devops).

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py      # + GpuNeed, DeviceUse, DeviceRunner protocol, merge_device(), SeparationResult.device (architect)
├── schemas.py    # + ProcessingDeviceKind, DeviceInfo, JobDevice, Job.device, DetailJob.device, Health.deviceInfo (architect)
├── config.py     # + fake_device (STEMSTUDIO_FAKE_DEVICE) (architect)
├── devices/      # stem-separation
│   ├── __init__.py   # create_device_runner(settings) factory (real probe or fake device)
│   ├── probe.py      # start-up probe -> DeviceInfo (torch + NVML adapters injected)
│   ├── nvml.py       # NVML free/total reader (pynvml), lazy
│   ├── lease.py      # process-wide GPU lease
│   ├── runner.py     # GpuRunner / CpuRunner: memory check, fraction, OOM -> CPU retry
│   ├── oom.py        # is_gpu_out_of_memory(exc)
│   └── fake.py       # FakeDeviceRunner for STEMSTUDIO_FAKE_DEVICE
├── separation/   # DemucsSeparator runs through DeviceRunner; GpuNeed constant
├── detail/       # loaders per device, onnxruntime.preload_dlls() + CUDA EP for KARA 2, GpuNeed per model, idle release
├── jobs/         # Job.device / DetailJob.device set and folded; idle hook releases GPU models
├── api/routes.py # /health deviceInfo
└── main.py       # composition: probe once, share the runner

src/StemStudio.Contracts/Tracks/{JobDeviceDto,ProcessingDeviceKindDto}.cs, TrackDto.Device,
    DetailPhaseDto.Device; Health/ProcessingDeviceDto.cs; ApiRoutes.Device            (architect)
src/StemStudio.Domain/Tracks/JobDevice.cs (+ Track.Device, DetailPhase.Device)        (backend)
src/StemStudio.Persistence/Migrations/*_AddJobDevice.cs                              (backend)
src/StemStudio.Api/Worker/{WorkerJob,WorkerDetailJob,WorkerHealth}.cs, IWorkerClient.GetHealthAsync
src/StemStudio.Api/{Sync/TrackJobApplier,Detail/DetailJobApplier}.cs (device mapping)
src/StemStudio.Api/Device/DeviceEndpoints.cs (GET /api/device)                        (backend)
src/StemStudio.Web/Components/Shared/DeviceBadge.razor; Import + TrackDetail + DetailSection (frontend)
docker/worker.Dockerfile (ORT_VARIANT), docker-compose.gpu.yml (cu132, auto), scripts/gpu-smoke.*,
    .github/workflows/ci.yml (cpu-image-guard, CONTRACTS_DIR)                          (devops)
e2e/tests/gpu.spec.ts, e2e compose env STEMSTUDIO_FAKE_DEVICE                          (qa)
```

**Dependency direction**: unchanged. Worker: `api -> jobs -> ports <- {separation, detail,
devices}`; `separation` and `detail` receive the `DeviceRunner` from the composition root and
never import `devices`; `devices` imports only `ports`, `schemas`, `config`. .NET:
`Api.Device -> Worker client`; Web -> API client.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/003-*/**`, ADR-012, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `StemStudio.Contracts/**`, project files | architect |
| `worker/src/stemstudio_worker/{devices,separation,detail,jobs,api}/**`, `main.py`, matching tests, `worker/scripts/**` | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `tests/fixtures/**`, `e2e/**`, `docs/testing/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**` (dependency rule for `devices`) | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

audio-engine, audio-ingest and beat-analysis have no work in this feature.

## Key flows

1. **Start-up**: `main.create_app` -> `create_device_runner(settings)` runs the probe once
   (CPU runner when `cpu`, fake runner when `separator=fake`) -> the same runner goes to
   `create_separator`, `create_detail_pipeline` and the health route.
2. **Model step** (standard separation or one detail parent step): `runner.run(work, need)` ->
   CPU worker: `work("cpu")`. GPU worker: acquire lease -> NVML free >= need? else
   `work("cpu")` + reason -> set memory fraction -> `work("cuda")` -> on GPU OOM:
   `empty_cache()`, `work("cpu")` + reason -> release lease -> `(result, DeviceUse)`.
3. **Device record**: the job runner / detail runner fold each `DeviceUse` with
   `merge_device` into `Job.device` / `DetailJob.device` (persisted with the job).
4. **Sync**: the backend copies `device` from every worker status poll into `Track.Device` /
   `DetailPhase.Device`; `GET /api/device` reads worker `/health`.
5. **Idle release**: when the scheduler has nothing queued, GPU model instances are dropped and
   `torch.cuda.empty_cache()` runs (ADR-012 D6).

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| NVIDIA CUDA runtime wheels (non-OSI) in the GPU image | CUDA-enabled torch/onnxruntime need them | Allowed by constitution 1.2.0 / ADR-011 for the optional GPU image only; CPU image and CI stay OSI-only (guarded) |
| Per-step `torch.cuda.set_per_process_memory_fraction` in addition to the free-memory check | WSL2/WDDM spills over-allocations into shared memory (~20x slower) instead of raising OOM, so FR-004's OOM fallback would never trigger and steps could silently crawl | Relying on CUDA OOM alone was measured not to work on the reference machine (research.md G-06) |
| GPU path not tested in CI | CI runners have no NVIDIA GPU | Covered by fakes in unit tests and by the reference-machine validation (quickstart G3-G9), recorded in `docs/STATUS.md` by the CTO |
