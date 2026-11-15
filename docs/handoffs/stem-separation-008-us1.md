# Hand-off: stem-separation / 008 US1 (T038, T039, T040)

## What was built

- `api/transcription_routes.py` (worker 1.6.0): `POST /v1/transcription-jobs` (typed body:
  profile/input mismatch, grid, key -> 422; library path guard -> 400 `invalid-input-path` /
  404 `input-not-found`; a library that cannot be read now still queues the job, which then fails
  with that text; 202 + `Location`), `GET /{id}` (404), `DELETE /{id}` (204, idempotent; a running
  job stops at its next progress report and leaves no files), `GET /{id}/files/{role}` (file with
  its media type and `Content-Length`; 409 `transcription-not-completed` before completion; 404
  for unknown jobs, roles or vanished files).
- `jobs/transcription_service.TranscriptionService` on its own `transcribe` `LaneScheduler`
  (never waits for the priority scheduler or the render lane; tested with both blocked); the
  shared `jobs/lane_service.LaneJobService` base (render service refactored onto it).
- `jobs/transcription_pipeline`: stages `loading-model` 0-0.05, `transcribing` 0.05-0.80 (engine
  progress mapped), `notating` 0.80-0.90, `engraving` 0.90-1.0 (`stage` null while queued and after
  the end); NotationSettings with `rules.resolution` (notation `resolve_resolution`) and the input
  length (range or file); summary from the score document + engraver pages; job warnings
  `detail-bleed`, `cpu-piano`, `missing-drum-piece` via notation `score_warning`, at most 8 kept;
  device `cpu` for the CPU engines, the runner's device for the piano CRNN; "No notes were
  detected in this audio." / engraving texts as job errors; unexpected -> "Unexpected error".
  `TranscriptionParts {engines, notator, engraver, rules, library, font}` keeps `jobs` free of
  `transcription`/`notation`/`engraving` imports.
- `main.py`: `AppOverrides.{transcription_engines, notator, score_engraver}`;
  `default_transcription` = `transcription.create_transcription_engines(settings, devices,
  analysis.LibrosaDrumTranscriber())`, `notation.create_notator()`,
  `engraving.create_score_engraver()`, `SCORE_RULES` (profile ranges, `resolve_resolution`,
  `score_warning`), the render library reader, `engraving.fonts.leipzig_available`.
- `/health`: `transcription.models` (model store readiness), `transcription.font.leipzig`,
  `lanes.transcribe`.
- `jobs/transcription_defaults.py`: stand-ins for runtimes without transcription parts.
- T040 `tests/transcription/test_accuracy_integration.py` (`@pytest.mark.integration`) on the qa
  fixtures (`score-manifest.json`; generated audio), real basic-pitch + notation cleaning and
  sixteenth quantisation.

Red -> green: T038 `2ffaa1f` -> T039 `7ca9b1c` (+ `034f0e3` edge cases; the real
notation + engraving API test passes after the Verovio thread fix merged in `4527170`);
T040 `c92dfaa` (integration test, measured below).

## SC-002 measurement (T040)

```text
STEMSTUDIO_MODEL_DIR=<scratch> uv run pytest -m integration tests/transcription/test_accuracy_integration.py -s
score_bass_line_120bpm.wav: F1 1.000 (78 raw notes), 0.023 s per audio s      (target >= 0.90)
```

## Files changed

- `worker/src/stemstudio_worker/api/transcription_routes.py`,
  `jobs/{transcription_service,transcription_pipeline,transcription_defaults,lane_service}.py` (new)
- `worker/src/stemstudio_worker/{main,api/routes,jobs/runtime,jobs/render_service}.py`
- `worker/tests/api/{test_transcription_routes,score_fakes}.py`,
  `worker/tests/jobs/test_transcription_service.py`,
  `worker/tests/transcription/test_accuracy_integration.py` (new); `worker/tests/api/conftest.py`
  (fake transcriber and a temp model dir for API tests: no network)

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
2357 passed, 3 skipped (libcairo, engraving), 10 deselected
TOTAL 9419 stmts, 0 missed, 1070 branches, 0 partial -> 100 % line and branch
```

## Quality checks

ruff, ruff format --check, mypy --strict src: clean. mypy --strict tests: 10 errors, all in
`tests/engraving/{scores,test_fonts}.py` (audio-ingest, present on the branch before my merge).
`run_all.py`: 1018 files, **0 errors**, 256 warnings.

## Dependencies added

None.

## Open issues / requests

- **audio-ingest**: `mypy --strict tests` reports 10 errors in `tests/engraving/scores.py` and
  `tests/engraving/test_fonts.py` (music21 untyped calls, pypdf `PdfObject | None`).
- music21's "might need matplotlib" message: printed by `notation` imports (beat-analysis); the
  worker code I own never imports music21.
- While a piano job runs, `device` shows `cpu` (planned); the runner's real device replaces it
  when the job ends.

## Next owner

backend (008 worker client); stem-separation US3/US4 (done, see the next hand-offs).
