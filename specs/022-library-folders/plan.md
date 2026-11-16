# Implementation Plan: Library by Artist and Album

**Branch**: `022-library-folders` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/022-library-folders/spec.md`; constitution 1.4.0
(ratified 2026-11-16; 022 touches no 1.4.0 change); `docs/MoSCoW.md` (S13, Should, owner request);
`docs/ROADMAP.md` (backend-api 1.22.0, ui-test-ids 1.22.0, agent tool catalogue 1.1.0); delivered
features 005 (metadata, artwork precedence), 007 (asset library), 008-010 (scores and takes in the
library), 004 (design system, levels, help), 011 (agent).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding decisions in
[ADR-036](../../docs/decisions/ADR-036-library-by-artist-and-album.md); spike and trade-offs in
[research.md](research.md); contract deltas in [contracts/DELTA.md](contracts/DELTA.md).

## Summary

- **Filing (US1)**: a pure `PlacementResolver` (Domain) chooses artist, album and song per level
  (item info > track info > 005 match > import names > fallback) and `LibraryKey` normalises them
  (case, accents, special letters, quotes/dashes, whitespace, leading "The", featuring). The API's
  `LibraryFiler` stores the placement on each `Asset` when it is created (all creators), re-files a
  track's items when its band info or library info changes, and a start-up backfill files every item
  below the current filing-rules version (data migration and 013 restores).
- **Folders and art (US2)**: folders are derived from the placements (no tables, never empty).
  `FolderTreeBuilder` builds artist/album/song folders with display names, counts, sizes, sort and
  art from the existing 005 artwork (song = track picture, album = best by precedence, artist =
  mosaic of up to 4 album covers, else a lettered placeholder). The Web library page gets routes
  `/library` and `/library/{folderId}`, a breadcrumb, grid/list tiles with lazy `srcset` art, and
  reuses the 007 item rows inside song folders. The flat list stays as "All items".
- **Live search (US3)**: `GET /api/library/search` scans an in-memory `LibraryIndex` (rebuilt per
  library revision) over five folded fields (artist, album, song, instrument words from the new
  `InstrumentLabels` catalogue, item name), returns grouped hits with paths and highlight ranges.
  The Web `LiveSearchController` debounces 200 ms, drops stale answers, keeps `?q=` in the URL and
  restores the open folder on clear.
- **Library info (US4)**: `GET/PUT/DELETE` library info for a track and an item; a dialog with
  per-field "Automatic" boxes and the 005 lookup link.
- **Agent (US5)**: catalogue 1.1.0 adds `browse_library_folder`, `search_library_items`,
  `open_library_folder`, `set_library_info` (inverse undo; names as `untrusted_text`).

Measured (research.md R-07): index build 21 ms and search p95 < 1 ms for 5,000 items.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; TypeScript only untouched).

**Primary Dependencies (new)**: **none** (.NET BCL `string.Normalize`, `CharUnicodeInfo`,
`SHA256`; Radzen Blazor already used; no npm or Python change).

**Storage**: SQLite - new nullable columns on `Assets` (placement, item library info, `FiledVersion`
default 0, `FiledAt`) and `Tracks` (library info); index `IX_Assets_Folder`; migration
`AddLibraryFolders`. No new files on disk. Web preferences (004) `library.layout`, `library.sort.*`.

**Testing**: Domain unit tests (normaliser >= 40 cases, folder ids, resolver precedence, filing
rules); API tests for the filer hooks, backfill (idempotent, batches, 5,000 items <= 10 s),
tree builder (names, counts, art, sorts), search (tokens, folding, highlights with index maps,
relevance, groups, `within`, one case per instrument family), endpoints (ETag/304, 404, 422, paging)
and contract tests against the delta; persistence tests with SQLite in memory; a performance test
(5,000 seeded items: folder p95 <= 100 ms, search p95 <= 50 ms). Web bUnit tests for routing,
breadcrumb, tiles/art, song folder, live search (fake `TimeProvider`, fake API client), dialog,
levels and help; pure C# tests for `LibraryLocation`, `HighlightedText`, `FolderArtModel`. Agent
tests for descriptors, projections, taint, inverse and the 32-tool cap. Playwright E2E on the fake
stack with axe checks and a 5,000-item latency spec.

**Target Platform**: unchanged; no GPU, no worker involvement.

**Performance Goals**: SC-003 backfill 5,000 items <= 10 s; SC-004 results <= 0.4 s after the last
key press (p95), folder page <= 0.3 s, API search <= 50 ms (p95) at 5,000 items.

**Constraints**: nothing written outside the app's storage; no network; covers in-app only; WCAG
2.2 AA (axe in E2E); SIG limits; no change to worker/generator/engine contracts.

**Scale/Scope**: up to ~10,000 items (target 5,000), ~2,000 song folders, 500 children per page.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every implementation task in tasks.md follows its failing test task; all logic is pure (resolver, keys, tree, search) and tested without I/O; fakes for the API client and clock in the Web; no model involved; E2E on the fake stack. Coverage reported per hand-off. | PASS |
| II | SIG 4 stars | Domain `Library/` split into `LibraryKey`, `FolderId`, `PlacementResolver`, `LibraryInfoRules`; API `Library/` split into filer, backfill, revision, index, tree builder, search, instrument terms, info service, endpoints; Web small components (tile, art, breadcrumb, search box, result group, dialog) plus pure helpers; dependency rules added (T081). | PASS |
| III | Contracts first | Standalone OpenAPI 3.1 delta 1.22.0-delta.1 (Redocly valid, 0 warnings), UI-id delta 1.22.0, catalogue delta 1.1.0 now; full documents, DTOs and routes in Phase 0 before the wave; ADR-036; all changes additive (the UI-id behaviour change is documented with migration). | PASS |
| IV | Open source, lawful use | No new dependency. Covers stay in-app only (005 FR-014); no new downloads or third-party calls; no artist photos (licences). | PASS |
| V | Simplicity and honesty | Derived folders instead of stored ones; art from existing pictures; one in-memory scan instead of FTS; limitations documented (same-name bands merge, no track numbers, items of deleted tracks lose pictures). Timers only for UI debounce, never for audio. | PASS |
| - | Tech & security | Stack unchanged; inputs validated (folder id pattern, names 1..200 without control characters, query 1..100, <= 8 tokens, paging); no paths in responses; folder names are rendered as text (no markup); `<mark>` built from ranges, never from HTML. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-off per checkpoint (ADR-008); manuals by the CTO (constitution 1.3.1 rule). | PASS |

**Post-design re-check (after Phase 1)**: PASS. The only cross-feature risk is the unmerged base
(012-021 and claude-key deltas): handled by the delta-first Phase 0 (Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/022-library-folders/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md
    ├── backend-api.library-folders.openapi.yaml   # 1.22.0-delta.1
    ├── redocly.yaml
    ├── ui-test-ids.delta.md                       # 1.22.0
    └── tool-catalogue.delta.md                    # 1.1.0
```

