# Hand-off: generative (agent A) / 009 foundational - devices and jobs (T012-T017)

## What was built

The devices and jobs half of the 009 foundational phase. Agent B is doing the audio and engines
half (T018-T021, 010 T010-T013). US1 and the later tasks wait until B's branch is merged into
`009-010-generative`. When this note was written, `009-010-generative` (fdb6034, merged here as
d8180f0) did not contain B's work yet.

### `devices/` (ADR-018 D3/D4, 003 data-model 2.1)

- `probe.py`: `probe_device(torch, nvml)` and `GpuProbe` (`DeviceProbe`, probes once). They run
  the worker's probe order, and the reason texts are **identical** (`NO_CUDA_BUILD`,
  `NOT_AVAILABLE`, `UNSUPPORTED`, `KERNEL_FAILED`, `NO_NVML`). The generator has no CPU
  preference, so it always probes. `TorchProbe(module)` and `load_torch_probe()` use a lazy
  torch import, so the base install reports a CPU build. `is_ready(info)` is true only for
  `kind == "gpu"` (FR-010).
- `nvml.py`: `GpuMemory`, the `NvmlReader` protocol, and `PynvmlReader`. NVML is initialised
  once (thread-safe), and memory is reported in MiB, rounded down.
- `lease.py`: `FileLease(path, clock, poll_seconds=1.0)`. On Linux it uses
  `fcntl.flock(LOCK_EX|LOCK_NB)`; on Windows it uses `msvcrt.locking` (local development only).
  Each attempt opens its own descriptor, so threads also exclude each other. The lease is
  polled; while it waits it calls `on_wait("Waiting for the GPU (another StemStudio job is using
  it)")`, which may raise `Cancelled`. `ThreadLease` is used when no lease file is set.
  `open_lease(path | None, clock)` picks between the two.
- `gate.py`: `LeaseGpuGate(GpuAccess(lease, nvml, cap, name), clock, GatePolicy(wait_seconds,
  recheck_seconds=10, poll_seconds=1))`. It implements the `GpuGate` port. It takes the lease,
  reads NVML and admits the first `GpuNeed` that fits. If none fits, it releases the lease,
  reports `"Waiting for free GPU memory: needs X GB, Y GB free"` every poll and re-checks every
  10 s. When the wait limit is reached it raises `GpuBusyError` with `GPU_BUSY_TEMPLATE`, using
  the smallest need. A wait limit of 0 fails on the first check. While a step is admitted, the
  cap is set to `(free - 256 MiB) / total` (clamped to 0..1); it is reset on exit, including on
  exceptions. If NVML cannot be read, it raises `GpuRequiredError(NO_NVML)`.
- `memory.py`: `MemoryCap`, `TorchMemoryCap`
  (`set_per_process_memory_fraction(f, 0)`, reset to 1.0), `NoMemoryCap`, `load_torch_cap()`
  and `cap_fraction()`.
- `oom.py`: `is_gpu_oom(error)` walks the cause chain and honours `from None`. It matches a
  class named `OutOfMemoryError`, the texts "out of memory", "Insufficient free VRAM" and
  "Failed to allocate memory", and the port's `GpuOutOfMemoryError`.
- `fake.py` (fake stack): `FakeDeviceProbe(mode)` (`no-gpu` gives `NOT_AVAILABLE`; any other
  mode gives the "Fake GPU (StemStudio test stack)" at 16,303 MiB), `FakeNvml(mode)` (`busy`
  reports 2,048 MiB free, otherwise 14,800), and `fake_gate(mode, clock, wait_seconds,
  lease_file)`. The fake gate is the **real** gate over the fake NVML, without a memory cap.

### `jobs/` (data-model 2.4, FR-007, FR-013, SC-006)

- `store.py`: `JsonJobStore(data_dir)` implements `JobStore`. Each job is stored as
  `jobs/<id>.json` (camelCase wire shape, atomic tmp file + replace). `list_all` returns jobs
  oldest first and skips unreadable files. `job_dir(id)` creates and returns `jobs/<id>/`.
  `delete` removes both the file and the directory.
- `clock.py`: `SystemClock`.
- `progress.py`: `JobReporter` is the single writer of a job while it is processed. Every change
  is validated with `Generation.model_validate` and stamped with the time.
  - `status()` starts a new phase with progress 0.
  - `progress()` is the `ProgressCallback`: an integer 0-100 that never goes back down.
  - `wait()` is the `WaitCallback`: it sets `waitingForGpu`, but a running retry stays
    `running`.
  - Also: `admitted(device)`, `complete(JobOutcome)`, `fail(code, msg)` and an idempotent
    `cancelled()`.
  - `request_cancel()`: `queued`/`waitingForGpu` jobs are cancelled immediately. For
    `running`/`downloadingModel` it sets a flag, and the next callback raises `Cancelled`.
    Terminal jobs are left unchanged.
  - `JobOutcome(takes, warnings)` and `updated(job, changes, now)` are also defined here.
