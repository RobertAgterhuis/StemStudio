# Hand-off: stem-separation / 003-us2

## What was built

US2 "Works everywhere, falls back automatically", worker side (T034, T035):
`worker/tests/api/test_device_fallback.py` drives the real `main` composition (fake ingest and
analysis, fake separator and fake detail models built on the device runner that `main` creates)
through the HTTP API:

- `STEMSTUDIO_FAKE_DEVICE=gpu-oom`: the standard job and the detail job both complete. The
  standard job's `device` is `{kind: cpu, cpuFallback: true, fallbackReason: "The GPU ran out of
  memory during the standard separation; the step was repeated on the CPU."}`. The detail job is
  `{kind: gpu, name: "Fake GPU", cpuFallback: true}` with a reason containing "ran out of memory"
  (its first step fell back, the later ones ran on the fake GPU). Both bodies conform to worker
  1.2.0 (SC-004).
- A failing probe (`STEMSTUDIO_DEVICE=cuda`, "No usable NVIDIA GPU ..."): `/health` reports
  `device: cuda`, `deviceInfo.kind: cpu` with that reason; both jobs report CPU without a fallback
  (FR-003).
- `STEMSTUDIO_DEVICE=cpu`: torch and NVML adapters are never created (a monkeypatched loader would
  raise); `/health` reports the CPU without a reason; both jobs report CPU without a fallback.

T034 commit: `1e8e080`. **The tests passed on their first run** against the T028 implementation
(`029e3c1`), which already contained the fake `gpu-oom` context reset on idle, the probe-reason
CPU runner and the shared runner. So **T035 needed no production change**, and there is no
separate red commit for this pair. The red -> green evidence for the code behind them is in the
foundational (T011-T016) and us1 (T023-T028) pairs.

## Files changed

- `worker/tests/api/test_device_fallback.py` (new)

## Tests and coverage

`uv run pytest -q --cov --cov-branch` (worker): **759 passed**, 3 deselected (integration);
3541 statements, 0 missed, 394 branches, 1 partial (`jobs/scheduler.py` thread-loop exit, as in
002) -> **line 100 %, branch 99.75 %**.

## Quality checks

`ruff check .`, `ruff format --check .` (146 files) and `mypy --strict src tests` (150 files) are
clean. `sig_check.py worker/src` reports 0 errors and 2 warnings, both pre-existing in other
owners' modules (`analysis/downbeats.py`, `ingest/spotify.py`).

## Dependencies added

None.

## Open issues / requests

None for US2.

## Next owner

stem-separation T045 (polish); qa runs the `@gpu` E2E specs against this behaviour in T051.
