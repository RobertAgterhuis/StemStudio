# Hand-off: backend / 009-us4 (T063-T064)

## What was built

`POST /api/generations/{generationId}/takes/{index}/import` (`TakeImporter`): runs the existing upload import with
the take file (`ImportService.ImportUploadAsync`, source key `upload:<sha256>`, worker contract unchanged), then
gives the track the take title (`<source> - <style> (take n)`), `Origin = generated` (generation, take, kind, source
track) and records `ImportedTrackId` on the take. 202 + `Location` for a new track, 200 for the same take again
(idempotent by SHA-256, no second upload), 404 unknown generation/take, 409 `generation-not-completed`, 503 when the
worker is down (nothing recorded). `TrackDto.origin` is mapped and absent for imported songs. `importTake` un-staged:
`PendingGenerationOperations` is empty.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T063/T064 | bce0792 | e0d2a59 |

## Tests and coverage

`TakeImportTests` 5 passed; cumulative numbers in backend-009-polish.

## Dependencies added

None.

## Open issues / requests

None new.

## Next owner

backend (009 US5).
