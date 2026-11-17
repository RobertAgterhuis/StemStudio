# Research: Library by Artist and Album (feature 022)

Architect, 2026-11-16. Every decision below is binding through
[ADR-036](../../docs/decisions/ADR-036-library-by-artist-and-album.md). Inputs: spec.md, constitution
1.4.0 (ratified 2026-11-16 on the claude-key branch; 022 touches none of the 1.4.0 changes), the
delivered code of 005/007/008/009/010/011 on `main` (`15e2c5bd`), and the designs of 013/014/015.

## R-01 Where the names come from (grouping source)

What exists today (code on `main`):

| Data | Where | Notes |
|---|---|---|
| `Track.Title`, `Track.Artist` | raw names from the import (yt-dlp `title`/`artist`/`uploader`, spotDL, file tags) | YouTube titles carry noise ("(Official Video)") |
| `TrackMetadata.LookupTitle`, `LookupArtist`, `LookupAlbum` | names the worker cleaned for the 005 lookup (`metadata/titles.py`: noise suffixes and `feat.` removed, `- Topic`/`VEVO` removed, `"<artist> - <title>"` split); `LookupAlbum` from Spotify/tags | also hold the user's 005 correction (`CorrectedByUser`) |
| `TrackMetadata.ArtistName`, `Album`, `RecordingTitle`, `Year`, `ReleaseGroupMbid` | the MusicBrainz match, only when `Status = found` | canonical spelling; release group = album |
| `Asset.Provenance.TrackTitle`, `TrackArtist` | snapshots at creation | survive track deletion; no album |

**Decision (per level, first available wins)**:

| Level | 1 item info | 2 track info | 3 match (found) | 4 import | 5 fallback |
|---|---|---|---|---|---|
| Artist | `Asset.LibraryInfo.Artist` | `Track.LibraryInfo.Artist` | `Metadata.ArtistName` unless it is Various Artists | `LookupArtist`, then `Track.Artist`, then `Provenance.TrackArtist` | "Unknown artist" |
| Album | `...Album` | `...Album` | `Metadata.Album` (+ `Year`) | `LookupAlbum` | "Unknown album" |
| Song | `...Song` | `...Song` | `Metadata.RecordingTitle` | `LookupTitle`, then `Track.Title`, then `Provenance.TrackTitle` | "Untitled song" |

Why the match outranks the import (unlike 005's artwork, where Spotify outranks the Cover Art
Archive): folders are about *grouping across imports*. The MusicBrainz spelling is the same for
every import of a band, while YouTube and Spotify spell names differently ("MetallicaTV",
"Metallica - Topic"). Spotify's album string is still used when nothing was matched. The lookup
names (level 4) are already cleaned by the worker, so no new title heuristics are needed in .NET
(no duplicated noise rules; the worker stays untouched).

Each level is chosen **independently** (an override of only the album keeps the automatic artist).
The source of each level is stored (`item`, `track`, `match`, `import`, `fallback`) so the UI can
say where a name comes from (FR-033) and FR-011 can prefer the match spelling.

Items without a track: (a) made from another library item (008 score from an audio item): copy the
source item's placement at creation; (b) neither (010 song from own lyrics, a 009/021 generation
without source track): "My songs" > "Songs from my lyrics" > `Provenance.TrackTitle` (the lyrics
title). The labels are fixed English strings (W7: no localisation).

Rejected: a separate "album artist" (we do not store it; MusicBrainz release artist would need a
worker change); splitting artists into several folders (one item must live in exactly one folder,
spec FR-001).

## R-02 Normalisation (one folder for "Metallica" and "metallica")

`LibraryKey.For(name)` (Domain, pure):

1. `Normalize(FormKD)`, drop `NonSpacingMark` (accents), then `Normalize(FormKC)`.
2. `ToLowerInvariant()`; map the letters that do not decompose: `ß`->`ss`, `æ`->`ae`, `ø`->`o`,
   `ł`->`l`, `đ`->`d`, `þ`->`th`, `œ`->`oe`.
3. Unify `’‘‛´`` ` -> `'`, `“”„` -> `"`, `‐‑‒–—―` -> `-`, `＆` -> `&` (compatibility forms are
   handled by step 1).
