# Hand-off: backend / 020 US3 song handouts and practice notes (T056-T067)

| Task pairs | Red | Green |
|---|---|---|
| T056/T057, T058/T059, T060/T061, T062/T063, T064/T065, T066/T067 | 22c60833 | 2842666b (refactor 52be7bf8) |

## What was built

- Domain (`src/StemStudio.Domain/Handouts/`): `HandoutChords` (adapter over 016 `ChordSpeller`, `ChordDisplay`,
  `ChordSheetBuilder`, `KeyFormatter`; no own parser): B-flat +2, E-flat +9, F +7 with the target key's spelling,
  `transpositionNote`, chords over lyrics (word/line/none) and plain lyrics; `BarMapper` (014 start bar when set, else the
  grid; pickup 0; downbeats inside; chords per bar); `Families` (labels, table 6.2, notation fit 6.1, defaults newest per
  kind); `IngredientPlanner` (defaults, omissions `not-selected`/`not-available`/`no-timing`, 422 texts for codes not
  offered); `HandoutAvailability`; `HandoutNotice` (attributions that apply) and `HandoutNaming`.
- Api (`src/StemStudio.Api/Handouts/`): `HandoutMaterialLoader` (source ports, lyrics, artwork path, practice note),
  `HandoutPlanner` (404/409/422), `HandoutAssembler` (resolved worker document: fixed notice, `generatedOn` from the clock,
  app version), `HandoutService` (create, "Make again" = post the recipe, retry via `POST /api/assets/{id}/retry`),
  `HandoutOptionsService`, `HandoutJobApplier` in the asset sync (PDF to `assets/<id>/handout.pdf`, SHA-256 check, result
  check, warnings/outline cut to the contract limits), `PracticeNoteService`, endpoints (`getHandoutOptions`,
  `createHandout`, `getPracticeNotes`, `setPracticeNote` moved to `InsightsOperations`).
- `Asset.handout` is shown only when the item is ready (plan stored at creation); `Provenance.handoutRecipe`;
  `listAssets?kind=handout`; `getAssetFile` serves the `pdf` with the 1.7.0 rule; the download name is the item name
  "<Title> - <Family label> handout.pdf" (qa assumption holds).

## Decisions and readings (please confirm)

1. Worker refusal of a handout job (400/404/422) -> 422 with field `document` and the worker's detail; nothing stored.
2. Repeated bars keep their chord symbols: the contract's `ChordSymbol` pattern has no `%`, so no repeat sign is sent
   (task T058 named `%` repeats; the renderer may draw one). Adjacent equal chords in one bar are merged.
3. `LyricLine.section` is always null (016 sheet rows carry no times); drum "cues" are the plain lyrics.
4. "(in above)" lyrics of overview/vocals are not offered separately (inside chords over lyrics).
5. Ingredient availability: chord chart needs sections and a reliable grid (`no-timing` without grid); chords over lyrics
   need chords and synced lyrics (`no-timing` for plain lyrics).
6. Default paper a4; `estimateSeconds` = 4 + 2 x songs (6 for one song, as the contract example).
7. Melodic C label "Melodic instruments"; others "Melodic instruments in B-flat/E-flat/F".
8. `setlist-notes` is offered (default) for setlist handouts only.

## Security self-review (untrusted input)

Worker results are checked before storing (quality rules; handout result: content type, pages >= 1, counts, warning codes,
PDF hash; 64 MiB cap while streaming). Paths sent to the worker are built from our ids only (`assets/<guid>/score.pdf`,
`tracks/<guid>/artwork/{auto,user}/cover.jpg`); `notationAssetIds` must be ready score items of the songs (422 otherwise,
no foreign items). Query enums are parsed by exact wire names. Practice notes: 4,000 limit, control characters except
`\n` removed (HTML is escaped by the worker's templates). Item names via `AssetNaming.Validate`; download names sanitised
by the existing rule. New write endpoints inherit refactor 003's host/Origin rule (tests use the loopback client).

## Open items

- `AssetLibraryService` (delete of an active item) calls `DeleteRenderJob` for a handout job; the worker sweeps it after
  24 h. Small follow-up (not changed to keep the 007 path untouched).
- LibraryCare shows an active handout as "export" work and backs it up in the Exports category.
