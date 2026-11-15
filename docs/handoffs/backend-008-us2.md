# Hand-off: backend / 008-us2 (T046-T047)

## What was built

No new production code. The T042 planner already covers US2, and T046 proves it:

- `keyOverride` must be one of the 30 key names (case-sensitive; 422 `keyOverride`). It is stored in the recipe
  and sent to the worker.
- `resolution` is stored and sent.
- A track without a reliable beat grid and without `tempo` gets 422 `tempo-required`.
- A `tempo` is stored and sent as the `manual` grid, together with the range. The first downbeat is relative to the
  range start, as the contract says.
- A range shifts the track grid by its start (beats before 0 are kept).
- 422 `range` when the range is under 2 s or outside the track; 422 `tempo.*` outside 30..300 bpm or 1..12 beats
  per bar.
- The options report `gridStatus` `unreliable`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T046/T047 | 2a3a8a5 (green on its first run, as in 007 US2) | b5da236 (T042) |

## Tests and coverage

`ScoreKeyTempoTests`: 10 passed. See HO:polish for the component numbers.

## Quality checks

Clean (see HO:polish).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend T048/T049.
