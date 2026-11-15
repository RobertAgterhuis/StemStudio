# Hand-off: backend / 008-us4 (T061-T062)

## What was built

No new production code. T042 covers US4, and T061 proves it:

- Before the drums are split, `drums` answers 409 `drums-not-split`. The options for that state are
  `drumsRequireSplit: true`, allowed `[drums]` and one drums estimate.
- After the split, the request carries `drumInputs` with the kept `tracks/<id>/detail/drums/<piece>.flac` paths and
  no `input`. Missing pieces are sent as null, and the worker adds the `missing-drum-piece` warning.
- `drums.<piece>` channels and pitched channels cannot use the drums profile (422 `profile`).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T061/T062 | 487fc2c (green on its first run) | b5da236 (T042) |

## Tests and coverage

`DrumScoreTests`: 5 passed. See HO:polish for the component numbers.

## Quality checks

Clean (see HO:polish).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend T063/T064.
