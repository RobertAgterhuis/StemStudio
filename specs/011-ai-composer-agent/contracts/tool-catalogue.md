# Contract: Agent tool catalogue (draft 0.1.0; final version assigned at feature start)

Binding for the **ai-agent** agent (tool descriptors, schemas, projections, policy), the
**backend** agent (loopback client, GPU release hook) and the **frontend** agent (UI tool host).
The tool **names**, **risk classes** and **undo kinds** are part of the contract: changing one is a
breaking change (bump the version, record it in the hand-off). Descriptions and examples may be
tuned freely to improve the evaluation score.

## Rules

- **Kinds**: `server` tools call the public backend HTTP API over loopback; `ui` tools are
  executed by the open browser session (mixer page / layout) and report a result back
  (contracts/DELTA.md, "UI tool round trip"); `meta` tools change only the run (plan).
- **Risk** (fixed in code, never decided by the model): `read` (no state change), `change`
  (reversible state change), `job` (starts a short background job: under ~2 min on the reference
  GPU), `heavy` (long GPU/CPU job or large disk use), `destructive` (deletes or overwrites without
  inverse).
- **Approval** (ADR-020 D5): `heavy`, `destructive` always; `change`, `job` when the run is
  tainted or a provenance check fails; `read`, `meta` never.
- **Undo kinds**: `none`, `inverse` (automatic inverse call), `compensate` (delete what the call
  created; destructive itself, so it asks for approval unless marked "fresh" below).
