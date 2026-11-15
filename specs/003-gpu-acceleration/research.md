# Research: GPU Acceleration

Feasibility spike and decisions for feature 003 (architect, 2026-11-15). Earlier desk research:
`docs/research/roadmap-003-011.md` section 3 (there called "004"). Binding design: ADR-012.

## Setup of the spike

| Item | Value |
|---|---|
| Machine | Intel Core Ultra 9 285K (24 threads), 127 GB RAM, NVIDIA GeForce RTX 5070 Ti 16,303 MiB, compute capability **12.0** (sm_120), driver **610.88** (NVML CUDA driver version 13030), Windows 11 + Docker Desktop (WSL2), Docker memory limit 62.4 GiB |
| Image | `stemstudio/worker:gpu-spike` = current `docker/worker.Dockerfile` with `TORCH_VARIANT=cu132` plus one extra step that replaces `onnxruntime` by `onnxruntime-gpu==1.32.0` (scratchpad copy of the Dockerfile; nothing in the repo was changed for the spike) |
| Code under test | the worker's own 002 code: `DemucsSeparator` + `demucs_runtime`, `DetailPipeline` + `RegistrySource` + `RealModelProvider` (loaders as in 002, device `cuda`; KARA 2 loader overridden to request the CUDA execution provider) |
| Input | reference song "Hero of the Day" (4:22 = 261.9 s), worker cache `ec5c42f0...`: the cached CPU `htdemucs_6s` stems and the CPU detail result of 002 T092, mounted **read-only** from the `stemstudio_worker-data` volume; weights read-only from `stemstudio_model-cache` (`HF_HUB_OFFLINE=1`); outputs on a tmpfs. The worker does not keep the original mix, so the standard separation ran on the **sum of the six CPU stems** (proxy mix) |
| Load on the machine | the owner's CPU stack processed another song during the whole spike (worker at 800-2,400 % CPU). CPU-side parts of the GPU runs (model loading, STFTs, FLAC writing, gating) were therefore slowed; GPU kernels were not |
| VRAM measurement | `torch.cuda.max_memory_allocated/reserved` per step and an NVML sampler (`nvmlDeviceGetMemoryInfo().used` every 0.1 s; includes onnxruntime and the CUDA context); desktop baseline 873 MiB |

## G-01 Image and runtime facts

| Fact | Result |
|---|---|
| Build | `docker build -f worker-gpu.Dockerfile --build-arg TORCH_VARIANT=cu132 --build-arg ORT_GPU=1 -t stemstudio/worker:gpu-spike .` succeeded in about 7.5 min (cached base layers, cold uv cache for the CUDA wheels) |
| torch | `2.15.0+cu132`, CUDA runtime 13.2, cuDNN 9.26 (`92600`), `torch.cuda.is_available()` True |
| Architectures | `get_arch_list()` = `sm_75, sm_80, sm_86, sm_90, sm_100, sm_120` (SASS for Blackwell sm_120 present; Turing 7.5 is the oldest supported GPU) |
| Kernels on sm_120 | 20 x 4096^2 matmul in 0.23 s; all four models ran |
| GPU access | `docker run --gpus all` worked with Docker Desktop as installed (no NVIDIA Container Toolkit or Linux driver inside WSL needed); `nvidia-smi` and NVML work inside the container |
| CUDA wheels in the GPU image | `cuda-toolkit`, `nvidia-cublas`, `nvidia-cuda-cccl`, `nvidia-cuda-cupti`, `nvidia-cuda-nvrtc`, `nvidia-cuda-runtime`, `nvidia-cudnn-cu13`, `nvidia-cufft`, `nvidia-cufile`, `nvidia-cusolver`, `nvidia-cusparse`, `nvidia-cusparselt-cu13`, `nvidia-nccl-cu13`, `nvidia-nvjitlink`, `nvidia-nvshmem-cu13`, `nvidia-nvtx`, `triton`; plus `nvidia-curand` 10.4.2.66 for onnxruntime (below). Installed sizes: `nvidia/` 3.0 GB, `torch/` 1.2 GB, `triton/` 1.0 GB, `onnxruntime/` (GPU) 348 MB |
| onnxruntime-gpu 1.32.0 | providers `TensorrtExecutionProvider, CUDAExecutionProvider, CPUExecutionProvider`, **but** creating a CUDA session failed silently (warning, falls back to CPU) because (a) torch's CUDA libraries are not on the loader path -> `onnxruntime.preload_dlls()` is required, and (b) `libcurand.so.10` is missing (torch does not ship cuRAND) -> install `cuda-toolkit[curand]==13.2.2` (= `nvidia-curand` 10.4.2.66, +128 MB). With both, `session.get_providers()[0] == "CUDAExecutionProvider"` |
| Image size | CPU `stemstudio/worker:local` **3.36 GB** on disk (790 MB content); GPU spike **11.31 GB** on disk (3.8 GB content); with cuRAND **11.57 GB** |

