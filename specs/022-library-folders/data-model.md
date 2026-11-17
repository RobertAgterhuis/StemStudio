# Data Model: Library by Artist and Album (feature 022)

Builds on the 001-011 data models (in particular 005 data-model 3.2 `TrackMetadata` and 007
data-model 4.1 `Asset`). Wire shapes: [contracts/backend-api.library-folders.openapi.yaml](contracts/backend-api.library-folders.openapi.yaml)
(backend-api 1.22.0-delta.1). Design: [ADR-036](../../docs/decisions/ADR-036-library-by-artist-and-album.md),
[research.md](research.md). **Worker, generator and audio-engine contracts are unchanged.**

## 1. Configuration

| Where | Name | Default | Meaning |
|---|---|---|---|
| API | `StemStudio:LibraryBackfillBatchSize` | `500` | items filed per transaction by the start-up backfill |
| Web | preference `library.layout` | `grid` | `grid` or `list` (004 preferences, per device) |
| Web | preference `library.sort.<level>` | `name` (artists, songs), `year` (albums) | Advanced sort choice per level |
| Web | `StemStudio:LibrarySearchDebounceMs` | `200` | debounce of the live search (tests use a fake `TimeProvider`) |

## 2. Domain (`src/StemStudio.Domain/Library/`, pure, no I/O)

### 2.1 FolderLevel, PlacementSource

```text
FolderLevel     = Artist | Album | Song
PlacementSource = Item | Track | Match | Import | Fallback
```

### 2.2 LibraryInfo (value object; on Track and on Asset)

| Field | Type | Rule |
|---|---|---|
| `Artist`, `Album`, `Song` | string \| null | null = automatic; else trimmed, 1..200 characters, no control characters (`LibraryInfoRules.Validate`) |

### 2.3 Placement (value object; stored on Asset)

| Field | Type | Rule |
|---|---|---|
| `Artist`, `Album`, `Song` | `PlacementLevel` | one per level |
| `Year` | int \| null | album year (1000..2999) from the match; null otherwise |
| `Version` | int | `LibraryFilingRules.Version` used (1) |

**PlacementLevel**: `Name` (display text, <= 200, cut with "..." when longer), `Key` (R-02, <= 400
characters after folding), `Source` (`PlacementSource`).

### 2.4 LibraryKey (static)

- `For(FolderLevel level, string name)` -> key (research R-02; "The" removal for artist and album,
  featuring removal for artist only).
- `Fold(string text)` -> `FoldedText { Value, Map }` (accent/case folding without the level rules;
  `Map[i]` = UTF-16 index in the original for folded index `i`; used by the search for highlights).
- Fallback labels (constants): `UnknownArtist = "Unknown artist"`, `UnknownAlbum = "Unknown album"`,
  `UntitledSong = "Untitled song"`, `OwnSongsArtist = "My songs"`, `OwnSongsAlbum = "Songs from my lyrics"`.
- `VariousArtistsMbid = 89ad4ac3-39f7-470e-963a-56509c546377` (MusicBrainz special purpose artist)
  and the key `various artists` are ignored as match.

### 2.5 FolderId (static)

`Artist(artistKey)`, `Album(artistKey, albumKey)`, `Song(artistKey, albumKey, songKey)` ->
`ar|al|so` + 16 lower-case hex of SHA-256 over `"<prefix>\u001f" + keys joined by "\u001f"` (UTF-8).
`TryParse(string id, out FolderLevel level)` validates `^(ar|al|so)[0-9a-f]{16}$`.

### 2.6 PlacementResolver (static, pure)

Input `PlacementInput { ItemInfo: LibraryInfo?, TrackInfo: LibraryInfo?, Track: TrackFacts?,
Provenance: Provenance, SourcePlacement: Placement? }` where `TrackFacts` = `{Title, Artist,
Metadata (TrackMetadata), ArtistMbid}`. Output `Placement`. Rules: research R-01 table, each level
independently; `SourcePlacement` (score from a library item) is used for every level without
item info when `Track` is null; `OwnSongs` rule when `Track` and `SourcePlacement` are both null.

### 2.7 LibraryFilingRules

`Version = 1`. `NeedsFiling(asset)` = `asset.Placement is null || asset.Placement.Version < Version
|| a level's Source != Item and its input changed` (the filer recomputes and compares).

### 2.8 Asset additions (007 entity)

| Member | Type | Notes |
|---|---|---|
| `Placement` | `Placement?` | null only before the backfill ran |
| `LibraryInfo` | `LibraryInfo` | user names for this item (default all null) |
| `FiledAt` | DateTime? (UTC) | last filing |
| `IsFiled` | bool (computed) | `Placement is not null && Placement.Version == LibraryFilingRules.Version` |
| `File(Placement placement, DateTime nowUtc)` | method | sets placement and `FiledAt`; does **not** change `UpdatedAt` unless the placement changed (keeps 007 ordering stable) |
| `SetLibraryInfo(LibraryInfo info, DateTime nowUtc)` | method | validates, sets, `UpdatedAt = now` |

