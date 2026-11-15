# Hand-off: backend / 009-us3 (T058-T059)

## What was built

`POST /api/styles`, `PUT/DELETE /api/styles/{styleId}`, `POST /api/styles/{styleId}/duplicate`
(`StyleEditingService`, `LibraryStyleGuard`, `StyleProblems`):

- field limits -> 400 (standard validation problem, `errors` per camelCase field);
- imitation guard -> 400 `type: imitation`, title "Styles cannot imitate an artist.", `errors` keyed by the
  offending field (`name`, `subGenre`, `instrumentation`, `moods`, `description`); the guard includes the artist and
  metadata artist names of the user's own tracks (qa's `libraryArtist` case is a test);
- case-insensitive name clash (also with presets) -> 409 `style-name-taken`; presets -> 403 `preset-read-only`;
- duplicate of a preset or user style -> user style "<name> (copy)", "(copy 2)", ...;
- generations keep their style snapshot after an update or delete (test).

Un-staged `createStyle`, `updateStyle`, `deleteStyle`, `duplicateStyle`. Only `importTake` remains staged for 009.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T058/T059 | 62a1e12 | e1ef757 |

## Tests and coverage

Reported cumulatively at HO:us4.

## Quality checks

Build 0 warnings; `dotnet format` clean.

## Dependencies added

None.

## Open issues / requests

None new.

## Next owner

backend (009 US4: T063-T064).