4. Collapse all Unicode whitespace to one space, trim.
5. Artist and album keys only: remove a leading `the ` (English article only; "De Dijk", "Les
   Négresses Vertes" keep theirs because the article is part of the sort name there and a general
   rule would merge real bands). Artist key only: remove featuring parts with the same patterns as
   the worker (`\s*[\(\[]\s*(feat|ft|featuring)\b[^\)\]]*[\)\]]` and `\s+(feat\.?|ft\.?|featuring)\s.*$`).
6. Empty result -> the fallback label's key.

No splitting on `&`, `and`, `,`, `x`, `/`: "Simon & Garfunkel", "Crosby, Stills, Nash & Young",
"AC/DC" are single artists; splitting would be wrong more often than right. Two different bands
with the same name merge (MusicBrainz disambiguation is not used for the key, because a matched
"Nirvana" and an unmatched "nirvana" import must land together); documented edge case, fixable with
library info.

**Display name** of a folder (FR-011): among the folder's items, the stored name of an item whose
level source is `match`; otherwise the most frequent stored name; ties -> earliest `FiledAt`.
Computed when the tree is built (R-06), not stored.

Test set: at least 40 cases (SC-002): case, trailing/double spaces, NBSP, accents (Motörhead,
Björk, Sigur Rós, Beyoncé), ß/æ/ø/ł, curly apostrophes ("Guns N’ Roses"), en dash, full-width
letters, "The Beatles"/"Beatles", "Thee Oh Sees" (not stripped), "The The" (key "the"), featuring
in brackets and as tail, "Various Artists" match, emoji-only names, Cyrillic/Greek case folding,
CJK unchanged.

## R-03 Folders: derived, not stored

| Option | For | Against |
|---|---|---|
| **Derived** from per-item placement (chosen) | no empty folders by construction (owner: "create if not present"); no folder rows to keep consistent on delete, move, restore or merge; renames are re-filing | a folder has no own data (no custom folder picture, no folder notes) - not requested |
| Stored folder tables (`Artists`/`Albums`/`Songs`) with FKs | own folder metadata possible | must create, merge and garbage-collect folders on every save, delete, rename, re-file and restore (013 merge restore!); race conditions; YAGNI |

The **placement is stored on each item** (denormalised): artist, album, song (display text and key),
the source of each, the album year, `FiledVersion` and `FiledAt`. Reasons: items outlive tracks
(FR-008), the tree can be built from one table scan, and backups/restores carry it for free.

**Folder ids** are derived, stable and URL-safe: `ar` + first 16 hex of SHA-256(`"ar\u001f" +
artistKey`), `al` + 16 hex of SHA-256(`"al\u001f" + artistKey + "\u001f" + albumKey`), `so` + 16 hex
of SHA-256(`"so\u001f" + artistKey + "\u001f" + albumKey + "\u001f" + songKey`). Pattern
`^(ar|al|so)[0-9a-f]{16}$`. 64 bits: collision probability ~ n²/2^65 ≈ 7e-13 at 5,000 folders.
Unit separator `U+001F` cannot occur in a key (control characters are rejected or removed).

## R-04 Filing and re-filing

`LibraryFiler` (API) computes the placement with the pure `PlacementResolver` (Domain) and writes it:

| Trigger | Scope | Where (existing code) |
|---|---|---|
| item created | the item, same `SaveChanges` | `ExportService`, `StemPackService`, `ScoreService`, `GeneratedAudioLibrary`, and every later creator through `Asset` factory + `ILibraryFiler.File(asset)` |
| track lookup completed or failed/retried with corrected names | items of the track | `MetadataJobApplier` (005) after `CompleteLookup` |
| track library info set/reset | items of the track | new `LibraryInfoService` |
| item library info set/reset | the item | new `LibraryInfoService` |
| track source metadata applied (import) | items of the track (normally none yet) | `TrackSynchronizer` after `ApplyMetadata` |
| `FiledVersion < LibraryFilingRules.Version` | every such item | `LibraryFilingBackfill` hosted service at start (batches of 500, one transaction per batch, idempotent) |

