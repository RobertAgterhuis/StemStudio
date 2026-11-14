# Hand-off: backend / us4

## What was built

- `src/StemStudio.Api/Mixes/`:
  - `MixEndpoints`: `listMixes` (`GET /api/mixes[?trackId=]`, malformed `trackId` -> 400 on `trackId`),
    `createMix` (201 + `Location`), `getMix`, `updateMix` (PUT), `deleteMix` (204), `duplicateMix` (201 +
    `Location`). Mix routes use a `{mixId:guid}` constraint.
  - `MixValidator`: presence checks for the body (`name`, `metronome`, `channels`, each `channels[i]` and its
    `stem`); value rules come from the Domain (`MixSettings.Validate`). Error keys are JSON paths of the request:
    `settings.name`, `settings.metronome.bpmOverride`, `settings.loop.end`, `settings.channels[0].stem` for create;
    the same paths without the `settings.` prefix for PUT (its body is the settings); `trackId` stays at the root.
  - `MixService`: create -> 400 (shape or values; track not completed -> `trackId`), 404 unknown track (A1),
    409 "Mix name taken" (trimmed, case-insensitive via `NormalizedName`; the unique index also catches a
    concurrent clash); update -> 404/400/409/200 with a new `UpdatedAt` (the mix is unchanged on 400/409);
    delete keeps the track and stems; duplicate -> "<name> (copy)", "(copy 2)", ... (Domain `MixNaming`).
  - `MixMapper`: Domain <-> contract DTOs (UTC `DateTimeOffset`).
- `T134` contract tests: `getStem` (200/206/304/404/416 incl. `Accept-Ranges`, `ETag`, `Last-Modified`,
  `Content-Range`), every mix operation and status, and `/openapi/v1.json` serves exactly the 13 contract
  operationIds. Together with US1, **every operation of backend-api.openapi.yaml 1.0.1** is checked for status,
  declared headers, media type and body schema.
- SC-005: `SavedMix_SurvivesAnApiRestart` saves a mix, disposes the host, starts a new host on the same data
  directory and reads every setting back.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T102 / T103 mixes | 3d21a17 | f3652dc |
| T134 contract (test-only) | c0c0beb | (no production change needed) |

## Files changed

- `src/StemStudio.Api/Mixes/{MixEndpoints,MixService,MixMapper,MixValidator}.cs`, `src/StemStudio.Api/ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Mixes/{MixEndpointsTests,MixServiceTests}.cs`,
  `tests/StemStudio.Api.Tests/Contract/{BackendContractTests.StemsAndMixes,ContractAssert}.cs`,
  `tests/StemStudio.Api.Tests/TestSupport/ApiFactory.cs` (optional shared data directory for the restart test)
- `specs/001-stemstudio-mvp/tasks.md` (T102, T103, T134 ticked)

## Tests and coverage

| Project | Passed / Failed | Line | Branch | Method |
|---|---|---|---|---|
| StemStudio.Domain.Tests | 173 / 0 | 97.73 % | 97.72 % | 96.96 % |
| StemStudio.Persistence.Tests | 21 / 0 | 99.02 % | 100 % | 97.29 % |
| StemStudio.Api.Tests (Api) | 211 / 0 | 99.88 % | 98.61 % | 100 % |
| StemStudio.Api.Tests (Contracts) | (same run) | 100 % | 100 % | 100 % |

Command: `dotnet test tests/StemStudio.<X>.Tests -p:CollectCoverage=true` (gate 90 % line AND branch, passes).

## Quality checks

- `dotnet build StemStudio.slnx --no-incremental`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.

## Dependencies added

None.

## Open issues / requests

- A concurrent duplicate of the same mix could collide on the unique index; it answers 409 although the contract
  lists only 201/404 for `duplicateMix`. Extremely unlikely for a single local user; the architect may add 409 to
  `duplicateMix` in a later contract version.

## Next owner

backend (self): Polish (T111). CTO: review.
