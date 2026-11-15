# Hand-off: frontend / 005 US2 (band info)

Tasks 005:T041 (red) and T042 (green).

## What was built

- Client: `IStemStudioApiClient.GetBandInfoAsync` (`GET /api/tracks/{id}/band-info`) and
  `LookupMetadataAsync(trackId, MetadataLookupRequest?)` (`POST .../metadata/lookup`; null sends `{}` = retry).
- `Components/BandInfo/`:
  - `BandInfoPanel` (+ code-behind): `aside#band-info-panel` (`band-info-panel`, `data-help-id="band-info"`,
    `aria-labelledby`), focus moves to its heading on open, Escape / `band-info-close` closes it. Loads the band
    info and polls it while `none`/`pending`/`searching` (backend note: the first GET of an old track moves it to
    `pending`); raises `OnChanged` when a lookup settles so the host reloads the track (cover, status).
  - `BandInfoStatus`: `band-info-status` with `data-status` at every state; searching skeleton; "No band
    information found. ..."; failed = backend reason or "Band information could not be retrieved; try again
    later."; disabled + `band-info-disabled` (first link `/help/online-lookups`, `data-help-id="online-lookups"`).
  - `BandFactsView`: `band-info-name` (name only), `-type` ("Group", "Person", ...), `-years` ("2001 - present",
    "1981 - 2001"), `band-info-album` ("Synthetic Sessions (2020)"), and behind the new catalogue feature
    `artist.details` (Advanced+): `-country` (English region name, code when unknown), `-genres`, `-members`
    (groups with members; "Ada Sample (since 2001)", "Bo Waveform (2001 - 2015)"), `-website`, `-sources`
    ("Data: MusicBrainz (CC0), ..."). Facts exist only when known.
  - `WikipediaAttribution`: `band-info-attribution` with links "<article title>", "Wikipedia contributors",
    "CC BY-SA 4.0" (target `_blank`, `rel="noopener noreferrer"`) and "Excerpt, unmodified."
  - `CorrectMetadataDialog` (+ code-behind): modal, prefilled title/artist, both 1..500 after trimming
    (`MetadataNames`), Enter in a field submits, Escape cancels, focus returns to the opener.
  - `BandInfoToggle`: `band-info-open` button with `aria-expanded`/`aria-controls`.
- Track page: `metadata-status` (`data-status`, readable text) and `band-info-open` in the header; polling now also
  runs while the lookup is pending/searching. Mixer: `band-info-open`, `mixer-album` (found + album only), header
  refresh (picture, metadata) when the panel settles; a failed refresh keeps the header.
- Import legal notice: new sentence in `<span data-help-id="online-lookups">` (FR-014).
- Help topics `tracks/band-info.md` (feature `artist.details`), `start/online-lookups.md`; `artwork` links both.
- `Services/PollLoop.cs`: one shared polling loop (track page + panel; later the library), removing a duplicate.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T041 / T042 | 1d0f157 (build fails: BandInfo components and client methods missing) | 2e8aa74 |

`FeatureCatalogTests` got the `artist.details` row together with the catalogue entry (green commit).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 879, Skipped: 0, Total: 879
| StemStudio.Web | 99.26% line | 93% branch | 100% method |
```

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; `run_all.py`: 0 errors, 240 warnings (none in the new
005 files); `css_tokens_check`: 0.

## Dependencies added

None.

## Deviations / notes for qa

- Extra ids (not in ui-test-ids 1.4.0, additive): `band-info-close`, `band-info-album`, `band-info-error`
  (load/retry failure), `metadata-correct-cancel`, `metadata-correct-error`. Proposed for the next minor.
- `band-info-correct` renders at Beginner (ui-test-ids table) although spec FR-015 lists the correction dialog
  under Advanced/Expert; the binding id table wins.
- The panel is an in-page `aside` (not a modal), so a help link inside it opens the help panel normally.
- The mixer does not poll by itself; `mixer-album` appears when the track was already found on open or after the
  panel settled.

## Next owner

frontend (005 US3), qa (005:T043).