Track deletion does **not** re-file: items keep their stored placement (FR-008). Items whose names
come from `item` keep them; items with `track`/`match`/`import`/`fallback` sources are recomputed.

Backfill cost: placement for one item is microseconds (fold + SHA-256); 5,000 items = 10 batches;
the dominant cost is SQLite writes (~5,000 updated rows, well under 1 s on NVMe). SC-003 (10 s) has a
wide margin. Restore of a pre-022 archive (013): the EF migration adds the columns with
`FiledVersion = 0`, so the backfill files them on the next start - no special restore code.

`LibraryFilingRules.Version = 1`; any change of R-01/R-02 rules bumps it and every item is re-filed
once (the rule change is therefore safe to ship).

## R-05 Folder art

There are **no artist photos** in StemStudio: 005 (ADR-014 D8) left out Wikimedia Commons artist
pictures because of per-file licences and attribution. Adding them is out of scope (a separate
feature with its own licence handling). Decision:

| Folder | Art | Data |
|---|---|---|
| Song | the track's picture (005 precedence already applied per track: user > Spotify > embedded > Cover Art Archive > YouTube) | `Track.Artwork` thumb/cover URLs |
| Album | best picture among the tracks of its songs: lowest 005 rank, ties -> latest `Track.UpdatedAt` | same |
| Artist | the album covers of its albums in album order: 1 cover -> full tile; 2-4 -> 2x2 mosaic (empty cells get the placeholder tone); none -> placeholder | up to 4 album arts |
| Any, no picture | placeholder: first grapheme of the display name (`?` for "Unknown ..."; a note glyph for "My songs") on one of 8 design-system "art" colour tokens chosen by `key hash mod 8`; letter contrast >= 4.5:1 in both themes | none |

- **No new image files, no worker change, no new downloads**: art references the existing immutable
  URLs `/api/tracks/{id}/artwork/{thumb|cover}?v=<version>` (`Cache-Control: private,
  max-age=31536000, immutable`, strong ETag), so the browser caches them for good. The mosaic is
  composed in the browser with CSS grid (four `<img>`), not rendered on the server. Consequences:
  nothing to back up (013), nothing to clean (013), no licence change.
- Sizes: tiles are 160 CSS px (comfortable) / 128 (compact); `src` = thumb (160 px), `srcset` =
  `thumb 160w, cover 600w`, `sizes` = the tile width, so 2x screens get the 600 px cover. Mosaic
  cells use the thumb (80 CSS px per cell at 160). `width`/`height` set (no layout shift),
  `loading="lazy"`, `decoding="async"`; `onerror` swaps in the placeholder (SC-005).
- Items of a deleted track lose its picture (005 FR-016 deletes pictures with the track); their
  folders use other songs' pictures or the placeholder. We do not keep copies (licence: covers are
  kept only while their track exists; less disk).
- **Alt text**: art is decorative because each tile is a single link whose accessible name is the
  folder name plus level and counts ("Metallica, artist, 3 albums, 12 items"); images get `alt=""`
  (WCAG H67, avoids announcing "Master of Puppets cover" twice). The folder header art next to the
  `h1` is decorative for the same reason. The placeholder letter is `aria-hidden`.
- **Licence**: covers are copyrighted and shown in-app only (005 FR-014, ADR-014 D8); folder art is
  never part of a download, export or backup; the existing personal-practice notice already
  mentions pictures, and the `library-folders` help topic repeats it.

## R-06 API shape: one folder view, one search

- `GET /api/library/folders` (root) and `GET /api/library/folders/{folderId}` return a
  `LibraryFolderView`: the folder (null at root), breadcrumb, child folders (with art and counts),
  and at song level the items (`Asset[]`, so the 007 UI components and actions are reused).
  Query: `sort`, `kind`, `status`, `limit`/`offset` for children. ETag = library revision;
  `If-None-Match` -> 304 (the page polls while items render, as 007 does).
- `GET /api/library/search?q=&within=&limit=` returns grouped hits with paths and highlight ranges.
- `GET/PUT/DELETE /api/tracks/{trackId}/library-info` and `/api/assets/{assetId}/library-info`.
- `Asset.placement` (new required response member) on every asset response.

