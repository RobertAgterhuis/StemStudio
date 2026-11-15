# Hand-off: stem-separation / 002-foundational

## What was built

- `detail/names.py`: standard display order, splittable parents, detail names per parent, display
  order with `rest` last.
- `detail/gating.py`: mono frame levels (2048/512, cumulative-sum RMS, -160 dB floor), parent gate
  (>= 8 % of frames above -40 dBFS), sub-stem gate (>= -20 dB energy and >= 5 % of frames above
  max(-50 dBFS, parent level - 25 dB)); all research.md D-04 constants named.
- `detail/folding.py`: fold tables DrumSep 6 -> 5 (`hh -> hihat`, `ride + crash -> cymbals`),
  KARA 2 (`lead`, `backing`), Mega 53 -> 8 families (one umbrella output each, D-05);
  `MEGA_OUTPUTS` for family accumulation inside `demix`.
- `detail/reassign.py`: STFT power-mask re-assignment (numpy Hann STFT, n_fft 4096, hop 1024, per
  channel); the last parent receives the remainder, so shares sum to the family exactly; silent
  parents share equally; a single parent receives the family unchanged.
- `detail/checks.py` + `detail/audio_io.py`: 16-bit quantisation, `rest = parent - sum(kept)` on
  the PCM grid, model residual, double count, `pre_write_outcome` in D-04 order (policy per model:
  residual check for partitioning models, double-count check for overlapping family models),
  reconstruction check of the written FLACs.
- `detail/registry.py` (three `ModelSpec`s, D-07 URLs/sizes/SHA-256, `SEPARATOR_VERSION = "1"`)
  and `detail/download.py` (`<model_dir>/detail/<id>/<file>.part`, streamed SHA-256, rename;
  fixed user texts for offline and corrupt downloads).
- `jobs/json_store.py` (generic JSON record store; `JobStore` now builds on it),
  `jobs/detail_store.py` (`StoredDetailJob` with `cancel_requested`, atomic `update`,
  `find_reusable` per `audioSha256`), `jobs/detail_service.py` (create idempotent for
  queued/running/completed, new job after failed/cancelled, 404 unknown standard job, 409 not
  completed or stems missing; cancel queued/running -> `cancelled` + flag, cancelled -> no-op,
  completed/failed -> 409), `jobs/detail_texts.py`, `DataPaths` detail locations.
- `jobs/scheduler.py` `PriorityScheduler`: one thread; standard FIFO always before detail units;
  detail units of the oldest active job first; `stop()` waits for the current unit; a crashing
  unit is logged and dropped. `jobs/runtime.py` uses it instead of `JobQueue` (removed; its three
  tests now run against the scheduler).

## Red -> green commits

| Tasks | Red | Green |
|---|---|---|
| T012 / T013 gating | `830b86f` | `57c2357` |
| T014 / T015 folding, re-assignment | `f62a13c` | `0b96c28` |
| T016 / T017 checks | `75ace8f` | `c121f9d` |
| T018 / T019 registry, download | `2b91903` | `dcfa25d` |
| T020 / T021 detail store, service | `a559d0f` | `c53696e` |
| T022 / T023 scheduler, runtime switch | `14d49d5` | `76952b1` |
| SIG refactor (checks, download, reassign) | - | `18bc703` |

## Files changed

- `worker/src/stemstudio_worker/detail/{names,gating,folding,reassign,checks,audio_io,registry,download}.py`
- `worker/src/stemstudio_worker/jobs/{json_store,detail_store,detail_service,detail_texts,scheduler}.py` (new),
  `jobs/{store,paths,runtime}.py` (changed), `jobs/queue.py` (removed)
- `worker/tests/detail/{signals,test_gating,test_folding,test_reassign,test_checks,test_download}.py`
- `worker/tests/jobs/{detail_env,test_detail_store,test_detail_service,test_scheduler}.py` (new),
  `tests/jobs/test_runner.py`, `tests/api/{conftest,test_app}.py` (`runtime.scheduler`)

## Tests and coverage

```text
cd worker && uv run pytest --cov
535 passed, 2 deselected; total line 99.59 % (2190/2199), branch 98.41 % (248/252)
stem-separation component (detail, jobs, api, separation, main): line 99.31 % (1301/1310),
branch 97.30 % (144/148). Uncovered so far: detail_service result/cancel race branches,
detail_store.update unknown id, DataPaths detail paths, runtime placeholder - all exercised by
the US1 runner and API tasks.
```

## Quality checks

```text
uv run ruff check src tests      All checks passed!
uv run ruff format --check       unchanged
uv run mypy src tests (strict)   Success: no issues found
uv run --project tools/quality python tools/quality/run_all.py
  quality gate: 388 files, 2815 units, 17 clones, 1 exemptions, 0 errors, 111 warnings -> exit 0
  worker only: 0 errors; no warnings in the new modules (pre-existing: reconcile, runner.process,
  downbeats, spotify). "detail is not in the dependency map" is quality-gate T007.
```

## Dependencies added

None.

## Open issues / requests

- quality-gate (T007): add `detail` to the dependency map (allowed: `ports`, `schemas`, `config`,
  own `vendor`); the new `jobs/*` modules import only `jobs`, `ports`, `schemas`, `config`.

## Next owner

stem-separation continues with US1 (T040-T050).