Items with `Provenance.Origin = recording` (015) are never filed (`Placement` stays null) and never
listed in folders or search.

### 2.9 Track additions (001/005 entity)

| Member | Type | Notes |
|---|---|---|
| `LibraryInfo` | `LibraryInfo` | user names for every item of the track (default all null) |
| `SetLibraryInfo(LibraryInfo info, DateTime nowUtc)` | method | validates, sets, `UpdatedAt = now` |

## 3. Persistence (EF Core, migration `AddLibraryFolders`)

### 3.1 `Assets` (new columns, all nullable unless stated)

| Column | Type | Notes |
|---|---|---|
| `FolderArtist`, `FolderAlbum`, `FolderSong` | TEXT (<= 200) | display names |
| `FolderArtistKey`, `FolderAlbumKey`, `FolderSongKey` | TEXT (<= 400) | keys (R-02) |
| `FolderArtistSource`, `FolderAlbumSource`, `FolderSongSource` | TEXT (enum, lower-case) | `item`, `track`, `match`, `import`, `fallback` |
| `FolderYear` | INTEGER | album year |
| `FiledVersion` | INTEGER NOT NULL DEFAULT 0 | 0 = not filed yet (all rows after the migration) |
| `FiledAt` | TEXT (UTC) | |
| `LibraryArtist`, `LibraryAlbum`, `LibrarySong` | TEXT (<= 200) | item library info (user) |

Index `IX_Assets_Folder (FolderArtistKey, FolderAlbumKey, FolderSongKey)`; index `IX_Assets_FiledVersion`.
The placement is mapped as an owned type (`Placement` with three owned `PlacementLevel`s) onto these
columns (no JSON, so the backfill and the index can query them).

### 3.2 `Tracks` (new columns, nullable)

`LibraryArtist`, `LibraryAlbum`, `LibrarySong` (TEXT <= 200): track library info.

### 3.3 Down migration

