# Hand-off: beat-analysis (+ audio-ingest, qa) / 016 HO:foundational (lane L2)

Branch `016-round-lane-l2` (worktree `.claude/worktrees/r16-l2`), from the design head 7463ac54. Not pushed. This note
covers all three L2 roles; `audio-ingest-016-foundational.md` and `qa-016-foundational.md` point here.

## What was built

| Task | Role | Red -> green | Result |
|---|---|---|---|
| T014 | qa | 9e8d7c21 -> 0d59c5d8 (+ af2fd911 refactor) | `tests/fixtures/generate_instrument_fixtures.py`: `sing-on-pitch.wav` (reference melody -1200 cents, partials 1-3), `sing-semitone-off.wav` (-1100 cents), `silence.wav` in `tests/fixtures/audio/` (git-ignored, 44.1 kHz mono PCM16, 20 s); `tests/fixtures/instrument-manifest.json` (reference melody + SHA-256) |
| T013 | qa | 5e57eb9a (docs) | `docs/testing/TEST-STRATEGY.md` section 22: levels, fixtures, family -> tests table (8 families), integration-only tests, commands |
| T015/T016 | beat-analysis | dea8c8c9 -> 8901afdc | `harmony/labels.py` (Harte parse/format, chord tones, inversion rule, simple class order, display symbols = the shared spelling vectors), `harmony/postprocess.py` `BeatSnapPostProcessor` |
| T017/T018 | beat-analysis | 1ef0a036 -> b1cf0c27 | `harmony/chroma_hmm.py` `ChromaHmmRecognizer` |
| T019/T020 | beat-analysis | f94da4ef -> 66555517 | `harmony/key.py` `HmmKeyEstimator`, `apply_key` |
| T035/T036 | audio-ingest | d40fc682 -> 2131586e | `analysis_inputs/mixdown.py` `SoundfileHarmonicMixer` |

`542152e8` takes L3's `harmony/__init__.py` from 74b443af (same blob, only that file, agreed with the orchestrator)
so imports sort the same way before and after the merge.

### Classes and constructors for L3 (composition, hook)

None of them needs an argument.

| Port | Class | Constructor |
|---|---|---|
| `ChordRecognizer` (chroma-hmm) | `stemstudio_worker.harmony.chroma_hmm.ChromaHmmRecognizer` | `(*, self_transition: float = 0.9)` |
| `ChordPostProcessor` | `stemstudio_worker.harmony.postprocess.BeatSnapPostProcessor` | `()` |
| `KeyEstimator` | `stemstudio_worker.harmony.key.HmmKeyEstimator` | `(*, beta: float = 3.0, p_stay: float = 0.995, smooth_beats: int = 8)` |
| (step 5 helper) | `stemstudio_worker.harmony.key.apply_key(chart, estimate) -> ChordChart` | function: sets `key`, `keyRegions`, `preferFlats`, `key-uncertain` |
| `PitchAnalyzer` | `stemstudio_worker.analysis.vocal_pitch.LibrosaPitchAnalyzer` (T022, next) | `(engine: Literal["pyin", "yin"] = "pyin")` |
| `HarmonicMixer` | `stemstudio_worker.analysis_inputs.mixdown.SoundfileHarmonicMixer` | `(allowed_root: Path \| None = None)` |

Chords job order: `mixer.mix` -> `recognizer.recognize` -> `post.segments(frames, PostProcessOptions(grid, vocabulary,
bass))` -> `estimator.estimate(frames.chroma, chart.segments, grid)` -> `apply_key(chart, estimate)`. chroma-hmm class
order: index `root * 2 + (0 maj, 1 min)`, 24 = `N` (`labels.simple_labels()`). A BTC fallback sets
`dataclasses.replace(frames, fallback=True)`; the post-processor then writes `fallback-engine`. A `full` request on the
`chroma-hmm` engine gives `vocabulary: simple` + `vocabulary-reduced`.

### Behaviour worth knowing

- Labels are written with sharp roots (`A#:maj`); display spelling is done from `root`/`bass` (backend, engine). The
  inversion rule: the bass is the third, fifth or seventh (not for sus chords; for 6 chords only the third/fifth).
- Beat snapping: frame posteriors are summed per beat; the part before the first beat is its own span; a beat closer
  than one frame to the previous cut is dropped. Without a grid: runs < 500 ms merge (shortest first) into the
  neighbour with the higher mean posterior (ties: the earlier one). More than 4000 segments: the shortest one is joined
  with a neighbour until 4000 remain. `few-chords` = fewer than 2 distinct chords (inversions ignored).