- **Availability**: `always` (001-003 delivered), `feature NNN` (registered only when that
  feature's tool provider ships, i.e. "available when feature NNN is delivered"), `+service`
  (hidden while the backing service reports unreachable, with the reason), `+mixer` (UI tool
  offered only when a mixer page is open in the session; otherwise `open_mixer` is offered).
- **Arguments**: JSON Schema, `additionalProperties: false`, every id a GUID string, stem names
  from the 001/002 enums, numbers with ranges. Relative and absolute values are separate
  arguments (research F-11).
- **Results**: compact projections (whitelisted fields), <= 4 KB; external text only in fields
  named `untrusted_text` (marks the run tainted, FR-013); never lyrics text, biographies, file
  paths, internal URLs or secrets (FR-026).
- **Names**: `snake_case`, verb first, stable across languages (the model sees English names and
  descriptions; it answers in the user's language).

## Meta tools (011)

| Tool | Kind | Risk | Undo | Arguments | Result | Notes |
|---|---|---|---|---|---|---|
| `set_plan` | meta | read | none | `steps: [{title: string<=120, tool?: string}]` (1-10) | `{accepted: n}` | publishes / replaces the plan (FR-007); calling it again replaces not-yet-started steps |
| `wait_for_job` | meta | read | none | `trackId`, `phase: standard\|detail\|lyrics\|export\|transcription\|restyle\|generation`, `jobId?` | `{status, progress, error?}` | runner polls the API (every 2 s, max 45 min) without a model turn; step shows progress (FR-009) |

## Feature 001 - MVP (available: always)

| Tool | Kind | Risk | Undo | HTTP / UI mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `search_library` | server | read | none | `GET /api/tracks` + filter in the agent | `query?: string<=200`, `status?: enum` | `[{trackId, untrusted_text: "title - artist", status, durationSeconds, bpm, hasDetail}]` max 10 |
| `get_track` | server | read | none | `GET /api/tracks/{id}` | `trackId` | `{trackId, untrusted_text: title/artist, status, stage, progress, error, bpm, timeSignature, stems[], detail.status, device}` |
| `import_from_url` | server | job | compensate | `POST /api/imports` | `url: string` (YouTube or Spotify track URL; must appear in a user message, FR-014) | `{trackId, status, cached: bool}` |
| `retry_track` | server | job | inverse (cancel n/a: none) | `POST /api/tracks/{id}/retry` | `trackId` | `{trackId, status}` |
| `delete_track` | server | destructive | none | `DELETE /api/tracks/{id}` | `trackId` | `{deleted: true}` |
| `list_mixes` | server | read | none | `GET /api/mixes` | `trackId?` | `[{mixId, untrusted_text: name, trackId, updatedAt}]` max 20 |
| `get_mix` | server | read | none | `GET /api/mixes/{id}` | `mixId` | `{mixId, name, trackId, channels[{stem, gainDb, pan, mute, solo}], master, loop, metronome}` |
| `save_mix_as` | server | change | compensate (fresh: no approval for its own undo) | `POST /api/mixes` with the **open mixer's** current state (read via `mixer_get_state`) or the given settings | `trackId`, `name: string<=100`, `fromOpenMixer: bool` | `{mixId, name}` |
| `update_mix` | server | change | inverse (previous version kept in the undo entry) | `PUT /api/mixes/{id}` | `mixId`, `fromOpenMixer: bool` or `channels[]` | `{mixId, updatedAt}` |
| `duplicate_mix` | server | change | compensate (fresh) | `POST /api/mixes/{id}/duplicate` | `mixId`, `name?` | `{mixId, name}` |
| `rename_mix` | server | change | inverse | `PUT /api/mixes/{id}` (name only) | `mixId`, `name` | `{mixId, name}` |
| `delete_mix` | server | destructive | none | `DELETE /api/mixes/{id}` | `mixId` | `{deleted: true}` |
| `open_track` | ui | read | none | navigate to `/tracks/{id}` | `trackId` | `{opened: true}` |
| `open_mixer` | ui | read | none | navigate to `/tracks/{id}/mixer` (optionally load `mixId`; the 001 unsaved-changes guard applies and is answered by the user) | `trackId`, `mixId?` | `{opened: true, mixLoaded?}` |
| `mixer_get_state` | ui | read | none | read the open `MixerState` | - (+mixer) | `{trackId, mixId?, unsaved, channels[...], master, position, playing, loop, metronome}` |
| `mixer_set_channel` | ui | change | inverse | `MixerState` channel setters (+mixer) | `stem` (standard or detail name), `gainDb?: -60..+12` (absolute), `gainChangeDb?: -24..+24` (relative), `pan?: -1..1`, `mute?: bool`, `solo?: bool` | `{stem, previous{...}, current{...}}` |
| `mixer_set_master` | ui | change | inverse | master fader (+mixer) | `gainDb?` / `gainChangeDb?` | `{previous, current}` |
| `mixer_reset` | ui | change | inverse | all channels to 0 dB, centre, unmuted, unsoloed (+mixer) | - | `{previous[...]}` |
| `transport` | ui | change | none | play / pause / stop / seek (+mixer) | `action: play\|pause\|stop\|seek`, `positionSeconds?` | `{playing, position}` |
| `set_loop` | ui | change | inverse | A-B loop (+mixer); `bar` arguments use the beat grid | `startSeconds?`/`endSeconds?` or `startBar?`/`endBar?`, `enabled: bool` | `{previous, current}` |
| `set_metronome` | ui | change | inverse | metronome panel (+mixer) | `enabled?`, `volume?: 0..1`, `beatsPerBar?: 2..12`, `offsetMs?` | `{previous, current}` |

## Feature 002 - Detail separation (available: always)

| Tool | Kind | Risk | Undo | HTTP / UI mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `start_detail_separation` | server | heavy | inverse (cancel while running) | `POST /api/tracks/{id}/detail` | `trackId` | `{trackId, detail.status, estimatedMinutes, device}` (estimate from 003 device: GPU ~4 min, CPU ~30 min per 4 min of audio) |
| `cancel_detail_separation` | server | change | none | `POST /api/tracks/{id}/detail/cancel` | `trackId` | `{detail.status}` |

Detail stems are controlled with `mixer_set_channel` using the detail names (ui-test-ids rules).

## Feature 003 - GPU acceleration (available: always)

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_processing_device` | server | read | none | `GET /api/device` | - | `{reachable, kind, name, reason}` |

## Feature 004 - Design system, journey levels, in-app help (available when feature 004 is delivered)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `search_help` | server | read | none | help-topic search endpoint of 004 | `query` | `[{topicId, title, summary<=300}]` (own content, trusted) |
| `open_help` | ui | read | none | open the 004 help panel / tour | `topicId` | `{opened}` |
| `navigate` | ui | read | none | open a named page (`import`, `tracks`, `mixes`, `exports`, `settings`, ...) | `page: enum` | `{opened}` |

The journey level filters nothing in the catalogue; it changes only how much of the step log the
panel shows (spec Clarifications).

## Feature 005 - Artwork and band info (available when feature 005 is delivered)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_artist_info` | server | read | none | 005 track metadata endpoint | `trackId` | `{artist, untrusted_text: summary <= 500 chars, source: "Wikipedia (CC BY-SA 4.0)"}` (taints the run) |
| `refresh_artwork` | server | job | none | 005 refresh endpoint | `trackId` | `{status}` |

## Feature 006 - Synced lyrics and karaoke (available when feature 006 is delivered)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_lyrics_status` | server | read | none | 006 lyrics status | `trackId` | `{available, synced, wordSynced, autoAligned, source}` - **no lyrics text** |
| `fetch_lyrics` | server | job | compensate | 006 LRCLIB lookup | `trackId` | `{status, found}` |
| `align_lyrics` | server | heavy | compensate | 006 forced alignment (GPU job, `+service`) | `trackId` | `{status, estimatedMinutes}` |
| `show_karaoke` | ui | change | inverse | toggle the karaoke banner (+mixer) | `enabled: bool`, `offsetMs?` | `{previous, current}` |

## Feature 007 - Export library (available when feature 007 is delivered)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `export_audio` | server | job (heavy when the estimated size > 500 MB) | compensate (fresh) | 007 `POST` export job | `trackId`, `sources: [stem or detail names]` or `asMixed: bool` (+`mixId?` / open mixer state), `format: wav\|flac\|mp3`, `range?: {startSeconds, endSeconds}`, `name?` | `{exportId, status}` |
| `list_exports` | server | read | none | 007 list | `trackId?` | `[{exportId, name, format, createdAt, aiGenerated}]` |
| `delete_export` | server | destructive | none | 007 delete | `exportId` | `{deleted}` |

## Feature 008 - Sheet music (available when feature 008 is delivered)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `transcribe_stem` | server | heavy | compensate | 008 transcription job | `trackId`, `stem`, `formats: [pdf, musicxml, midi]` | `{jobId, status, estimatedMinutes}` |
| `list_scores` | server | read | none | 008 list | `trackId?` | `[{scoreId, stem, formats, createdAt}]` |

## Feature 009 - Style transfer (available when feature 009 is delivered, +service)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `list_styles` | server | read | none | 009 style library | `query?` | `[{styleId, name, tags}]` (own library, trusted) |
| `restyle_track` | server | heavy | compensate | 009 restyle job | `trackId`, `styleId`, `strength: 0..1`, `section?: {startSeconds, endSeconds}` | `{jobId, status, estimatedMinutes}` |

Guardrail: free-text style requests naming a real artist or singer are refused before any call
(shared deny rule with 009, research 8.3; FR-025). The model can only pass `styleId`s from
`list_styles`.

## Feature 010 - Own lyrics to song (available when feature 010 is delivered, +service)

| Tool | Kind | Risk | Undo | Mapping | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `generate_song` | server | heavy | compensate | 010 generation job | `lyrics: string<=4000` (must come from the user's own message; provenance check like URLs), `styleId`, `durationSeconds: 10..600`, `title?` | `{jobId, status, estimatedMinutes}` |

Excluded on purpose: voice cloning / "sing with my voice" (010 P2) - consent flow only in the 010
UI (research 9.5); re-importing a generated song uses `import_from_url`-equivalent 010/007
endpoints when they exist.

## Undo tool (011)

Undo is not a model tool: the panel's Undo button and the phrase "undo"/"maak ongedaan" call
`POST /api/agent/conversations/{id}/undo` (contracts/DELTA.md); the runner executes the recorded
inverse/compensating calls (compensating deletes need approval unless "fresh").

## Counts

| Feature | Tools | of which approval always (heavy/destructive) |
|---|---|---|
| 011 meta | 2 | 0 |
| 001 | 21 | 2 (`delete_track`, `delete_mix`) |
| 002 | 2 | 1 |
| 003 | 1 | 0 |
| 004 | 3 | 0 |
| 005 | 2 | 0 |
| 006 | 4 | 1 |
| 007 | 3 | 1 (+ `export_audio` when large) |
| 008 | 2 | 1 |
| 009 | 2 | 1 |
| 010 | 1 | 1 |
| **Total** | **43** | **9 (+1 conditional)** |

At 011 start (001-003 only) the registry offers 26 tools; without an open mixer the 7 mixer UI
tools are not offered (19). Cap: 32 tools per request (research section 4).
