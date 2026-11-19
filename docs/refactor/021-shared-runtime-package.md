# 021 Extract a shared runtime package for the worker and generator
- Status: proposed · Date: 2026-11-19 · Area: worker + generator (devices, command runner, env parsing) · Priority: P2

## Problem statement
The two Python services carry copied infrastructure code. Measured with a normalised duplicate-window scan
(`.claude/scratchpad/main/dups.py`, 6 significant lines per window, vendor and imports excluded) over
`worker/src` and `generator/src`: 78 duplicate windows in total, of which 71 are cross-service:

| Copy A (worker) | Copy B (generator) | Duplicate windows |
|---|---|---|
| `stemstudio_worker/devices/probe.py:31` (186 lines) | `stemstudio_generator/devices/probe.py:31` (198 lines) | 39 |
| `stemstudio_worker/config.py:174` (env helpers) | `stemstudio_generator/config.py:107` | 15 |
| `stemstudio_worker/ingest/normalizer.py:38-81` (`CommandResult`, `CommandRunner`, `SubprocessRunner`) | `stemstudio_generator/audio/commands.py:9-50` | 15 (near-verbatim) |
| `stemstudio_worker/devices/nvml.py:36` | `stemstudio_generator/devices/nvml.py:38` | 2 |
| `stemstudio_worker/devices/lease.py` / `oom.py` | `stemstudio_generator/devices/lease.py` / `oom.py` | same structure, drifted (see the GPU policy proposal) |

`diff` of the two `probe.py` shows the same check order and the same user texts (`NO_CUDA_BUILD`,
`NOT_AVAILABLE`, `UNSUPPORTED`, `KERNEL_FAILED`, `NO_NVML`), differing only in `Final` annotations, docstrings
and `_MAX_ERROR/_MAX_NAME` vs `_MAX_TEXT`. Each copy has its own tests
(`worker/tests/devices/*`, `generator/tests/...`), so the duplication is in tests too.

## Impact
- SIG/Sigrid duplication and SonarQube duplicated-lines both count these blocks; every security or behaviour
  fix (subprocess handling, GPU probe texts that the UI shows, NVML errors) must be made twice, and has
  already drifted once (OOM chain, lease semantics).
- The probe texts are user-visible and "identical to the worker's" by design (generator `probe.py` docstring),
  which today is only enforced by copy-paste.

## Risk
- Leaving it: steady cost and divergence; low immediate exposure.
- Change: medium. The generator's base install deliberately has no torch and OSI-only licences (generator
  `pyproject.toml` comment, ADR-018 D9); the shared package must keep torch/pynvml imports lazy and add no
  dependencies. The two lock files (`worker/uv.lock`, `generator/uv.lock`) both need the path dependency;
  Docker build contexts must include the package. Rollback: keep the old modules as re-export shims until both
  services are migrated.

## Effort
M (4–6 days): new `packages/stemstudio-common` (or `python/common`) with uv path dependency, move four
modules, adjust two Dockerfiles and two CI jobs, keep shims one release.

## Step-by-step plan
1. Safety net: run both suites and note the coverage of `devices/*`, `ingest/normalizer.py`,
   `audio/commands.py`, `config.py` (both `fail_under = 90`).
2. Create the package (pure Python, no runtime dependencies, py.typed, same ruff/mypy settings) and move
   `CommandResult/CommandRunner/SubprocessRunner` first (verbatim copies, lowest risk). Keep the old import
   paths as re-exports. Check: both suites green.
3. Move the env-parsing helpers of both `config.py` files; keep each service's settings dataclass local.
4. Move NVML reader and the GPU probe with the shared texts, parameterised where the services differ (the
   generator always probes; the worker honours the `cpu` preference).
5. Move lease and OOM detection after the policy decision of the GPU-coordination proposal.
6. Update both Dockerfiles/CI to install the path package; build both images.
7. Remove the shims and the duplicated tests; re-run the duplication scan (target: no cross-service windows).
   Rollback: re-introduce the module from git history; the shims make every step independently revertible.

## Dependencies and related proposals
- Precedes step 5 of 011-gpu-coordination-policy; hosts the container sniffing of
  005-worker-ffmpeg-input-hardening. Coordinate with the CI/docker auditor (image build contexts).
