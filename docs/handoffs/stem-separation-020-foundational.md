# Hand-off: stem-separation / 020 foundational (T016, T017)

Lane WORKER-IO, branch `020-lane-worker-io` (from `ca82a055`).

## What was built

- `jobs/insights_lane.py`: the `insights` lane (data-model 2.1). One thread, FIFO across quality
  and handout jobs, every job inside a BLAS thread cap (`ThreadLimit(threads, cap)`, default cap
  `blas_threads` = `threadpoolctl.threadpool_limits(limits=n, user_api="blas")`, a no-op when
  threadpoolctl cannot be imported). `InsightsStores` (load, reconcile, sweep of both stores),
  `InsightsLane.loaded(paths, clock, limit)` fails interrupted jobs at start-up.
- `jobs/quality_store.py`, `jobs/handout_store.py`: `StoredQualityJob` / `StoredHandoutJob`
  (`{job, request}`) over the existing `LaneJobStore` (restart -> `failed` "interrupted by
  restart" with `stage` cleared, 24 h sweep of terminal jobs, idempotent delete with files).
  `HandoutJobStore.pdf_path(id)` = `handout-jobs/<id>/handout.pdf`.
- `jobs/paths.py`: `DataPaths.quality_jobs` (`quality-jobs/`), `DataPaths.handout_jobs`
  (`handout-jobs/`).
- Refactor: `jobs/lanes.py::SharedLane` (start/stop/join/health of a lane several job kinds share);
  `PracticeLane` (014) and `InsightsLane` both use it (removed a 14-line production clone).

## Commits

| Task | Red | Green |
|---|---|---|
| T016 / T017 | 808e517e | 5503ce6a |
| refactor (clone, parameter limit) | - | 68658fd8 |

## Tests and coverage (Windows host, `pytest tests/jobs tests/api --cov=stemstudio_worker.jobs --cov-branch`)

| Module | Line | Branch |
|---|---|---|
| `jobs/insights_lane.py` | 100 % (63) | 100 % |
| `jobs/quality_store.py` | 100 % | 100 % |
| `jobs/handout_store.py` | 100 % | 100 % |
| `jobs/lanes.py` (changed) | 100 % | 100 % (4) |
| `jobs/practice_lane.py` (changed) | 100 % | 100 % (2) |

675 passed (tests/jobs + tests/api). New tests: `tests/jobs/test_insights_lane.py`,
`tests/jobs/test_insights_stores.py`, requests from the contract examples
(`tests/jobs/insights_requests.py`, `QualityRequest` / `BassHandoutRequest`).

## Quality checks

`ruff check`, `ruff format --check`, `mypy --strict src tests/jobs`: clean.
`tools/quality/run_all.py worker/src/stemstudio_worker/{jobs,handouts,api,main.py} worker/tests/{jobs,handouts}`:
PASS, 0 errors (jscpd output read: the remaining test clone is the `Kinds` helper shared in spirit
with `test_practice_lane_store.py`, a warning).

## Decisions

1. Thread cap through `threadpoolctl` (already locked, transitive via scikit-learn/librosa); not
   a declared dependency of `worker/pyproject.toml` (architect-owned). **Request to architect**:
   declare `threadpoolctl==3.8.0` (BSD-3-Clause) directly, or accept the transitive use (the cap
   degrades to a no-op if it disappears).

## Deviations

None.

## Next

US1 (T040/T041): quality routes, service, composition and `Health.insights`.
