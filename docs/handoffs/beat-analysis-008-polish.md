# Hand-off: beat-analysis / 008 polish (T071)

## What was built

- `analysis/downbeats.py`: `detect_meter` was at CC 7 (from the MVP) and is now under the limit
  of 5. The candidate enumeration moved into `_candidates`; behaviour is unchanged and covered by
  the existing meter tests.
- `analysis/drum_onsets.py`: the `transcribe` docstring repeated the port's docstring (jscpd
  reported a 17-line production clone with `ports.py`). It now points to the port.
- New end-to-end test `worker/tests/notation/test_drum_pipeline.py`. Generated kick, snare and
  hi-hat stems of a two-bar rock groove at 120 BPM (first downbeat at 0.5 s) go through the real
  `LibrosaDrumTranscriber` and `create_notator().notate_drums`. Result: 2 bars, 26 notes, no
  warnings, and the kick and hi-hat land exactly on their 8th/16th offsets in the two voices.
- Every public unit of `notation/**` and `analysis/drum_onsets.py` has a docstring (Google style;
  ruff `D` is clean).

Summary of the 008 work (details in `beat-analysis-008-foundational.md` and
`beat-analysis-008-us4.md`):

| Task | Red | Green |
|---|---|---|
| T012/T013 profiles + cleaning | `9063323` | `ce84519` |
| T014/T015 time base | `6b94047` | `278239d`, `c4017b5` (stdlib only) |
| T016/T017 keys | `52b2109` | `40c79d1` |
| T018/T019 builders + Notator | `ad58c35` | `a311437` |
| HO:foundational | | `8f74453` |
| T057/T058 drum onsets | `9b2c2ce`, `a9de326` | `f320c5a`, `469cdd2` |
| HO:us4 | | `7ed01cf` |
| T071 polish | | `1cb7a74` |

## Files changed

- `worker/src/stemstudio_worker/analysis/{downbeats,drum_onsets}.py`
- `worker/tests/notation/test_drum_pipeline.py` (new)
- `docs/handoffs/beat-analysis-008-polish.md`

## Tests and coverage

```text
uv run pytest --cov --cov-branch              (whole worker, after T071)
  1930 passed, 7 deselected in 71 s; TOTAL 7330 stmts, 838 branches: 100 % line, 100 % branch
  notation/**            585 stmts, 78 branches  100 % / 100 %
  analysis/drum_onsets.py 74 stmts, 6 branches   100 % / 100 %
  analysis/** (package)  219 stmts, 20 branches  100 % / 100 %
```

## Quality checks

```text
ruff check src tests                            All checks passed!
ruff format --check src tests                   249 files already formatted
mypy src tests (strict)                         Success: no issues found in 255 source files
sig_check.py analysis + notation (src+tests)    30 files, 294 units, 0 errors, 0 warnings
duplication.py                                  50 clones, 0 in production code (0.50 %)
run_all.py                                      885 files, 0 errors, 210 warnings (none in my paths) -> exit 0
dependency_check (this branch)                  1 warning: notation is not in the dependency map
```

## Dependencies added

None. music21 10.5.2 (BSD-3-Clause), librosa and numpy were already locked.

## Open issues / requests

- **architect**: confirm or adjust the interpretations in the foundational hand-off:
  1. pickup longer than a bar: whole bars are kept and measure 1 is the first whole bar;
  2. chords per staff are cut at the next onset;
  3. the drum gate uses the linear onset peak instead of the dB onset strength (data-model 2.3
     wording).
- **stem-separation**:
  - Wire `create_notator()` and `LibrosaDrumTranscriber()` into the transcription runtime.
  - Resolve `resolution` with `profiles.resolve_resolution`.
  - Add the job-level warnings (`detail-bleed`, `cpu-piano`, `missing-drum-piece` via
    `notation.notices.score_warning`).
  - 008:T040 (accuracy integration with real basic-pitch) uses `notation.cleaning.clean_notes`
    with `TimeBase.quantiser`. Send tuning requests to beat-analysis.
- **quality-gate**: the `notation` rule is on `006-008-lyrics-sheet-music`; this branch reports
  "not in the dependency map". notation imports only ports, schemas, music21 and the stdlib.
- **CTO**: tuning on real detail stems (bleed gate, `delta`) on the reference machine (008:T080).

## Next owner

CTO (merge into `006-008-lyrics-sheet-music`), stem-separation (B) for the transcription runtime.