Drops the columns and indexes (user library info is lost; documented in the migration's XML doc).

## 4. API components (`src/StemStudio.Api/Library/`)

| Component | Responsibility |
|---|---|
| `LibraryFiler` (`ILibraryFiler`) | builds `PlacementInput` (loads track and source item), calls `PlacementResolver`, `asset.File(...)`; `FileAsync(asset)`, `RefileTrackAsync(trackId)` |
| `LibraryFilingBackfill` (hosted service) | at start **and whenever a library read sees `pendingFiling > 0`** (single-flight trigger; a 013 restore switches the library without a restart): while items with `FiledVersion < Version` exist (excluding `recording`), file a batch (default 500) per transaction; waits with `LibraryGate.WaitWhileMaintenanceAsync` before every batch (`LibraryGate` is a required constructor dependency since 013 is on `main`) and, when it had to wait, re-counts pending items afterwards (the restored library may hold pre-022 items); exposes `Pending` count for the folder view (Phase 0 part 2, research R-11 re-check) |
| `LibraryRevision` | `GetAsync()` = hash of `(count, max(Assets.UpdatedAt), max(Assets.FiledAt), max(Tracks.UpdatedAt))` -> 16 hex; ETag `"lib-<hex>"` |
| `LibraryIndex` (singleton) | cached `LibrarySnapshot` for a revision: projected rows (asset id, kind, status, origin, size, created, placement, names, track id, artwork rank/version/track updated, instrument terms) + folded search fields; rebuilt when the revision changes (one in-flight rebuild, readers keep the old snapshot) |
| `FolderTreeBuilder` (pure) | from a snapshot: folders per level, display name (FR-011), counts, sizes, art (R-05), sort (FR-018), filters `kind`/`status` |
| `LibrarySearch` (pure) | tokens, matching, relevance, groups, `within`, highlights (research R-07) |
| `InstrumentTerms` (pure) | terms of an item from its recipe/score recipe/generation (research R-08) |
| `LibraryInfoService` | GET/PUT/DELETE library info for tracks and items; re-files; returns the new placement and the moved count |
| `LibraryEndpoints` | routes of section 5 |

Filing hooks (existing services call `ILibraryFiler.FileAsync` before `SaveChanges`): `ExportService`,
`StemPackService`, `ScoreService`, `GeneratedAudioLibrary`; `MetadataJobApplier` and
`TrackSynchronizer` call `RefileTrackAsync` after the track facts changed.

## 5. Wire shapes (summary; the OpenAPI delta is binding)

### 5.1 `GET /api/library/folders[/{folderId}]`

Query: `sort` (`name`\|`year`\|`recent`\|`size`\|`items`; default per level), `kind`, `status`,
`limit` (1..500, default 200), `offset` (>= 0). Response `LibraryFolderView`:

| Field | Type | Notes |
|---|---|---|
| `revision` | string (16 hex) | same as the ETag |
| `folder` | `LibraryFolder` \| null | null at the root |
| `breadcrumb` | `FolderRef[]` | root excluded; 0..3 entries |
| `children` | `LibraryFolder[]` | the next level (empty at song level) |
| `childrenTotal` | int | for paging |
| `items` | `Asset[]` | song level only (all items of the song, max 500), else empty |
| `totalItems`, `totalSizeBytes` | int, int64 | of this folder (root: of the library) |
| `pendingFiling` | int | items the backfill has not filed yet |

**LibraryFolder**: `id`, `level` (`artist`\|`album`\|`song`), `name`, `year` (albums), `counts`
(`{albums?, songs?, items}`), `sizeBytes`, `latestAt` (newest item `createdAt`), `nameSource`
(`PlacementSource` of the displayed name), `art` (`FolderArt`), `trackIds` (song level: the tracks
whose items it holds, existing tracks only, max 10).

**FolderArt**: `kind` (`cover`\|`mosaic`\|`placeholder`), `images` (0..4 `{thumbUrl, coverUrl}`:
1 for `cover`, 2-4 for `mosaic`), `placeholder` (`{glyph, tone}`; `glyph` 1 grapheme, `tone` 0..7).

**FolderRef**: `id`, `level`, `name`.

### 5.2 `GET /api/library/search`

Query: `q` (1..100 after trimming; required), `within` (folder id), `limit` (per group 1..200,
default 20). Response `LibrarySearchResult`: `query` (the used text), `tokens` (folded, <= 8),
`revision`, `artists`/`albums`/`songs` (`FolderHit[]`), `items` (`ItemHit[]`), `totals`
(`{artists, albums, songs, items}`).

**FolderHit**: `folder` (`LibraryFolder`), `path` (`FolderRef[]`, parents), `highlights`
(`Highlight[]` on `name`). **ItemHit**: `asset` (`Asset`), `path` (`FolderRef[]` artist, album,
song), `highlights` (`Highlight[]`), `matchedInstruments` (labels that matched, <= 8).
**Highlight**: `field` (`artist`\|`album`\|`song`\|`name`\|`instrument`), `start`, `length`
(UTF-16 offsets in the displayed text of that field; for `instrument` in the label).

### 5.3 Library info

`PUT /api/tracks/{trackId}/library-info` and `PUT /api/assets/{assetId}/library-info`, body
`LibraryInfoRequest {artist, album, song}` (each `string|null`, null = automatic; all three
required members). `DELETE` = all automatic. `GET` returns the current state.

Response `LibraryInfoState`: `artist`/`album`/`song` each `{value: string|null (user name),
effective: string (name used), source: PlacementSource, automatic: string (the name automatic
filing would use)}`, plus `placement` (`Placement`, items only) and `movedItems` (int, PUT/DELETE).

### 5.4 `Asset.placement` (new required member of `Asset`)

`Placement`: `artist`, `album`, `song` (each `{folderId, name, source}`), `year`, `filed` (bool;
false only while the backfill has not reached the item: then names are the automatic preview and
`folderId`s are still valid).

## 6. Limits (`ContractLimits` additions)

| Constant | Value |
|---|---|
| `LibraryNameMaxLength` | 200 |
| `LibrarySearchMaxLength` | 100 |
| `LibrarySearchMaxTokens` | 8 |
| `LibrarySearchGroupDefault` / `Max` | 20 / 200 |
| `LibraryFolderPageDefault` / `Max` | 200 / 500 |
| `LibrarySongItemsMax` | 500 |
| `FolderArtMosaicMax` | 4 |

## 7. Web state (`src/StemStudio.Web/Library/`)

| Type | Responsibility |
|---|---|
| `LibraryLocation` | parses `/library`, `/library/{folderId}`, `?q=`, `?view=all`; builds URLs |
| `LiveSearchController` | debounce (TimeProvider), sequence numbers, cancellation, Enter/Escape, restore context |
| `FolderArtModel` | from `FolderArt`: `src`, `srcset`, `sizes`, mosaic cells, placeholder glyph/tone class |
| `HighlightedText` | splits a text into plain and `<mark>` segments from `Highlight[]` |
| `LibraryAnnouncer` | polite live-region text ("12 results"), announced once 500 ms after the last change |

## 8. State and lifecycle

```text
item created --File--> filed (version 1)
   track lookup found / track info set --Refile--> filed (new place; items with Item source untouched)
   item info set --File--> filed (Item source on the edited levels)
   track deleted --> unchanged (place kept)
   rules version bump / restore of older archive --> FiledVersion < Version --backfill--> filed
item deleted --> gone; folders without items disappear (derived)
```
