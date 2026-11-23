# Hand-off: stem-separation (lane L3) / 016 HO:us4

Branch `016-round-lane-l3`, not pushed, not merged. Tasks T093, T094 (ticked).

## What was built

| Task | Commits | Result |
|---|---|---|
| T093 | 11d4ee29 (with T061), 87113d7d | Route tests of `kind: vocal-pitch`: the vocal stem is read by the `HarmonicMixer` at `PITCH_SAMPLE_RATE` (16 kHz) and passed to the `PitchAnalyzer` (fake) with its `source`; `VocalPitch` inline; `NoMelodyError` -> `failed` with `NO_MELODY_TEXT`; progress and stage while running. Added in this session: a stem removed between create and run fails the job with "Input not found: <path>" (the lane keeps running), the analyzer's engine (`yin`) is recorded in the result and `/health.instrumentAnalysis.pitchEngine`, deletion of a running job |
| T094 | dd685fd1 (with T062) | `InstrumentAnalysisService._vocal_pitch`: mix -> progress 0.1 -> `analyze(audio, source, progress 0.1..0.95)`. The new T093 cases pass against it, so no further code change |

Composition: `analysis.vocal_pitch:LibrosaPitchAnalyzer(engine=STEMSTUDIO_PITCH_ENGINE)` (L2), or
`views_fake.FakePitchAnalyzer` with `STEMSTUDIO_PITCH_ENGINE=fake`. Smoke with L2's module overlaid (HO:us1): a
220 Hz tone gives voiced frames at 5699 cents with `pyin`.

## Tests and coverage (one run for the US1-US4 checkpoints)

| Component | Command (in `worker/`) | Result | Coverage |
|---|---|---|---|
| Worker | `uv run --frozen pytest --cov --cov-branch` | 2981 passed, 3 skipped (fonts/cairo), 11 deselected (integration) | **100 % line, 100 % branch** (12,829 statements, 1,436 branches; vendor omitted) |
| T063 integration | `pytest -m integration tests/harmony/test_chords_integration.py` | 1 passed (numbers in HO:us1) | - |

`ruff check .`, `ruff format --check .` (422 files) and `mypy src tests` (440 files): clean. This session fixed 19
mypy findings in the 016 test files that the previous agent left behind (misplaced `type: ignore` comments after
formatting, `str` where the protocols need the `ChordEngine`/`DrumPiece` literals); commit 5befc586, tests only.

Security self-review: the vocal path passes the path guard at create and is resolved again at run time (a stem
deleted in between fails readably, no traceback); the pitch frames are capped by the contract (`MAX_PITCH_FRAMES`,
and L2 truncates the audio to that length before analysing).

## Decisions / deviations / requests

None.
