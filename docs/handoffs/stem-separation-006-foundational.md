# Hand-off: stem-separation / 006 foundational (T017, T018)

## What was built

- `jobs/scheduler.py`: third queue for lyrics alignments. Order per unit: waiting standard jobs >
  queued alignments > the next detail unit (plan, one parent step, finalise); a running unit is
  never pre-empted (a standard job submitted during an alignment runs right after it). The align
  unit is `SchedulerHooks.align` (default: drop), `submit_align(id)`, `pending_alignments`; the
  idle hook (GPU release) waits until all three queues are empty.
- `jobs/lanes.py`: `LaneScheduler.submit_call(process, job_id)` so another job kind can share a
  lane (lyrics lookups on the 005 network lane next to the metadata jobs).
- `jobs/paths.py`: `lyrics_jobs`, `lyrics_job_dir(id)`, `lyrics_result(id)`.
- `jobs/lyrics_store.py`: `StoredLyricsJob {job, request (discriminated by mode)}`,
  `LyricsJobStore` (`lyrics-jobs/<id>.json` + `<id>/result.json`, `unfinished`, `expired`,
  `remove`), `MAX_AGE` 24 h.
- `jobs/lyrics_runner.py`: `LyricsParts {lookup, aligner, vocals: VocalLoader, devices:
  DeviceRunner, model_cached}`, `LyricsContext`, `LyricsJobs` (locked changes of live jobs only:
  a terminal job never changes, so a cancel or delete always wins over a late result),
  `LyricsRunner.lookup(id)` / `.align(id)`. Alignment: vocal stem = `vocals.lead` detail stem when
  cached, else `vocals` (`vocal_stem`), `VocalLoader.load` (silent stem -> its fixed text, no
  device), then `DeviceRunner.run(work, WHISPER_GPU_NEED)`; the job device is set when the step
  starts (`cuda` -> gpu + GPU name) and folded with `merge_device` afterwards (CPU fallback
  reason kept). Progress never decreases; the aligner's stage text is shown; a progress report on
  a cancelled/deleted job raises `AlignmentCancelledError`. Errors: `LyricsError` -> its fixed
  text, anything else -> `LYRICS_UNREACHABLE_TEXT` / `ALIGNMENT_FAILED_TEXT` with only the
  exception **type** logged (never the message: it might hold lyrics). Source of an aligned
  document: the `refine` document's `source/sourceId/sourceDurationSeconds`, else `user`.
- `jobs/lyrics_lane.py`: `LyricsService` (`create`, `get`, `result`, `cancel`, `delete`,
  `sweep`, `start` = restart: queued/running jobs reset to `Queued`/0 and queued again by mode),
  `LyricsQueues {lookup, align}`, `LyricsConflictError(kind)` with kinds
  `standard-job-not-completed`, `vocal-stem-missing`, `lyrics-job-not-cancellable`,
  `lyrics-not-completed`; unknown ids raise the existing `UnknownJobError`. Align create checks the
  standard job (404/409) and the vocal stem (409) and sets `estimatedSeconds` from
  `estimate_alignment_seconds(duration, device.kind, model_cached=...)`.

Red -> green: T017 `001fb31` -> T018 `5416e78`.

## Files changed

- `worker/src/stemstudio_worker/jobs/{lyrics_store,lyrics_runner,lyrics_lane}.py` (new)
- `worker/src/stemstudio_worker/jobs/{scheduler,lanes,paths}.py`
- `worker/tests/jobs/{test_lyrics_jobs,lyrics_fakes}.py` (new), `worker/tests/jobs/test_scheduler.py`
- `specs/006-synced-lyrics-karaoke/tasks.md` (T017, T018 ticked)

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
1769 passed, 7 deselected in 53.18s
TOTAL 6980 stmts, 0 missed, 804 branches, 0 partial -> 100 % line and branch (whole worker)
tests/jobs only: lyrics_store / lyrics_runner / lyrics_lane / scheduler / lanes 100 % / 100 %
```

## Quality checks

ruff check, ruff format --check: clean; mypy --strict (241 files): clean;
`tools/quality/sig_check.py worker/src/stemstudio_worker/jobs`: 32 files, 307 units, 0 errors,
0 warnings (test units: 4 new unit-size warnings in `test_lyrics_jobs.py`, like the existing
test files; warnings only).

## Dependencies added

None.

## Open issues / requests

- Not wired into the runtime/HTTP yet (006 T029/T030, next).
- `alignment/` stem choice lives in `jobs/lyrics_runner.vocal_stem` (it needs the worker cache
  layout, which `alignment` must not import); `alignment/audio_prep` will only load/resample.

## Next owner

stem-separation (006 T029/T030); backend can build against the worker contract as before.
