# Hand-off: backend / 008-us5 (T066-T067)

## What was built

No new production code. T042 covers US5, and T066 proves it:

- `source = {assetId}` accepts ready audio items only (422 `source.assetId` otherwise). The item is read from
  `assets/<id>/audio.<ext>` and its audio duration is used.
- The item suggests a profile only for a raw single-channel export with a pitched suggestion. A mixed export has no
  suggestion, so the request needs `profile` (422 otherwise).
- The provenance track's grid is shifted by the export range start plus the score range start.
- When the track is gone, `gridStatus` is `none` and a `tempo` is required.
- The provenance records `sourceAssetIds = [assetId]` and the track id.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T066/T067 | aa78d9f (green on its first run) | b5da236 (T042) |

## Tests and coverage

`AssetScoreTests`: 5 passed. See HO:polish for the component numbers.

## Quality checks

Clean (see HO:polish).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend T068/T069.
