# Hand-off frontend-022, checkpoint B (US2 browse + US3 live search): T046-T053, T064-T069

Lane `frontend-022`, branch `r22-lane-fe022`, 2026-11-17. Builds on checkpoint A (`frontend-022-a.md`).

## Delivered

| Task | What | Files (under `src/StemStudio.Web/`) |
|---|---|---|
| T046/T047 | `LibraryPage` is now the route host of `/library` and `/library/{folderId}`: follows the URL (`LocationChanged`), shows `FolderView` (breadcrumb `nav` landmark with `aria-current`, collapse under 480 px, header art/title/counts, `library-filing-pending`, `library-folder-missing`, polling about every 2 s while items are queued/rendering or the backfill runs, `If-None-Match` on polls, `503 library-maintenance` transient) or "All items" (`?view=all` at Advanced, and `?asset=` links from before 1.22.0) | `Components/Library/{LibraryPage,FolderView,LibraryBreadcrumb}.*` |
| T048/T049 | `FolderGrid`/`FolderTile`/`FolderArt`: one link per tile named "<name>, <level>, [year,] <counts>" from its content (level word `ss-sr-only`, no `aria-label`, so the visible text is in the name), `alt=""` images with width/height, lazy, `srcset`/`sizes`, mosaic 2-4 + tone cells, glyph `aria-hidden`, `@onerror` fallback, grid/list persisted (`stemstudio.library`), year on album tiles, compact 128 px; 8 art tones | `Components/Library/{FolderGrid,FolderTile,FolderArt}.razor`, `Library/{FolderText,LibraryViewPreferences}.cs`, `wwwroot/css/components/library.css` |
| T050/T051 | `SongFolder`: 007 rows via the new shared `AssetTable` (rename/delete/retry/sheet music unchanged, extracted from the old `LibraryPage`), `asset-path-<id>` (`FolderPath`), row anchor `id="asset-<id>"`, `song-open-track`/`song-open-mixer` only while the track exists, API order kept | `Components/Library/{SongFolder,AssetTable*,AssetRow*,AllItemsView*,FolderPath}` |
| T052/T053 | Levels: Beginner sees folders, art, breadcrumb, search, layout, item actions; `library-sort`, `library-view-all`/`-folders`, `library-search-scope` gated with `library.filters`; sort select per child level (year only for albums, default year/name), stored per level, sent as `sort` | `FolderView.razor(.cs)`, `Library/FolderSortOptions.cs` |
| T064/T065 | `LiveSearchController`: 200 ms debounce on `TimeProvider`, Enter at once, sequence numbers drop stale answers, cancellation, `?q=` replaced while typing / pushed on Enter, clear/Escape restore the folder URL and focus, scope, "Show all" (limit 200), 100-char cut | `Library/LiveSearchController.cs` |
| T066/T067 | `LibrarySearchBox`, `SearchResults`, `SearchGroup`, `MarkedText`, `LibraryAnnouncer` (polite count once 500 ms after the last change); `<mark data-testid=search-mark>` underlined + bold; item hits are 007 rows with marks, path links and matched instruments; `library-search-empty` with tips and clear; `library-search-error` keeps results; Arrow Down to the first hit (or the item table) | `Components/Library/{LibrarySearchBox,SearchResults,SearchGroup,MarkedText}.*`, `Library/LibraryAnnouncer.cs` |
| T068/T069 | `/` focuses the search: scope `library` in web-interop (`{ key: '/', mod: false, command: 'library.search.focus' }`, typing guard as the other keys, no conflict with Mod+K / Mod+/), `ShortcutScope.Library = 2`, registered by `LibraryPage` | `src/web-interop/src/{shortcuts,types}.ts`, `src/web-interop/tests/shortcuts-library.test.ts`, `Interop/ShortcutScope.cs` |
| (T084 part) | Help topics `library-folders` (screen topic of `/library/{folderid}`), `library-search`, `library-art` in `Help/topics/mixes/` | `Help/topics/mixes/library-{folders,search,art}.md` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2542
| StemStudio.Web | 99.53% line | 93.85% branch | 99.62% method |   gate 90/90 met
lane files (41): 98.6 % line (727/737), 92.0 % branch (1060/1152)
src/web-interop: npm run test:coverage -> 306 passed; 99.72 % stmts / 98.19 % branch (gates held); lint + typecheck clean
tools/quality run_all.py: 0 errors (exit 2 only from "tool error: jscpd failed", environment, 0 clones reported)
```

New tests: `tests/StemStudio.Web.Tests/Library/{LibraryTestContext,LibraryFolderPageTests,FolderTileTests,SongFolderTests,LibraryLevelTests,LiveSearchControllerTests,LibrarySearchTests}.cs`.

## Decisions

1. `/library` opens the folders; the 007 flat list is `AllItemsView` (`?view=all`, Advanced; Beginner gets the
   folders). A `?asset=` link (confirmations before 1.22.0) also opens "All items" with the row highlighted.
2. In "All items" the live search box is not rendered (the 007 `library-search` filter is there, ids unique).
3. Song items keep the API order (FR-018 order is the backend's); the UI does not re-sort.
4. Folder polling: the shared `IPollingTimer` ticks every 1 s; `FolderView` refreshes only when 2 s passed
   (`FolderView.PollEvery`, `TimeProvider`) and sends the shown revision as `If-None-Match` (304 keeps the view).
5. Art tones are `color-mix()` of existing theme tokens in `components/library.css` (allowed by
   `css_tokens_check.py`), so `tokens.css` is untouched; ink letter >= 4.5:1 on 32 % mixes in both themes
   (checked by hand: lowest about 6:1, dark warning tone).
6. "Show all" re-asks the search with `limit=200` for all groups; groups not expanded still show 20.
7. Duplicate ids from the API (folders or items) render once (`DistinctBy`), so a bad answer cannot crash the
   renderer through duplicate `@key`s.

## Deviations / files outside the nominal list (please confirm at integration)

- `wwwroot/css/components/library.css` (appended a 022 section): the brief names `wwwroot/css/library*`, but a new
  sheet needs an `App.razor` entry (not mine); the existing `library` sheet is already loaded.
- Mechanical test migration `Render<LibraryPage>` -> `Render<AllItemsView>` (the 007 list moved; tests mirror the
  source owner): `Export/ExportAccessibilityTests.cs`, `Generation/TakeImportTests.cs`,
  `Scores/{ScoreAccessibilityTests,ScoreEntryTests,ScorePianoTests,ScoreSourceTests}.cs`,
  `Library/{LibraryManagementTests,LibraryPageTests}.cs`; `Skeleton/Feature004SkeletonTests.cs` now expects the
  scopes `Global, Mixer, Library`.

## Requested edits to nobody-owned files

- `contracts/web-interop.md` -> **1.2.0**: `ShortcutScope` adds `library`; key map row `/` -> `library.search.focus`
  (scope `library`, ignored while typing; Mod+/ stays `shortcuts.open`). The built `wwwroot/js/web-interop.js` is a
  build output (vite) and needs no commit.
- Help shortcut overview / 014 catalogue: row "`/` - Focus library search (library pages)", owner 022.

## Security self-review

Untrusted inputs are the URL (folder id, `?q=`) and API texts: folder ids are checked against the contract pattern
before any request and escaped in paths; `?q=` is cut at 100 characters and escaped; picture URLs are accepted only
as `/api/tracks/...` paths (no `..`, no spaces, no absolute or protocol-relative URLs; tests for `https://`, `//`,
`javascript:`); highlight ranges are clipped and surrogate-safe; names render as text (no `MarkupString`).

## Open (checkpoint C)

T074-T075 (library info dialog + `library-info` topic and its `data-help-id`), T084 remainder (`LibraryHelpTests`),
T087 polish (320 px / 720 px checks, reduced motion, accessibility CSS assertions, coverage of the remaining
branches).
