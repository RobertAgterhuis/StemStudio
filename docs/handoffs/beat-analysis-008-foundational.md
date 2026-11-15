# Hand-off: beat-analysis / 008 foundational (T012-T019)

## What was built

Package `worker/src/stemstudio_worker/notation/` with the `Notator` port implementation
(data-model 2.1-2.7, ADR-017 D3-D6):

| Module | Content |
|---|---|
| `profiles.py` | `Profile`, `PROFILES` (table 2.1), `get_profile`, `suggest_profile(channel)`, `resolve_resolution`, `engine_for(profile, quality)`, `staff_of` (grand staff split 60), `GRAND_STAFF_SPLIT`, `MAX_NOTES_PER_BAR` |
| `cleaning.py` | steps 1-6 in the binding order: `keep_in_range`, `reduce_monophonic` (60 ms, louder wins, window from the kept onset), `quantise`, `end_at_next_onset` (single voice, one loudest note per slot), `drop_short`, `merge_duplicates`, generic `cap_per_bar`, `clean_notes`, `clean_hits` (drums: merge per piece/slot, cap hands and feet), `cleaning_warnings`; `PlacedNote`, `PlacedHit`, `Quantiser` protocol |
| `timebase.py` | `TimeBase.from_grid(grid, duration)` (interpolated beat index, median-period extrapolation, bar 1 = beat nearest `downbeats[0]`), `GridQuantiser.snap` (half up, exact `Fraction`s), `grid_step`, `bar_layout` (pickup), `manual_beats`, `shift_grid`, `round_bpm` |
| `keys.py` | `estimate_key` (music21 Krumhansl-Schmuckler weighted by duration, confidence clamped 0..1), `key_from_name` (30 names), `resolve_key`, `spell` (scale spelling, otherwise sharps for sharp keys and C/a, flats for flat keys), `key_sharps` |
| `events.py` | chords/strokes per voice and measure contents (rests in gaps, ties across bar lines) |
| `builders.py` | music21 scores: clefs per profile, grand staff (`PartStaff` x2 + brace), two-voice percussion staff (hands up / kick down, x noteheads for hi-hat and cymbals, `PercussionChord` for simultaneous hand hits, GM drum map kick 35, snare 38, toms 41, hi-hat 44, cymbals 49), key signature, time signature, tempo mark, titles, software "StemStudio" |
| `writer.py` | `musicxml_bytes` (MusicXML 4.0), `midi_bytes` (tied notes merged) |
| `notator.py` | `MusicNotator.notate` / `notate_drums` (clean -> quantise -> key -> build -> write); warnings in the order manual-tempo, notes-cleaned, dense-capped, key warnings (few-notes / low-key-confidence), at most 8 |
| `__init__.py` | `create_notator()`: composition-root factory, imports music21 lazily |

Interpretations (please confirm or correct):

