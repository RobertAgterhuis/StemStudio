# Hand-off: stem-separation (lane L3) / 016 HO:us3

Branch `016-round-lane-l3`, not pushed, not merged. Tasks T084, T085 (ticked).

## What was built

| Task | Commits | Result |
|---|---|---|
| T084 | 11d4ee29 (with T061), 8230fdc7 | Route tests of `kind: drum-hits`: the 008 `DrumTranscriber` (fake) gets the resolved file per given piece; `DrumHits` with `engine`, `pieces`, hits in time order; progress and stage `analysing` while running; added in this session: hits in the same millisecond keep the kit order, time/strength clamps, the `MAX_DRUM_HITS` cap, a `TranscriptionError` fails the job with its text, deleting a running job (parametrized over all three kinds) |
| T085 | dd685fd1 (with T062), 73e30d7f | `InstrumentAnalysisService._drum_hits`: `transcribe(files, None, progress 0.05..0.95)`, hits rounded to ms (>= 0), strength clamped to 0..1, at most 40,000. Fix in this session (red in 8230fdc7): simultaneous hits were re-sorted alphabetically (`cymbals` before `kick`); they now keep the kit order kick, snare, toms, hihat, cymbals (the `DrumPiece` literal order, as the 008 transcriber) |

Composition: the real `LibrosaDrumTranscriber` (`drum-onsets`), or `views_fake.FakeDrumHitsTranscriber` (`fake`)
when the 008 transcriber is `fake` (`STEMSTUDIO_TRANSCRIBER=fake`, or unset with `STEMSTUDIO_SEPARATOR=fake`: the
E2E stack).

## Reading of the task text

T084 says "missing pieces listed in `pieces`". The contract (`DrumHits.pieces`: "Pieces that had an input") and
data-model 4.8 list only the pieces with an input, so a request without `toms` gives `pieces` without `toms`
(test `test_missing_drum_pieces_are_not_listed`). The api always sends the five detail paths (data-model 7.4); a
missing file is a 404 at create. Not a deviation; please confirm if the task meant something else.

## Tests and quality

See HO:us4 (one worker run for the US1-US4 checkpoints).

Security self-review: drum paths pass the path guard (absolute or `..` paths 400, missing 404); hit counts are capped
before the response; strengths and times are clamped so a misbehaving transcriber cannot produce an invalid
response.
