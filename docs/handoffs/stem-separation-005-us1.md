# Hand-off: stem-separation / 005 US1 (T024, T025)

## What was built

- **"Saving cover"** (`jobs/artwork.py`, `CoverStep`, `ArtworkCapture`): after the download
  (uploads: right away), before "Preparing audio"; state `downloading`, progress weight 0.
  Candidates: the source's own picture persisted with the queued job
  (`StoredJob.artwork_candidate`: Spotify cover URL, or an upload's embedded cover staged by
  `stage_upload_artwork` as `uploads/<jobId>/cover.bin`) and the picture the download reports
  (`FetchedAudio.artwork`, YouTube thumbnail). Tried in precedence order (user > spotify >
  embedded > coverartarchive > youtube), each with a 15 s budget on a daemon thread; any error
  or timeout is logged ("The <source> cover could not be saved") and the next candidate or no
  artwork follows; the job never fails because of a picture (FR-002).
- Files `jobs/<jobId>/artwork/{cover,thumb}.jpg`, `Job.artwork` set (`ArtworkImages.info`).
- **Cache**: the picture is kept with the cached result (`cache/by-audio/<sha>/artwork/` +
  `info.json`); a job answered from the source or audio cache without its own picture gets the
  cached one; a job with its own picture replaces the cached one.
- **Metadata**: `SourceInfo.{album, isrc, lookup_title, lookup_artist}` -> `SourceMetadata`
  (ISRC dropped unless it matches the contract pattern); YouTube downloads fill `album` and
  the lookup names from `FetchedAudio`; `durationSeconds` is set right after normalisation and
  also on cache hits (`rebind`). The worker does not invent lookup names: when ingest leaves
  them null they stay null (the backend falls back to title/artist).
- **Route** `GET /v1/jobs/{jobId}/artwork/{variant}` (`api/routes.py`, shared
  `api/responses.artwork_response`): `image/jpeg`, `ETag` = quoted cover SHA-256, 404 problem
  for unknown job/variant or no artwork.
- **Composition** (`main.py`): `create_app(settings, adapters, devices, overrides)` with
  `AppOverrides(detail_pipeline, image_normalizer, ...)` (replaces the old `detail_pipeline`
  keyword); `optional_factory(module, name)` looks the ingest factories up by name, because the
  `metadata` package is written in parallel: `stemstudio_worker.metadata.create_image_normalizer(settings)`.
  Without the package the worker starts without source pictures (logged); a present package
  that fails to import still raises.

Red -> green: T024 `033fc44` + `92fd3ee` (composition) -> T025 `4dcc3a4`.

## Files changed

- `worker/src/stemstudio_worker/jobs/{artwork}.py` (new), `jobs/{runner,progress,models,cache,service,paths,runtime}.py`
- `worker/src/stemstudio_worker/api/{routes,responses,uploads}.py`, `main.py`
- `worker/tests/jobs/{test_artwork_step,artwork_fakes}.py`, `worker/tests/api/{test_artwork_routes,test_composition}.py` (new)
- Updated literals for the new `durationSeconds`: `tests/jobs/test_runner.py` (4),
  `tests/api/test_jobs_api.py` (1); `tests/api/test_device_health.py` uses `AppOverrides`;
  `tests/{jobs,api}/fakes.py`, `tests/api/conftest.py` (artwork, overrides).

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            941 passed, 3 deselected
TOTAL 4312 stmts, 0 missed, 470 branches, 1 partial (pre-existing scheduler.py 142->exit)
jobs + api + main.py: 1677 stmts, 0 missed, 218 branches, 1 partial -> line 100 %, branch 99.5 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (165 files) clean; sig_check (jobs, api, main
+ tests): 0 errors, 19 warnings (all pre-existing).

## Dependencies added

None.

## Open issues / requests

- **audio-ingest**: please expose `stemstudio_worker.metadata.create_image_normalizer(settings) -> ImageNormalizer`
  (fake mode inside it for `STEMSTUDIO_FAKE_ONLINE` + `separator=fake`), and hand embedded
  upload covers over as `SourceInfo.artwork` with `data` set.
- **CTO (integration)**: after the merge `optional_factory` finds the real package; the
  integration smoke should check `/v1/jobs/{id}` has `artwork` on the fake stack.

## Next owner

stem-separation continues with 005:T035/T036 (metadata routes).