- `runner.py`: `JobRunner(gate, {kind: JobHandler})`.
  - Protocols: `ModelHost` (`required_models`, `load`, `unload`, i.e. the shared part of
    `MusicEngine` and `VoiceConverter`) and `JobHandler` (read-only `engine`, `needs(job)`,
    `execute(job, use, on_progress) -> JobOutcome`).
  - Flow: `waitingForGpu` ("Waiting for the GPU") -> gate -> device recorded ->
    `downloadingModel` if any required model is not present, else `running` "Loading model" ->
    `engine.load` -> `running` "Generating" -> handler -> `completed`.
  - OOM handling: on the first OOM it calls `engine.unload()` and retries **once** with the
    low-memory needs (stage `LOW_MEMORY_RETRY_STAGE`). If the second attempt also runs out of
    memory, or the failed attempt was already low-memory, or there is no low-memory profile
    (SVC), the job fails with `outOfMemory` and `OUT_OF_MEMORY_TEXT`.
  - Other failures: a `GeneratorError` keeps its code and message (cut to 500 characters). Any
    other exception is logged and reported as `engineError` with a generic text, so no internals
    or secrets reach the message. A failure after a cancel request ends as `cancelled`.
  - `idle()` unloads each distinct engine once.
- `scheduler.py`: `Scheduler(store, runner, clock)` runs one daemon thread over a FIFO queue.
  - Methods: `submit`, `resume(ids)`, `get` (returns the live state for the running job),
    `cancel`, `delete`, `queue_status()`, `run_next()`, `sweep()`, `start()`/`stop()`.
  - `delete` of the running job cancels it and removes it once it has finished.
  - The idle hook runs when the queue becomes empty.
  - `sweep()` uses 24 h retention on `updated_at`, under the lock.
  - The loop survives errors (they are logged).
  - `sweep_seconds` is a class attribute, so the constructor stays at 4 parameters (SIG).
- `reconcile.py`: `reconcile(store, clock) -> queued ids` sets `running`/`waitingForGpu`/
  `downloadingModel` jobs to `failed interrupted` (`INTERRUPTED_TEXT`). `queued` jobs stay
  queued and are returned oldest first, for `Scheduler.resume`.

Layer rule (quality-gate, merged): `devices` and `jobs` import only `ports`, `schemas`, `config`
and their own package. `jobs` does not import `audio`: handlers (US1) will get the audio
functions injected from `main.py`.

## Red -> green (TDD)

| Task | Red (failing tests) | Green |
|---|---|---|
| 009 T012 -> T013 | `af20de6` test(generator): failing probe and NVML tests | `50c3825` feat(generator): start-up GPU probe and NVML reader |
| 009 T014 -> T015 | `a555ccb` test(generator): failing GPU lease, gate, memory cap and OOM tests | `85fa8f9` feat(generator): cross-container GPU lease, free-memory gate, ... |
| 009 T016 -> T017 | `fb42b46` test(generator): failing job store, reporter, runner, scheduler and reconcile tests | `83f7e67` feat(generator): JSON job store, reporter, runner with OOM retry, ... |

Merge of `009-010-generative` (quality-gate + stem-separation): `d8180f0`.

## Files changed

- `generator/src/stemstudio_generator/devices/{__init__,probe,nvml,lease,gate,memory,oom,fake}.py`
- `generator/src/stemstudio_generator/jobs/{__init__,clock,store,progress,runner,scheduler,reconcile}.py`
- `generator/tests/fakes.py`, `generator/tests/devices/*`, `generator/tests/jobs/*`
- `docs/handoffs/generative-009-foundational-a.md`

## Tests and coverage

Windows (local), `cd generator; uv run --frozen pytest -q --cov --cov-branch`:

```text
211 passed
TOTAL  1189 stmts, 9 missed, 110 branches, 2 partial   -> line 99.24 %, branch 98.2 %, combined 99.15 %
devices/lease.py 86 % (Linux flock branch, lines 106-116, cannot run on Windows); all other new modules 100 % / 99 %
```

Linux (throw-away `ghcr.io/astral-sh/uv:python3.12-bookworm-slim` container, copy of `generator/`
and `specs/`, no GPU):

```text
211 passed in 2.12s
TOTAL  1189 stmts, 11 missed, 110 branches, 2 partial  -> combined 99.00 %
devices/lease.py 83 % (Windows msvcrt branch, lines 91-103); the flock path and the second-process test pass on Linux
mypy --strict: Success: no issues found in 41 source files
```

## Quality checks

- `ruff check .`: All checks passed. `ruff format --check .`: clean. `mypy src tests` (strict):
  no issues in 41 files (Windows and Linux).
- `tools/quality/sig_check.py generator/src`: 19 files, 163 units, 0 errors, 0 warnings.
- `tools/quality/dependency_check.py --root .` (after the merge): 0 errors, 0 warnings.

## Dependencies added

None (`nvidia-ml-py` was already a base dependency; torch is only reached through lazy imports).

## GPU measurements

None in this part. The probe, the gate and the cap were tested with fakes only. No container was
started with a GPU, and no `stemstudio` container was touched.

## Deviations and notes

- The reason texts are kept identical to the worker's, as the task asks. This includes
  `NO_CUDA_BUILD`, which says "This worker image ...". The contract's `NoGpuHealth` example shows
  a different text; it is only an example and is not validated against the probe.
- `ports.GpuGate.hold` has no `on_wait` lease timeout. The generator waits for the lease without
  a limit (ADR-018 D3: "it never runs without the lease"), and a cancel ends the wait. Only the
  free-memory wait has a limit (`gpuBusy`).
- A low-memory retry passes through the gate again, because the lease is released in between.
  The job stays `running`: waits during the retry change only the stage. Progress restarts at 0
  for the retry phase.
- `JobStore` port: data-model 2.3 names `list_active`. The port (Phase 0) has `list_all`, and the
  code follows the port.

## Open issues / requests

- None for other owners. US1 (T032-T036) starts after agent B's work (T018-T021, 010
  T010-T013) has been merged into `009-010-generative`.

## Next owner

generative agent A (this agent), after the CTO merges agent B: 009 T032 -> T036 (HO:us1), and so on.
