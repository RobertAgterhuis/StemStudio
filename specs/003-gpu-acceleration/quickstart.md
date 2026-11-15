# Quickstart and validation guide: GPU Acceleration

Builds on [001 quickstart](../001-stemstudio-mvp/quickstart.md) and
[002 quickstart](../002-detail-separation/quickstart.md). Contracts: [contracts/](contracts/).
Entities: [data-model.md](data-model.md). Measurements: [research.md](research.md).
Design: [ADR-012](../../docs/decisions/ADR-012-gpu-device-policy.md).

## Running StemStudio

```bash
# Default: CPU only, unchanged (CI, any machine)
docker compose up --build

# Optional NVIDIA GPU (Windows 11 + Docker Desktop WSL2, or Linux + NVIDIA Container Toolkit)
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

GPU prerequisites (research.md G-05): an NVIDIA GPU with compute capability >= 7.5 (Turing or
newer; the reference machine has an RTX 5070 Ti, sm_120), a current NVIDIA driver with CUDA 13.2
support on the host (610.88 on the reference machine), and on Windows **no** Linux NVIDIA driver
inside WSL. Docker Desktop needs nothing else; check with
`docker run --rm --gpus all nvcr.io/nvidia/k8s/cuda-sample:nbody nbody -gpu -benchmark`.
The GPU worker image is about 11.3 GB on disk (CPU image 3.4 GB) and contains the NVIDIA CUDA
runtime libraries (system dependency, ADR-011; not part of the CPU image).

Which device is used: the import page shows "GPU (<name>)" or "CPU" (`GET /api/device`); every
track shows the device of its standard and detail jobs, with a notice when a step fell back to
the CPU. A GPU busy with another program (game, local LLM) can make steps run on the CPU (free
memory is checked before every heavy step).

## Validation (G1..G10)

| # | What | How | Expected |
|---|---|---|---|
| G1 | CPU default unchanged (SC-002) | `scripts/test-all.ps1` (or `.sh`) on any machine | every step green, coverage gates as before; no test needs a GPU |
| G2 | CPU image has no NVIDIA runtime (FR-008) | `docker build -f docker/worker.Dockerfile -t stemstudio/worker:cpu-check .` then `docker run --rm --entrypoint python stemstudio/worker:cpu-check -c "import importlib.metadata as m; n=[d.metadata['Name'].lower() for d in m.distributions()]; print([x for x in n if (x.startswith(('nvidia-', 'cuda-')) and x != 'nvidia-ml-py') or x in ('onnxruntime-gpu', 'triton')])"` | `[]`; `torch.version.cuda` is None (CI job `cpu-image-guard`) |
| G3 | GPU image works on the reference machine (FR-009) | `scripts/gpu-smoke.ps1` (builds `stemstudio/worker:local-gpu`, runs it with `--gpus all`) | torch `2.15.0+cu132`, `sm_120` in the arch list, matmul OK, onnxruntime `CUDAExecutionProvider` active after `preload_dlls()`, `/health` `deviceInfo.kind = gpu` |
| G4 | Health without a usable GPU (FR-003, US2-2) | run the GPU image **without** `--gpus all` (`docker run --rm stemstudio/worker:local-gpu` + `curl /health` inside) | starts; `deviceInfo.kind = cpu`, `reason` = "No usable NVIDIA GPU: CUDA is not available ..." |
| G5 | Speed (SC-001) | GPU stack, import the reference song "Hero of the Day" (4:22) as a new track (or delete the cached result first), record standard and detail wall time from the job timestamps | standard + detail <= 31.5 min / 5 = **6.3 min**; spike: 3.7 min cold (research.md G-02) |
| G6 | Outcome equality (FR-007, SC-003) | compare `detail.parents[*].outcome` and the kept stem names of the GPU run with the CPU run in `docs/STATUS.md` (002 T092); `uv run python worker/scripts/check_reconstruction.py <track detail dir>` | identical per parent: drums split (kick, snare, toms, hihat, cymbals + rest), vocals split (lead, backing + rest), guitar split (acoustic, electric + rest), bass not supported, piano and other skipped; reconstruction <= -30 dB |
| G7 | Device display (US3, FR-006) | open the track page during and after processing in both stacks | GPU stack: `track-device` / `detail-device` "GPU (NVIDIA GeForce RTX 5070 Ti)"; CPU stack: "CPU"; import page `worker-device` likewise |
| G8 | Simulated OOM fallback (FR-004, SC-004) | unit/integration tests with the fake device runner (stem-separation, backend) and Playwright `@gpu` with `STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DEVICE=gpu-oom` | job completes; `track-device-fallback` / `detail-device-fallback` show "The GPU ran out of memory during ..."; 100 % of runs |
| G9 | Busy GPU (edge case) | on the GPU stack, occupy VRAM first (e.g. `ollama run qwen3.5:9b` or a 10 GB torch allocation in a throwaway container), then split a song | the Mega 53-stem step reports "Not enough free GPU memory ... ran on the CPU"; job completes; no step runs in WDDM shared memory |
| G10 | One GPU step at a time (FR-005) | unit tests of the GPU lease (two threads, fake clock); on the GPU stack import two songs at once | lease tests green; `nvidia-smi` never shows two model steps (single scheduler thread) |

## Developer commands

```bash
cd worker && uv run pytest --cov            # device runner, probe, fallback with fakes (no GPU needed)
npx @redocly/cli@2.78.0 lint --config specs/003-gpu-acceleration/contracts/redocly.yaml \
  specs/003-gpu-acceleration/contracts/worker-api.openapi.yaml \
  specs/003-gpu-acceleration/contracts/backend-api.openapi.yaml
# Fake stack with a fake GPU (E2E of the device display and the fallback notice)
STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DEVICE=gpu-oom docker compose up -d --build --wait
```
