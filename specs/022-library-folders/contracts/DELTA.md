# Contract delta: feature 022 "library by artist and album"

Feature 022 adds the `library` tag to the backend API (folder view, live search, library info), one
required response member on `Asset`, UI test ids and four agent tools. **Worker API, generator API,
audio engine, render vectors, web-interop, help-content format, backup-archive and remote-protocol
are unchanged.**

## Status (design round, 2026-11-16)

| Contract | Base | Feature document | State |
|---|---|---|---|
| Backend API | newest full document when 022 is built (1.21.0 or the merge of 012-021 + claude-key; 1.10.0 is the last full document on `main`) | [backend-api.library-folders.openapi.yaml](backend-api.library-folders.openapi.yaml) **1.22.0-delta.1** -> full `backend-api.openapi.yaml` **1.22.0** (T002) | delta written; Redocly 2.78.0 lint: **valid, 0 warnings** (OpenAPI 3.1) |
| UI test ids | newest full document (1.21.0 or later) | [ui-test-ids.delta.md](ui-test-ids.delta.md) -> full `ui-test-ids.md` **1.22.0** (T004) | delta written |
| Agent tool catalogue | 1.0.0 (011) | [tool-catalogue.delta.md](tool-catalogue.delta.md) -> **1.1.0** (T004) | delta written |
| Help content | 1.0.0 (004) | four new topics (format unchanged) | ids in the UI delta |
| C# | - | `src/StemStudio.Contracts/Library/*`, `AssetDto.Placement`, `ApiRoutes.Library*`, `ContractLimits` additions, `InstrumentLabels` (T003) | Phase 0 |

## 1. Merge rule (architect, T002)

1. Add the tag `library` and the paths `/api/library/folders`, `/api/library/folders/{folderId}`,
   `/api/library/search`, `/api/tracks/{trackId}/library-info`, `/api/assets/{assetId}/library-info`
   unchanged.
2. Add the parameters `FolderId`, `FolderSort`, `KindFilter`, `StatusFilter`, `FolderLimit`,
   `FolderOffset`, `IfNoneMatch`; the header `LibraryETag`; the responses `FolderNotFound`,
   `LibraryQueryInvalid`, `LibraryInfoInvalid`, `NotFileable`; the schemas `FolderId`, `FolderLevel`,
   `FolderSort`, `PlacementSource`, `FolderRef`, `FolderCounts`, `ArtImage`, `ArtPlaceholder`,
   `FolderArtKind`, `FolderArt`, `LibraryFolder`, `LibraryFolderView`, `SearchField`, `Highlight`,
   `FolderHit`, `ItemHit`, `SearchTotals`, `LibrarySearchResult`, `LibraryName`,
   `LibraryInfoRequest`, `LibraryInfoLevel`, `LibraryInfoState`, `PlacementLevel`, `Placement`; and
   the examples unchanged. Rename on collision only (none expected; checked against 1.10.0 and the
   012-021/claude-key deltas: `NotFound`, `TrackId`, `AssetId` already exist with the same meaning
   and are **not** added again - the external `$ref`s to 1.10.0 become local `#/components/...`).
3. In `Asset` add `placement` to `properties` (`oneOf: [Placement, null]`) and to `required`; keep
   every member the base has (later features may have added some). No other schema changes.
4. Bump `info.version` to 1.22.0 with the changelog line of this delta; contract tests
   (`LibraryContractTests`) prove the full document = base + this delta.
5. Lint the full document with `redocly.yaml` of the base (expected warnings: those of the base
   only; this delta adds none).

**Compatibility**: additive. Old clients ignore `Asset.placement` and the new paths. `GET
/api/assets` keeps its 1.5.0 semantics (`q` = name contains). The 013 write gate is not extended
(library info edits are metadata edits like rename).

## 2. Decisions that shape the delta (ADR-036)

| # | Decision | Where |
|---|---|---|
| D-1 | Folders are derived; folder ids are hashes of normalised keys (`ar|al|so` + 16 hex) | research R-03 |
| D-2 | Placement stored per item; per-level source (`item`, `track`, `match`, `import`, `fallback`) | R-01, data-model 2.3 |
| D-3 | One folder-view operation for every level, items at song level as full `Asset`s (reuse of 007 UI) | R-06 |
| D-4 | Server-side search over an in-memory index keyed by the library revision; highlights as UTF-16 ranges | R-07 |
| D-5 | Folder art references existing 005 artwork URLs; no new image endpoint | R-05 |
| D-6 | ETag = library revision on every `GET` of the tag | R-06 |

## 3. Cross-feature notes

| Feature | Note |
|---|---|
| 013 | No backup-archive change; restore of older archives relies on `FiledVersion = 0` + start-up backfill. Storage overview unchanged. |
| 014 | Shortcut `/` (scope `library`, "Focus library search") added to the FR-020 catalogue with owner 022. |
| 015 | `origin = recording` items are excluded from folders and search; `PUT/DELETE .../library-info` on them answers `409 not-fileable`. |
| 011 | Catalogue 1.1.0 (tool-catalogue.delta.md); `list_exports`, `list_scores`, `search_library` unchanged. |
| 012-021 | Any new asset origin is filed by the generic rule (track -> source item -> "My songs"); no 022 change needed. |