Rejected: per-level endpoints (`/artists`, `/albums/{id}/songs`, ...) - three near-identical
operations; a full tree endpoint - grows with the library and needs client-side paging anyway.
`GET /api/assets` keeps its 007 behaviour (`q` = name contains), so existing clients and the agent's
`list_exports` are unchanged.

**Revision** (ETag): `count(Assets) + max(Assets.UpdatedAt) + max(Tracks.UpdatedAt)` hashed - one
indexed query (~1 ms), robust against missed invalidations. The in-memory `LibraryIndex` (R-07) is
rebuilt when the revision changes.

## R-07 Live search: server-side, with numbers

The UI is Blazor **Interactive Server**: "client-side filtering" would mean holding a copy of the
library in every circuit of the Web process and keeping it in sync, not filtering in the browser.

Spike S-01 (this round, `dotnet run` file app, .NET 10.0.401, Release, reference machine): 5,000
synthetic items, 150 artists, five fields per item (artist, album, song, instrument terms, name),
folding as R-02:

| Measure | Result |
|---|---|
| fold + build the index (5,000 x 5 fields) | **21 ms** |
| folded strings in memory | **708 KiB** |
| search, linear scan, `string.Contains(Ordinal)` per token, 12 queries ("m", "met", "snare", "the dij", "bjork sig", "zzzz", ...) x 50 runs | median **0.12-0.40 ms**, p95 **<= 0.89 ms** |

| Option | Latency per keystroke | Memory | Consistency | Verdict |
|---|---|---|---|---|
| **API-side index, debounced request** (chosen) | debounce 200 ms + HTTP loopback (~2-5 ms) + search (< 1 ms) + JSON <= ~60 KB (4 groups x 20) + Blazor diff | one index per API process (~1.5 MB with originals) | one source of truth, revision-checked; the agent uses the same endpoint | simple, fast, shared with the agent |
| Index per Web circuit | debounce + < 1 ms | ~1.5 MB per open tab plus a 2-7 MB initial load (full asset rows) | must be invalidated on every save/rename/delete/re-file and on other tabs' changes | duplicate matcher in Web, harder to keep correct |
| SQL `LIKE` per keystroke | ~5-20 ms | none | always fresh | SQLite `LIKE` is case-insensitive only for ASCII and has no accent folding; would need stored folded columns and FTS5 (diacritics option) - more schema for no gain at 5,000 items |

Budget for SC-004 (0.4 s after the last key press, p95): 200 ms debounce + <= 50 ms API (target,
measured < 1 ms for the match) + <= 50 ms render/diff + network. Index rebuild (21 ms + one DB read
of ~5,000 projected rows, ~30 ms) happens only after a change.

**Matching rules** (FR-023/025): fold the query like R-02 steps 1-4 (no "The"/featuring removal: a
user typing "the beat" must find "The Beatles"; the index therefore folds the *display* names, not
the keys); split on whitespace, drop empty tokens, at most 8 tokens of the first 100 characters;
every token must be a substring of at least one field. Relevance per hit: sum over tokens of the best
match class (0 = whole field equals, 1 = starts a word, 2 = inside a word), then display name. A
folder hit needs >= 1 token in its own name (so "metallica" lists the artist, not all its albums); an
item hit needs all tokens across its fields. Highlight ranges are computed on the folded string and
mapped back to UTF-16 offsets of the original through an index map built while folding (ß -> ss,
ligatures, removed marks), so `<mark>` wraps the right characters.

**Debounce and races**: 200 ms debounce on a `TimeProvider` (testable), first search from one
character, Enter skips the debounce, each request carries a sequence number and responses older than
the newest shown are dropped (no flicker), in-flight requests are cancelled
(`CancellationToken`). Audible timing is not involved (constitution V allows timers for UI).

**Scope**: global by default, because a user who types expects to find things anywhere; results
show their path, and clearing returns to the open folder ("keeps the folder context"). Advanced:
`within=<folderId>` ("Only in this folder").

## R-08 Instrument words (equal for every family)

New `InstrumentLabels` catalogue in `StemStudio.Contracts` (shared by API search and Web labels,
closed set as `StemNames`/`DetailNames`):

