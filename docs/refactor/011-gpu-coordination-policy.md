# 011 Align the cross-container GPU lease and memory policy between worker and generator
- Status: proposed · Date: 2026-11-19 · Area: worker/devices + generator/devices (GPU lease, NVML, OOM) · Priority: P2

## Problem statement
The worker (GPU image), generator and singer share one GPU through a lock file on the `gpu-lease` volume
(`docker-compose.generative.yml:74,82,118`, ADR-018 D3). Both sides implement the protocol independently and
the two copies have drifted on the behaviour that matters most for a shared GPU:

| Concern | Worker | Generator |
|---|---|---|
| Lease file cannot be opened | **fail-open**: logs and grants the GPU without the lease (`worker/src/stemstudio_worker/devices/lease.py:158-166,177-182`) | fail-closed: `os.open` raises (`generator/.../devices/lease.py:68-69`) |
| Wait for the lease | bounded by `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS` (900 s), then CPU (`worker/.../lease.py:184-193`) | **unbounded**: `while not _try_lock(fd)` loops until cancelled (`generator/.../lease.py:79-82`); the gate's `wait_seconds` only bounds the free-memory wait (`devices/gate.py:104-112,133-137`) |
| NVML unreadable | fail-open: runs on `cuda` without a memory check (`worker/.../devices/runner.py:148-152,175-180`) | fail-closed: `GpuRequiredError(NO_NVML)` (`generator/.../devices/gate.py:127-130`) |
| OOM detection, cause chain | follows `__context__` even when suppressed (`worker/.../devices/oom.py:36`) | honours `__suppress_context__` (`generator/.../devices/oom.py:36`) |
| OOM markers | "out of memory", "failed to allocate memory" | adds "insufficient free vram" |
| Lock file open mode / dir | `O_RDONLY`, no `mkdir` | `O_RDWR`, `mkdir(parents=True)` |

Duplication measurement (`.claude/scratchpad/main/dups.py`, 6-line normalised windows over `worker/src` and
`generator/src`, vendor excluded): 39 duplicate windows between the two `devices/probe.py`, 2 between the two
`devices/nvml.py`, 1 between the two `devices/lease.py`. Neither side has a test that runs both
implementations against the same lock file.

## Impact
- A generator job can sit in `waitingForGpu` forever when a worker process hangs while holding the lock
  (the kernel only releases it on process exit); the user sees no terminal state until they cancel.
- A worker that cannot open the lease file (permissions, volume not mounted) silently runs GPU steps in
  parallel with ACE-Step/SoulX: the likely result is CUDA OOM on one side, misreported or retried on CPU.
- The suppressed-context difference can classify a non-OOM error as OOM in the worker, turning a real
  defect into a silent CPU fallback.
- Every change to the protocol must be made twice by two teams (worker lanes, generative lane).

## Risk
- Leaving it: medium — only bites with the optional generator stack, but then on every long job overlap.
- Change: medium. Fail-closed in the worker changes behaviour for users without a mounted lease volume;
  keep `NoFileLease` for the unset variable (`STEMSTUDIO_GPU_LEASE_FILE` absent) so the default stack is
  unaffected. Rollback per step.

## Effort
M (3–5 days): decide the policy (ADR-018 amendment), adjust both lease/gate implementations, add a
cross-implementation contract test.

## Step-by-step plan
1. Safety net: a contract test (Linux CI, `fcntl`) that opens the same temp lock file with the worker's
   `FileGpuLease` and the generator's `FileLease` and asserts mutual exclusion in both directions; pin the
   current OOM classification with a table of exception chains for both sides.
2. Write the policy down (ADR-018 amendment): lease wait bound for every holder, fail-open or fail-closed on
   an unopenable lease file and on NVML errors, the OOM marker list.
3. Generator: bound the lease wait (`STEMSTUDIO_GPU_LEASE_WAIT_SECONDS`, same default as the worker) and fail
   with `gpuBusy` when it runs out; test with the fake clock.
4. Worker: on an unopenable configured lease file fail the GPU attempt to CPU with a reason
   (instead of granting the GPU); honour `__suppress_context__`; adopt the shared marker list.
5. Move the protocol pieces into one module (see the shared-runtime proposal) so the two sides cannot drift
   again.
6. Verify: contract test and both suites green; manual run of the generative compose stack with a long
   Demucs job and a concurrent generation. Rollback: revert per service.

## Dependencies and related proposals
- Builds on 021-shared-runtime-package (step 5); can start before it.