## G-02 Speed (reference song, 4:22)

CPU baseline: 002 (`docs/STATUS.md`, specs/002 research.md "Full-song pipeline"): standard about
2.7 min, detail 28.8 min wall in the integrated run (T092); per-model CPU inference times from the
002 spike. GPU: this spike, cold process (first load of every model), worker code.

| Step | CPU | GPU | Speed-up | GPU s per audio minute |
|---|---|---|---|---|
| Standard `htdemucs_6s` (load + inference + FLAC) | ~162 s (2.7 min) | **23.2 s** (load 4.4 + 18.8) | 7.0x | 4.3 |
| DrumSep MDX23C, drums | 234.2 s | **31.5 s** (29.7 s in the second run) | 7.4x | 7.2 |
| KARA 2 (onnxruntime), vocals | 53.1 s | **19.6 s** (CUDA EP; 122-145 s on the CPU EP under contention) | 2.7x | 4.5 |
| Mega 53-stem, guitar (only active instrument parent) | 881.8 s | **55.7 s** (56.5 s second run) | 15.8x | 12.8 |
| Detail plan unit (gating of 6 stems) | (incl. above) | 3.7 s | | |
| Detail model loads, cold | 0.6-5 s each (002, idle CPU) | 87.7 s (Mega 53 84.3 s, of which `load_state_dict` 56.5 s under heavy CPU contention) | | |
| **Standard + detail, cold** | **~1,890 s = 31.5 min** | **221 s = 3.7 min** | **8.5x** | |
| Standard + detail, models already loaded (warm worker) | | 129 s = 2.2 min | 14.6x | |

Beat analysis (librosa, CPU in both modes) is not in the GPU figures; it adds the same few
seconds in both. **SC-001 (>= 5x) is met with margin**; the CTO measures the integrated stack in
T046 (end-to-end job timestamps). The Mega 53 cold load is the largest remaining CPU-side cost
(G-07 lever 2).

## G-03 Outcome equality (FR-007, SC-003)

**A. Detail on the same input (cached CPU standard stems), GPU vs CPU result of 002 T092:**

| Parent | CPU outcome | GPU outcome | Kept stems (both) | Metrics GPU vs CPU | SDR of GPU vs CPU detail stems |
|---|---|---|---|---|---|
| vocals | split | split | lead, backing, rest | identical (-24.61 dBFS, 0.6686, residual exact) | lead 69.2 dB, backing 53.4 dB (KARA 2 on CUDA); rest is numerically empty (-108.7 dB) |
| drums | split | split | kick, snare, toms, hihat, cymbals, rest | identical (residual -25.80 vs -25.81 dB) | 52.8-61.6 dB; rest 39.1 dB |
| bass | not-supported | not-supported | - | identical | - |
| guitar | split | split | acoustic-guitar, electric-guitar, rest | identical (residual -16.54 dB) | 71.7-89.2 dB |
| piano | skipped-silent | skipped-silent | - | identical | - |
| other | skipped-silent | skipped-silent | - | identical (4.82 % active, gate 8 %) | - |

Reconstruction error -160 dB (exact, as on the CPU) for every split parent. **Per-parent
outcomes and kept stem names are identical; the detail stems agree to >= 52 dB SDR**
(16-bit FLAC limits the comparison to about 90 dB).

**B. Full GPU chain** (GPU standard separation of the proxy mix, then GPU detail): the same
per-parent outcomes and kept stem names again; metrics drift slightly because the standard stems
differ (e.g. drums residual -25.36 dB, other 0.67 % active).

