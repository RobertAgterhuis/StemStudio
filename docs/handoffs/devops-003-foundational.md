# Hand-off: devops / 003 foundational (T008, T009)

## What was built
- **T008** (`a444800`): `docker/worker.Dockerfile` has a new build arg `ORT_VARIANT` (`cpu` by
  default; `gpu` uninstalls `onnxruntime` and installs `onnxruntime-gpu==1.32.0` without extras
  plus `cuda-toolkit[curand]==13.2.2`) next to `TORCH_VARIANT`. Both args are validated: an unknown
  value fails the build. A new build arg `WORKER_DEVICE` (`cpu` by default) sets the image's
  default `STEMSTUDIO_DEVICE`. `docker-compose.gpu.yml` now uses `TORCH_VARIANT` **cu132** (cu128
  had no wheel, FR-009) with `ORT_VARIANT: gpu`, `WORKER_DEVICE: auto`, image
  `stemstudio/worker:local-gpu` and `STEMSTUDIO_DEVICE` default `auto`. It keeps the `deploy`
  GPU reservation and documents the prerequisites, the ~11.5 GB image and "no Linux driver in
  WSL". `docker-compose.yml` has a comment on the GPU option and passes
  `STEMSTUDIO_FAKE_DEVICE` (default `cpu`) to the worker. `scripts/dev.{ps1,sh}` export
  `STEMSTUDIO_FAKE_DEVICE` (default `cpu`; an existing value wins). The CI `CONTRACTS_DIR` and
  `scripts/test-all.*` point at `specs/003-gpu-acceleration/contracts`.
- **T009** red `46f68c6` -> green `9fbadb2`:
  - `scripts/cpu_image_guard.py` runs inside the image, called by
    `scripts/check-cpu-image.{sh,ps1} [--build|-Build] [image]`. It fails on any `nvidia-*`
    except `nvidia-ml-py`, any `cuda-*`, `triton` or `onnxruntime-gpu`, or when
    `torch.version.cuda` is not None.
  - New CI job `cpu-image-guard` builds the default image and runs the guard.
  - `scripts/gpu-smoke.{sh,ps1} [--no-build|-NoBuild] [image]` with
    `scripts/gpu_smoke_checks.py` checks the runtime, then `/health` with `--gpus all`
    (expects `gpu`) and without it (expects `cpu` with a reason). It uses throwaway containers
    and never touches the compose stack.

## Files changed
`docker/worker.Dockerfile`, `docker-compose.yml`, `docker-compose.gpu.yml`,
`.github/workflows/ci.yml`, `scripts/{dev,test-all}.{ps1,sh}`,
`scripts/check-cpu-image.{ps1,sh}`, `scripts/cpu_image_guard.py`, `scripts/gpu-smoke.{ps1,sh}`,
`scripts/gpu_smoke_checks.py`, `specs/003-gpu-acceleration/tasks.md` (T008 and T009 ticked).

## Tests and coverage
devops owns no unit-tested component, so the coverage gate does not apply. The guard has a
red -> green run on real images:

```
docker build (CPU, tag devops-cpu-check)  -> exit 0, 2m04s, 3.36 GB
docker build --build-arg TORCH_VARIANT=cu132 --build-arg ORT_VARIANT=gpu --build-arg WORKER_DEVICE=auto
  (tag devops-gpu-check)                  -> exit 0, 5m24s (warm uv cache), 11.5 GB
check-cpu-image.sh devops-gpu-check (RED) -> 21 x "FAIL forbidden distribution" (cuda-bindings,
  cuda-pathfinder, cuda-toolkit, nvidia-cublas ... nvidia-nvtx, onnxruntime-gpu, triton)
  + "FAIL torch is a CUDA build (torch.version.cuda = 13.2)", exit 1
check-cpu-image.sh / .ps1 devops-cpu-check (GREEN) -> "OK CPU-only image", exit 0
CPU image: torch 2.15.0+cpu, onnxruntime 1.32.0 [Azure, CPU EP], STEMSTUDIO_DEVICE=cpu
gpu-smoke.sh --no-build devops-gpu-check (RTX 5070 Ti, Docker Desktop WSL2):
  OK torch 2.15.0+cu132 | OK cuda available | OK sm_120 in [sm_75..sm_120] | OK 2048^2 matmul
  OK onnxruntime 1.32.0 ['CUDAExecutionProvider', 'CPUExecutionProvider'] | OK Relu on CUDA EP
  FAIL /health deviceInfo.kind == gpu: the skeleton still returns the placeholder
       {"kind":"cpu","reason":null} (the start-up probe is a stem-separation task)
  health cpu branch: kind cpu OK, reason None -> FAIL (same cause)
```
The `/health` part of the smoke test turns green once the stem-separation probe is merged; the CTO
reruns it in T052.

## Quality checks
- `docker compose config -q` (default and with `docker-compose.gpu.yml`): both exit 0. Resolved
  GPU worker: args `{TORCH_VARIANT: cu132, ORT_VARIANT: gpu, WORKER_DEVICE: auto}`, image
  `local-gpu`, `STEMSTUDIO_DEVICE=auto`, `STEMSTUDIO_FAKE_DEVICE=cpu`.
- actionlint 1.7.12: clean. shellcheck 0.11.0 (all `scripts/*.sh`): clean.
- PSScriptAnalyzer (Warning, Error): 0 findings. The 6 pre-existing Information findings in
  dev.ps1 are unchanged.
- ruff check, ruff format and mypy --strict on the two Python scripts: clean.
- `sig_check.py scripts`: 2 files, 18 units, 0 errors, 0 warnings.

## Dependencies added
No new locked dependencies. The GPU image variant only (never the CPU image or CI):
onnxruntime-gpu 1.32.0 (MIT, https://github.com/microsoft/onnxruntime) and
cuda-toolkit[curand] 13.2.2 / nvidia-curand 10.4.2.66 (NVIDIA proprietary EULA, system
dependency per ADR-011, https://pypi.org/project/cuda-toolkit/).

## Open issues / requests
- stem-separation: the `/health` `deviceInfo` probe is needed before gpu-smoke passes end to end.
- Deviation: the build arg `WORKER_DEVICE` is not in tasks.md. Without it the GPU image run on its
  own (G4: `docker run` without compose) would default to `cpu`, and `reason` would be null.

## Next owner
devops T039 (us2), then CTO T052.
