# Hand-off frontend-022, checkpoint C (US4 library info + help + polish): T074-T075, T084, T087

Lane `frontend-022`, branch `r22-lane-fe022`, 2026-11-17. Final checkpoint of the lane; earlier ones:
`frontend-022-a.md` (T018-T019, T036-T037), `frontend-022-b.md` (T046-T053, T064-T069).

## Delivered

| Task | What | Files (under `src/StemStudio.Web/`) |
|---|---|---|
| T074/T075 | `LibraryInfoDialog` (`library-info-dialog`, `data-scope` track/item, `data-help-id=library-info`): three labelled inputs (maxlength 200), "Automatic" boxes whose label shows the automatic name and its source ("from band info", "from the import", "fallback", ...), client validation as the API (1-200 after trimming, no control characters; `aria-invalid` + `aria-describedby`), 400 field messages at the field and in `library-info-error`, 404/409 readable, Reset (DELETE), Cancel/Escape, focus capture of `ModalDialog`; `library-info-lookup` (track scope) links to the track page. `library-info-edit` in a song folder (only while its track exists) and `asset-library-info-<id>` on every item row (song folder, search, All items), every level. After a track save the view follows to the new song folder; after an item save the list reloads | `Components/Library/{LibraryInfoDialog.razor(.cs),FolderView.Filing.cs,AssetTable.Filing.cs,SongFolder.razor,AssetRow.razor(.cs)}`, `Library/LibraryInfoForm.cs` |
| T084 | Help topics `library-folders` (screen topic of `/library/{folderid}`), `library-search`, `library-info`, `library-art` in `Help/topics/mixes/` (CTO decision: existing area); `LibraryHelpTests` (titles, area, anchors in markup, routes, help search) | `Help/topics/mixes/library-*.md`, `tests/.../Help/LibraryHelpTests.cs` |
| T087 | Polish: `LibraryAccessibilityTests` (unique ids and test ids, named buttons whose label contains the visible text, decorative sized pictures, labelled inputs, one `h1` per screen; root, song folder and search at Beginner and Advanced), target size 24 px on small links, marks underlined + bold, scroll padding above the bottom bar under 768 px, 320 px grid (2 shrinkable columns, long names wrap), compact 128 px, no animation/transition/transform in the 022 styles (reduced motion has nothing to stop), 8 tones from theme tokens; dead code removed | `wwwroot/css/components/library.css`, `tests/.../Library/LibraryAccessibilityTests.cs` |

## Tests and coverage (whole lane)

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2605 (lane start: 2429)
| StemStudio.Web | 99.64% line | 93.83% branch | 99.72% method |   gate 90/90 met
lane files (46): 99.6 % line (834/837), 92.1 % branch (1218/1322)
src/web-interop: 306 passed, 99.72 % stmts / 98.19 % branch (gates held), lint + typecheck clean
dotnet format --verify-no-changes (src/StemStudio.Web, tests/StemStudio.Web.Tests): clean; build: 0 warnings
tools/quality run_all.py: 0 errors, 0 clones (exit 2 only from "tool error: jscpd failed", environment);
  css_tokens_check: 0 errors
```

## Decisions (this checkpoint)

1. **Following a track save** (FR-032 "the view follows"): the track response has no placement, so the dialog asks
   `getAssetLibraryInfo` for one item of that track in the open folder whose three levels are not `item`-sourced
   (it moves with the track) and opens its new song folder; if there is none, or the lookup fails, the folder
   reloads (an emptied folder shows `library-folder-missing`). Contract gap candidate: a `songFolderId` in the track
   `LibraryInfoState` would save the extra call (CTO to decide; not needed for correctness).
2. `library-info-lookup` links to `tracks/{trackId}` (the 005 band info panel and its "Correct" action are there;
   there is no deeper anchor).
3. Typing in a name unticks "Automatic"; ticking it clears the typed name; an empty field counts as automatic.
4. 022 adds no navigation item, so the 720 px height check of the rail is not affected (no rail change).

## Requested edits to nobody-owned files (all checkpoints)

- `contracts/web-interop.md` -> **1.2.0**: scope `library`, key `/` -> `library.search.focus` (see B).
- `docs/public/**` user manual "Library" chapter: folders, art, live search (incl. `/`), library info; rebuild PDFs.
- Optional editorial: `Help/topics/mixes/library.md` (007 topic, screen topic of `/library`) still says "From the
  Advanced level you can filter by track and kind and search by name"; it could add one line pointing to
  "All items" and the `library-folders` topic.
- Shortcut overview / 014 catalogue row for `/` (owner 022).

## Deviations

None from the contracts. Files outside the nominal write list are listed in `frontend-022-b.md` ("Deviations /
files outside the nominal list"): `components/library.css` and the mechanical `AllItemsView` test migration.

## Open

None for this lane. Integration: qa page objects open `/library?view=all` for the 007/008 specs (lane qa).
