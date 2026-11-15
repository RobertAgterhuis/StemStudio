# ADR-012: GPU device policy and image variants (feature 003)

- **Status**: Accepted (architect, 2026-11-15; within ADR-011 and constitution 1.2.0)
- **Context documents**: `specs/003-gpu-acceleration/{spec.md, research.md, data-model.md}`,
  ADR-003 (worker stack), ADR-010 (detail models), ADR-011 (GPU vendor runtime exception)

## Context

Feature 003 runs Demucs `htdemucs_6s`, MDX23C DrumSep, UVR MDX-Net KARA 2 (ONNX) and MVSep
Mega 53-stem on an NVIDIA GPU when the user opts in, and must keep working everywhere on the CPU
(spec FR-001..FR-009). The feasibility spike on the reference machine (RTX 5070 Ti 16 GB,
sm_120, driver 610.88, Docker Desktop WSL2; research.md G-01..G-06) found:

1. torch 2.15.0 has CUDA wheels only for cu132/cu134; the repo's `cu128` default cannot build.
2. `onnxruntime-gpu` 1.32.0 does not find torch's CUDA 13 libraries by itself and needs cuRAND,
   which torch does not ship; the CUDA execution provider silently falls back to the CPU unless
   `onnxruntime.preload_dlls()` runs before the session is created and `nvidia-curand` is installed.
3. Under WSL2/WDDM, CUDA allocations beyond physical VRAM do **not** raise out-of-memory: they
   spill into shared system memory (30 GB allocated on the 16 GB card) and run about 20x slower.
   `torch.cuda.mem_get_info()` over-reported free memory (15.0 GiB while NVML/`nvidia-smi` showed
   9.7 GiB because another process held 6 GB).
4. Mega 53-stem allocates up to 9.35 GiB (11.3 GiB reserved) for a 20 s chunk; the whole job
   peaks at 13.2 GB NVML-used, close to the 12-13 GB a desktop leaves free.

## Decisions

### D1 Image variants (one Dockerfile, CI stays CPU)

`docker/worker.Dockerfile` keeps building the CPU image from `worker/uv.lock` by default. Two
build arguments select the GPU variant: `TORCH_VARIANT=cu132` (replace torch with
`torch==2.15.0` from `https://download.pytorch.org/whl/cu132`) and `ORT_VARIANT=gpu` (replace
`onnxruntime` with `onnxruntime-gpu==1.32.0`, without extras, so it reuses torch's CUDA 13 and
cuDNN 9 wheels, and adds `cuda-toolkit[curand]==13.2.2`, the one CUDA library onnxruntime needs that
torch does not ship). `docker-compose.gpu.yml` sets both (defaults `cu132`/`gpu`), tags the image
`stemstudio/worker:local-gpu`, sets `STEMSTUDIO_DEVICE=auto` and reserves the GPU with
`deploy.resources.reservations.devices: [{driver: nvidia, count: 1, capabilities: [gpu]}]`.
The NVIDIA CUDA runtime wheels exist only in that image (ADR-011). CI builds and tests the CPU
image only and fails when the CPU image contains a CUDA wheel (any `nvidia-*` distribution other
than `nvidia-ml-py`, `cuda-*`, `triton`, `onnxruntime-gpu`, or `torch.version.cuda` not None). The
GPU path is validated on the reference machine (`scripts/gpu-smoke.{ps1,sh}`, quickstart G3-G9).
`nvidia-ml-py` (BSD-3, pure Python NVML binding) is a normal locked dependency in both images;
it is inert without a driver.

### D2 Device selection (start-up probe, never fails start)

`STEMSTUDIO_DEVICE=cpu` never touches CUDA. `cuda` and `auto` are equivalent: the composition
root runs one probe (data-model 2.1: CUDA build, CUDA available, architecture in
`torch.cuda.get_arch_list()`, test kernel, NVML) and the worker runs on the GPU only if every
check passes; otherwise it runs on the CPU and `/health` `deviceInfo.reason` says why. The
probe result is fixed for the worker's lifetime.

### D3 One GPU step at a time (GPU lease)

All model work goes through one `DeviceRunner` (port in `ports.py`, implementation
`stemstudio_worker/devices/`). On a GPU worker every model step (one Demucs separation, one
detail parent step) holds a process-wide GPU lease for its duration. The worker's single
`PriorityScheduler` thread already runs one unit at a time; the lease makes the rule explicit
and safe for future threads. Other processes on the same GPU (games, the host's local LLM, future
generator containers) are handled by D4; a cross-service GPU scheduler is deferred to the first
feature that adds a second GPU service (roadmap 009/011).

