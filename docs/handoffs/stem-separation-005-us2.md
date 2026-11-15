# Hand-off: stem-separation / 005 US2 (T035, T036)

## What was built

- `api/metadata_routes.py`: `POST /v1/metadata-jobs` (typed body `CreateMetadataJobRequest`,
  202 + `Location`), `GET /v1/metadata-jobs/{id}` (404), `GET .../result` (404, 409 "Job not
  completed" while queued/running/failed), `GET .../artwork/{variant}` (JPEG, `ETag` = quoted
  `result.artwork.sha256`; 404 without a cover, unknown variant or job). All responses validate
  against the 007 worker contract (which contains the 005 one).
- `api/problems.py`: `RequestValidationError` -> 422 problem+json `{"title": "Validation failed",
  "status": 422, "detail": "The request body is not valid.", "errors": {field: [messages]}}`;
  field paths drop the `body` prefix (`inputs.0.path`), a malformed body is reported under
  `body`. Installed once for the app; it only applies to typed-body routes (metadata jobs and,
  in 007, render jobs). The 1.0 routes (`/v1/jobs`, `/v1/detail-jobs`) keep their 400 from
  `parse_body`, as their contract says.
- `/health`: `onlineLookups` = `settings.online.lookups_enabled`.
- `jobs/runtime.py`: `OnlineParts.lookup`; `Lanes` (metadata lane + `Sweeper`, hourly and at
  start: `MetadataLane.sweep`) started with the runtime and stopped before the scheduler;
  `WorkerRuntime.lanes`. `jobs/metadata_lane.DisabledLookup` answers `disabled` without a request
  when no lookup is available.
- `main.py`: `AppOverrides.metadata_lookup`; default from
  `stemstudio_worker.metadata.create_metadata_lookup(settings)` via `optional_factory`.

Red -> green: T035 `593e44f` -> T036 `5964621`.

## Files changed

- `worker/src/stemstudio_worker/api/metadata_routes.py` (new), `api/{problems,routes}.py`, `jobs/{runtime,metadata_lane}.py`, `main.py`
- `worker/tests/api/test_metadata_routes.py` (new)

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            962 passed, 3 deselected
TOTAL 4389 stmts, 0 missed, 476 branches, 1 partial (pre-existing scheduler.py 142->exit)
jobs + api + main.py: 1754 stmts, 0 missed, 224 branches, 1 partial -> line 100 %, branch 99.6 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (167 files) clean; sig_check (jobs, api, main +
tests): 0 errors, 19 warnings (pre-existing).

## Dependencies added

None.

## Open issues / requests

- **audio-ingest**: factory `stemstudio_worker.metadata.create_metadata_lookup(settings) -> MetadataLookup`
  (one argument; build the normaliser inside, and the fake provider for
  `STEMSTUDIO_FAKE_ONLINE` with `separator=fake`). Lookups off must still be handled inside the
  lookup (outcome `disabled`, no request), as `ports.MetadataLookup` documents.

## Next owner

stem-separation continues with 005:T044/T045 (user picture upload).
