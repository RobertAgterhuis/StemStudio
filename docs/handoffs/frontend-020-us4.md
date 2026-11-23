# Hand-off: frontend / 020 HO:us4 (T085, T086)

Branch `020-lane-frontend`. Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T085/T086 | 63586762 | 86be79ff | Setlist page (`SetlistEditor`): `HandoutMenu` with prefix `setlist-handout` (`setlist-handout-menu`, `setlist-handout-family-<family>`, `setlist-handout-customise` at Advanced+) in the action row right after 014's unchanged `setlist-pdf`; one click posts `{source: {setlistId}, family}`; "Customise..." opens the shared `handout-dialog` for the setlist without create links; `handout-saved`/`handout-saved-open`/`handout-missing` as on the track page |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 4043/4043 | 99.1 % line, 92.52 % branch (gate 90/90) |

`dotnet build StemStudio.slnx` 0 warnings; format clean; `run_all.py` PASS (0 errors; the only SetlistEditor warning,
complexity of `OnAfterRenderAsync`, is 014 code I did not change).

## Decisions

1. An empty setlist shows no handout menu (the backend answers 409 `setlist-empty`); more than 50 songs cannot occur
   on this page because 014 caps a setlist at `MaxSetlistEntries`; a 409 `too-many-songs` would still show in
   `handout-error`.
2. Progress (US4 AS2): the item is queued/rendering in the library, where the 007 row shows the status and progress
   bar; the setlist page links there via "Open in library".
3. Create links are not offered in the setlist dialog: the missing parts belong to individual songs (Q3: existing
   flows only, which are per track).
4. Help: topic `handouts` gets the anchor `setlist-handout-menu` (literal `HelpHint` next to the menu); it already
   explains the difference with "PDF for the stand".

## Deviations

None.

## Open items

None. G2 tasks T097/T098 were not built (deferred).
