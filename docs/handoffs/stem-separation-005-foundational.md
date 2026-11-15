# Hand-off: stem-separation / 005 foundational (T014, T015)

## What was built

- `jobs/lanes.py` `LaneScheduler(name, process)`: generic light lane, one FIFO daemon thread per
  lane, one job at a time, a crashing job is logged ("A job in the <name> lane crashed") and the
  lane continues; `start` (idempotent), `submit`, `join(timeout)`, `stop(timeout)`, `running`.
  Built generic on purpose so 007's render lane (007:T021/T022) reuses it.
- `jobs/metadata_store.py`: `StoredMetadataJob {job: MetadataJob, request}` in
  `metadata-jobs/<id>.json`; results in `metadata-jobs/<id>/result.json`, Cover Art Archive
  picture in `metadata-jobs/<id>/artwork/{cover,thumb}.jpg` (data-model.md 5);
  `save_findings`, `result`, `artwork`, `unfinished`, `expired(cutoff)`, `remove`.
- `jobs/metadata_lane.py`: `MetadataRunner` (queued -> running "Searching MusicBrainz" -> the
  lookup's own stages -> completed "Completed" | failed "Failed" with `METADATA_FAILED_TEXT`
  for `MetadataLookupError` and for unexpected errors, which are logged); `MetadataLane.start(paths,
  lookup, clock)` loads the store, puts `queued`/`running` jobs back to `queued` and re-runs them
  oldest first (ADR-014: idempotent, no partial state), then starts the `metadata` lane;
  `create`, `get`, `result`, `artwork`, `sweep()` (terminal jobs older than 24 h, with their
  files), `join`, `stop`.
- `jobs/sweeper.py` `Sweeper(interval, tasks)`: runs the tasks at start and then hourly
  (`SWEEP_INTERVAL_SECONDS`), a failing task is logged; wired by the composition root in US2.
- `jobs/artwork_files.py`: fixed file names `cover.jpg`/`thumb.jpg`, atomic `write_artwork`.
- `jobs/json_store.py` + `delete`; `jobs/paths.py` + `metadata_jobs`, `metadata_job_dir`,
  `metadata_result`, `metadata_artwork`.

Red -> green: T014 `7791d6f` -> T015 `01ad439`.

## Files changed

- `worker/src/stemstudio_worker/jobs/{lanes,sweeper,metadata_lane,metadata_store,artwork_files}.py` (new), `jobs/{json_store,paths}.py`
- `worker/tests/jobs/{test_metadata_lane,test_sweeper,metadata_fakes}.py` (new)
- `specs/005-artwork-band-info/tasks.md` (T014, T015 ticked)

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            904 passed, 3 deselected
TOTAL 4134 stmts, 0 missed, 440 branches, 1 partial (pre-existing scheduler.py 142->exit)
jobs + api + main.py: 1499 stmts, 0 missed, 188 branches, 1 partial -> line 100 %, branch 99.5 %
lanes.py, sweeper.py, metadata_lane.py, metadata_store.py, artwork_files.py: 100 % line and branch
```

## Quality checks

`ruff check` clean, `ruff format --check` clean, `mypy --strict` (160 files) clean;
`sig_check.py` on jobs/api/main + their tests: 0 errors, 19 warnings (all pre-existing test units).

## Dependencies added

None.

## Open issues / requests

- None yet; the lane is wired into the runtime with the routes (005:T036).

## Next owner

stem-separation continues with 005:T024/T025 (artwork step).
