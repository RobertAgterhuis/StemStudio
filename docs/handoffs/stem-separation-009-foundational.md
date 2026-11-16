# Hand-off: stem-separation / 009 foundational (T022, T023)

Cross-container GPU lease on the worker side (FR-011, ADR-018 D3, plan.md flow 6), plus the
supply-chain fix for `fastapi` in `worker/uv.lock` that the architect hand-off requested.

## What was built

- `devices/lease.py`
  - `LockApi` protocol with `FlockApi` (`fcntl.flock(LOCK_EX | LOCK_NB)`, Linux; the same lock
    the generator takes) and `MsvcrtApi` (`msvcrt.locking(LK_NBLCK, 1)` on byte 0, Windows dev).
    `platform_lock_api(platform, importer)` picks one; both are tested with fake modules, the
    real one of the host through the tmp-path file tests.
  - `LeaseWait(seconds, poll_seconds=1.0, now, sleep)`: wait limit plus injectable clock.
  - `FileGpuLease(path, wait, api=None)`: `hold()` is a context manager that yields `True`
    while the lock is held and `False` when `seconds` ran out (polling every `poll_seconds`).
    The file is opened `O_RDONLY | O_CREAT` per step and closed afterwards. An unopenable file is
    logged and grants the GPU without the lease (same rule as an unreadable NVML).
  - `NoFileLease` / `NO_FILE_LEASE`: always grants, touches no file (the 003 behaviour).
  - `GpuLease` / `GPU_LEASE` (003 thread lease) are unchanged.
- `devices/runner.py`
  - `GpuTools.file_lease: FileLease = NO_FILE_LEASE` (default keeps every existing caller).
  - `GpuRunner.run`: thread lease -> file lease -> NVML check / reclaim / memory cap / GPU
    attempt -> release. When the file lease is not granted the step runs on the CPU with
    `config.GPU_LEASE_BUSY_REASON` ("GPU busy with another StemStudio job"); NVML is not read.
  - Every CPU fallback (busy, not enough memory, OOM retry) now runs **after** the file lease is
    released (still inside the thread lease), so the generator is not blocked by a long CPU step.
    The fallback reasons and the OOM `empty_cache` (done under the lease) are unchanged.
- `devices/__init__.py`: `create_device_runner` gives the `GpuRunner` a `FileGpuLease` when
  `settings.gpu_lease_file` is set (`LeaseWait(settings.gpu_lease_wait_seconds)`), otherwise
  `NO_FILE_LEASE`. `CpuRunner` and `FakeDeviceRunner` never get it.
- `worker/pyproject.toml` / `worker/uv.lock`: `fastapi` 0.163.0 (2026-11-14) -> **0.162.0**
  (2026-10-23, newest release published before 2026-11-01). The floor `>=0.163,<1` became an
  exact pin `==0.162.0` (with comment) because 0.162.0 does not satisfy the old floor and a range
  would let `uv lock --upgrade` bring the young release back. Separate commit.

## Files changed

- `worker/src/stemstudio_worker/devices/{__init__,lease,runner}.py`
- `worker/tests/devices/test_file_lease.py` (new, 15 tests), `worker/tests/devices/fakes.py`
  (`FakeClock`)
- `worker/pyproject.toml`, `worker/uv.lock`

## Tests and coverage

```
uv run --frozen pytest --cov --cov-branch          (worker, Windows host)
TOTAL    9552 stmts, 0 miss, 1086 branches, 0 partial   100 %
2378 passed, 3 skipped, 10 deselected

uv run --frozen pytest tests/devices tests/test_config.py --cov=stemstudio_worker.devices --cov-branch
devices/__init__.py 33/6 100 %, lease.py 107/10 100 %, runner.py 114/14 100 %, rest 100 %
TOTAL 426 stmts, 44 branches, 100 % line and branch; 85 passed
```

T022 red state before T023: 9 failed, 6 passed (`tests/devices/test_file_lease.py`).

Linux check (`stemstudio/worker:local` container, src/tests mounted read-only, pytest from
pure-Python wheels on `PYTHONPATH`): `tests/devices` 74 passed with the real `fcntl.flock`.
A two-process script confirmed: a lock held by another process -> `granted=False` after the
1 s wait; after release -> `True`; after the holder is killed (SIGKILL) -> `True` (kernel
releases the lock).

Suite also run after the fastapi downgrade alone: 2363 passed, 3 skipped (before the new tests).

## Quality checks

- `ruff check .`: All checks passed; `ruff format --check .`: 339 files already formatted
- `mypy` (strict, package config): no issues in 180 source files; `mypy --strict src tests/devices`: 187 files clean
- `sig_check.py worker/src/stemstudio_worker/devices worker/tests/devices`: 15 files, 187 units,
  0 errors, 0 warnings

## Dependencies added

None. `fastapi` changed version only: fastapi | 0.162.0 | MIT | https://github.com/fastapi/fastapi

## Open issues / requests

- **Fairness (documented limitation)**: the worker polls with `LOCK_NB` so it can give up
  after the wait limit; waiting is therefore not strictly FIFO between worker and generator
  (ADR-018 D3 says "FIFO by arrival at the lock"). A blocking `flock` cannot be timed out without
  signals/threads. Architect may want to reword D3 to "no ordering guarantee for the worker".
- **Unopenable lock file**: the step runs on the GPU without the cross-container lease and an
  error is logged (no new user-facing reason text). Alternative would be a CPU fallback with a
  new reason; that needs a data-model text, so left for the architect to decide.
- **devops (T009)**: the lock file must be openable by both containers' users. The worker opens
  it read-only (`O_RDONLY | O_CREAT`, mode 0o666 minus umask), which suffices for `flock`; the
  `gpu-lease` volume directory must be writable by whichever container creates the file first.
- **Other young packages in `worker/uv.lock`** (first upload on/after 2026-11-01, not changed
  here because only fastapi was in scope): annotated-doc 0.0.6, anyio 4.16.0, click 8.5.1,
  coverage 7.17.3, dnspython 2.9.2, filelock 4.2.10, librosa 1.0.1, markdown-it-py 4.3.0,
  mypy-extensions 1.2.0, onnxruntime 1.32.0, platformdirs 4.14.5, pycryptodomex 3.24.2,
  pydantic 2.14.3, pydantic-core 2.50.4, pymongo 4.20.0, python-dateutil 4.0.0,
  rotary-embedding-torch 0.9.2, ruff 0.17.5, soupsieve 4.1.1, threadpoolctl 3.8.0,
  typing-inspection 0.4.5. All of these are less than two weeks old today (2026-11-15); several
  were presumably accepted earlier (e.g. onnxruntime, librosa pins). CTO to decide.
- `specs/009-style-transfer/tasks.md` checkboxes T022/T023 not ticked (not my path).

## Next owner

devops (T009: mount `gpu-lease` and set `STEMSTUDIO_GPU_LEASE_FILE` in the GPU worker),
generative (T014/T015: generator side of the same lock), qa (cross-container E2E).