| Family | Stem / detail ids | Labels | Everyday words (aliases) |
|---|---|---|---|
| Vocals | `vocals`, `vocals.lead`, `vocals.backing`, `vocals.rest` | Vocals, Lead vocals, Backing vocals, Other vocals | voice, vocal, singing |
| Drums | `drums`, `.kick`, `.snare`, `.toms`, `.hihat`, `.cymbals`, `.rest` | Drums, Kick, Snare, Toms, Hi-hat, Cymbals, Other drums | drum, percussion kit |
| Bass | `bass` | Bass | bass guitar, low end |
| Guitar | `guitar`, `.acoustic-guitar`, `.electric-guitar`, families, `.rest` | Guitar, Acoustic guitar, Electric guitar, ... | gtr |
| Piano | `piano`, `.keys`, `.synth`, families, `.rest` | Piano, Keys, Synth, ... | keyboard |
| Other | `other`, `.strings`, `.brass`, `.woodwinds`, `.percussion`, ... | Other, Strings, Brass, Woodwinds, Percussion, ... | orchestra, horns |

The id is indexed with `.` and `-` as spaces ("drums snare", "acoustic guitar"), plus the label and
the aliases of its family (family aliases only on the parent stem and its detail stems). Sheet
music adds its 008 profile label; generated takes add "mix" and their known stems. Every family has
the same structure (MoSCoW principle "every instrument counts equally"); SC-006 tests one case per
family and per detail group. Mixes ("What I hear", "Selected channels") index every channel they
contain (from `ExportRecipe.channels` or the resolved plan when `null`) plus "mix".

## R-09 UI: navigation, levels, views

- Routes: `/library` (root, artists) and `/library/{folderId}` (any level; the id prefix gives the
  level); `?q=` holds the search (replaced, not pushed, while typing; pushed on Enter so Back
  works); `?view=all` is the 007 flat list "All items". Grid/list and sort are device preferences
  (004 preferences, `library.layout`, `library.sort.<level>`).
- Breadcrumb: `<nav aria-label="Library path">` with an ordered list; last entry `aria-current="page"`;
  collapses to "... > Album > Song" under 480 px.
- Grid: responsive CSS grid of square tiles (160 px), name under the art (two lines, ellipsis,
  full name in the accessible name), counts line; list: rows with a 48 px art, name, counts, size.
  Under 768 px the grid has two columns (004 bottom bar layout); 320 px works (SC 004).
- Journey levels (004 R7-R9): Beginner - folders, art, breadcrumb, search, grid/list, item actions
  and "Edit library info" (owner decision 2026-11-16: every level); Advanced adds sort, "All items",
  "Only in this folder"; Expert adds nothing.
- Item rows inside song folders and search results reuse the 007 `AssetRow` component (same ids),
  so play/download/rename/delete/retry and status polling are unchanged.
- Shortcut: `/` focuses the library search (scope `library`); registered in 014's shortcut
  catalogue with owner column 022 when 014 has shipped, otherwise bound by 022 through the 004 key
  map and adopted by 014 (R-11).
- Help topics: `library-folders`, `library-search`, `library-info`, `library-art`.

## R-10 UI test ids and the existing library tests

Two existing behaviours change on `/library`, both owner-requested:

1. `/library` now opens the folder view; the 007/008 flat list (ids `asset-row-<id>`,
   `library-filter-track`, `library-filter-kind`, ...) renders unchanged on `/library?view=all` and,
   for `asset-row-*`, inside song folders and search results.
2. `library-search` (007: Advanced only, name contains) becomes the live search, rendered at every
   level, matching the five fields.

No id is renamed or removed; all other ids keep their level rule. The two changes are recorded in
ui-test-ids **1.22.0** with their test migration: the qa page object `e2e/pages/library-page.ts`
opens `/library?view=all` for the 007/008 specs, `e2e/support/levels.ts` drops `library-search`
from `beginnerHiddenLibraryIds`, and the frontend bUnit test `LibraryManagementTests` (level check)
is updated. Treated as a minor with documented migration (no external consumers; the suite is
ours) rather than a major for the whole UI contract; ADR-036 records it.

