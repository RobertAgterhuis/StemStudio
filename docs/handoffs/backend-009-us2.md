# Hand-off: backend / 009-us2 (T046-T047)

## What was built

`GET /api/styles` (`StyleService.ListAsync`, `StyleListQuery`): filters `kind`, `genreFamily` (wire names,
case-insensitive), `mood`/`instrument` (case-insensitive tag equality, at most 30), `q` (case-insensitive substring
of name, description and tags, at most 100); invalid values -> 400 per field; presets first, then by name.
`GET /api/styles/{styleId}` (200/404). `StyleMapper` maps styles, snapshots and editor inputs.
Un-staged `listStyles`, `getStyle`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T046/T047 | 9a7b837 | e89f06e |

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests --filter "FullyQualifiedName~Styles|FullyQualifiedName~BackendContract"`:
56 passed. Component numbers are reported cumulatively at HO:us6.

## Quality checks

Build 0 warnings, `dotnet format` applied.

## Dependencies added

None.

## Open issues / requests

None new.

## Next owner

backend (009 US6: T053-T054).
