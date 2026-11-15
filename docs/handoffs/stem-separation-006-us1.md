# Hand-off: stem-separation / 006 US1 (T029, T030)

## What was built

- `api/lyrics_routes.py` (worker contract 1.5.0, in the 1.6.0 document):
  - `POST /v1/lyrics-jobs`: typed body `CreateLyricsJobRequest` (discriminated by `mode`; schema
    violations, control characters, bad language codes -> 422 `Validation failed`); 202 +
    `Location`. Align: unknown `standardJobId` -> 404, not completed -> 409
    `type: standard-job-not-completed`, vocal stem gone -> 409 `type: vocal-stem-missing`
    (detail = `VOCAL_STEM_MISSING_TEXT`).
  - `GET /v1/lyrics-jobs/{id}` (404 unknown), `DELETE` (204; 404 unknown, as the contract says;
    a running alignment stops at its next report), `GET .../result` (200 with
    `Cache-Control: no-store`; 409 `type: lyrics-not-completed` before completion; 404 when the
    result file vanished), `POST .../cancel` (200 cancelled; 409
    `type: lyrics-job-not-cancellable` for lookups and terminal jobs).
- `jobs/runtime.py`: `WorkerParts.lyrics: LyricsParts` (stand-ins by default),
  `Lanes.lyrics: LyricsService`, `LaneWiring`; lookups are queued on the metadata jobs' network
  lane (`MetadataLane.lane`), alignments on the scheduler (`SchedulerHooks.align`); the lyrics
  sweep joins the hourly sweeper; restart re-queues unfinished jobs.
- `jobs/lyrics_defaults.py`: `DisabledLyricsLookup` (outcome `disabled`), `UnavailableAligner`,
  `UnavailableVocals`, `CpuOnlyRunner` for runtimes built without the adapters.
- `jobs/job_records.py`: `JobRecordStore` (`unfinished`, `expired`) shared by the metadata and
  lyrics stores (removes a clone the quality gate found).
- `main.py`: `AppOverrides.{lyrics_lookup, aligner, vocal_loader}`; `default_lyrics(overrides,
  devices)` builds `LyricsParts` with the shared device runner.

Red -> green: T029 `70017d7` -> T030 `e11b719` (+ refactor `7e0f077`).

## Files changed

- `worker/src/stemstudio_worker/api/lyrics_routes.py`, `jobs/{lyrics_defaults,job_records}.py` (new)
- `worker/src/stemstudio_worker/{main,jobs/runtime,jobs/metadata_lane,jobs/metadata_store,jobs/lyrics_store}.py`
- `worker/tests/api/{test_lyrics_routes,lyrics_fakes}.py`, `worker/tests/jobs/test_lyrics_defaults.py` (new)
- `specs/006-synced-lyrics-karaoke/tasks.md` (T029, T030 ticked)

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
1788 passed, 7 deselected in 49.66s
TOTAL 7087 stmts, 0 missed, 804 branches, 0 partial -> 100 % line and branch (whole worker)
```

## Quality checks

ruff check / format --check clean; mypy --strict clean; `sig_check.py` (jobs, api, main.py):
46 files, 397 units, 0 errors, 0 warnings; `tools/quality/run_all.py` (after merging the
quality-gate 006/008 rules): 881 files, **0 errors**, 217 warnings.

## Dependencies added

None.

## Open issues / requests

- **Composition with the real lookup is pending the audio-ingest `lyrics/` merge**: `main.py`
  defaults to `DisabledLyricsLookup` until then; I will switch the default to the `lyrics`
  factory (real LRCLIB, or the `STEMSTUDIO_FAKE_LYRICS` provider with `separator=fake`) when it
  lands on `006-008-lyrics-sheet-music`. `test_the_default_app_runs_lookups_with_its_own_provider`
  holds for both.
- Aligned documents get `source: user` unless `refine` names the record; the backend keeps its
  own source for LRCLIB plain records.
- No `/health.lyricsAlignment` yet (006 T042/T043).

## Next owner

backend (worker client against the routes); stem-separation 006 US2 (T040-T043).
