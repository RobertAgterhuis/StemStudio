# Hand-off: backend / 010-us1 and 010-us2 (T027-T030, T034-T035)

## What was built

- **US1** `POST /api/songs` (`SongService`): lyrics from a sheet (404 unknown) or inline text (normalised; empty
  only for instrumentals, which send `[Instrumental]`), style (404 unknown) or free caption (400 both), `SongRules`
  (bpm 40-220, key `^[A-G](#|b)? (major|minor)$`, time signature 2/3/4/6, 10-480 s, 1-2 takes, delivery <= 40,
  caption <= 300), language list, imitation guard on `freeCaption`/`delivery` (400 `imitation`, `errors` keyed by
  field; library artists included), availability gate (409 `not-installed`/`gpu-required`), caption from
  `SongCaptionBuilder` (contract vectors), density warning (> 4 words/s) in `warnings[]`, JSON job to the
  generator (`GenerationSubmitter` routes by kind). `POST /api/lyrics/sections` (nothing stored). Collected takes
  keep the generator's vocal label; song library items are titled `<lyrics title> - <style> (take n)` or
  `<lyrics title> (take n)`.
- **US2** `/api/lyrics` CRUD, search (`q` <= 100 on title and text) and `POST /api/lyrics/upload` (.txt/.lrc/.md,
  strict UTF-8, BOM stripped, <= 64 KiB; 400 / 413 / 415; qa `sample.lrc` normalisation and `.pdf` -> 415 are
  tests); songs keep their snapshot after a sheet edit or delete.
- Un-staged: `createSong`, `proposeSections`, `listLyricsSheets`, `createLyricsSheet`, `uploadLyricsSheet`,
  `getLyricsSheet`, `updateLyricsSheet`, `deleteLyricsSheet`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T027/T028 | 7f28868 | 39a0237 |
| T029/T030 | 36a65d9 | 9ad77b1 |
| T034/T035 | 52be722 | 6e58629 |

## Tests and coverage

Song/lyrics tests 66 passed with the contract tests; cumulative numbers in backend-010-polish. Build 0 warnings,
format clean.

## Deviations

- `wordsPerSecondAt120s` is words / 120 without rounding (qa fixture 0.775); the contract example shows 0.24 for a
  30-word text (0.25 here).
- Unknown `lyricsId`/`styleId` in `createSong` answer 404 (contract lists 404; 009's restyle uses 400 `styleId`).

## Dependencies added

None.

## Next owner

backend (010 US3).
