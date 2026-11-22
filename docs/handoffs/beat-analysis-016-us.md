# Hand-off: beat-analysis (+ audio-ingest) / 016 HO:us (lane L2)

Branch `016-round-lane-l2`. Covers the worker work after HO:foundational (21a44430): T021-T028 (beat-analysis) and
T037/T038 (audio-ingest). Not pushed.

## What was built

| Task | Red -> green | Result |
|---|---|---|
| T021/T022 | 5e82b89a -> 60442d57 | `analysis/vocal_pitch.py` `LibrosaPitchAnalyzer(engine="pyin"\|"yin")`: 16 kHz, 10 ms frames, 60 s chunks, centi-semitone packing, `no-melody` / low-voicing warnings |
| T023/T024 | 51b56c5e -> d950c49d | `notation/tablature.py` `ViterbiTabFingerer` (distinct strings, stretch/high-fret/hand-position cost, octave fit, drops trailing pitches only when unplayable); `notation/tab_staff.py` TAB staff post-processor (`staff-tuning`, `<string>`/`<fret>`, `notation-and-tab` / `tab-only`, capo); notator wiring |
| T025/T026 | 20527b9e -> fc2d7400 | `notation/parts.py` transposed parts (`<transpose>`, written key spelled by the R-04 rule, clefs incl. alto/tenor/8vb, range warnings) and the `strings`/`brass`/`woodwinds` profiles; MIDI stays at concert pitch |
| T027/T028 | 65c6c335 -> 74970de6, 054f5dab | `notation/chord_symbols.py` (`<harmony>` from the chart, transposed with the part) and `notation/drum_midi.py` (GM map, channel 10, `singlePiece`); 054f5dab moves the spelling tables into `notation/keys.py` so `notation` imports only `ports`/`schemas` (dependency rule) |
| T037/T038 (audio-ingest) | 003a04ce -> a20fdadd | `engraving/tablature.py` `check_tab_pages` (Verovio TAB staff: line count, fret numbers, "Capo n"); hand-position cost in the fingerer |

Constructors for L3: `LibrosaPitchAnalyzer(engine: Literal["pyin", "yin"] = "pyin")`, `ViterbiTabFingerer()` (no
arguments). Parts, chord symbols and drum MIDI are pure functions called by the notator.

## Tests and coverage

| Component | Command | Result | Line / branch |
|---|---|---|---|
| Worker (all) | `uv run pytest --cov --cov-branch` in `worker/` | 3541 passed, 4 skipped (libcairo/Leipzig absent), 11 deselected (integration) | 100 % / 99.9 % (13,305 statements, 1,610 branches, 2 partial) |
| L2 files | same | `harmony/*`, `vocal_pitch`, `tablature`, `chord_symbols`, `drum_midi`, `keys`, `analysis_inputs/*`, `engraving/tablature` | 100 % / 100 % |
| L2 files with a partial branch | same | `notation/parts.py` 180->179, `notation/tab_staff.py` 92->89 | 100 % / 99 % (closed in T130) |

## Quality checks

`ruff check`, `mypy` strict (445 files): clean. `run_all.py` (uv project `tools/quality`): **PASS**, 0 errors,
1892 warnings, 447 clones (jscpd ran). L2 production warnings (unit size 16-22, complexity 6-9) in `tablature.py`,
`tab_staff.py`, `key.py`, `postprocess.py`, `labels.py`, `chroma_hmm.py`, `vocal_pitch.py`, `chord_symbols.py`,
`mixdown.py`, `engraving/tablature.py`: refactored in T130/T131.

## Security self-review

Untrusted input: stem audio (pitch analysis: decoded by soundfile/librosa, chunked, no path leaves the error text) and
the MusicXML/SVG of the engraving check (parsed with the standard library from Verovio output the worker wrote itself,
no external entities). Tuning and instrument ids are looked up in fixed catalogues (unknown -> `ValueError`, tested).
No subprocess, no network.

## Dependencies added

None.

## Open issues / requests

1. L3 (`tools/quality`): `harmony` and `analysis_inputs` still miss in the dependency map (warning), as in HO:foundational.
2. The TAB PDF check skips without libcairo/Leipzig on this PC (1 skip); CI/the container runs it.

## Next owner

L3 (wiring of the pitch analyzer and fingerer), L2 continues with T130/T131.