### D4 Free-memory check and budget (NVML, not torch)

Before a step the runner reads NVML free memory (`nvmlDeviceGetMemoryInfo`) and compares it with
the step's `GpuNeed.memory_mib` (measured allocated peak of the step, or the NVML peak for
onnxruntime, + 1,024 MiB margin; research.md G-04). If it does not fit, the step runs on the CPU with the fallback reason "Not enough free GPU
memory ...". If it fits, the runner sets `torch.cuda.set_per_process_memory_fraction((free -
256 MiB) / total)` for the step, so that an allocation beyond the physically free memory raises
`torch.cuda.OutOfMemoryError` instead of spilling into WDDM shared memory (measured: the fraction
turns the spill into an exception). The fraction is reset after the step.

### D5 Out of GPU memory: retry the step once on the CPU

When the GPU attempt raises a GPU out-of-memory error (`torch.cuda.OutOfMemoryError`, or an
onnxruntime/`RuntimeError` whose message contains "out of memory" or "Failed to allocate memory"
raised while running on CUDA), the runner calls `torch.cuda.empty_cache()` and runs the same step
once on the CPU (a CPU instance of the model, loaded on demand). The job record gets
`cpuFallback=true` with the reason. The CPU attempt's errors propagate unchanged; any other GPU
error fails the job with the existing readable texts (FR-004). The OOM check runs on the raw
exception, before the detail code maps allocation errors to `MemoryError` (002 `demix`).

### D6 Model residency

GPU model instances stay loaded while the scheduler has queued work (warm start for the next
step or job). When the scheduler becomes idle, the GPU instances are released and
`torch.cuda.empty_cache()` returns the VRAM to other programs. CPU instances keep the 002
behaviour (loaded once). The onnxruntime CUDA session follows the same rule.

### D7 Device records and display

Every standard and detail job carries a `JobDevice` record (data-model 2.2) folded from the
steps' `DeviceUse` by `ports.merge_device`; worker contract 1.2.0 (`Job.device`,
`DetailJob.device`, `Health.deviceInfo`), backend 1.3.0 (`Track.device`, `DetailPhase.device`,
`GET /api/device`), UI test ids 1.2.0. The backend stores the records and never derives them.
Display texts come from `JobDeviceDto.DisplayText()`.

### D8 Numerics

Inference stays float32 with the same chunking, overlap and models as 002; no autocast/fp16, no
smaller Mega 53 chunks (both would lower VRAM but change the outputs). torch defaults are kept
(cuDNN may use TF32 for convolutions on Ampere+); the spike measured identical per-parent
outcomes and near-identical stems (research.md G-03). Revisit only if a future GPU misses the
memory need.

### D9 Fake device for tests

`STEMSTUDIO_FAKE_DEVICE` (`cpu`/`gpu`/`gpu-oom`, only with `STEMSTUDIO_SEPARATOR=fake`) lets the
fake stack report a fake GPU and a simulated OOM fallback, so E2E tests cover the display and
the fallback notice without a GPU (SC-004).

## Consequences

- Two worker images: CPU (3.4 GB on disk, default, CI) and GPU (11.3 GB on disk, opt-in).
- GPU speed-up on the reference song 8.5x cold, 14.6x with warm models (research.md G-02);
  SC-001 (>= 5x) met.
- A machine whose free VRAM is below the Mega 53 need runs that step on the CPU (slow, but
  completes); the track page says so.
- The WSL2 spill behaviour means the OOM retry is the second line of defence; the NVML check and
  the per-step memory fraction are the first.
- Generative features (009/010) and the agent LLM (011) reuse `DeviceRunner`, `GpuNeed` and the
  free-memory rule; a cross-container GPU broker will be decided there.

## Alternatives rejected

| Alternative | Why not |
|---|---|
| `cu128` / CUDA 12 wheels | no torch 2.15.0 wheel exists for them |
| `onnxruntime-gpu[cuda,cudnn]` extras | would install a second copy of the CUDA 13 libraries (~1 GB more); `preload_dlls()` reuses torch's |
| `torch.cuda.mem_get_info()` for the free-memory check | measured wrong under WSL2 (ignores other processes until memory pressure) |
| Rely on CUDA OOM only | WDDM spills instead of failing; steps would silently run ~20x slower |
| fp16 / smaller chunks for Mega 53 | changes outputs (D8); not needed on the reference GPU |
| GPU jobs scheduled by the API (`GpuScheduler`) now | only one GPU service exists; the worker's single thread + lease already gives FR-005; YAGNI |
| Separate GPU Dockerfile | duplicates the image definition; build args keep one source |
