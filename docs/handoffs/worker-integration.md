# Hand-off: stem-separation (worker integrator) / worker-integration

This closes audio-ingest-us1 open issues 2 (real titles) and 3 (upload status mapping), and adds a
streamed 413 for uploads that have no `Content-Length`. The CTO authorised edits to `ports.py`,
`ingest/**` and `worker/tests/**` for this task only.

## What was built
1. **Real titles.** `AudioFetcher.fetch` now returns `FetchedAudio(path, title, artist)`.
   `YtDlpFetcher` fills them from the yt-dlp info dict it already extracts: `title`, plus
   `artist`, else `uploader`, else `channel`, all stripped and cut to 500 characters. After the
   download the runner calls `fetched_metadata(job, fetched)` (jobs/models.py):
   - YouTube jobs get the downloaded title and artist, saved right away. So `GET /v1/jobs/{id}`
     shows the real title while the job is still running, and also when a later stage fails.
   - Spotify jobs keep their spotDL metadata, which is authoritative (US1-3). Upload jobs are
     unchanged.
   `JobTracker.complete` now copies the result metadata into the job, so the job and the result
   always match. `rebind` (cache.py): when a YouTube job is completed from a cached result for
   the same source key, that result's downloaded title replaces the placeholder.
2. **Upload error classes** in ports.py: `EmptyUploadError`, `UploadTooLargeError` and
   `UnsupportedMediaError`, all subclasses of `InvalidSourceError`. `FileUploadIntake` raises
   them, and `api/uploads.py` maps them to 400 / 413 / 415. A plain `InvalidSourceError` maps
   to 400 "Invalid source" (before, it was 415).
3. **Streamed 413.** `create_from_upload` no longer calls `request.form()`. It feeds Starlette's
   `MultiPartParser` from `_limited(request.stream(), limit)`, which raises 413 once the body
   passes `limit + 64 KiB`. The exact per-file limit is still checked while copying the file.
   Malformed multipart returns 400. The `Content-Length` pre-check stays.
4. The `ingest/text.py` helper `clean_text` is now shared by spotify.py and fetcher.py. The
   real-Demucs `integration` test is unchanged, and no model tests were added.

## Files changed
- `worker/src/stemstudio_worker/ports.py`: `FetchedAudio`, the 3 upload errors, `fetch` return type.
- `worker/src/stemstudio_worker/ingest/{fetcher.py,upload.py,spotify.py,resolver.py,text.py (new)}`
- `worker/src/stemstudio_worker/jobs/{models.py,progress.py,runner.py,cache.py}`
- `worker/src/stemstudio_worker/api/uploads.py`
- `worker/tests/{test_ports.py,api/fakes.py,api/test_jobs_api.py,ingest/test_fetcher.py,
  ingest/test_upload_intake.py,jobs/fakes.py,jobs/test_runner.py,jobs/test_cache.py}`

New tests:
- fetcher: title/artist, uploader and channel fallbacks, unusable titles, clipping.
- runner: YouTube title adopted, title kept on a later failure, clipping, Spotify
  authoritative, no title means no change, job without metadata.
- cache: a source hit reuses the downloaded title; Spotify metadata is kept on a source hit.
- API contract: intake errors map to 400/413/415/400, empty file returns 400, `.ogg` returns
  415, malformed multipart returns 400. A chunked body with no Content-Length returns 413 after
  fewer than half of its chunks are read, driven over raw ASGI because TestClient buffers
  bodies. YouTube job and result carry the downloaded title; Spotify keeps its title.

## Tests and coverage
`cd worker && uv run pytest -q --cov=stemstudio_worker --cov-branch`
```
TOTAL                                                   1591      0    188      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
413 passed, 2 deselected in 11.86s
```
That is 100 % line (1591 statements) and 100 % branch (188 branches) for the whole worker. The
2 deselected tests are the `integration` markers (real Demucs, real network).

## Quality checks
- `uv run ruff check`: All checks passed! `uv run ruff format --check`: 75 files already
  formatted. `uv run mypy --strict src tests`: Success: no issues found in 74 source files.
- `uv run --project tools/quality python tools/quality/sig_check.py worker`:
  `74 files, 568 units, 0 errors, 18 warnings`. The warnings are older: test unit sizes, plus
  CC 6-7 in `reconcile`, `JobRunner.process` and `detect_meter`.
- `uv run --project tools/quality python tools/quality/run_all.py`:
  `337 files, 2479 units, 15 clones, 2 errors, 120 warnings -> exit 1`. Both errors are outside
  the worker and existed before this change:
  - `tests/StemStudio.Api.Tests/TestSupport/ListLoggerProvider.cs:29` `ListLogger::Log<TState>`
    has 5 parameters. It comes from commit 200d4ff (T111). This is an `ILogger.Log` override, so
    the signature is fixed by the interface, which points to a gate exemption.
  - `tests/StemStudio.Domain.Tests/Mixes/MixTests.cs:143` `Validate_RejectsMetronomeOutOfRange`
    has 5 parameters. It comes from commit 871b516 (T027).
  The gate does not report zero errors because of these two; this task did not have permission
  to edit them.

Live check (`STEMSTUDIO_SEPARATOR=fake`, uvicorn on 127.0.0.1:18765, curl):
```
upload tone.wav  -> 202  (job later "completed", title "tone")
empty.mp3        -> 400  {"title":"Invalid request","detail":"The file is empty."}
song.ogg         -> 415  {"title":"Unsupported file type",...}
250 MiB chunked, no Content-Length -> 413 "File too large", sent=210554316 bytes (aborted at limit)
```
No upload directories were left behind.

## Dependencies added
None. The upload route now uses `starlette.formparsers.MultiPartParser` directly (Starlette
1.10.0 and python-multipart 0.0.33 were already dependencies).

## Open issues / requests
1. **CTO / backend / quality-gate:** the 2 parameter errors above. One option is a gate
   exemption for interface overrides such as `ILogger.Log`; the other is a refactor of the
   `MixTests` theory to 4 parameters (backend-owned).
2. **Contract note (architect):** a plain `InvalidSourceError` from the intake now maps to 400,
   not 415. The contract's 400 ("invalid or unsupported source") already covers this, so the
   contract does not need to change.
3. **Backend:** the job's `metadata.title` can change while the job is running (from
   `YouTube video <id>` to the real title). If the backend copies the title only when the job
   is created, it should copy it again on sync or on completion.
4. `MultiPartParser` is imported from `starlette.formparsers`, which is not re-exported at the
   top level. If Starlette is upgraded, check that the import still works; the contract tests
   cover this.

## Next owner
CTO (merge), then backend for open issue 3.