## R-11 Overlaps with 013, 014, 015 and later features

| Feature | Overlap | Resolution |
|---|---|---|
| 013 library care (backup, storage) | backup contents; storage overview per item; restore of old archives; clean-up | Library info and placement are database columns: backed up and restored with the database, no archive format change (backup-archive 1.0.0 unchanged). A pre-022 archive restores through the EF migration and the start-up backfill (R-04). Folder art adds no files. Clean-up never touches items (013 FR-022), so folders never change through clean-up. The storage overview stays per track and category; folder sizes in 022 are sums of item sizes (the same bytes, a different grouping); no 013 change. The 013 write gate does not include library-info writes (small metadata edits like rename, which 013 does not gate). |
| 014 practice flow (setlists, shortcuts) | "folders" vs setlists; keyboard | Setlists are user-ordered lists of tracks/mixes for playing; 022 folders are automatic groupings of saved items for finding. No shared entity; a song folder links to its track, where 014's "Add to setlist" lives. The `/` shortcut gets a row in 014's catalogue (FR-020 table, owner 022). |
| 015 play-along recording | takes are hidden assets (`origin = recording`) | Excluded from folders and search, like `listAssets` excludes them; exports that mix a take are normal export items and are filed under their track. |
| 009/010/019/021 generations, arrangements, partner parts, DAWproject export | new asset origins | The generic rule (track -> source item -> "My songs") files them without code per origin; instrument words use their recipe channels when present, "mix" otherwise. |
| 011 agent | `list_exports`, `list_scores`, `search_library` (tracks) | Unchanged; four new tools in catalogue 1.1.0 (R-12). |
| 018 tablet | layout | Folder grid is responsive; nothing remote-specific. |
| 004 | key map, preferences, help, levels | Uses the existing hooks only. |

### Re-check against 013 as built (022 Phase 0 part 2, 2026-11-17)

Read-only check of branch `013-library-care` (head `e0e48e72`, integration in progress; not yet on
`main`) against the R-11 rows. Result: the 013 row holds, with two refinements for the backend lane
and one for the frontend lane.

