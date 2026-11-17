# Hand-off frontend-022, checkpoint A (foundational + US1): T018-T019, T036-T037

Lane `frontend-022`, branch `r22-lane-fe022` (from 4486ddb7), 2026-11-17.

## Delivered

| Task | What | Files |
|---|---|---|
| T018/T019 | Typed client for the 9 `library` operations (ETag: `knownRevision` -> `If-None-Match: "lib-<rev>"`, `304` -> `null`; 404/422/400/409 as `ApiException` with code and field messages) | `src/StemStudio.Web/Api/{IStemStudioApiClient,StemStudioApiClient}.Library.cs`, `src/StemStudio.Web/Library/LibraryRequests.cs` (`LibraryFolderRequest`, `LibrarySearchRequest`) |
| T018/T019 | Pure helpers: `LibraryLocation` (routes, `?q=`, `?view=all`, URL building, 100-char cut, folder id validation), `HighlightedText` (clip, merge, surrogate-safe), `FolderArtModel` (cover/mosaic 2-4/placeholder, `srcset` thumb 160w + cover 600w, `sizes`, tone class 0..7, 160/128 px), `LibraryLinks.Item` | `src/StemStudio.Web/Library/*.cs` |
| T036/T037 | Export and sheet music confirmations show `export-saved-folder`/`score-saved-folder` (three links, separators `aria-hidden`) and `*-saved-open` links to `library/<songFolderId>#asset-<id>`; without a placement the 007 link stays | `Components/Export/ExportDialog.razor(.cs)`, `Components/Scores/ScoreDialog.razor`, `Components/Library/FolderPath.razor`, `Components/Library/_Imports.razor` |

Tests: `tests/StemStudio.Web.Tests/Library/{LibraryApiClientTests,LibraryHelpersTests}.cs`,
`Export/ExportSavedFolderTests.cs`, `Scores/ScoreSavedFolderTests.cs`; fakes `Fakes/LibraryData.cs` hold the contract
examples `RootView`, `SongView`, `EmptySearch`, the four problem examples and the 1.22.0 `Asset` placement verbatim.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2434
| StemStudio.Web | 99.66% line | 94.1% branch | 99.75% method |   gate 90/90 met
lane files (11): 100 % line (193/193), 95.4 % branch (290/304)
```

## Decisions (CTO-confirmed 2026-11-17)

1. `/library?view=all` keeps the 007 client-side `library-search` filter of the flat list; the folder levels use the
   live search. Ids stay unique per page.
2. The Advanced-only 022 ids (`library-sort`, `library-view-all`, `library-view-folders`, `library-search-scope`) are
   gated with the existing catalogue feature `library.filters` (Advanced); `FeatureCatalog.cs` needs no edit.
3. Grid/list and sort are stored per device under BrowserStorage key `stemstudio.library` (KaraokePreferences
   pattern), not in `UserPreferences`.
4. T068 (`/` shortcut): option (a) - this lane adds scope `library` (`ShortcutScope.Library = 2`) with
   `{ key: '/', command: 'library.search.focus' }` in `src/web-interop` (checkpoint B).
5. Help topics go into the existing `Help/topics/mixes/` area (help-content allows only existing areas), not
   `topics/library/` (checkpoint C).
6. Own: picture URLs in folder art are used only when they are API artwork paths (`/api/tracks/...`, no `..`, no
   spaces); anything else renders the placeholder (defence in depth; the API is trusted).
7. Own: `ExportDialog.razor.cs` (code-behind of an owned component) carries the placement in `SavedExport`.

## Requested edits to nobody-owned files

- `web-interop.md`: row for scope `library` / command `library.search.focus`, label **1.2.0** (follows at checkpoint B).

## Open

Checkpoint B (US2 + US3: T046-T053, T064-T069) and C (T074-T075, T084, T087).
