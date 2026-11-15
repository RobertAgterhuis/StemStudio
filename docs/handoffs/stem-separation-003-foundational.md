# Hand-off: stem-separation / 003-foundational

## What was built

New package `worker/src/stemstudio_worker/devices/` (ADR-012 D2-D6, D9), imported only by the
composition root:

- `probe.py`: `probe_device(preference, torch, nvml)` (data-model 2.1). `cpu` returns `CPU_DEVICE`
  without creating any adapter; `cuda`/`auto` check CUDA build -> CUDA available -> `sm_<cc>` in
  `get_arch_list()` -> 256x256 matmul finite -> NVML memory; the first failure's exact text becomes
  `DeviceInfo.reason` (kernel error: first line, <= 200 chars). Never raises. `TorchProbe` is the
  real adapter (torch imported when it is created).
- `nvml.py`: `PynvmlReader` (pynvml imported and `nvmlInit` called on the first read, GPU 0, MiB).
- `lease.py`: `GpuLease` + process-wide `GPU_LEASE`.
- `oom.py`: `is_gpu_out_of_memory(exc)`: `torch.cuda.OutOfMemoryError` by class name or the texts
  "out of memory" / "failed to allocate memory", walking `__cause__`/`__context__` (so errors the
  detail models map to `MemoryError` with `raise ... from` are still recognised, ADR-012 D5).
- `reasons.py`: the two per-step fallback texts of data-model 2.3.
- `runner.py`: `CpuRunner(info)`; `GpuRunner(info, GpuTools(nvml, memory, lease))`: lease held per
  step; NVML free < `need.memory_mib` -> `work("cpu")` + "Not enough free GPU memory ..." with no
  CUDA call; else `set_per_process_memory_fraction((free - 256) / total)` for the step, reset to
  1.0 afterwards; GPU OOM -> `empty_cache()` and exactly one `work("cpu")` with "... ran out of
  memory ..."; CPU errors and non-OOM GPU errors propagate. `release()` = `gc.collect()` +
  `empty_cache()`. `TorchCudaMemory` is the real torch adapter.
- `fake.py`: `FakeDeviceRunner(cpu|gpu|gpu-oom)`, `FAKE_GPU` (name exactly "Fake GPU"). `gpu-oom`
  falls back on the first step of every job context; a context ends at `release()` (worker idle),
  so one import gives a fallback in its standard job and in its detail job.
- `__init__.py`: `create_device_runner(settings, probe=default_probe) -> ManagedRunner`
  (`DeviceRunner` + `release()`): fake runner for `separator=fake` (no probe), else probe once ->
  `GpuRunner` or `CpuRunner` with the reason.

Commits (red -> green): T011 `fc2a9e2` -> T012 `ba4b27a`; T013 `ea68adb` -> T014 `631309f`;
T015 `91f4d9c` -> T016 `b2651ad`.

## Files changed

- `worker/src/stemstudio_worker/devices/{__init__,probe,nvml,lease,oom,reasons,runner,fake}.py` (new)
- `worker/tests/devices/{__init__,fakes,test_probe,test_runner,test_factory,test_fake}.py` (new)

## Tests and coverage

`uv run pytest -q --cov --cov-branch` (worker): **723 passed**, 3 deselected (integration);
TOTAL 3396 stmts, 0 missed, 374 branches, 1 partial (pre-existing `scheduler.py 127->exit`).
`devices/*`: 296 statements, 24 branches, **100 % line, 100 % branch** (54 tests).
No test needs a GPU: fake torch/NVML/memory adapters; the real adapters are tested against the CPU
torch build (monkeypatched `torch.cuda` functions) and a fake pynvml module; two subprocess tests
prove that the `cpu` start-up imports neither torch nor pynvml.

## Quality checks

- `ruff check .`, `ruff format --check .` (140 files), `mypy --strict src tests` (144 files): clean.
- `sig_check.py worker/src/stemstudio_worker/devices worker/tests/devices`: 14 files, 128 units,
  0 errors, 0 warnings.
- `tools/quality/run_all.py`: 509 files, 4016 units, 0 errors, 136 warnings (one new: "devices is
  not in the dependency map", resolved by quality-gate T006/T007).

## Dependencies added

None (uses `nvidia-ml-py` 13.615.71 added by the architect in T002).

## Open issues / requests

- quality-gate: add `devices` to `PYTHON_ALLOWED` (`ports`, `schemas`, `config`), as planned.
- NVML reads GPU index 0; with several GPUs and a remapped `CUDA_VISIBLE_DEVICES` the NVML and
  CUDA indices could differ (out of scope: one GPU, plan.md).
- When NVML fails during a step (after a passing probe) the step runs on the GPU without the memory
  check and fraction (logged warning) rather than failing the job.

## Next owner

stem-separation continues with US1 (T023-T028).
