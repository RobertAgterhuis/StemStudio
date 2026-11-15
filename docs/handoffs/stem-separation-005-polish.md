# Hand-off: stem-separation / 005 polish (T052)

## What was built

- `jobs/threads.py` `QueueThread`: the start / join-until-idle / stop life cycle of a daemon
  queue thread, now shared by `PriorityScheduler` and `LaneScheduler` (removes the three
  9-line clones `run_all.py` reported between `lanes.py` and `scheduler.py`). Behaviour unchanged
  (all scheduler and lane tests pass unmodified).
- The last partial branch of the worker (`scheduler._drop` guard, pre-existing since 002) is
  covered: the worker is at 100 % line and branch.
- Docstrings on every public module, class and function of `jobs/`, `api/`, `main.py` (ruff
  pydocstyle rules clean).

Commit: `8398b56`.

## Summary of feature 005 (stem-separation)

| Task | Red | Green |
|---|---|---|
| T014/T015 metadata lane, store, sweep | `7791d6f` | `01ad439` |
| T024/T025 Saving cover, job artwork route, metadata fields, composition | `033fc44`, `92fd3ee` | `4dcc3a4` |
| T035/T036 metadata routes, 422 handler, onlineLookups | `593e44f` | `5964621` |
| T044/T045 user picture upload | `23733fa` | `bba1f2d` |
| T052 polish | - | `8398b56` |

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            986 passed, 3 deselected (integration)
TOTAL 4506 stmts, 0 missed, 494 branches, 0 partial -> 100 % line, 100 % branch
jobs + api + main.py: 1871 stmts, 0 missed, 242 branches, 0 partial -> 100 % / 100 %
```

## Quality checks

- ruff check, ruff format --check, mypy --strict (173 files): clean.
- `sig_check.py` (jobs, api, main + tests): 0 errors, 19 warnings (all pre-existing test units).
- `run_all.py` (whole repo): 0 errors in stem-separation paths; the one remaining error is a
  clone between `e2e/support/preferences.ts` and `src/web-interop/src/types.ts` (not mine).

## Dependencies added

None.

## Open issues / requests

- audio-ingest: `metadata.create_image_normalizer(settings)` and `metadata.create_metadata_lookup(settings)`
  (see us1/us2 hand-offs); composed through `main.optional_factory`.
- architect: 503 for `POST /v1/artwork` without a normaliser (us3 hand-off).

## Next owner

stem-separation continues with feature 007 (T021 ...); CTO for the 005 integration.
