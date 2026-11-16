# ADR-036: Library by artist and album (derived folders, folder art, live search)

- **Status**: Accepted (CTO, 2026-11-16; number may be renumbered at merge)
- **Date**: 2026-11-16
- **Decider**: Architect (autonomous mode), to be confirmed by the CTO
- **Feature**: [specs/022-library-folders](../../specs/022-library-folders/spec.md)

## Context

The owner asked (2026-11-16) that saved stems are filed automatically into Artist > Album > Song
folders, with folder art, and that the library filters live while typing. Clarified: the folders
exist in the app only; the search matches artist, album, song and stem/instrument. The 007 library
is a flat list of `Assets`; 005 stores matched artist/album/year and per-track artwork with a fixed
precedence; there are no artist photos (licences, ADR-014 D8). The UI is Blazor Interactive Server.

## Decisions

### D1 Folders are derived from a placement stored on each item

Each item stores its placement (artist, album, song: display name, normalised key, source; album
year; filing-rules version). Folders are computed from the placements; a folder exists while it
holds an item. No folder tables. Folder ids are `ar|al|so` + 16 hex of SHA-256 over the keys.
Rejected: stored folder tables (create/merge/garbage-collect on every save, delete, re-file and 013
merge restore; no requested folder-own data).

### D2 Name precedence per level and normalisation

Per level independently: item library info > track library info > 005 match (found; "Various
Artists" ignored) > import names (005 lookup names, raw names, provenance snapshot) > fallback
("Unknown artist", "Unknown album", the track title). Items without a track follow their source
item; items without either go to "My songs" > "Songs from my lyrics". Keys fold Unicode
compatibility, accents, case, special letters (ß, æ, ø, ł, đ, þ, œ), quotes, dashes and whitespace,
drop a leading English "The" (artist, album) and featuring parts (artist). No splitting on "&", ",",
"and", "x". The display name prefers the match spelling, else the most used spelling.

### D3 Filing is synchronous at creation, re-filing on facts changes, backfill by version

Every creator of an item calls `ILibraryFiler` before saving, so no item is ever unfiled. The
lookup applier, the track synchroniser and the library-info service re-file a track's items (user
names stay). A start-up backfill files items whose `FiledVersion` is below
`LibraryFilingRules.Version` - this is the data migration for existing items, for restored older
backups (013) and for future rule changes. Track deletion never re-files (items keep their place).

### D4 Folder art reuses the 005 pictures

Song: the track's picture. Album: the best picture among its songs by the 005 rank. Artist: a CSS
mosaic of up to four album covers. Otherwise a lettered placeholder on one of eight design-system
art tones. Art references the existing immutable artwork URLs (thumb 160 / cover 600 via `srcset`);
no new image files, endpoints, downloads or worker changes; nothing for backups. Images are
decorative (`alt=""`) because each tile is one link with the full accessible name. Covers stay
in-app only (005 FR-014).

### D5 Live search runs in the API on an in-memory index

`GET /api/library/search` scans an in-memory index rebuilt when the library revision changes (hash
of counts and max timestamps). Spike S-01: build 21 ms, search p95 < 1 ms for 5,000 items. With a
200 ms debounce, sequence numbers and cancellation, results show < 0.4 s after the last key press.
One matcher serves the UI and the agent. Rejected: an index per Blazor circuit (duplicate matcher,
sync problems, 1.5 MB per tab plus a large initial load) and SQLite `LIKE`/FTS5 (no accent folding
without extra schema; no gain at this size).

### D6 Contracts

Backend API 1.22.0 (additive; `Asset.placement` new required response member), UI test ids 1.22.0
(minor; `/library` opens the folder view, the 007 flat list moves to `/library?view=all`,
`library-search` becomes the live search at every level - no id renamed or removed, test migration
listed), agent tool catalogue 1.1.0 (four tools; library page context rebalanced to 32). Worker,
generator, audio engine, backup archive unchanged.

## Consequences

- Positive: no empty folders by construction; filing survives track deletion and restores; zero new
  dependencies, image processing or worker work; search fast enough to run on every pause; the
  agent and UI see the same folders.
- Negative: two bands with the same name share a folder (fix via library info); no folder-own data
  (custom folder pictures, notes) without a later stored-folder design; items of a deleted track
  lose its picture (005 deletes pictures with the track).
- The UI test-id change on `/library` needs a small, listed migration of existing tests (two qa
  files, one bUnit test); the CTO confirmed 1.22.0 as a minor on 2026-11-16.
- Owner decisions 2026-11-16: "Edit library info" is available at every journey level including
  Beginner; the track list (`/tracks`) stays ungrouped; no folder archive download.
- Follow-ups (not planned): licensed artist photos as first choice of artist art; renaming a whole
  folder in one action. Downloading a folder as an archive is out of scope (owner, 2026-11-16).