- Key: units are beats (1 s without a grid) over the non-`N` part; profile = mean of Krumhansl-Kessler and Temperley;
  score = 0.5 chroma correlation (smoothed over +-8 units) + 0.5 chord-evidence correlation (+-2 bars); Viterbi on
  `softmax(3 * score)`, `p_stay` 0.995. **Confidence** = mean `softmax(10 * score)` (temperature 0.1, R-13 step 2), not
  the beta-3 decoding softmax: with beta 3 every key would be "uncertain". On the synthetic set: 24/24 cadences exact,
  confidence 0.75 for a clear cadence; the C -> D fixture changes exactly at the modulation bar.
- Mixdown: soundfile + soxr, float32 reads, zero-padding to the longest stem, peak > 1 scaled down to 1. Error texts
  carry only the file name (no absolute server path).

## Files changed

`tests/fixtures/{generate_instrument_fixtures.py, instrument-manifest.json, pyproject.toml (coverage source, isort),
tests/test_generate_instrument_fixtures.py}`, `docs/testing/TEST-STRATEGY.md`, `worker/src/stemstudio_worker/harmony/
{__init__ (L3's), labels, postprocess, chroma_hmm, key}.py`, `worker/src/stemstudio_worker/analysis_inputs/{__init__,
mixdown}.py`, `worker/tests/harmony/{__init__, conftest, test_labels, test_postprocess, test_chroma_hmm, test_key}.py`,
`worker/tests/analysis_inputs/{__init__, test_mixdown}.py`, `specs/016-instrument-views/tasks.md` (own rows ticked).

## Tests and coverage

| Component | Command | Result | Coverage (line / branch) |
|---|---|---|---|
| Worker (all) | `uv run pytest --cov --cov-branch` in `worker/` | 3331 passed, 3 skipped, 10 deselected | 100 % / 100 % (12,639 statements, 1,456 branches) |
| `harmony/` (L2 files) | same | labels 76, postprocess 113, chroma_hmm 55, key 168 statements | 100 % / 100 % each |
| `analysis_inputs/` | same | mixdown 43 statements | 100 % / 100 % |
| Fixtures | `pytest -c tests/fixtures/pyproject.toml --cov --cov-branch tests/fixtures/tests` (CI form) | 312 passed | 100 % / 100 % (`generate_instrument_fixtures` 100 %) |

The harmony suites also pass with `-W error::UserWarning` (no librosa short-signal warnings).

## Quality checks

`ruff check`, `ruff format --check`, `mypy` strict on `worker/`: clean ("no issues found in 215 source files").
Fixtures: ruff, `mypy --exclude backup` clean. `tools/quality/run_all.py`: **PASS**, 0 errors, 1865 warnings, 445 clones
(jscpd ran). `dotnet format StemStudio.slnx --verify-no-changes`: clean (no .NET change). L2 SIG warnings (no errors):
unit size of `ChromaHmmRecognizer.recognize` (20), `HmmKeyEstimator.estimate` (17), `key._global` (17),
`postprocess._segment` (16); complexity 6-7 of `parse_label`, `display_symbol`, `_beat_spans`, `_with_bass`,
`_warnings`, `_global`; fixture import-block clones with the 015 generator. They are fixed in T130 (polish).

## Security self-review

Untrusted input in this checkpoint: stem files and their paths (mixdown). Paths: the mixer resolves each path and, with
`allowed_root`, refuses anything outside it (`..` escapes and absolute paths elsewhere are tested); missing files ->
`InputNotFoundError(name)`; corrupt files -> `AnalysisError("A stem could not be read.")` (tested with a fake FLAC
header); 0 or more than 4 paths -> `ValueError`. Memory: one stem at a time in float32. No subprocess, no network.

## Dependencies added

None (librosa, soundfile, soxr, numpy were already in the worker).

## Docs impact

None at this checkpoint (the strategy section is in `docs/testing`). Technical manual later: chroma-hmm fallback
behaviour, key method.

## Open issues / requests

1. **L3 (`tools/quality`)**: `harmony` and `analysis_inputs` are not in the dependency map
   (`run_all.py`: "harmony is not in the dependency map" warnings). Please add them in T009/T010 (L2 depends on
   `ports`, `schemas` only).
2. **L3 (`views_fake.py`)**: use the reference melody rule of the fixtures (120 BPM from 0.5 s, the note of beat k =
   `walk[k % 14]`, walk 60..72..62, the global beat index) so `sing-on-pitch.wav` scores >= 90 (already sent).
3. The engraving PDF checks (T037) need libcairo + Leipzig; they are absent on this PC (2 existing skips). T037 will
   skip the PDF part the same way when they are missing.

## Next owner

L3 (wiring of the classes above), orchestrator (mid-wave checkpoint). L2 continues with T021-T022 (vocal pitch).