### Source Code (additions)

```text
src/StemStudio.Contracts/Library/                 # architect: DTOs (LibraryFolderViewDto, LibraryFolderDto,
                                                  #   FolderRefDto, FolderArtDto, ArtImageDto, ArtPlaceholderDto,
                                                  #   LibrarySearchResultDto, FolderHitDto, ItemHitDto, HighlightDto,
                                                  #   LibraryInfoRequest, LibraryInfoStateDto, LibraryInfoLevelDto,
                                                  #   PlacementDto, PlacementLevelDto, enums), InstrumentLabels.cs
src/StemStudio.Contracts/Assets/AssetDto.cs       # architect: + Placement
src/StemStudio.Contracts/{ApiRoutes,ContractLimits}.cs   # architect: Library* routes, limits
src/StemStudio.Domain/Library/                    # backend: LibraryKey, FoldedText, FolderId, FolderLevel,
                                                  #   PlacementSource, Placement, PlacementLevel, LibraryInfo,
                                                  #   LibraryInfoRules, PlacementInput, PlacementResolver, LibraryFilingRules
src/StemStudio.Domain/{Assets/Asset.cs,Tracks/Track.Library.cs}  # backend: placement / library info members
src/StemStudio.Persistence/                       # backend: configurations, migration AddLibraryFolders
src/StemStudio.Api/Library/                       # backend: LibraryFiler, LibraryFilingBackfill, LibraryRevision,
                                                  #   LibraryIndex, LibrarySnapshot, FolderTreeBuilder, FolderArtPicker,
                                                  #   LibrarySearch, InstrumentTerms, LibraryInfoService, LibraryEndpoints,
                                                  #   LibraryMapper, LibraryProblems
src/StemStudio.Api/{Assets,Scores,Generations,Metadata,Sync}/    # backend: filer hooks
src/StemStudio.Web/Api/                           # frontend: client methods
src/StemStudio.Web/Library/                       # frontend: LibraryLocation, LiveSearchController, FolderArtModel,
                                                  #   HighlightedText, LibraryAnnouncer
src/StemStudio.Web/Components/Library/            # frontend: LibraryPage (folders/all), FolderGrid, FolderTile, FolderArt,
                                                  #   LibraryBreadcrumb, SongFolder, LibrarySearchBox, SearchResults,
                                                  #   SearchGroup, LibraryInfoDialog, AssetPath
src/StemStudio.Web/Help/                          # frontend: 4 topics
src/StemStudio.Web/wwwroot/css or Styles/         # frontend: art tones (8 tokens, both themes), tile/mosaic styles
src/StemStudio.Agent/Tools/Library/               # ai-agent: 4 tools, projections; registry groups
src/StemStudio.Agent/Ports/                       # ai-agent: IStemStudioActions additions (architect stubs in T003)
tests/StemStudio.{Domain,Api,Persistence,Web,Agent}.Tests/Library/   # owners as above
e2e/tests/library-*.spec.ts, e2e/pages/library-*.ts, e2e/a11y/, tests/fixtures/library/   # qa
tools/quality/                                    # quality-gate: dependency rules
```

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/022-*/**`, ADR-036, `src/StemStudio.Contracts/**`, `src/StemStudio.Agent/Ports/**` (interface stubs), project files | architect |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `src/StemStudio.Agent/**` (except Ports), `tests/StemStudio.Agent.Tests/**`, `specs/011-ai-composer-agent/evaluation/**` (022 cases) | ai-agent |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/public/src/**`, `docs/STATUS.md`, `docs/THIRD-PARTY-LICENSES.md` | CTO |
| devops | **no task** (no compose, image, CI or script change; the perf spec runs in the existing E2E job) |

## Key flows

1. **Save**: `ExportService.CreateAsync` -> `Asset.Create` -> `ILibraryFiler.FileAsync(asset)` (loads
   track; resolver; `asset.File`) -> `SaveChanges` -> response `AssetDto` with `placement` -> export
   dialog shows `export-saved-folder` and links to `/library/<songFolderId>#asset-<id>`.
2. **Browse**: `/library/{folderId}` -> `GET /api/library/folders/{id}` (If-None-Match) ->
   `LibraryRevision` -> `LibraryIndex.Snapshot` (rebuild if the revision changed) ->
   `FolderTreeBuilder.View(id, sort, filters)` -> tiles with `FolderArt` -> song level renders 007
   `AssetRow`s; poll every 2 s while an item is active.
3. **Search**: keypress -> `LiveSearchController` (200 ms, sequence n, cancel n-1) -> `GET
   /api/library/search?q=` -> `LibrarySearch.Run(snapshot, query)` -> groups + highlights ->
   `SearchResults` with `<mark>` segments -> `LibraryAnnouncer` (polite, 500 ms after last change);
   Escape -> clear, `NavigationManager` back to the folder URL, focus the box.
4. **Lookup later**: `MetadataJobApplier` -> `track.CompleteLookup` -> `ILibraryFiler.RefileTrackAsync`
   -> items with non-`item` sources recomputed -> revision changes -> next poll shows new folders.
5. **Library info**: dialog -> `PUT /api/tracks/{id}/library-info` -> `track.SetLibraryInfo` ->
   refile -> `LibraryInfoState {movedItems}` -> UI navigates to the new song folder id.
6. **Start-up**: `LibraryFilingBackfill` -> batches of 500 -> `pendingFiling` in folder views drops
   to 0 -> UI hides `library-filing-pending`.
7. **Agent**: `search_library_items` -> `IStemStudioActions.SearchLibraryAsync` -> loopback `GET
   /api/library/search` -> projection with `untrusted_text` -> taint; `set_library_info` -> GET
   (inverse record) -> PUT.

## Phases

- **Phase 0 part 1 (done in this round)**: spec, research with spike S-01, ADR-036, data-model,
  contract deltas (Redocly valid), quickstart, tasks, checklist, MoSCoW/ROADMAP rows.
- **Phase 0 part 2 (architect, when 022 is scheduled; after the base contracts it builds on are
  merged)**: full backend-api 1.22.0, ui-test-ids 1.22.0, catalogue 1.1.0; Contracts DTOs, routes,
  limits, `InstrumentLabels`; `IStemStudioActions` stubs; skeleton green.
- **Wave (parallel owners, max 4 agents)**: backend, frontend, ai-agent, qa (tasks.md).
- **Integration (CTO + qa + quality-gate)**: merge, E2E on the fake stack (incl. perf and a11y),
  SIG/Sonar, manuals (user manual "Library" chapter), STATUS.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Placement denormalised onto every item | items outlive tracks (FR-008); one-table tree build; backups carry it | computing from the track on every read loses the place of items of deleted tracks |
| In-memory index in the API | accent-insensitive search with highlights in < 1 ms; shared by UI and agent | SQLite `LIKE` cannot fold accents; FTS5 needs extra schema and triggers for no gain at 5,000 items |
| Start-up backfill instead of SQL data migration | folding and hashing need .NET; the same step serves rule-version bumps and 013 restores | a SQL migration cannot fold Unicode; a one-off migration would not cover restores |
| UI-id behaviour change on `/library` as a minor | the owner wants folders as the library's face; no id is renamed or removed and the test migration is three lines | a major bump of the whole UI contract for one screen; or keeping the flat list as default, which defeats the request |
| Delta-first contracts | 012-021 and the claude-key follow-up are not merged; 1.22.0 must sit on top of them | writing a full 1.22.0 on 1.10.0 now would need manual re-basing later |
