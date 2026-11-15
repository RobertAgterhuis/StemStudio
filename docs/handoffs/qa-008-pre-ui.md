# Hand-off: qa / 008 pre-UI (synthetic note material, strategy, sheet music specs)

Branch `006-008-lyrics-sheet-music` from 62d107e (architect Phase 0). Same run as
`qa-006-pre-ui.md` (numbers repeated where they concern 008).

## What was built

- **008:T011 (done, ticked)** `docs/testing/TEST-STRATEGY.md` section 14: notation/engraving on
  synthetic note lists with MusicXML assertions, real-model integration tests on synthetic audio
  (SC-002), fake transcriber with real notation + engraving for E2E, run matrix, fixtures and the
  traceability US1..US5 / FR-001..FR-014 / SC-001..SC-006 -> tests.
- **Synthetic note material** `tests/fixtures/generate_score_fixtures.py` -> git-ignored
  `tests/fixtures/audio/` + committed `tests/fixtures/score-manifest.json` (every note/hit, the
  shared grid as a worker `TempoGrid` of kind `beatGrid`, F1 targets):
  - `score_bass_line_120bpm.wav`: research.md S-03 single voice, 78 notes, MIDI 40-52 pentatonic,
    eighths/quarters, ~15 % rests (target F1 0.90).
  - `score_piano_texture_20s.wav`: S-04 polyphonic texture, 38 melody notes >= MIDI 60 over 19
    bass-staff triads, inharmonic partials (target F1 0.85).
  - `score_drums_{kick,snare,toms,hihat,cymbals}.wav`: the five `drumInputs` of an 8-bar groove
    (16/16/8/64/2 hits).
  - `onset_f_measure(reference, estimate)`: the reference metric (onset 50 ms, same pitch).
- **Specs authored, not executed (no 008 UI/backend yet; tasks stay unticked)**:
  `e2e/tests/sheet-music.spec.ts` (`@sheet-music`) covering 008:T045 (bass: dialog, ready, PDF
  preview `inline=true`, `%PDF`, measure count = `expectedFakeBars(grid)`, `<fifths>`, time
  signature, F clef with octave -1, `MThd`; downloads are attachments), T050 (E minor +
  simplified: `<fifths>1</fifths>`, no `16th`), T056 (piano: `<staves>2</staves>`, G and F clefs),
  T065 (drums after the split, `@detail`: percussion clef, voices 1+2, x noteheads; plus the
  "split drums first" notice before the split), T070 (score from a loop-region export: provenance
  track + `sourceAssetIds`, library source text).
  New support/page objects: `support/score-api.ts`, `support/musicxml.ts` (text-level MusicXML /
  PDF / MIDI readers and the fake bar count), `pages/score-dialog.ts`, `pages/score-items.ts`;
  fixtures in `support/lyrics-test.ts`; `npm run test:sheet-music`. Every case skips with a reason
  while `/api/scores/options` answers 404/405. The readers are self-tested (green) in
  `e2e/tests/lyrics-score-support.spec.ts`.

## Files changed

- New: `tests/fixtures/generate_score_fixtures.py`, `tests/fixtures/score-manifest.json`,
  `tests/fixtures/tests/test_generate_score_fixtures.py`, `e2e/support/{score-api.ts, musicxml.ts}`,
  `e2e/pages/{score-dialog.ts, score-items.ts}`, `e2e/tests/sheet-music.spec.ts`,
  `docs/handoffs/qa-008-pre-ui.md`
- Changed: `tests/fixtures/tests/test_generator_contract.py` (score generator joins the shared
  determinism/manifest/CLI contract), `tests/fixtures/pyproject.toml`, `e2e/package.json`,
  `docs/testing/TEST-STRATEGY.md`, `specs/008-sheet-music/tasks.md` (T011 ticked)

## Tests and coverage

```text
fixtures pytest (all generators)       216 passed, 974 stmts / 106 branches, 100 % line and branch
  generate_score_fixtures.py           172 stmts  22 branches  100 % line, 100 % branch
e2e stack-free (lyrics-score-support + audio-check + panner-law)   26 passed, 8 skipped, retries 0
e2e playwright --list                  425 tests in 33 files
```

Not run: `@sheet-music` against a stack (no 008 code yet); open until CTO 008:T079.

## Quality checks

ruff/format/mypy --strict clean; e2e eslint (0 warnings) and tsc clean; `sig_check.py` 0 errors
(warnings only in test helpers, listed in the 006 hand-off); `run_all.py` 0 errors, no
duplication warning in a new file.

## Dependencies added

None.

## Open issues / requests

- **stem-separation (008:T026/T027, T040)**: the fake transcriber must follow data-model 2.6
  exactly on the track grid (one note per beat, beat 0 to the last beat), because the E2E bar count
  is `pickup + ceil((beats - k0) / beatsPerBar)` with `k0` the beat nearest the first downbeat.
  T040/T051 may use `score_bass_line_120bpm.wav` / `score_piano_texture_20s.wav` and the manifest
  notes instead of an in-test generator (same S-03/S-04 recipes); `onset_f_measure` is available.
- **beat-analysis (008:T057)**: `score_drums_*.wav` + manifest hits are ready for the drum-onset
  and percussion tests.
- **frontend (008:T043/T044)**: the specs read the new item id from the `href` of
  `score-saved-open` (a UUID anywhere in it), select the key override by its label ("E minor"),
  and expect `asset-preview-<id>` to carry `src` with `inline=true`.
- **Pending edits to existing e2e files (after the 004/007 merge)**: `support/levels.ts` (1.7.0 ids,
  Beginner rules: `score-quality`, `score-key-override`, `score-range-*`, `score-detail-*` not
  rendered; `score-tempo-*` only with `score-tempo-required`), help topics
  (`sheet-music`, `sheet-music-profiles`, `sheet-music-key-tempo`, `sheet-music-drums`,
  `sheet-music-piano`), `pages/library-page.ts` (score row helpers now in `pages/score-items.ts`),
  `pages/track-detail-page.ts` (`score-stem-*` buttons), a11y/visual screens with the score
  dialog and a score row.
- No unreliable-grid fake track exists, so US2-4 (manual tempo) has no E2E case in this wave.

## Next owner

qa runs `@sheet-music` once the 008 frontend lands (after the 004 and 007 frontend merges); CTO
008:T079 for the integration run.
