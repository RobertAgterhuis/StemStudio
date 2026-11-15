# Hand-off: stem-separation / 007 US1 (T033, T034)

## What was built

- `api/render_routes.py` (contract 1.4.0):
  - `POST /v1/render-jobs`: typed body `RenderJobRequest` (schema violations -> 422
    `ValidationFailed` with `errors`, via the 005 handler); `RenderExecutor.validate` on a
    thread: `InputPathError` -> 400 `type: invalid-input-path`, `InputNotFoundError` -> 404
    `type: input-not-found` (detail names the path); any other `RenderError` (library not
    mounted) still queues the job, which then fails with `str(error)` when it runs; 202 +
    `Location`.
  - `GET /v1/render-jobs/{id}` (404 unknown), `DELETE` (always 204: removes JSON + output; a
    running render stops at its next progress report and leaves nothing behind),
    `GET .../file` (409 `type: render-not-completed` until completed, then the file with
    `RenderResult.contentType` and `Content-Length`; 404 when it vanished).
- `jobs/render_service.py` `RenderService`: own `render` lane (`LaneScheduler`), `queued ->
  running -> completed | failed | cancelled`, progress 0..1 non-decreasing (pass 1 / pass 2 as
  the executor reports), output in `render-jobs/<id>/` (named after the job id: the renderer's
  dither seed), partial output removed for every non-completed outcome; unexpected exceptions are
  logged and become "Unexpected error". Restart: `RenderJobStore.reconcile` at start-up; sweep:
  hourly with the other sweep tasks.
- `/health`: `library` = the shared `LibraryReader.status()` (checked on every call), `lanes.render`
  = `LaneScheduler.health()`; before the lifespan the 1.4.0 defaults are reported. The two
  `/health` literals (`tests/api/test_jobs_api.py`, `tests/test_smoke.py`) now run with a
  mounted library dir and expect `{"readable": true, "reason": null}`; a missing library gives
  `readable: false` + `LIBRARY_UNAVAILABLE_TEXT` (`test_render_routes.py`).
- `api/problems.py`: `ProblemKind` + `TypedProblemError` for problem `type`s; `problem_response`
  takes the type.
- `main.py`: `AppOverrides.{render_executor, library}`; default
  `library = render.create_library_reader(settings)`,
  `executor = render.create_render_executor(settings, library)` (one reader for `/health` and the
  executor), statically imported now that `render/` is merged. `jobs/runtime.RenderParts`
  (defaults `NoRenderer`/`NoLibrary` for runtimes built without it, e.g. unit tests);
  `Lanes.render`, `Lanes.library`.

Red -> green: T033 `7328168` -> T034 `efed344`.

## Files changed

- `worker/src/stemstudio_worker/{api/render_routes,jobs/render_service}.py` (new)
- `worker/src/stemstudio_worker/{api/problems,api/routes,jobs/runtime,main}.py`
- `worker/tests/api/{test_render_routes,render_fakes}.py`, `worker/tests/jobs/test_render_service.py` (new)
- `worker/tests/api/{conftest,test_jobs_api}.py`, `worker/tests/test_smoke.py` (`/health` literals, library dir)

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            1158 passed, 3 deselected (integration)
TOTAL 5087 stmts, 0 missed, 576 branches, 0 partial -> 100 % line and branch (whole worker)
jobs + api + main.py: 2108 stmts, 0 missed, 264 branches, 0 partial -> 100 % / 100 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (194 files) clean; sig_check (jobs, api, main +
tests): 0 errors, 19 warnings (pre-existing); `run_all.py` whole repo: **0 errors**, 168 warnings.

## Dependencies added

None.

## Open issues / requests

- None for 007. devops' mount check can rely on `/health.library.readable`.

## Next owner

stem-separation: 007:T065 (polish). backend consumes the render routes (007:T035 ...).
