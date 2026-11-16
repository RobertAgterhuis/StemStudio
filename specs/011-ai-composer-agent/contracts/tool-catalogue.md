# Contract: Agent tool catalogue (v1.0.0)

Binding for the **ai-agent** agent (tool descriptors, schemas, projections, policy), the
**backend** agent (loopback client `IStemStudioActions`, GPU release hook) and the **frontend**
agent (UI tool host). The tool **names**, **kinds**, **risk classes** and **undo kinds** are part of
the contract: changing one is a breaking change (bump the major, record it in the hand-off); adding
a tool is additive (minor). Descriptions and examples may be tuned freely to improve the evaluation
score.

Changelog: 0.1.0 (draft, 011 plan). **1.0.0** (011 Phase 0 part 1, 2026-11-15) - finalised against
the delivered contracts of 001-008 on branch `006-008-lyrics-sheet-music` (backend API 1.7.0,
UI test ids 1.7.0, help content 1.0.0): 004 help/navigation tools are `ui` tools (the help catalogue
lives in the Web app; there is no help endpoint), 005 `refresh_artwork` maps to the metadata lookup,
006 `get_lyrics_status` never sees lyrics text, 007 `export_audio` follows `CreateAudioExportRequest`
(`mode` raw/mix, `channels`) and gains `export_stem_pack`, 008 `transcribe_stem` follows
`CreateScoreRequest` (`channel`, `profile`, `quality`; formats are always PDF + MusicXML + MIDI) and
gains `delete_score`. 009/010 entries are **pending 009/010**: they become binding in 1.1.0 once
those features fix backend API 1.8.0/1.9.0 (Phase 0 part 2).

## Rules

- **Kinds**: `server` tools call the public backend HTTP API over loopback (`IStemStudioActions`,
  `src/StemStudio.Agent/Ports`); `ui` tools are executed by the open browser session (mixer page /
  layout) and report a result back (contracts/DELTA.md section 4); `meta` tools change only the run.
- **Risk** (fixed in code, never decided by the model): `read` (no state change), `change`
  (reversible state change), `job` (starts a short background job: under ~2 min on the reference
  GPU), `heavy` (long GPU/CPU job or large disk use), `destructive` (deletes or overwrites without
  inverse).
- **Approval** (ADR-020 D5): `heavy`, `destructive` always; `change`, `job` when the run is
  tainted or a provenance check fails; `read`, `meta` never.
- **Undo kinds**: `none`, `inverse` (automatic inverse call), `compensate` (delete what the call
  created; destructive itself, so it asks for approval unless marked "fresh").
- **Availability**: `always` (feature delivered on this branch), `pending NNN` (registered only when
  that feature's contracts and tool provider ship), `+service` (hidden while the backing service
  reports unreachable or disabled, with the reason), `+mixer` (UI tool offered only when a mixer page
  is open in the session; otherwise `open_mixer` is offered).
- **Arguments**: JSON Schema, `additionalProperties: false`, every id a GUID string, stem names from
  the 001/002 enums (`StemNames`, `DetailNames`), channel ids as in backend API `ChannelId`, numbers
  with ranges. Relative and absolute values are separate arguments (research F-11).
- **Results**: compact projections (whitelisted fields), <= 4 KB; external text only in fields named
  `untrusted_text` (marks the run tainted, FR-013); never lyrics text, full biographies, file paths,
  internal URLs or secrets (FR-026). With the **Claude** backend, the Wikipedia summary of
  `get_artist_info` is omitted as well (constitution 1.3.0 IV exception item 5: no biographies are
  sent to the paid service).
