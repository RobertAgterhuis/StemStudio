# Hand-off: stem-separation / 007 foundational (T021-T024)

## What was built

- Merged `005-007-artwork-export` (6a087a9: audio-ingest's `render/` package, devops wave)
  into this branch (`3044346`), so the composition can import the real render factories.
- `jobs/lanes.py`: the render lane reuses the generic `LaneScheduler` built in 005 (no second
  scheduler); new `LaneScheduler.health() -> LaneHealth(queued, running)` for `/health`.
- `jobs/render_store.py`: `StoredRenderJob {job: RenderJob, request: RenderJobRequest}` in
  `render-jobs/<id>.json`; output dir `render-jobs/<id>/` (named after the job id, as the
  renderer's dither seed requires); `reconcile(clock)` fails `queued`/`running` jobs with
  "interrupted by restart" and removes their output; `remove(id)` (idempotent, ignores invalid
  ids, so no path is built from them); `sweep(now)` removes terminal jobs older than 24 h.
- `jobs/paths.py` + `render_jobs`.

Red -> green: T021 `ff1d5a8` -> T022 `5ea32b0`; T023 `595b995` -> T024 `1180fe1`.
Note for T021: the FIFO, crash isolation and "a render starts while a separation blocks the
`PriorityScheduler`" cases passed at once (the lane was built generic in 005:T015, as the
architect asked); the red part was the queue health.

## Files changed

- `worker/src/stemstudio_worker/jobs/{render_store}.py` (new), `jobs/{lanes,paths}.py`
- `worker/tests/jobs/{test_lanes,test_render_store}.py` (new)
- `specs/007-export-library/tasks.md` (T021-T024 ticked)

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            1125 passed, 3 deselected (incl. the merged render tests)
TOTAL 4896 stmts, 0 missed, 560 branches, 0 partial -> 100 % line and branch
jobs + api + main.py: 1917 stmts, 0 missed, 248 branches, 0 partial -> 100 % / 100 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (189 files) clean; sig_check (jobs, api, main +
tests): 0 errors, 19 warnings (pre-existing).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

stem-separation continues with 007:T033/T034 (render routes, `/health` library and lanes).
