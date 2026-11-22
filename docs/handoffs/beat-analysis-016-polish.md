# Hand-off: beat-analysis (+ audio-ingest) / 016 HO:polish (lane L2)

Branch `016-round-lane-l2`. T130 (beat-analysis) and T131 (audio-ingest). Not pushed. `audio-ingest-016-polish.md`
points here.

## What changed

| Commit | What |
|---|---|
| 054f5dab | `notation` no longer imports `harmony.labels` (dependency rule notation -> ports, schemas); spelling tables and the flat-key rule in `notation/keys.py` |
| 48458683 | tests closing the last partial branches: an unfingered chord note (`tab_staff._carry_ties`), a part without attributes (`parts.with_transpose`), a part without measures (`tab_staff._first_clef`) |
| de633ba8 | units of `harmony/{chroma_hmm,key,labels,postprocess}.py`, `analysis/vocal_pitch.py`, `notation/{tablature,tab_staff,chord_symbols,extensions,notator}.py`, `analysis_inputs/mixdown.py` (T131), `engraving/tablature.py` (T131) split to unit size <= 15 and complexity <= 5; docstrings on the new helpers; no behaviour change (all suites unchanged and green) |

## Tests and coverage

| Component | Command | Result | Line / branch |
|---|---|---|---|
| Worker (all) | `uv run pytest --cov --cov-branch` in `worker/` | 3544 passed, 4 skipped (libcairo/Leipzig absent on this PC), 11 deselected (integration) | **100 % / 100 %** (13,354 statements, 1,594 branches) |
| L2 files (`harmony/` own files, `analysis/vocal_pitch.py`, `notation/**`, `analysis_inputs/**`, `engraving/tablature.py`) | same | all in the "complete coverage" list | 100 % / 100 % |

## Quality checks

| Check | Result |
|---|---|
| `ruff check`, `ruff format --check`, `mypy` strict (`worker/`) | clean (445 files) |
| `run_all.py` on the L2 production paths | PASS, 0 errors, **0 SIG warnings** (before: 25 unit-size/complexity warnings) |
| `run_all.py` whole repo (uv project `tools/quality`) | PASS, 0 errors, 1911 warnings, 459 clones; jscpd ran (clone list in its own output) |
| `dotnet format` | not applicable (no .NET change in L2) |

Left as warnings (test code or other owners): test helpers in `worker/tests/{harmony,notation}` (complexity 6-9 of
assertion helpers), `tests/fixtures/generate_instrument_fixtures.py` `generate` (16 lines) and its import-block clone
with the 015 generator.

## Security self-review

The refactor moves code only; untrusted inputs are unchanged from HO:us (stem audio, own MusicXML/SVG parsed with the
standard library, fixed catalogues). The mixdown path checks (`allowed_root`, missing/corrupt file texts with the file
name only) are untouched and still tested.

## Dependencies added

None.

## Open issues / requests

1. L3 (`tools/quality`): `harmony` and `analysis_inputs` still miss in the dependency map (warning only).

## Next owner

Orchestrator (merge), quality-gate T138 (whole-repo run).

## Review fixes (2026-11-22)

Independent code review of the L2 worker paths; orchestrator decision: fix all, test-first. Commits `5264b61c..337b86e2`
on `016-round-lane-l2` (each finding: `test(...)` red, then `fix/perf(...)` green).

| Finding | Fix | Tests |
|---|---|---|
| M1 TAB fingered from the written pitch of a transposing part (FR-021) | `tab_staff.py` reads the bar-1 `<transpose>` (chromatic + 12 x octave-change, written -> sounding) and fingers the concert pitch; the notation staff stays written | double bass part, `bass-4-standard`, both layouts: E1 -> string 4 fret 0, written E2 |
| L2 notes dropped by the fingerer stayed on the TAB staff without `<technical>`; docs said "quietest" | unfretted TAB notes are removed (a chord hands `<chord/>` to the next kept note; a lone tied continuation becomes a rest; tie carry only from a fingered start); the count is added to `tablature.notesDropped` and `tab-notes-dropped`; chord notes are passed loudest first (`dynamics`), and since a music21 chord has one loudness the dropped note is the highest; docstrings say so | E2+F2 on `guitar-standard` (both layouts): no TAB note without a fret, F2 dropped and counted; crafted lead-drop and tie-to-rest cases |
| L3 up to 6^6 products per 6-note chord, fully sorted | per-string pruned search (at most 6! = 720 combinations, `itertools.product` order) + `heapq.nsmallest(64)` (equivalent to `sorted()[:64]`) | equality with the full product on the golden, dense and unison cases; bench 1800 six-note chords (900 s): 3.4 s -> 0.8 s without coverage (report-only, budget 10 s for coverage runs) |
| L4 YIN fallback voiced on RMS only | also requires the minimum cumulative-mean-normalised difference <= 0.25 (FFT, librosa's centred framing, 512-sample window, 65-1047 Hz lags), the R-05 rule | breath-like noise at about -26 dBFS -> unvoiced / `NoMelodyError`; a tone followed by noise: tone voiced, noise not |
| L6 `Music21PartWriter.write` always reported `out_of_range=0` | counts the attacked concert notes of the parsed score (ties once) with the same range rule as the pipeline | tuba on the C major scale: 4 out of range, equal to the notator pipeline |

Note on L2: the red commit `4736fdeb` expected the quieter E2 to be dropped. Implementing it showed that music21 writes one
`dynamics` value per chord (`builders.py` uses the loudest note of the group), so "quietest" cannot be decided in the
MusicXML; the green commit `586fd28e` changed that one expectation to the documented tie rule (highest dropped) instead of
changing the 008 builder and its MIDI. The crafted case with different `dynamics` still shows loudest-first ordering.

Checks after the fixes (`worker/`):

| Check | Result |
|---|---|
| `uv run pytest --cov --cov-branch` | 3556 passed, 4 skipped (libcairo/Leipzig), 11 deselected (integration); **100.00 % line and branch** (13450 statements, 1620 branches; gate 90 %) |
| `ruff check`, `ruff format --check`, `mypy` strict | clean |
| `run_all.py` on `notation/` + `analysis/` production paths | PASS, 0 errors, 0 SIG warnings, 0 clones (2 dependency-map warnings, L3 open item) |
| jscpd (`duplication.py`) on the changed source and test folders | ran: 2 clones, both pre-existing test import blocks, 0 in production code |

Left as warnings (tests): complexity/size of `test_an_unplayable_chord_drops_a_note_from_the_tab_staff`,
`test_the_tied_continuation_of_a_dropped_note_becomes_a_tab_rest` and `_brute_candidates` (as for the earlier test helpers).
Security: no new untrusted input (our own MusicXML, own audio arrays); no new dependency.