- **Names**: `snake_case`, verb first, stable across languages (the model sees English names and
  descriptions; it answers in the user's language).
- **Jobs**: tools that start a job return a `jobRef` (`trackId`, `jobId` = asset id for 007/008,
  `phase`) that `wait_for_job` accepts.

## Meta tools (011, always)

| Tool | Kind | Risk | Undo | Arguments | Result | Notes |
|---|---|---|---|---|---|---|
| `set_plan` | meta | read | none | `steps: [{title: string<=120, tool?: string}]` (1-10) | `{accepted: n}` | publishes / replaces the plan (FR-007); calling it again replaces not-yet-started steps |
| `wait_for_job` | meta | read | none | `phase: standard\|detail\|lyrics\|export\|transcription` (+ `restyle\|generation` pending 009/010), `trackId?`, `jobId?` (one of them required; `jobId` for export/transcription) | `{status, progress, error?}` | runner polls the API every 2 s (track: `GET /api/tracks/{id}`; asset: `GET /api/assets/{id}`), max `MaxJobWaitMinutes`, without a model turn (FR-009) |

## Feature 001 - MVP (always)

| Tool | Kind | Risk | Undo | HTTP / UI mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `search_library` | server | read | none | `GET /api/tracks` + filter in the agent (case- and diacritics-insensitive on title/artist) | `query?: string<=200`, `status?: queued\|processing\|completed\|failed` | `[{trackId, untrusted_text: "title - artist", status, durationSeconds, bpm, hasDetail, lyricsStatus}]` max 10 |
| `get_track` | server | read | none | `GET /api/tracks/{id}` | `trackId` | `{trackId, untrusted_text: title/artist, status, stage, progress, error, bpm, timeSignature, stems[], detail.status, device, lyricsStatus, metadataStatus}` |
| `import_from_url` | server | job | compensate | `POST /api/imports` | `url: string<=2048` (YouTube or Spotify track URL; must appear literally in a user message, FR-014) | `{trackId, status, cached: bool, jobRef}` |
| `retry_track` | server | job | none | `POST /api/tracks/{id}/retry` | `trackId` | `{trackId, status, jobRef}` |
| `delete_track` | server | destructive | none | `DELETE /api/tracks/{id}` | `trackId` | `{deleted: true}` |
| `list_mixes` | server | read | none | `GET /api/mixes` | `trackId?` | `[{mixId, untrusted_text: name, trackId, updatedAt}]` max 20 |
| `get_mix` | server | read | none | `GET /api/mixes/{id}` | `mixId` | `{mixId, untrusted_text: name, trackId, channels[{stem, gainDb, pan, mute, solo}], master, loop, metronome}` (volume converted to dB) |
| `save_mix_as` | server | change | compensate (fresh: no approval for its own undo) | `POST /api/mixes` with the open mixer's state (read via `mixer_get_state`; +mixer) | `trackId`, `name: string 1..100`, `fromOpenMixer: true` | `{mixId, name}` |
| `update_mix` | server | change | inverse (previous version kept in the undo entry) | `PUT /api/mixes/{id}` with the open mixer's state (+mixer) | `mixId`, `fromOpenMixer: true` | `{mixId, updatedAt}` |
| `duplicate_mix` | server | change | compensate (fresh) | `POST /api/mixes/{id}/duplicate` (named "&lt;name&gt; (copy)"; then `rename_mix` when the user named it) | `mixId` | `{mixId, name}` |
| `rename_mix` | server | change | inverse | `GET` + `PUT /api/mixes/{id}` (name only) | `mixId`, `name: string 1..100` | `{mixId, name}` |
| `delete_mix` | server | destructive | none | `DELETE /api/mixes/{id}` | `mixId` | `{deleted: true}` |
| `open_track` | ui | read | none | navigate to `/tracks/{id}` | `trackId` | `{opened: true}` |
| `open_mixer` | ui | read | none | navigate to `/tracks/{id}/mixer` (optionally load `mixId`; the 001 unsaved-changes guard applies and is answered by the user) | `trackId`, `mixId?` | `{opened: true, mixLoaded?}` |
| `mixer_get_state` | ui | read | none | read the open `MixerState` (+mixer) | - | `{trackId, mixId?, unsaved, channels[...], master, position, playing, loop, metronome, karaoke?}` |
| `mixer_set_channel` | ui | change | inverse | `MixerState` channel setters (+mixer) | `stem` (standard or detail name), `gainDb?: -60..+12` (absolute), `gainChangeDb?: -24..+24` (relative; never both), `pan?: -1..1`, `mute?: bool`, `solo?: bool` | `{stem, previous{...}, current{...}}` |
| `mixer_set_master` | ui | change | inverse | master fader (+mixer) | `gainDb?` / `gainChangeDb?` | `{previous, current}` |
| `mixer_reset` | ui | change | inverse | all channels to 0 dB, centre, unmuted, unsoloed (+mixer) | - | `{previous[...]}` |
| `transport` | ui | change | none | play / pause / stop / seek (+mixer) | `action: play\|pause\|stop\|seek`, `positionSeconds?` (seek) | `{playing, position}` |
| `set_loop` | ui | change | inverse | A-B loop (+mixer); `bar` arguments use the beat grid | `startSeconds?`/`endSeconds?` or `startBar?`/`endBar?`, `enabled: bool` | `{previous, current}` |
| `set_metronome` | ui | change | inverse | metronome panel (+mixer) | `enabled?`, `volume?: 0..1`, `beatsPerBar?: 2..12`, `offsetMs?: -2000..2000` | `{previous, current}` |

## Feature 002 - Detail separation (always)

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `start_detail_separation` | server | heavy | inverse (cancel while running) | `POST /api/tracks/{id}/detail` | `trackId` | `{trackId, detailStatus, estimatedMinutes, device, jobRef}` (estimate from the 003 device: GPU ~4 min, CPU ~30 min per 4 min of audio) |
| `cancel_detail_separation` | server | change | none | `POST /api/tracks/{id}/detail/cancel` | `trackId` | `{detailStatus}` |

Detail stems are controlled with `mixer_set_channel` using the detail names (ui-test-ids rules).

## Feature 003 - GPU acceleration (always)

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_processing_device` | server | read | none | `GET /api/device` | - | `{reachable, kind, name, reason}` |

## Feature 004 - Design system, levels, in-app help (always)

The help catalogue (`src/StemStudio.Web/Help`, help-content contract 1.0.0) is loaded by the Web app;
the backend has no help endpoint (004 DELTA.md), so all three tools are `ui` tools of the layout's
tool host and work on every page.

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `search_help` | ui | read | none | `HelpCatalog` search of the Web app (same ranking as `help-search-input`) | `query: string 1..200` | `[{topicId, title, summary<=300}]` max 5 (own content, trusted) |
| `open_help` | ui | read | none | open the help panel on a topic (`help-panel`) | `topicId` (pattern `^[a-z0-9-]+$`) | `{opened}` |
| `navigate` | ui | read | none | open a named page; the unsaved-changes guard applies | `page: import\|tracks\|mixes\|library\|settings` (`library` once the 007 frontend ships, `settings` once the 011 frontend ships; otherwise `{opened: false, reason}`) | `{opened, reason?}` |

The experience level filters nothing in the catalogue; it changes only how much of the step log the
panel shows (spec Clarifications).

## Feature 005 - Artwork and band info (always, +service)

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_artist_info` | server | read | none | `GET /api/tracks/{id}/band-info` | `trackId` | `{status, artist: {name, type, country, beginYear, endYear, genres[<=5], members[<=10 names]}, recording: {album, year}, untrusted_text: Wikipedia summary <= 500 chars (omitted for Claude), source: "Wikipedia (CC BY-SA 4.0)"}` - **taints the run** (names, genres and the summary are external text) |
| `refresh_artwork` | server | job | none | `POST /api/tracks/{id}/metadata/lookup` (no body: retry with the stored names) | `trackId` | `{metadataStatus}` |

Own pictures (`PUT/DELETE /api/tracks/{id}/artwork`) are not agent tools: they need a file upload.

## Feature 006 - Synced lyrics and karaoke (always, +service)

| Tool | Kind | Risk | Undo | HTTP / UI | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_lyrics_status` | server | read | none | `GET /api/tracks/{id}/lyrics` reduced to `LyricsSummary` by the backend (lines dropped before they reach the agent) | `trackId` | `{status, syncLevel, origin, source, instrumental, reason}` - **no lyrics text** |
| `fetch_lyrics` | server | job | compensate | `POST /api/tracks/{id}/lyrics/search` (LRCLIB) | `trackId` | `{status, jobRef}`; compensation `DELETE /api/tracks/{id}/lyrics` |
| `align_lyrics` | server | heavy | none | `POST /api/tracks/{id}/lyrics/align` (GPU job, 503 when the worker is unavailable) | `trackId` | `{status, estimatedMinutes, jobRef}` |
| `show_karaoke` | ui | change | inverse | karaoke toggle of the mixer (`karaoke-toggle`, +mixer) | `enabled: bool` | `{previous, current}` |

Entering or editing lyrics text (`PUT /api/tracks/{id}/lyrics`) and the offset are not agent tools
(lyrics text is never passed through the model, FR-026).

## Feature 007 - Export library (always)

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `export_audio` | server | job; **heavy** when the estimated size > 500 MB | compensate (fresh) | `POST /api/assets/audio-exports` (`CreateAudioExportRequest`) | `trackId`, `mode: raw\|mix` (`mix` = what I hear: the open mixer's state via `mixer_get_state`, or `mixId`), `channels?: [ChannelId] 1..`, `mixId?`, `format: flac\|wav\|mp3`, `bitDepth?: 16\|24`, `mp3BitrateKbps?: 192\|256\|320`, `range?: {startSeconds, endSeconds}`, `name?: string<=100` | `{assetId, status, jobRef}` |
| `export_stem_pack` | server | job (heavy > 500 MB) | compensate (fresh) | `POST /api/assets/stem-packs` | `trackId`, `format: flac\|wav\|mp3`, `includeDetail?: bool` | `{assetIds[], status}` |
| `list_exports` | server | read | none | `GET /api/assets?kind=audio` | `trackId?`, `query?` | `[{assetId, untrusted_text: name, format, status, sizeBytes, createdAt}]` max 20 |
| `delete_export` | server | destructive | none | `DELETE /api/assets/{id}` | `assetId` | `{deleted}` |

## Feature 008 - Sheet music (always)

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `transcribe_stem` | server | heavy | compensate | `GET /api/scores/options` (estimate, allowed profiles) then `POST /api/assets/scores` (`CreateScoreRequest` with `source: {trackId, channel}`) | `trackId`, `channel: ChannelId`, `profile?: vocals\|bass\|guitar\|keys\|piano\|drums` (default: suggested), `quality?: accurate\|fast` (piano only) | `{assetId, status, estimatedMinutes, jobRef}` (files: PDF, MusicXML, MIDI) |
| `list_scores` | server | read | none | `GET /api/assets?kind=score` | `trackId?` | `[{assetId, untrusted_text: name, profile, status, createdAt}]` max 20 |
| `delete_score` | server | destructive | none | `DELETE /api/assets/{id}` | `assetId` | `{deleted}` |

## Feature 009 - Style transfer (pending 009, +service)

Draft entries, **not binding** until backend API 1.8.0 is fixed by the 009 Phase 0; finalised in
catalogue 1.1.0.

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `list_styles` | server | read | none | 009 style library (pending 009) | `query?` | `[{styleId, name, tags}]` (own library, trusted) |
| `restyle_track` | server | heavy | compensate | 009 restyle job (pending 009) | `trackId`, `styleId`, `strength: 0..1`, `section?: {startSeconds, endSeconds}` | `{jobId, status, estimatedMinutes}` |

Guardrail: free-text style requests naming a real artist or singer are refused before any call
(shared deny rule with 009, research 8.3; FR-025). The model can only pass `styleId`s from
`list_styles`.

## Feature 010 - Own lyrics to song (pending 010, +service)

Draft entry, **not binding** until backend API 1.9.0 is fixed by the 010 Phase 0.

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `generate_song` | server | heavy | compensate | 010 generation job (pending 010) | `lyrics: string<=4000` (must come from the user's own message; provenance check like URLs), `styleId`, `durationSeconds: 10..600`, `title?` | `{jobId, status, estimatedMinutes}` |

Excluded on purpose: voice cloning / "sing with my voice" (010 P2) - consent flow only in the 010
UI (research 9.5).

## Undo (011)

Undo is not a model tool: the panel's Undo button and the phrase "undo"/"maak ongedaan" call
`POST /api/agent/conversations/{id}/undo` (agent-api.openapi.yaml); the runner executes the
recorded inverse/compensating calls (compensating deletes need approval unless "fresh").

## Counts (1.0.0)

| Feature | Tools | of which approval always (heavy/destructive) | Status |
|---|---|---|---|
| 011 meta | 2 | 0 | binding |
| 001 | 21 | 2 (`delete_track`, `delete_mix`) | binding |
| 002 | 2 | 1 | binding |
| 003 | 1 | 0 | binding |
| 004 | 3 | 0 | binding |
| 005 | 2 | 0 | binding |
| 006 | 4 | 1 (`align_lyrics`) | binding |
| 007 | 4 | 1 (`delete_export`; + exports > 500 MB) | binding |
| 008 | 3 | 2 (`transcribe_stem`, `delete_score`) | binding |
| **Binding total** | **42** | **7 (+ large exports)** | |
| 009 | 2 | 1 | pending 009 |
| 010 | 1 | 1 | pending 010 |
| **With 009/010** | **45** | **9** | |

All 42 binding tools are registered; the registry offers at most 32 per request (research
section 4). Outside a mixer page it drops the 10 `+mixer` tools (the 7 mixer UI tools,
`show_karaoke`, and `save_mix_as` / `update_mix`, which save the open mixer's state): 32 offered.
On a mixer page it drops 10 library-management tools that need no open mixer (`list_exports`,
`delete_export`, `list_scores`, `delete_score`, `export_stem_pack`, `refresh_artwork`,
`retry_track`, `cancel_detail_separation`, `duplicate_mix`, `rename_mix`): 32 offered. With 009/010
(45 tools) the ai-agent extends the page-context rule (T012) so the cap holds; the selection is
measured by the evaluation set.