| 013 area (as built) | Finding | 022 consequence |
|---|---|---|
| Backup (`DatabaseSnapshot`, `BackupArchiveWriter`) | the database file is copied as a whole; backup-archive 1.0.0 has no per-column list | library info and placements are backed up without a format change (confirmed) |
| Replace restore (`ArchiveValidator`, `KnownSchema`, `DatabaseRebuild`, `LibrarySwitcher`) | an archive is accepted only with known migrations and tables (taken from this app's migrations assembly); it is rebuilt at **its** migration level (row copy over the fresh schema's columns) and then migrated forward; the switch happens **without an app restart** | `AddLibraryFolders` must add only columns and indexes to existing tables (as planned; a new table would also be accepted, `KnownSchema` reads the migrations). A pre-022 archive arrives with `FiledVersion = 0`: the backfill must also start after a restore, not only at start-up -> **refinement 1**: `LibraryFilingBackfill` runs at start-up and whenever a library read (folder view, search) sees `pendingFiling > 0`, single-flight |
| Merge restore (`MergePlanner`, `LibraryMerger`) | the staged database is migrated forward, then missing tracks/assets are added as EF entities (same id or same track source = skipped, with dependants) | owned `Placement` and the library info columns are carried by the entities; merged pre-022 items have `FiledVersion = 0` and are filed by refinement 1; deterministic folder ids make merged and live items of the same names land in one folder |
| Write gate (`LibraryGate`) | `BackupRunning` refuses only the listed destructive operations ("every read, every job start, mix and style edits, renames" stay allowed); `Maintenance` answers `503 library-maintenance` + `Retry-After: 5` on every `/api/*` except three; background loops call `WaitWhileMaintenanceAsync` / check `IsMaintenance` per tick | library-info `PUT`/`DELETE` stay outside the write gate (metadata edits like rename; unchanged). **Refinement 2**: the backfill waits while `Maintenance` (optional `GetService<LibraryGate>()` until 013 is merged in, as `AgentRunExecutor` does). Frontend: the 2 s folder polling treats `503 library-maintenance` as transient (keep the last view, no error banner; 013's `library-maintenance` overlay explains it) |
| Clean-up (`CleanupPlanner`, `CleanupExecutor`) | removes regenerable track files (detail stems, caches) and never library items; skips tracks with an active export | folders never change through clean-up (confirmed) |
| Storage overview (`StorageOverviewService`) | per volume, category and track | unchanged; folder sizes are sums of item sizes (another grouping of the same bytes) |
| UI (`/care`, ids `nav-care`, `library-job*`, `library-maintenance`, help `library-care`, `backup-*`, `storage-*`) | no id or help topic on `/library` | no collision with the 022 ids or help topics |
| Contracts | backend-api 1.12.0 (= 1.11.0 + 013) adds `library-care`/`storage` tags, `DetailPhase.filesRemovedAt`, write-gate responses on ten operations; it changes no `Asset` member and adds no 022 name | the 022 merge onto 1.12.0 is mechanical (`merge_full.py --base specs/013-library-care/contracts/backend-api.openapi.yaml`); the same holds for ui-test-ids 1.12.0 |

014 (shortcut catalogue FR-020) and 015 (`origin = recording`) are built **after** 022 (owner order
2026-11-17): `/` is bound through the 004 key map by 022 and adopted into 014's catalogue with owner
022 when 014 is built; the recording exclusion is written as a predicate (`LibraryFilingRules`) that
is true for every origin that exists today, and 015 adds its origin to it. The `409 not-fileable`
response stays in the contract (it cannot occur before 015).

## R-12 Agent tools (tool catalogue 1.1.0)

| Tool | Kind | Risk | Undo | Purpose |
|---|---|---|---|---|
| `browse_library_folder` | server | read | none | children (<= 20) and items (<= 20) of a folder, root when no id |
| `search_library_items` | server | read | none | grouped search (<= 10 per group) |
| `open_library_folder` | ui | read | none | navigate to `/library/{folderId}` (or the search) |
| `set_library_info` | server | change | inverse | set/reset artist, album, song for a track or an item; inverse restores the previous values |

Folder and item names are external or user text: they appear only in `untrusted_text` fields and
taint the run (011 FR-013). `set_library_info` is `change`, so it needs approval in a tainted run;
the approval card shows the exact names. Group `library` grows from 5 to 9 tools; to keep the cap
of 32 in the `library` page context, `delete_mix`, `rename_mix`, `duplicate_mix` and `retry_track`
leave that context (they stay in the `tracks` group for the track pages). No tool is renamed or
removed; minor bump.

## R-13 Migration

EF Core migration `AddLibraryFolders`: nullable columns on `Assets` (placement, item library info,
`FiledVersion` default 0, `FiledAt`), nullable library info columns on `Tracks`, index
`IX_Assets_Folder` on (`FolderArtistKey`, `FolderAlbumKey`, `FolderSongKey`). No data is moved in
SQL (folding needs .NET); the backfill (R-04) files every item at the next start. Down migration
drops the columns (data loss only of user library info; documented).

## Sources

- Spike S-01: `dotnet run` file-based app (search.cs) in the architect's scratchpad; numbers above.
- Delivered code: `src/StemStudio.Domain/Tracks/TrackMetadata.cs`, `Track.Metadata.cs`,
  `Assets/Asset.cs`, `Assets/AssetValues.cs`, `src/StemStudio.Api/Assets/AssetListQuery.cs`,
  `worker/src/stemstudio_worker/metadata/titles.py` (cleaning rules reused through lookup names).
- 005 ADR-014 D2/D5/D8 (precedence, storage, licences); 007 data-model 4.1/4.4; 011 tool catalogue
  1.0.0 (rules, groups, cap 32); 013 spec FR-020..FR-022, backup-archive 1.0.0; 014 spec FR-020;
  015 data-model 2.2.
- WCAG 2.2: H67 (decorative images), 1.4.1 (use of colour), 4.1.3 (status messages); WAI-ARIA APG
  breadcrumb pattern.
- Unicode UAX #15 (normalisation forms); .NET `string.Normalize`, `CharUnicodeInfo`.
