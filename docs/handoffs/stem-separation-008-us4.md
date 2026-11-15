# Hand-off: stem-separation / 008 US4 (T059, T060)

## What was built

- Drums jobs: `drumInputs` required and `input` forbidden (schema validator -> 422); the pipeline
  resolves every given piece, runs the `DrumTranscriber` (real: `analysis.LibrosaDrumTranscriber`;
  fake stack: `FakeDrumTranscriber`, kick 1/3, snare 2/4, eighth hi-hats) with progress per piece
  mapped into `transcribing`, then `Notator.notate_drums`; one `missing-drum-piece` warning per
  absent piece ("No {piece} stem was available; that part is empty."), in the order kick, snare,
  toms, hihat, cymbals; `key` null, `attribution` null, `engine` `drum-onsets` (fake: `fake`).
- `FakeDrumTranscriber.engine` = `fake`.
- Polish in the same step: `jobs/lane_service.LaneJobService` (shared by render and
  transcription; removed four clones the gate reported).

Red -> green: T059 `499b8f5` (the engine assertion failed) -> T060 `46e6fe8`. Most of the drum
path existed from T039, so only the engine and the tests were new here.

## Files changed

- `worker/src/stemstudio_worker/transcription/fake.py`, `jobs/lane_service.py` (new),
  `jobs/{render_service,transcription_service}.py`
- `worker/tests/api/test_transcription_routes.py`

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
2357 passed, 3 skipped, 10 deselected; 100 % line and branch (9419 stmts, 1070 branches)
```

## Quality checks

ruff / format / mypy src clean; `run_all.py` 0 errors.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend/frontend (drums scores).
