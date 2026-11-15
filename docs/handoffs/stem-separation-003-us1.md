# Hand-off: stem-separation / 003-us1

## What was built

US1 "Faster separation on a GPU machine" on the worker side (T023-T028):

- **Separation** (`separation/`): `create_separator(settings, devices)`. `DemucsSeparator(models,
  runtime, devices)` runs `apply` as one device step with `GpuNeed(<model name>, 1700)`
  (`HTDEMUCS_NEED_MIB`, research.md G-04; the same need for the `htdemucs` fallback). The runner
  sees the raw inference error (so a GPU OOM is retried on the CPU); only the final error becomes
  "Stem separation failed.". `SeparationResult.device = merge_device(None, use)`.
  `DemucsRuntime.cuda_available` was removed (the runner decides the device).
  `FakeSeparator(devices=None)` runs its copy through the runner when it has one
  (`FAKE_NEED = GpuNeed("the standard separation", 0)`).
- **Detail** (`detail/`): `registry.GPU_NEEDS` (DrumSep 4400, KARA 2 3600, Mega 53-stem 10600 MiB).
  `RealModelProvider(downloader, loaders)` caches per `(model_id, device)`, `get(id)` = the CPU
  instance (002 port), `load(id, device)`, `release_gpu()` drops only GPU instances.
  `load_kara2(path, "cuda")` calls `onnxruntime.preload_dlls()`, requests
  `["CUDAExecutionProvider", "CPUExecutionProvider"]`, and when `session.get_providers()[0]` is not
  CUDA the model carries "ONNX Runtime has no usable CUDA provider; UVR MDX-Net KARA 2 ran on the
  CPU.", which turns the step's `DeviceUse` into a CPU fallback. `ModelChoice` now has
  `load(device)` and `need` instead of a loaded `model`. `DetailPipeline(source, devices=None,
  version)`: every parent step is one `devices.run(...)` call and returns its `DeviceUse`
  (`None` without a runner: CPU, no record). `DetailPipeline.release_gpu()` reaches the source.
  `create_detail_pipeline(settings, devices)`. `create_detail_adapters(settings)` is unchanged.
- **Jobs** (`jobs/`): `JobContext.device` (default `CPU_DEVICE`); the runner sets the planned
  record `JobDevice(kind, name)` when the separation starts and replaces it with the result's
  record; cache hits keep `device = null`. `DetailStepUnit.run` returns `DeviceUse | None`;
  `DetailTracker.fold_device` folds it after each parent step with `merge_device` (persisted in
  `detail-jobs/*.json`, ignored after the job ended). `SchedulerHooks(on_yield, on_idle)`: `on_idle`
  runs on the worker thread after a unit that left both queues empty, before `join()` returns;
  its errors are logged. `DeviceHooks(info, on_idle)`, `WorkerParts(adapters, pipeline, devices)`,
  `WorkerRuntime.start_with(settings, parts)` (`start(settings, adapters, pipeline)` kept for CPU
  callers); `WorkerRuntime.device`.
- **API/main**: `/health` `deviceInfo` = the runtime's probed device (`CPU_DEVICE` before the
  lifespan). `create_app(settings, adapters, detail_pipeline, devices)`: in the lifespan one
  runner is built (`create_device_runner`, probe once) and shared by the separator, the detail
  pipeline, `/health` and the idle hook (`pipeline.release_gpu()` then `runner.release()`).
  Because the idle hook also ends a fake-device job context, `STEMSTUDIO_FAKE_DEVICE=gpu-oom` makes
  the first step of the standard job and of the detail job fall back ("... ran out of memory ...")
  (coordinator message about the qa specs: implementation matches).

Commits (red -> green): T023 `9e15870` -> T024 `56c98a1`; T025 `dc492f3` -> T026 `f211161`;
T027 `ee60b4c` -> T028 `029e3c1`.

## Files changed

- `worker/src/stemstudio_worker/separation/{__init__,demucs_separator,demucs_runtime,fake}.py`
- `worker/src/stemstudio_worker/detail/{__init__,registry,runtimes,mdx_net,steps,fake}.py`
- `worker/src/stemstudio_worker/jobs/{runner,progress,detail_ports,detail_progress,detail_runner,scheduler,runtime}.py`
- `worker/src/stemstudio_worker/api/routes.py`, `worker/src/stemstudio_worker/main.py`
- Tests: new `tests/separation/{fakes,test_demucs_device}.py`, `tests/detail/test_device_models.py`,
  `tests/jobs/test_job_device.py`, `tests/api/test_device_health.py`; adapted
  `tests/separation/{test_demucs_separator,test_demucs_integration}.py`,
  `tests/detail/{models,test_steps,test_fake,test_runtimes,test_detail_integration}.py`,
  `tests/jobs/{detail_fakes,test_scheduler}.py`.

## Tests and coverage

`uv run pytest -q --cov --cov-branch` (worker): **756 passed**, 3 deselected (integration);
TOTAL 3541 statements, 0 missed, 394 branches, 1 partial (`jobs/scheduler.py 143->exit`, the
pre-existing thread-loop exit, was `127->exit`) -> **line 100 %, branch 99.75 %**.
Every file of `devices`, `separation`, `detail` (excl. vendored), `jobs`, `api`, `main.py`: 100 %
line; branch 100 % except that one scheduler arc. No test needs a GPU.

## Quality checks

- `ruff check`, `ruff format --check` (143 files), `mypy --strict src tests` (149 files): clean.
- `sig_check.py worker/src worker/tests`: 143 files, 1326 units, **0 errors**, 35 warnings
  (test units > 15 LOC, mostly pre-existing; production: `jobs/scheduler.py __init__` 17 LOC,
  `main.py create_app` 17 LOC, docstring-heavy).
- `tools/quality/run_all.py`: 514 files, **0 errors**, 141 warnings; one new test-only clone
  (`tests/detail/models.py` RecordingRunner vs `tests/separation/fakes.py` ScriptedRunner, 8 lines).

## Dependencies added

None.

## Open issues / requests

- `jobs/scheduler.py` and `jobs/runtime.py` signatures changed (`SchedulerHooks`, `start_with`):
  internal to the worker, no other owner calls them.
- The KARA 2 CUDA session is verified only through fakes; the real CUDA EP path needs the GPU image
  (devops T008 cuRAND) and the CTO's T052 run.
- The Demucs bag moves its sub-model back to its original device after each run (demucs
  `apply_model`), so Demucs holds no GPU weights between jobs; detail models stay resident on the
  GPU until the idle release.

## Next owner

stem-separation continues with US2 (T034-T035); backend/frontend consume `device` per worker 1.2.0.
