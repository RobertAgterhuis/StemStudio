# Hand-off: beat-analysis / us1

Checkpoint US1 (ADR-008): T051, T052, T053, T054, T055, T141, T142.

## What was built

- `LibrosaBeatAnalyzer` (implements `ports.BeatAnalyzer`) returning a contract `BeatGrid`:
  - **Beats**: librosa onset envelope (22.05 kHz, hop 256 = 11.6 ms, `n_fft` 1024) ->
    `librosa.beat.beat_track` -> every beat snapped to its onset peak within +-4 frames with
    parabolic sub-frame interpolation -> weak leading/trailing beats (< 20 % of the median beat
    strength) trimmed. Beats are rounded to 0.1 ms and strictly ascending.
  - **BPM**: 60 / median inter-beat interval of the refined beats, capped at 400.
  - **Meter + downbeats**: every (beats per bar in {4, 3}, phase) is scored by the mean onset
    strength of the beats at that phase divided by the mean of the others; the best wins when
    the contrast is >= 1.25, otherwise 4/4 starting at the first beat is assumed. Downbeats are
    always a subset of beats.
  - **Confidence**: pulse clarity = normalised autocorrelation of the onset envelope at the
    beat-period lag, clipped to 0..1 (clicks 0.7-0.9, white noise ~0.01).
  - **Reliable**: confidence >= 0.2 and >= 4 beats.
  - **No clear pulse**: undecodable file, < 1024 samples, peak < 1e-4 (silence) or < 4 tracked
    beats -> `unreliable_grid()` (`bpm=0`, no beats, `beatsPerBar=4`, `confidence=0`,
    `reliable=false`). Noise keeps its (meaningless) beats but has low confidence and
    `reliable=false`. Nothing raises; an undecodable file is logged as a warning (exception type
    only, no path).
- `create_beat_analyzer(settings) -> BeatAnalyzer` returns a `LibrosaBeatAnalyzer`; signature
  unchanged from T001; settings are not needed yet.

Public API: `stemstudio_worker.analysis.{create_beat_analyzer, LibrosaBeatAnalyzer}`. Helpers in
`tempo.py`, `downbeats.py`, `reliability.py` are documented but internal to the component.

## Files changed

- `worker/src/stemstudio_worker/analysis/__init__.py` (factory, exports)
- `worker/src/stemstudio_worker/analysis/librosa_analyzer.py`
- `worker/src/stemstudio_worker/analysis/tempo.py`
- `worker/src/stemstudio_worker/analysis/downbeats.py`
- `worker/src/stemstudio_worker/analysis/reliability.py`
- `worker/tests/analysis/__init__.py` (makes `from .conftest import ...` work under
  `--import-mode=importlib`), `conftest.py`, `test_librosa_analyzer.py`, `test_reliability.py`,
  `test_tempo.py`, `test_factory.py`; removed `worker/tests/analysis/.gitkeep`
- `specs/001-stemstudio-mvp/tasks.md` (ticked only T051-T055, T141, T142)

Red -> green evidence (failing test commit -> implementation commit):

| Tasks | Red (failing tests) | Green |
|---|---|---|
| T051/T052 -> T053 | `b487cf0` (ModuleNotFoundError `librosa_analyzer`) | `ecdb1ef` |
| T054 -> T055 | `63d1b07` (ImportError `is_reliable`) | `9e43914` |
| T141 -> T142 | `f2aa7da` (ImportError `LibrosaBeatAnalyzer` from package) | `500642d` |

## Tests and coverage

Synthetic signals are generated in-test (`tests/analysis/conftest.py`, numpy + soundfile, 44.1 kHz
stereo 16-bit WAV in `tmp_path`): click tracks at 60/90/97/100/110/120/133/150/180 BPM with an
accented (louder, higher) first beat per bar in 4/4 and 3/4, unaccented clicks, a music-like mix
(chord pad + kick/tick per beat), white noise, silence, a 1.4 s clip, a single click, empty and
tiny files and a corrupt WAV. Assertions: BPM +-1, every detected beat within 30 ms of a true beat,
recall >= 90 %, beats per bar 4 / 3 / default 4, downbeats within 30 ms of true downbeats and a
subset of beats, confidence 0..1, unreliable grids for no clear pulse.

```
> uv run pytest tests/analysis --cov=stemstudio_worker.analysis --cov-branch --cov-report=term-missing
Name                                                 Stmts   Miss Branch BrPart  Cover   Missing
------------------------------------------------------------------------------------------------
src\stemstudio_worker\analysis\__init__.py               7      0      0      0   100%
src\stemstudio_worker\analysis\downbeats.py             28      0      2      0   100%
src\stemstudio_worker\analysis\librosa_analyzer.py      35      0      4      0   100%
src\stemstudio_worker\analysis\reliability.py           22      0      2      0   100%
src\stemstudio_worker\analysis\tempo.py                 50      0      6      0   100%
------------------------------------------------------------------------------------------------
TOTAL                                                  142      0     14      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
============================= 41 passed in 6.10s ==============================
```

Line coverage 100 % (142/142), branch coverage 100 % (14/14), 41 passed, 0 failed.

## Quality checks

```
> uv run ruff check src/stemstudio_worker/analysis tests/analysis
All checks passed!
> uv run ruff format --check src/stemstudio_worker/analysis tests/analysis
11 files already formatted
> uv run mypy --strict src/stemstudio_worker/analysis tests/analysis
Success: no issues found in 11 source files
```

`tools/quality/sig_check.py` does not exist yet; ruff's limits (mccabe 5, max-args 4,
max-statements 15, max-branches 5) pass. Largest unit: `LibrosaBeatAnalyzer.analyze` /
`_build_grid`, about 12 code lines each; no duplicated blocks.

## Dependencies added

None. Uses the already declared `librosa` 1.0.1 (ISC), `numpy` 2.5.4 (BSD-3-Clause) and
`soundfile` 0.14.0 (BSD-3-Clause, tests only).

## Open issues / requests

1. **architect**: `worker/tests/test_ports.py::test_factory_stubs_are_placeholders[create_beat_analyzer]`
   now fails by design (the factory is implemented). Please drop `create_beat_analyzer` from that
   parametrisation (the same will happen for the other two factories). Whole-worker run:
   `1 failed, 71 passed`, the failure being exactly this case.
2. Known heuristic limits (not contract issues): a backbeat-heavy 4/4 song (snare on 2 and 4
   louder than beat 1) can get its downbeat phase on beat 2 or 4; tempo octave errors on real
   music are possible (the user can override BPM, FR-014). The 0.2 reliability threshold is
   calibrated on synthetic signals; QA's real-music fixtures may justify tuning it.
3. Beats lag the true onset by about 11 ms (onset-envelope latency); well inside 30 ms and the
   user offset control covers it. Not compensated on purpose (no calibration constant).

## Next owner

stem-separation (`jobs/` runner calls `PipelineAdapters.analyzer.analyze(wav_path)` during
"Analysing beats"); CTO for checkpoint review. beat-analysis continues with T115 (polish).
