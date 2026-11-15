# Hand-off: stem-separation / 008 foundational (T022-T029)

## What was built

- `transcription/` (new package, composed only by `main.py`):
  - `basic_pitch.BasicPitchTranscriber` (`engine` basic-pitch): audio -> 22,050 Hz mono (soxr),
    basic-pitch 0.4.0 windowing (2 s windows, 30 overlapping frames, half an overlap of zeros in
    front), ONNX session on `CPUExecutionProvider` with `intra_op_num_threads =
    transcribe_threads` (created once), outputs stitched and decoded with onset 0.6 / frame 0.4 /
    127.7 ms; progress per window; model via the model store; load/run failures ->
    `TranscriptionError("The transcription model could not be run.")`.
  - `piano.PianoTranscriber` (`engine` piano-crnn, `attribution` = `PIANO_ATTRIBUTION`): 16 kHz
    mono, 10 s half-overlapping segments (upstream `enframe`/`deframe`), progress per segment,
    `DeviceRunner.run(work, PIANO_GPU_NEED)` (fallback reason in `Transcription.device`; model
    errors propagate for the OOM retry), models cached per device, `release()`;
    `TorchRuntime(threads)` builds the vendored `Note_pedal`, `torch.load(..., weights_only=True)`
    (checked against the real Zenodo checkpoint, SHA-256 verified), `torch.set_num_threads`.
  - `models.ModelStore`: `STEMSTUDIO_MODEL_DIR/transcription/<model_id>/<file>`, `.part` +
    SHA-256 check + rename, `ready()` for `/health`; failures ->
    "The transcription model could not be downloaded; try again when online."
  - `audio.load_mono(path, window, rate)`: range read + downmix + soxr.
  - `fake.FakePitchTranscriber` / `FakeDrumTranscriber` / `grid_beats` (data-model 2.6; qa:
    one note per beat from the input start to its end, bar positions from the beat nearest the
    first downbeat; manual grids generate beats from `firstDownbeatSeconds`): the C-major scale
    inside the profile range from the C nearest the range middle (wrapping), piano + C3-E3-G3 per
    bar (lasting the bar), drums kick 1/3, snare 2/4, hi-hat every eighth, given pieces only.
  - `TranscriptionEngines` / `create_transcription_engines(settings, devices, drums)`:
    `pitched(request, pitch_range)` -> fake (with `STEMSTUDIO_TRANSCRIBER=fake`), the piano CRNN
    (piano + accurate) or the shared basic-pitch; `drums(request)` -> fake or the injected real
    drum transcriber (beat-analysis); `models_health()`; `release()`.
  - `vendor/basic_pitch` (Apache-2.0 LICENSE + NOTICE; constants + post-processing subset) and
    `vendor/piano_transcription` (upstream MIT LICENSE + NOTICE; models, post-processor,
    piano_vad, config, segments; no shell download, matplotlib, audioread or mido - guarded by a
    test).
- `jobs/lane_store.LaneJobStore` (output folder, restart failure "interrupted by restart" with
  the stage cleared, idempotent delete, 24 h sweep), used by the refactored `render_store` and the
  new `jobs/transcription_store.TranscriptionJobStore` (`save_files` -> the three
  `TranscriptionFile`s in contract order with size and SHA-256, `file(job_id, role)`);
  `DataPaths.transcription_jobs`.
- `worker/pyproject.toml` (CTO-approved one-time edit, own commit `c7107d3`):
  `transcription/vendor` excluded from coverage, ruff and mypy exactly like `detail/vendor`.

Red -> green: T022 `8b8614c` -> T023 `0aef148`; T024 `48aaf36` -> T025 `fc69a35`;
T026 `e3eb95a` -> T027 `ddf486f`; T028 `6517d13` -> T029 `6646e91`.

## Files changed

- `worker/src/stemstudio_worker/transcription/**` (new; vendored subsets under `vendor/`)
- `worker/src/stemstudio_worker/jobs/{lane_store,transcription_store}.py` (new),
  `jobs/{render_store,paths}.py`
- `worker/tests/transcription/**`, `worker/tests/jobs/test_transcription_store.py` (new)
- `worker/pyproject.toml`, `specs/008-sheet-music/tasks.md` (T022-T029 ticked)

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
2282 passed, 8 deselected in 91.98s
TOTAL 8961 stmts, 0 missed, 1040 branches, 0 partial -> 100 % (vendor excluded)
transcription/ (without vendor) and jobs/: 100 % line and branch
```

## Quality checks

ruff check / format --check clean; mypy --strict (330 files) clean; `vendor_check.py`: 0 errors;
`sig_check.py transcription`: 0 errors, 2 warnings (`audio.load_mono` 16 LOC,
`fake._triads` CC 6); `run_all.py`: 997 files, **0 errors**, 250 warnings.

## Dependencies added

None in the lock. Vendored (not installed): basic-pitch 0.4.0 subset (Apache-2.0, sdist SHA-256
`6f48ac4b...68e5`), piano_transcription_inference 0.0.6 subset (MIT, sdist SHA-256
`b6dd00f9...d231`, LICENSE from upstream commit `566057930a8423aa31b91296e7002c513269446d`).
Runtime: onnxruntime 1.32.0, torch, torchlibrosa 0.1.0, scipy, soxr (all already locked).

## Open issues / requests

- Not wired into routes/lanes yet (008 T038/T039 next).
- **architect**: `soxr` direct dependency + mypy override (also used here).

## Next owner

stem-separation (008 T038 onwards).
