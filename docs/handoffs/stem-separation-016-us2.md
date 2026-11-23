# Hand-off: stem-separation (lane L3) / 016 HO:us2

Branch `016-round-lane-l3`, not pushed, not merged. Tasks T077, T078 (ticked).

## What was built

| Task | Commits | Result |
|---|---|---|
| T077 | 46ccfc20, fa9ad36c | `tests/api/test_transcription_extensions.py`: `tablature`, `part`, `chords`, `preferFlats`, `singlePiece` reach `NotationSettings`; a capo tab reaches the notator; a single drum piece has no `missing-drum-piece` warnings (without `singlePiece` they stay); the 422 combinations of DELTA 1; the `ScoreSummary` extensions (`tablature`, `part`, `chordsIncluded`); an 008 request keeps the 008 settings and an empty extension summary. Chord cases use a chord-capable profile (the bass profile does not allow chords) |
| T078 | bfe6095c | `jobs/transcription_pipeline.py`: the request extensions go into `NotationSettings` (`chords` as a tuple, `prefer_flats` false when absent), the document's `tablature`/`part`/`chords_included` into the summary, and `missing-drum-piece` warnings only without `singlePiece` |

The 422 rules themselves are the frozen `schemas.py` validators (architect); the routes only surface them.

## Tests and quality

See HO:us4 (one worker run for the US1-US4 checkpoints).

Security self-review: the new fields are pydantic-validated before they reach the notator (chord symbols are
structured: pitch classes and a quality enum, no free text, at most 4000; `singlePiece` is a drum-piece enum); no new path, file or network input.

## Decisions / deviations / requests

None.
