# Contract delta: agent tool catalogue 1.1.0 (feature 022)

Status: **binding delta** (022 design, 2026-11-16). Base: `specs/011-ai-composer-agent/contracts/tool-catalogue.md`
**1.0.0** (no other feature of 012-021 or the claude-key follow-up changes the catalogue). Merged by
the architect in Phase 0 (tasks.md T004) into the catalogue as **1.1.0**. All rules of 1.0.0 apply
(kinds, risk classes, approval, undo kinds, results <= 4 KB, external text only in `untrusted_text`).

## Changelog line

> 1.1.0 (additive, feature 022) - library folders: `browse_library_folder`, `search_library_items`,
> `open_library_folder`, `set_library_info` (backend API 1.22.0, UI test ids 1.22.0). Group
> `library` 5 -> 9 tools; the `library` page context drops `retry_track`, `delete_mix`,
> `rename_mix`, `duplicate_mix` to keep the cap of 32. No tool renamed or removed.

## Feature 022 - Library by artist and album (always)

| Tool | Kind | Risk | Undo | HTTP / UI | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `browse_library_folder` | server | read | none | `GET /api/library/folders` or `GET /api/library/folders/{folderId}` (`limit=20`) | `folderId?: string` (pattern `^(ar|al|so)[0-9a-f]{16}$`), `sort?: name\|year\|recent\|size\|items`, `kind?: audio\|score` | `{folder: {folderId, level, untrusted_text: name} \| null, path: [{folderId, level, untrusted_text: name}], children: [{folderId, level, untrusted_text: name, year, items, sizeBytes}] <= 20, childrenTotal, items: [{assetId, kind, status, untrusted_text: name, channels: [ChannelId] <= 8, format, sizeBytes}] <= 20, itemsTotal}` - **taints the run** |
| `search_library_items` | server | read | none | `GET /api/library/search?q=&within=&limit=10` | `query: string 1..100`, `folderId?` | `{artists: [{folderId, untrusted_text: name, items}] <= 10, albums: [{folderId, untrusted_text: "album - artist", items}] <= 10, songs: [{folderId, untrusted_text: "song - album - artist", items}] <= 10, items: [{assetId, kind, status, untrusted_text: "name (artist > album > song)", matchedInstruments}] <= 10, totals}` - **taints the run** |
| `open_library_folder` | ui | read | none | navigate to `/library/{folderId}` or `/library?q=<query>` (unsaved-changes guard applies) | `folderId?`, `query?: string 1..100` (exactly one) | `{opened, reason?}` |
| `set_library_info` | server | change | inverse | `GET` then `PUT /api/tracks/{trackId}/library-info` or `PUT /api/assets/{assetId}/library-info` | `trackId?` xor `assetId?`, `artist?: string 1..200 \| null`, `album?: ... \| null`, `song?: ... \| null` (omitted = keep the current value, `null` = automatic) | `{scope: track\|item, movedItems, placement?: {artistFolderId, albumFolderId, songFolderId}}`; inverse: `PUT` with the values read by the `GET` before the change |

Rules for 022:

- Folder and item names are imported metadata or user text; they reach the model only in
  `untrusted_text` (011 FR-013). Counts, ids, sizes, kinds, statuses, channel ids and instrument
  labels (closed catalogue, `InstrumentLabels`) are trusted.
- `set_library_info` is `change`: approval is required in a tainted run or when a provenance check
  fails (011 ADR-020 D5). The approval card shows scope, target and every new name verbatim. The
  merged arguments are sent (omitted values are filled from the `GET`), so the request is always a
  complete `LibraryInfoRequest`.
- Recorded takes (015) are never returned; `set_library_info` on one fails with the backend's
  `409 not-fileable` reason.
- With the Claude backend nothing extra is withheld (no biographies or lyrics are involved).

## Counts (1.1.0)

| Feature | Tools | Approval always | Group |
|---|---|---|---|
| 1.0.0 total | 57 | 13 (+ large exports) | |
| 022 | 4 | 0 | library 4 |
| **Total** | **61** | **13** | |

## Groups and page contexts (changes only)

| Group | Tools |
|---|---|
| library (9) | `export_stem_pack`, `list_exports`, `delete_export`, `list_scores`, `delete_score`, `browse_library_folder`, `search_library_items`, `open_library_folder`, `set_library_info` |

| Page context | Groups offered | Count |
|---|---|---|
| `library` (`/library`, `/library/{folderId}`) | core (14) + library (9) + `export_audio`, `transcribe_stem`, `get_lyrics_status` + `get_generation_status`, `list_generations`, `import_take`, `cancel_generation`, `retry_generation`, `delete_generation` | 14 + 9 + 3 + 6 = **32** |

Every other context is unchanged (the 022 tools are reached from them with `navigate` to `library`).
`navigate`'s `library` page is unchanged; `AgentPageContexts` maps `/library/{folderId}` to `library`.

## Evaluation cases (added to the 011 evaluation set)

| Id | Prompt (en / nl) | Expected tools | Check |
|---|---|---|---|
| LIB-01 | "Which stems do I have of Metallica?" / "Welke stems heb ik van Metallica?" | `search_library_items` (or `browse_library_folder`) | answer lists the items of the artist folder |
| LIB-02 | "Find all my snare stems" | `search_library_items` with an instrument word | every snare item, no others |
| LIB-03 | "Open the Ace of Spades folder" | `search_library_items` -> `open_library_folder` | the UI shows the song folder |
| LIB-04 | "Put the Ace of Spades song on the album Ace of Spades" | `search_library_items` -> `set_library_info` | approval card (tainted run), items moved; Undo restores |
| LIB-05 | "Which bass stems are in Unknown artist?" | `browse_library_folder` | items of the fallback folder |
