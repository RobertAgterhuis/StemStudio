# Hand-off: beat-analysis / polish

Checkpoint Polish (ADR-008): T115.

## What was built

- Docstring review of every public and private unit in `worker/src/stemstudio_worker/analysis/`
  (module docstrings describe the algorithm and every tuning constant has a docstring stating its
  meaning and calibration). `ruff` pydocstyle (Google convention) is clean.
- Measured accuracy of `LibrosaBeatAnalyzer` per synthetic tempo (below). No code changes were
  needed after the US1 checkpoint.

## Measured accuracy (synthetic signals from `tests/analysis/conftest.py`)

Clicks 12 s, music-like mix 16 s; first beat at 0.5 s. "Beat error" = distance of each detected
beat to the nearest true beat; recall = share of true beats with a detected beat within 30 ms.

| Signal | True BPM | Detected BPM | BPM error | Max beat error (ms) | Mean beat error (ms) | Recall | Beats/bar | Confidence | Reliable |
|---|---|---|---|---|---|---|---|---|---|
| click 4/4 | 60 | 60.05 | +0.05 | 13.2 | 10.5 | 1.00 | 4 | 0.86 | True |
| click 4/4 | 90 | 90.10 | +0.10 | 13.2 | 10.9 | 1.00 | 4 | 0.73 | True |
| click 4/4 | 97 | 97.12 | +0.12 | 13.2 | 11.0 | 0.95 | 4 | 0.82 | True |
| click 4/4 | 120 | 120.11 | +0.11 | 13.1 | 10.7 | 0.91 | 4 | 0.91 | True |
| click 4/4 | 133 | 132.89 | -0.11 | 13.1 | 11.1 | 0.96 | 4 | 0.89 | True |
| click 4/4 | 150 | 150.19 | +0.19 | 13.2 | 10.9 | 1.00 | 4 | 0.72 | True |
| click 4/4 | 180 | 179.56 | -0.44 | 13.2 | 10.9 | 1.00 | 4 | 0.83 | True |
| click 3/4 | 90 | 90.24 | +0.24 | 13.1 | 10.8 | 1.00 | 3 | 0.73 | True |
| click 3/4 | 150 | 150.41 | +0.41 | 13.2 | 10.8 | 0.97 | 3 | 0.72 | True |
| music 4/4 | 100 | 99.83 | -0.17 | 15.1 | 13.1 | 1.00 | 4 | 0.84 | True |
| white noise 10 s | - | 135.33 | - | - | - | - | 4 | 0.01 | False |
| silence 5 s | - | 0.00 | - | - | - | - | 4 | 0.00 | False |

Worst BPM error 0.44 BPM (limit 1); worst beat error 15.1 ms (limit 30 ms). Recall below 1.00
comes from librosa not emitting the last one or two beats of a clip, never from misplaced beats.

## Files changed

- `docs/handoffs/beat-analysis-polish.md` (this note)
- `specs/001-stemstudio-mvp/tasks.md` (ticked T115 only)

## Tests and coverage

```
> uv run pytest tests/analysis --cov=stemstudio_worker.analysis --cov-branch --cov-report=term-missing
TOTAL                                                  142      0     14      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
============================= 41 passed ==============================
```

Line 100 % (142/142), branch 100 % (14/14), 41 passed.

## Quality checks

ruff check: All checks passed; ruff format --check: 11 files already formatted;
mypy --strict: Success, no issues found in 11 source files (see `beat-analysis-us1.md`).

## Dependencies added

None (librosa 1.0.1 ISC, numpy 2.5.4 BSD-3-Clause, soundfile 0.14.0 BSD-3-Clause were already
declared).

## Open issues / requests

Unchanged from `beat-analysis-us1.md`: architect to remove `create_beat_analyzer` from
`worker/tests/test_ports.py::test_factory_stubs_are_placeholders`; heuristic limits on real music
(backbeat downbeat phase, tempo octaves, 0.2 reliability threshold to be re-checked against QA's
`music_100bpm_20s` fixture in T120/T122).

## Next owner

CTO (merge, T119); qa for the real-fixture check.