1. **Pickup longer than a bar** (data-model 2.4 says "a pickup bar of `ceil(-min q / bpb)·bpb`
   beats, shortened to the used length"): when notes start more than one bar before the first
   downbeat, I keep the meter phase. The pickup (measure 0) is the partial bar only, the whole
   bars before the downbeat stay whole, and measure 1 is the first whole bar. The alternative
   would be one pickup measure longer than the time signature. With up to one bar of lead-in
   both readings give the same result.
2. **Polyphonic staves** (guitar, keys, piano) are written as chords per staff. A chord lasts
   until its longest note ends or until the next onset of that staff, whichever comes first, so
   a note held under a later onset is cut. ADR-017 D5 says "chords per staff". It saves voice
   splitting, but sustained notes get shorter.
3. **Counts**: `notes-cleaned` counts out-of-range and too-short notes (the warning text). The
   monophonic reduction, the per-slot reduction and merged duplicates are not counted. `few-notes`
   is raised for < 8 notes also with a key override and for drums.
4. **Override confidence** is 1.0 (`KeyInfo.confidence` is required).
5. **Drum strokes** last until the next onset of their voice, at most a quarter, and never past
   the bar line (no ties in the drum staff).
6. **Composer**: with no subtitle, the composer is set to `""`. music21 would otherwise write
   "Music21" as the composer.
7. `shift_grid` (beat grid: beats/downbeats minus the offset, earlier beats kept; manual: the
   downbeat moves by whole bars to `[0, bar)`) is provided as a utility. According to data-model
   4.3 the API shifts the grid; the notator expects an already shifted grid
   (`NotationSettings.grid`).

Dependency rule (coordinator message, 2026-11-15): `notation` imports only `ports`, `schemas`,
music21 and the stdlib. The first time base used numpy; I replaced it with
bisect/statistics in `c4017b5`.

## TDD evidence (red -> green)

| Task | Red (failing tests) | Green |
|---|---|---|
| T012 / T013 profiles + cleaning | `9063323` | `ce84519` |
| T014 / T015 time base | `6b94047` | `278239d` (+ `c4017b5` stdlib refactor) |
| T016 / T017 keys | `52b2109` | `40c79d1` (the weighting test was made stricter in the green commit) |
| T018 / T019 builders + Notator | `ad58c35` | `a311437` (title assertions adjusted: music21 reads the title back as `movementName`/`bestTitle`; drum length order follows the voice order) |

## Files changed

- `worker/src/stemstudio_worker/notation/{__init__,profiles,notices,cleaning,timebase,keys,events,builders,writer,notator}.py` (new)
- `worker/tests/notation/{__init__,conftest,test_profiles_cleaning,test_timebase,test_keys,test_builders}.py` (new)
- `docs/handoffs/beat-analysis-008-foundational.md`

## Tests and coverage

```text
uv run pytest tests/notation --cov=stemstudio_worker.notation --cov-branch
  173 passed; notation 585 stmts / 78 branches: 100 % line, 100 % branch
uv run pytest --cov --cov-branch          (whole worker)
  1909 passed, 7 deselected in 67 s; TOTAL 7253 stmts, 832 branches, 100 %
```

Sanity check outside the suite: Verovio 6.4.0 `loadData` accepts the keys, bass and drums
MusicXML (21 / 21 / 13 bars, 1 page each).

## Quality checks

```text
ruff check src tests                         All checks passed!
ruff format --check src tests                246 files already formatted
mypy src tests (strict)                      Success: no issues found in 252 source files
sig_check.py notation + tests/notation       16 files, 195 units, 0 errors, 0 warnings, 0 exemptions
dependency_check.py (this branch)            0 errors, 1 warning: "notation is not in the dependency map"
                                             (the 008 rules live on 006-008-lyrics-sheet-music)
```

## Dependencies added

None. music21 10.5.2 (BSD-3-Clause) was already locked by the architect. matplotlib stays
excluded: music21 prints an informational line at import and works without it.

## Open issues / requests

- **stem-separation (transcription jobs)**:
  - Call `create_notator()` from the composition root.
  - Build `NotationSettings` with `resolution = profiles.resolve_resolution(profile, request.resolution)`
    and `duration_seconds` = the input (window) length.
  - The `detail-bleed`, `cpu-piano` and `missing-drum-piece` warnings are the job's to add.
  - Order the merged warnings and keep at most 8.
  - `profiles.engine_for` gives the engine per profile.
  - `NoNotesError` is raised when nothing is left.
- **backend / API**: `profiles.suggest_profile` mirrors data-model 2.1 (for parity tests).
- **quality-gate**: the dependency map on this branch does not know `notation` yet (the rule is on
  `006-008-lyrics-sheet-music`).
- **architect**: please confirm interpretations 1 and 2 above (pickup longer than a bar, chords per
  staff), or adjust data-model 2.4 / 2.3.

## Next owner

beat-analysis continues with 008:T057 -> T058 (`analysis/drum_onsets.py`, HO:us4).
stem-separation (B) can wire the notator into the transcription job.
