# Contract delta: UI test ids for feature 022 (1.22.0)

Status: **binding delta** (022 design, 2026-11-16). Base: the newest full `ui-test-ids.md` when 022
is built (1.21.0, or the merge of 012-021 and the claude-key follow-up; 1.10.0 is the last full
document on `main`). In Phase 0 (tasks.md T004) the architect writes the full document **1.22.0** =
base + this delta. Every rule of the base applies (R1-R9: `getByTestId` only, level visibility,
placement inside 004's layout). The names are binding now: the qa agent may author the 022 specs
against them.

## Changelog line (for the merged document)

> 1.22.0 (minor, feature 022; minor with the listed test migration confirmed by the CTO 2026-11-16) - library folders by artist, album and song with art, breadcrumb,
> grid/list, sort, live search with grouped results and highlights, library info dialog, "All
> items" view, folder path on items and in the save confirmation. No id is renamed or removed. Two
> documented behaviour changes on `/library` (section "Changed behaviour"); all other ids are
> unchanged.

## Changed behaviour of existing ids (owner request; migration below)

| Id (version) | Before | From 1.22.0 |
|---|---|---|
| `/library` screen, ids of 1.5.0/1.7.0 listed under "Library" except `nav-library`, `library-total-size`, `library-empty`, `library-search` | rendered on `/library` | rendered unchanged on **`/library?view=all`** ("All items", Advanced+); `asset-*-<assetId>` ids also render inside song folders and in search results (one row per item on a page) |
| `library-search` (1.5.0) | Advanced+; "name contains" | rendered at **every level**; the live search of this feature (FR-022..FR-029); still a `<input type="search">` |
| `library-total-size` (1.5.0) | total of all items | unchanged meaning; shown on the root and in "All items" |
| `export-saved-open` (1.5.0), `score-saved-open` (1.7.0) | link to the item on `/library` | link to the item in its song folder (`/library/<songFolderId>#asset-<assetId>`); still "the item on the library page" |
| `library-empty` (1.5.0) | no items or none match | no items in the library (root or "All items"), or none match the "All items" filters; search without matches uses `library-search-empty` |

**Migration** (part of the 022 tasks, red first): qa - `e2e/pages/library-page.ts` opens
`/library?view=all` for the 007/008 specs; `e2e/support/levels.ts` removes `library-search` from
`beginnerHiddenLibraryIds`. frontend - `tests/StemStudio.Web.Tests/Library/LibraryManagementTests.cs`
level assertion updated likewise.

## Level visibility (R7-R9 apply)

- **Beginner** renders every id below except: `library-view-all`, `library-view-folders`,
  `library-sort` and `library-search-scope`. The library info ids (`library-info-edit`,
  `asset-library-info-<assetId>`, `library-info-*`) render at **every level** (owner decision
  2026-11-16).
- **Advanced** renders every id below (the level of the existing suite).
- **Expert** = Advanced (no Expert-only ids).

## Ids

`<folderId>` is a folder id (`^(ar|al|so)[0-9a-f]{16}$`); `<assetId>` the item GUID in lower-case
`D` format; `<group>` one of `artists`, `albums`, `songs`, `items`.

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Library (`/library`, `/library/{folderId}`) | `library-folders` | the folder view container; attribute `data-level` = `root`/`artist`/`album`/`song` | shown |
| Library | `library-breadcrumb` | `<nav>` landmark; contains `library-crumb-root` and `library-crumb-<folderId>` per parent; the current folder has `aria-current="page"` | shown |
| Library | `library-folder-title` | `h1` of the open folder ("Library" at the root) | shown |
| Library | `library-folder-art` | header art of the open folder (not at the root); attribute `data-art` = `cover`/`mosaic`/`placeholder` | shown |
| Library | `library-folder-counts` | counts and size of the open folder (text) | shown |
| Library | `library-layout-grid`, `library-layout-list` | toggle buttons (`aria-pressed`) | shown |
| Library | `library-sort` | select of the child order (`name`/`year`/`recent`/`size`/`items`) | not rendered |
| Library | `library-view-all` | link/button "All items" (to `/library?view=all`); in that view `library-view-folders` returns to the folders | not rendered |
| Library | `library-view-folders` | in "All items": back to the folders | not rendered |
| Library | `library-filing-pending` | "Sorting your library (n items left)"; exists only while `pendingFiling > 0` | shown |
| Library | `library-folder-missing` | "This folder is empty now" for an unknown or emptied folder id, with links to the root and the search | shown |
| Library | `folder-tile-<folderId>` | one link per child folder; attributes `data-level` and `data-art`; accessible name "<name>, <level>, <counts>" | shown |
| Library | `folder-art-<folderId>` | the tile art (contains 1 `<img>` for cover, 2-4 for mosaic, or the placeholder) | shown |
| Library | `folder-name-<folderId>`, `folder-counts-<folderId>` | name and counts text of the tile | shown |
| Library | `folder-year-<folderId>` | album year; exists only for album folders with a year | shown |
| Song folder | `song-open-track`, `song-open-mixer` | links to the track page and its mixer; exist only while the track exists (first track when several) | shown |
| Song folder | `asset-row-<assetId>` and its 1.5.0/1.7.0 ids | the item rows (007 component, unchanged actions) | shown |
| Items (song folder, search, All items) | `asset-path-<assetId>` | path "Artist > Album > Song" as links (`asset-path-<assetId>` contains three links) | shown |
| Items | `asset-library-info-<assetId>` | opens the library info dialog for this item | shown |
| Song folder | `library-info-edit` | opens the library info dialog for the song's track (first track when several) | shown |
| Library info dialog | `library-info-dialog` | the dialog; attribute `data-scope` = `track`/`item` | shown |
| Library info dialog | `library-info-artist`, `library-info-album`, `library-info-song` | text inputs (1..200) | shown |
| Library info dialog | `library-info-auto-artist`, `library-info-auto-album`, `library-info-auto-song` | "Automatic" checkboxes; label shows the automatic name and its source | shown |
| Library info dialog | `library-info-save`, `library-info-cancel`, `library-info-reset` | buttons ("Reset" = all automatic) | shown |
| Library info dialog | `library-info-error` | readable reason (400 field messages, 404, 409) | shown |
| Library info dialog | `library-info-lookup` | link "Look up band info again" (track scope; opens the 005 correction on the track page) | shown |
| Library | `library-search` | (1.5.0 id, changed) live search input, every level | shown |
| Library | `library-search-clear` | clear button; exists only while the box has text | shown |
| Library | `library-search-status` | polite live region with the result count ("12 results") | shown |
| Library | `library-search-scope` | checkbox "Only in this folder"; exists only inside a folder | not rendered |
| Library | `search-results` | results container while the query is not blank | shown |
| Library | `search-group-<group>` | a result group with its heading and count; exists only when the group has hits | shown |
| Library | `search-show-all-<group>` | "Show all N"; exists only when the group has more hits than shown | shown |
| Library | `search-hit-<folderId>` | a folder hit (link to the folder) with its path | shown |
| Library | `search-mark` | each highlighted part (`<mark>`); **not unique** (tests count them inside a hit) | shown |
| Library | `library-search-empty` | "No matches for '...'" with tips and `library-search-empty-clear` | shown |
| Library | `library-search-error` | readable reason when the search failed (previous results stay) | shown |
| Export dialog | `export-saved-folder` | inside `export-saved`: the folder path "Artist > Album > Song" (three links) | shown |
| Sheet music dialog (008) | `score-saved-folder` | inside `score-saved`: the folder path (three links) | shown |

## Help topics (004 framework; topic ids are also `data-help-id` values)

`library-folders` (on `library-folder-title`: how folders are made, where names come from, unknown
folders, pictures in-app only), `library-search` (on `library-search`: what the search looks at,
words, accents, "Only in this folder"), `library-info` (on `library-info-dialog`: automatic vs own
names, track vs item, band info lookup; every level), `library-art` (on `library-folder-art`: which
picture a folder shows, how to change a song picture).

## Keyboard

`/` focuses `library-search` on the library pages (scope `library`; 014 catalogue row, owner 022);
Escape in the box clears it and restores the folder; Arrow Down moves to the first result.
