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