**Standard separation CPU vs GPU**: on the same 30 s excerpt, CPU vs GPU stems agree to 16-28 dB
SDR (vocals 25-28, drums 16-21, bass 19-24, guitar 17-21); two GPU runs of the same input agree
only to 20-26 dB as well. The difference is Demucs' own randomness (`apply_model` default
`shifts=1`, a random time shift per run), not the device; disabling cuDNN TF32 did not change it
(15.9-26.5 dB). This is pre-existing 001 behaviour; FR-007 is defined on outcomes and
reconstruction, both met.

## G-04 GPU memory and the per-step need

| Step | torch max allocated | torch max reserved | NVML process peak (above 873 MiB baseline) | `GpuNeed.memory_mib` (allocated or NVML-only peak, rounded up to 100 MiB, + 1,024 MiB) |
|---|---|---|---|---|
| htdemucs_6s | 0.57 GiB | 0.68 GiB | ~1,040 MiB (incl. CUDA context) | **1,700** |
| MDX23C DrumSep | 3.24 GiB | 3.52 GiB | ~4,130 MiB (incl. resident Demucs) | **4,400** |
| KARA 2 (onnxruntime CUDA EP, not torch) | - | - | 2,465 MiB (fresh process) | **3,600** |
| Mega 53-stem (20 s chunk, overlap 2) | 9.35 GiB | 11.27 GiB | ~12,110 MiB (incl. resident models) | **10,600** |

Peak VRAM of the whole job: **NVML 13.2 GB used** (12.3 GB for the worker) during Mega 53. The
need uses the allocated peak, not the reserved one: under a per-process memory fraction the
caching allocator frees its cached blocks and retries before it raises OOM, so the reserved
overhead (1.9 GiB for Mega 53) is not needed. With the spec's ~12.5 GB usable on a desktop, every
step fits on the reference GPU; a GPU with less free memory runs Mega 53 on the CPU.

## G-05 Out-of-memory behaviour under WSL2 (drives ADR-012 D4/D5)

| Probe | Result |
|---|---|
| Allocate 512 MiB blocks until failure | **No OOM**: 30,720 MiB (60 blocks) and in a second run 40,960 MiB were allocated on the 16 GB card; WDDM spilled into shared system memory. A spilled block was **~20x slower** (20 in-place adds: 0.053 s first block vs 1.087 s last block) |
| `torch.cuda.mem_get_info()` vs NVML | while a host process (an Ollama model, unloaded later) held ~6 GB: torch reported **15.0 GiB free**, `nvidia-smi`/NVML inside the container **9.7 GiB**; with the GPU idle both agree (NVML 15,469 MiB, torch 15,009 MiB). torch's figure is not reliable for other processes' usage |
| `torch.cuda.set_per_process_memory_fraction((nvml_free - 1 GiB) / total)` | `torch.cuda.OutOfMemoryError: CUDA out of memory. Tried to allocate 256.00 MiB ...` at 14,336 MiB with a 14,445 MiB budget: the fraction **turns the spill into an exception**. NVML afterwards showed less in use than allocated (WDDM residency), so the fraction, not NVML, is the hard limit |
| Exception type | `torch.cuda.OutOfMemoryError` is a subclass of `RuntimeError`; its text contains "alloc", which the 002 `demix.inference_error` maps to `MemoryError` -> the OOM check must look at the original exception (ADR-012 D5) |
| onnxruntime | not limited by the torch fraction; KARA 2 needs ~2.5 GB, its "Failed to allocate memory" / "out of memory" errors count as GPU OOM |

GPU prerequisites (G-05b): NVIDIA GPU with compute capability >= 7.5 (from the arch list), a
driver that supports CUDA 13.2 (610.88 reports CUDA 13.3; NVIDIA states R580+ for CUDA 13.x;
**not verified on another machine**), Docker Desktop with the WSL2 backend (no Linux driver in WSL)
or Linux with the NVIDIA Container Toolkit (not tested here).

## G-06 Decisions (summary; binding text in ADR-012)

