# Hand-off: backend / 008-us3 (T052-T053)

## What was built

- The piano options list `accurate` and `fast` with their estimates. The factors are configurable
  (`StemStudio:ScoreEstimates`, tested with overridden values).
- The default quality is `accurate` on a GPU worker and `fast` on a CPU worker. An explicit quality is kept and
  sent.
- **New (T053)**: a piano CRNN score without a worker attribution carries the piano model credit
  (`ProfileCatalog.PianoAttribution`, CC BY 4.0). A worker attribution wins. Other engines keep the worker's value
  (null).
- The worker's `cpu-piano` warning appears in `score.warnings`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T052/T053 | c625b9a | fe420e5 |

## Tests and coverage

`PianoScoreTests`: 6 passed. See HO:polish for the component numbers.

## Quality checks

Clean (see HO:polish).

## Dependencies added

None.

## Open issues / requests

- **CTO / integration wave**: refine the `StemStudio:ScoreEstimates` defaults on the reference machine. The
  contract example's 515 s for piano accurate on the CPU does not follow from the data-model 4.5 formula for 262 s
  of audio: that gives 510 s with the 60 s download.

## Next owner

frontend T054/T055.
