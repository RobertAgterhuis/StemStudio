# Hand-off: stem-separation / 003-polish

## What was built

- T045: kept `jobs/scheduler.py PriorityScheduler.__init__` and `main.create_app` within 15 LOC
  (`self._hooks`, a one-line `FastAPI(...)`; commit `6b50289`). Every new public module, class and
  function has a Google-style docstring (ruff `D` clean).
- **Real-GPU verification (optional, done)** on the reference machine (RTX 5070 Ti 16 GB, sm_120,
  Docker Desktop WSL2). I built a throwaway image `stemstudio/worker:s3-check` from this worktree's
  `docker/worker.Dockerfile` (`TORCH_VARIANT=cu132`), plus one scratch layer that replaces
  `onnxruntime` by `onnxruntime-gpu==1.32.0` and adds `cuda-toolkit[curand]==13.2.2` (what devops
  T008 adds). The model cache was mounted read-only (`HF_HUB_OFFLINE=1`). The running `stemstudio`
  stack and its volumes were not touched, and both throwaway images were removed afterwards.

  | Check (worker code of this branch, `--gpus all`) | Result |
  |---|---|
  | `default_probe("cpu")` | `CPU_DEVICE`, no torch |
  | `default_probe("auto")` | `gpu`, "NVIDIA GeForce RTX 5070 Ti", cc `12.0`, 16303 MiB total, 14297 MiB free |
  | `create_device_runner(device=auto)` | `GpuRunner` |
  | fitting step (2048^2 matmul) | ran on `cuda`, `DeviceUse(gpu, name)` |
  | need 100000 MiB | ran on the CPU: "Not enough free GPU memory for Mega 53-stem (needs 100000 MiB, 14265 MiB free); ran on the CPU." |
  | step allocating 30 GiB on the GPU | **`OutOfMemoryError` raised under the per-step fraction after 0.2 s (no WSL2 spill)**, `empty_cache`, CPU retry succeeded: "The GPU ran out of memory during huge; the step was repeated on the CPU." |
  | KARA 2 `load_kara2(.., "cuda")` (real weights) | `preload_dlls` + CUDA EP active (`fallback_reason None`); 5 s separation ran as a GPU step |
  | `DemucsSeparator` htdemucs_6s, 8 s tone, through the runner | 6 stems, `JobDevice(gpu, "NVIDIA GeForce RTX 5070 Ti")`, 2.5 s incl. load |
  | real app `/health`, `--gpus all`, `STEMSTUDIO_DEVICE=auto` | `deviceInfo.kind=gpu`, name, cc 12.0, 16303/9389 MiB, `reason null` |
  | real app `/health`, no `--gpus` | `kind=cpu`, reason "No usable NVIDIA GPU: CUDA is not available in this container (is the GPU option enabled and the driver current?)." |

  Not measured here (CTO T052): the full reference song, Mega 53 / DrumSep on the GPU, peak VRAM.

## Files changed (whole wave)

- New `worker/src/stemstudio_worker/devices/{__init__,probe,nvml,lease,oom,reasons,runner,fake}.py`
- `worker/src/stemstudio_worker/separation/{__init__,demucs_separator,demucs_runtime,fake}.py`
- `worker/src/stemstudio_worker/detail/{__init__,registry,runtimes,mdx_net,steps,fake}.py`
- `worker/src/stemstudio_worker/jobs/{runner,progress,detail_ports,detail_progress,detail_runner,scheduler,runtime}.py`
- `worker/src/stemstudio_worker/api/routes.py`, `worker/src/stemstudio_worker/main.py`
- Tests under `worker/tests/{devices,separation,detail,jobs,api}/`
- `docs/handoffs/stem-separation-003-{foundational,us1,us2,polish}.md`; ticks in `specs/003-gpu-acceleration/tasks.md`

## Tests and coverage

`cd worker; uv run pytest -q --cov --cov-branch`: **759 passed**, 3 deselected (integration),
3541 statements, 0 missed, 394 branches, 1 partial -> **line 100 %, branch 99.75 %**.
By component: `devices` 296 statements / 24 branches 100/100; `separation` 163 / 18 100/100;
`detail` (excl. vendored) 100 % line and branch; `jobs` 100 % line, 1 partial arc
(`scheduler.py` thread-loop exit, the same arc 002 already had); `api` and `main.py` 100/100.
There is still exactly one real-model integration test per model family (the 002 Demucs and detail
tests, now given a `CpuRunner`); no new real-model test.

## Quality checks

- `ruff check .`, `ruff format --check .` (146 files), `mypy --strict src tests` (150 files): clean.
- `sig_check.py worker/src`: 75 files, 507 units, **0 errors**, 2 warnings (both in other owners'
  modules, pre-existing).
- `tools/quality/run_all.py`: 0 errors. Warnings: "devices is not in the dependency map" (until
  quality-gate T006/T007), plus one test-only clone between the two small fake runners in
  `tests/detail/models.py` and `tests/separation/fakes.py`.
- Dependency direction respected: only `main.py` imports `devices`. `separation`, `detail`, `jobs`
  and `api` use `ports.DeviceRunner`/`DeviceUse`/`GpuNeed` only.

## Dependencies added

None. `nvidia-ml-py` 13.615.71 (BSD-3) was added by the architect. The throwaway GPU check used the
GPU-variant packages already listed in `architect-003-plan.md`.

## Open issues / requests

- devops T008: the verified recipe is `TORCH_VARIANT=cu132`, then `uv pip uninstall onnxruntime` and
  `uv pip install "onnxruntime-gpu==1.32.0" "cuda-toolkit[curand]==13.2.2"` into `/app/.venv`; with
  it, KARA 2 really uses the CUDA EP.
- `onnxruntime-gpu` logs a harmless warning "Failed to persist telemetry device ID" (read-only home);
  cosmetic.
- No contract problems found; worker 1.2.0 was implemented as written.

## Next owner

CTO integration wave (T051-T054).