| # | Decision | Rationale | Alternatives rejected |
|---|---|---|---|
| D-01 | One Dockerfile; build args `TORCH_VARIANT=cu132` and `ORT_VARIANT=gpu` (onnxruntime-gpu without extras + `cuda-toolkit[curand]==13.2.2`); compose override fixed to `cu132`, `STEMSTUDIO_DEVICE=auto` | measured working image; one source of truth | cu128 (no wheel), ORT `[cuda,cudnn]` extras (duplicate CUDA libs) |
| D-02 | Start-up probe -> `DeviceInfo`; `cuda` == `auto`; never fails start | FR-003, health reason | failing start when the GPU is missing |
| D-03 | `DeviceRunner` port: lease, NVML check vs `GpuNeed`, per-step memory fraction, one CPU retry on GPU OOM | G-04, G-05 | relying on CUDA OOM (never raised under WSL2), torch `mem_get_info` |
| D-04 | `onnxruntime.preload_dlls()` before the KARA 2 session; verify the active provider; CPU fallback with reason otherwise | G-01 | silent CPU EP fallback |
| D-05 | float32, same chunking/overlap; no fp16, no smaller chunks | G-03 equality; G-04 fits | fp16 autocast (numerics), 10 s Mega chunks |
| D-06 | Release GPU models when the scheduler is idle | VRAM back to games/LLMs between jobs | always resident |
| D-07 | `JobDevice` record per job, worker 1.2.0 / backend 1.3.0, UI 1.2.0; `GET /api/device` | FR-006 | device only in logs |
| D-08 | CI stays CPU + guard; GPU validated on the reference machine | no GPU runners; ADR-011 | self-hosted GPU runner |
| D-09 | `nvidia-ml-py` 13.615.71 (BSD-3) locked in both images | NVML is the reliable free-memory source; pure Python, inert without a driver | parsing `nvidia-smi` output (subprocess, slower) |

## G-07 Levers if SC-001 or the memory need is missed (in this order)

1. Warm models: keep GPU models loaded while work is queued (already D-06).
2. Faster Mega 53 cold load: build the network on the `meta` device and `load_state_dict(...,
   assign=True)` directly on the GPU (cold load measured 84 s under contention, `load_state_dict`
   56.5 s; building on meta took 1.8 s). Needs care for buffers not in the checkpoint; not tried.
3. `PYTORCH_CUDA_ALLOC_CONF=expandable_segments:True` (less fragmentation; not measured).
4. Mega 53 overlap 1 or fp16 autocast: faster/smaller but changes outputs; needs a new equality
   check (not planned).

## Commands used

```bash
# Image (scratchpad copy of docker/worker.Dockerfile + ORT swap step)
docker build -f <scratchpad>/worker-gpu.Dockerfile --build-arg TORCH_VARIANT=cu132 \
  --build-arg ORT_GPU=1 -t stemstudio/worker:gpu-spike .
docker run --rm --gpus all --entrypoint python stemstudio/worker:gpu-spike -c \
  "import torch; print(torch.__version__, torch.cuda.get_arch_list())"
# OOM / NVML probes (nvidia-ml-py wheel unpacked into <scratchpad>/lib)
docker run --rm --gpus all -v <scratchpad>:/spike:ro --entrypoint python \
  stemstudio/worker:gpu-spike -I /spike/oom.py
docker run --rm --gpus all -e PYTHONPATH=/spike/lib -v <scratchpad>:/spike:ro \
  --entrypoint python stemstudio/worker:gpu-spike /spike/frac.py
# Measurements (worker code, read-only volumes, tmpfs outputs)
docker run --rm --gpus all -e PYTHONPATH=/spike/lib -e HF_HUB_OFFLINE=1 \
  --tmpfs /work:rw,size=6g,mode=1777 -v stemstudio_worker-data:/data:ro \
  -v stemstudio_model-cache:/models:ro -v <scratchpad>:/spike:ro \
  --entrypoint python stemstudio/worker:gpu-spike /spike/gpu_spike.py ec5c42f0... 30
#   (same mounts) /spike/kara_gpu.py, /spike/tf32.py, /spike/load.py
# KARA 2 on CUDA needs cuRAND:
#   FROM stemstudio/worker:gpu-spike; uv pip install "cuda-toolkit[curand]==13.2.2"
#   -> stemstudio/worker:gpu-spike-curand, then /spike/kara_gpu.py
docker rmi stemstudio/worker:gpu-spike-curand stemstudio/worker:gpu-spike   # cleanup (done)
```

Spike scripts and logs lived in the session scratchpad only; the running `stemstudio` stack and
its volumes were never stopped, recreated or written (read-only mounts). Both spike images were
removed. Docker's shared build cache still holds the spike's layer records (~5.9 GB) and the uv
download cache grew to 7.4 GB with the CUDA wheels; they were not pruned because the build cache
is shared with other projects on the machine (`docker builder prune` reclaims them on request) and
the uv cache speeds up the devops GPU build (T008/T009).
