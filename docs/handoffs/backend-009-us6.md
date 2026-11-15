# Hand-off: backend / 009-us6 (T053-T054)

## What was built

`GET /api/generation/status` (`GenerationStatusService` over `GenerationAvailability`): `notInstalled` with
"The generator service is not installed." when `StemStudio:GeneratorBaseUrl` is unset (no request is made) or
`/health` does not answer within 2 s; `unavailable` with the generator's probe reason and its device
(`ProcessingDevice`, `reachable: true`); `ready` with the models and `downloadBytesNeeded` (sum of absent models).
Cached 10 s (`FakeTimeProvider` test). `voice` is `notInstalled` ("The singer service is not installed.") until 010.
`createRestyle`/`retryGeneration` answer 409 `not-installed`/`gpu-required` from the same readiness (US1).
Un-staged `getGenerationStatus`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T053/T054 | cc239cf | e5bc578 |

## Tests and coverage

`GenerationStatusTests` 5 passed (part of the Api run in backend-009-us3). Build 0 warnings; format clean.

## Dependencies added

None.

## Open issues / requests

None new.

## Next owner

backend (009 US3).
