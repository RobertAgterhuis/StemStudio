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
gains `delete_score`. **1.0.0 final** (011 Phase 0 part 2, 2026-11-16; the catalogue had not been
released, so it stays 1.0.0 as allocated in docs/ROADMAP.md instead of the 1.1.0 planned in part 1) -
009/010 delivered: their 15 tools are binding against backend API 1.10.0 (= 1.9.0 + agent):
style library (`list_styles`, `create_style`, `delete_style`), restyle and generations
(`restyle_track`, `list_generations`, `cancel_generation`, `retry_generation`, `import_take`,
`delete_generation`, `get_generation_status`), song from own lyrics (`list_lyrics_sheets`,
`save_lyrics_sheet`, `generate_song`) and own voice (`list_voice_profiles`, `convert_take_voice`)
with the guardrails `artist-imitation`, `lyrics-not-from-user` and the consent rule; `wait_for_job`
phases `restyle`/`generation`; `navigate` pages of 007-011; page-context tool groups for the 32-tool
cap. The draft `generate_song` arguments (`lyrics` <= 4000, 10-600 s) follow `SongRequest` now
(`lyricsId` or `lyricsText` <= 4,096, 10-480 s); the draft `restyle_track.strength: 0..1` is the
`Strength` enum and `section` is `rangeStartSeconds`/`rangeEndSeconds`.
Pre-release clarifications within 1.0.0 (011 follow-ups, 2026-11-16, catalogue not yet released):
`mixer_get_state` result as returned by the frontend - `karaoke` is required (no longer optional) and
`settings` (exactly a `MixSettingsDto`) is added, so `save_mix_as`/`update_mix`/`export_audio` (mode
`mix`) can send the open mixer's state losslessly; a policy step whose dynamic heavy-risk check
fails carries the decision reason `risk-check-failed` (backend API 1.10.0).

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
- **Availability**: `always` (feature delivered; 001-010 all are), `+service` (hidden while the
  backing service reports unreachable or disabled, with the reason; `generation` and `voice` from
  `GET /api/generation/status`), `+mixer` (UI tool offered only when a mixer page is open in the
  session; otherwise `open_mixer` is offered). Tools of later features (012-020) are added in
  catalogue minors (docs/ROADMAP.md).
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
  generation id for 009/010, `phase`) that `wait_for_job` accepts.

## Meta tools (011, always)

| Tool | Kind | Risk | Undo | Arguments | Result | Notes |
|---|---|---|---|---|---|---|
| `set_plan` | meta | read | none | `steps: [{title: string<=120, tool?: string}]` (1-10) | `{accepted: n}` | publishes / replaces the plan (FR-007); calling it again replaces not-yet-started steps |
| `wait_for_job` | meta | read | none | `phase: standard\|detail\|lyrics\|export\|transcription\|restyle\|generation` (`JobPhase`), `trackId?`, `jobId?` (one of them required; `jobId` for export/transcription/restyle/generation) | `{status, progress, error?}` | runner polls the API every 2 s (track: `GET /api/tracks/{id}`; asset: `GET /api/assets/{id}`; generation: `GET /api/generations/{id}`, `waitingForStems`/`waitingForGpu`/`downloadingModel` count as running), max `MaxJobWaitMinutes`, without a model turn (FR-009) |

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
| `mixer_get_state` | ui | read | none | read the open `MixerState` (+mixer) | - | `{trackId, mixId?, unsaved, channels[...] (dB), master (dB), position, playing, loop, metronome, karaoke, settings}`: `karaoke` (bool) = the karaoke banner is visible; `settings` is exactly a `MixSettingsDto` (camelCase, linear volumes 0..1, `name` = "": the agent sets the name before `save_mix_as` / `update_mix` / `export_audio`) |
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
| `navigate` | ui | read | none | open a named page; the unsaved-changes guard applies | `page: import\|tracks\|mixes\|library\|styles\|generations\|lyrics\|songs\|voices\|settings` (`/`, `/tracks`, `/mixes`, `/library`, `/styles`, `/generations`, `/lyrics`, `/songs/new`, `/voices`, `/settings`; `settings` once the 011 frontend ships, otherwise `{opened: false, reason}`) | `{opened, reason?}` |

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

## Feature 009 - Style transfer and style library (always, +service `generation`)

Binding against backend API 1.10.0 (009's 1.8.0 entries). `+service generation`: offered only
while `GET /api/generation/status` reports `state: ready` (cached by the registry for 10 s); with
`notInstalled` / `unavailable` the tools are listed as unavailable with the status `reason`
(`get_generation_status` itself is always offered). Generations are GPU-only (constitution 1.3.0).

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `get_generation_status` | server | read | none | `GET /api/generation/status` | - | `{state, reason, device: {kind, name}\|null, voiceState, downloadBytesNeeded}` (no model paths) |
| `list_styles` | server | read | none | `GET /api/styles` (`q`, `kind`, `genreFamily`, `mood`, `instrument`) | `query?: string<=60`, `kind?: preset\|user`, `genreFamily?: GenreFamily` | `[{styleId, kind, name (presets, trusted) \| untrusted_text: name (user styles), genreFamily, tempoFeel, vocalHandling, defaultStrength}]` max 30 (24 presets + newest user styles) |
| `create_style` | server | change | compensate (fresh) | `POST /api/styles` (`StyleInput`) | `name: 1..60`, `genreFamily`, `subGenre?: <=40`, `tempoFeel`, `instrumentation: [StyleTag] <=8`, `moods: [StyleTag] <=8`, `vocalHandling`, `defaultStrength`, `description?: <=300` | `{styleId, name}`; 400 `imitation` -> tool error "Styles describe genres and sounds, not artists" |
| `delete_style` | server | destructive | none | `DELETE /api/styles/{id}` (user styles only; 409 `preset-read-only`) | `styleId` | `{deleted}` (generations keep their snapshot) |
| `restyle_track` | server | heavy | compensate | `POST /api/tracks/{id}/restyles` (`RestyleRequest`); compensation: `cancel_generation` while active, then `DELETE /api/generations/{id}` | `trackId`, `styleId` (from `list_styles`), `mode?: range\|inPlace` (default `range`), `rangeStartSeconds?`, `rangeEndSeconds?` (10-480 s span), `includedStems?: [StemName] 1..6`, `strength?: subtle\|light\|balanced\|strong\|free`, `takes?: 1..2` | `{generationId, status, estimatedMinutes, device, jobRef{phase: restyle, jobId: generationId, trackId}}` |
| `list_generations` | server | read | none | `GET /api/generations` (`kind`, `sourceTrackId`) | `kind?: restyle\|song\|voiceConversion`, `trackId?` (= `sourceTrackId`) | `[{generationId, kind, status, progress, untrusted_text: title or style name, takes: [{index, durationSeconds, vocal}], createdAt}]` max 20 |
| `cancel_generation` | server | change | none | `POST /api/generations/{id}/cancel` | `generationId` | `{status}` |
| `retry_generation` | server | heavy | inverse (cancel while active) | `POST /api/generations/{id}/retry` (failed or cancelled only; 409 `generation-not-retryable`) | `generationId` | `{generationId, status, estimatedMinutes, jobRef{phase: restyle\|generation}}` |
| `import_take` | server | job | compensate | `POST /api/generations/{id}/takes/{index}/import` (the take becomes a track and is separated; compensation `DELETE /api/tracks/{trackId}`) | `generationId`, `index: 1..2` | `{trackId, status, jobRef{phase: standard, trackId}}` |
| `delete_generation` | server | destructive | none | `DELETE /api/generations/{id}` | `generationId` | `{deleted}` |

Take audio (`getTakeAudio`) is played in the UI, not by the agent. Style editing
(`updateStyle`, `duplicateStyle`) stays in the style editor: the agent creates and deletes only.

**Guardrail `artist-imitation` (FR-025, shared with 009 research 8.3):** the agent's
`Guardrails` deny (`decision: denied`, reason `artist-imitation`, never executed) a call whose free
text (`create_style` names/tags/description, `generate_song` `freeCaption`/`delivery`/`title`)
names a real artist, band or singer (the 009 deny list plus the artist names of the library's
tracks), and the system prompt tells the model that styles are genres and sounds, not artists. The
backend's imitation guard (400 `type: imitation`) stays the authoritative check; a hit there is
reported as the same readable refusal. The model can pass only `styleId`s (UUIDs), never a free
style name, to `restyle_track` and `generate_song`.

## Feature 010 - Own lyrics to song and own voice (always, +service `generation`; voice tools +service `voice`)

Binding against backend API 1.10.0 (010's 1.9.0 entries). `+service voice`: `convert_take_voice`
and `list_voice_profiles` are offered only while `GenerationStatus.voice.state` is `ready`.

| Tool | Kind | Risk | Undo | HTTP | Arguments | Result projection |
|---|---|---|---|---|---|---|
| `list_lyrics_sheets` | server | read | none | `GET /api/lyrics` | `query?: string<=80` (filtered in the agent on the title) | `[{lyricsId, untrusted_text: title, language, updatedAt}]` max 20 - **never the text** |
| `save_lyrics_sheet` | server | change | compensate (fresh) | `POST /api/lyrics` (`LyricsInput`); compensation `DELETE /api/lyrics/{id}` (`ISongActions.DeleteLyricsSheetAsync`, not a model tool) | `title: 1..80`, `language: LyricsLanguage`, `text: 1..4096` (must appear in a user message of the conversation, see below) | `{lyricsId, title, warnings[]}` (no text) |
| `generate_song` | server | heavy | compensate | `POST /api/songs` (`SongRequest`); compensation as `restyle_track` | `lyricsId?` (from `list_lyrics_sheets`) **or** `lyricsText?: 1..4096` (provenance rule), `title?: <=80`, `language: LyricsLanguage`, `styleId?` **or** `freeCaption?: <=300` (guardrail), `leadVoice: female\|male\|any`, `delivery?: <=40` (guardrail), `instrumental?: bool`, `bpm?: 40..220`, `keyScale?`, `timeSignature?: 2\|3\|4\|6`, `durationSeconds?: 10..480` (default 120), `takes?: 1..2` | `{generationId, status, estimatedMinutes, device, jobRef{phase: generation, jobId: generationId}}` |
| `list_voice_profiles` | server | read | none | `GET /api/voice-profiles` | - | `[{profileId, untrusted_text: displayName, consentCurrent: bool (statementVersion = current), durationSeconds}]` - **never** `personName`, `sampleUrl` or the sample |
| `convert_take_voice` | server | heavy | compensate | `POST /api/generations/{id}/takes/{index}/voice-conversions` (`VoiceConversionRequest`); compensation as `restyle_track` | `generationId`, `index: 1..2` (a song take with `vocal: synthetic`), `profileId` (from `list_voice_profiles`), `pitchShift?: -12..12`, `autoShift?: bool` | `{generationId (the new conversion), status, estimatedMinutes, jobRef{phase: generation}}`; 409 `consent-required` / `consent-outdated` -> tool error "Renew the consent for this voice on the Voices page" |

**Lyrics provenance (`lyrics-not-from-user`, FR-013/FR-026):** `save_lyrics_sheet.text` and
`generate_song.lyricsText` are accepted only when the text occurs in a user message of the
conversation (comparison after the backend's normalisation: line endings, trimmed lines, collapsed
blank lines). Otherwise the step is **denied** (not offered for approval): lyrics from tool
results (LRCLIB, band info, an injected title) or written by the model never reach a song. Saved
sheets are referenced by `lyricsId`; their text never passes through the model.

**Consent and no voice cloning (FR-025, ADR-019):** the agent never creates, renames or deletes a
voice profile and never records, uploads or confirms consent (`createVoiceProfile`,
`deleteVoiceProfile`, `getVoiceSample`, `getVoiceConsentStatement` are not tools); a profile is
made only in the 010 Voices page by the person whose voice it is. `convert_take_voice` uses an
existing profile with a current consent record, is always `heavy` (approval card names the
profile's display name and the take), and the backend's consent check (409) is authoritative.
Requests to make a song "sung by" or "in the voice of" a real artist are refused like
`artist-imitation` (no tool call; eval case 30, supplementary case S3).

## Undo (011)

Undo is not a model tool: the panel's Undo button and the phrase "undo"/"maak ongedaan" call
`POST /api/agent/conversations/{id}/undo` (backend API 1.10.0); the runner executes the
recorded inverse/compensating calls (compensating deletes need approval unless "fresh").

## Counts (1.0.0)

| Feature | Tools | of which approval always (heavy/destructive) | Group (cap rule) |
|---|---|---|---|
| 011 meta | 2 | 0 | core |
| 001 | 21 | 2 (`delete_track`, `delete_mix`) | core 8, mixer 9, tracks 4 |
| 002 | 2 | 1 (`start_detail_separation`) | tracks |
| 003 | 1 | 0 | core |
| 004 | 3 | 0 | core |
| 005 | 2 | 0 | tracks |
| 006 | 4 | 1 (`align_lyrics`) | tracks 3, mixer 1 (`show_karaoke`) |
| 007 | 4 | 1 (`delete_export`; + exports > 500 MB) | tracks 1 (`export_audio`), library 3 |
| 008 | 3 | 2 (`transcribe_stem`, `delete_score`) | tracks 1 (`transcribe_stem`), library 2 |
| 009 | 10 | 4 (`restyle_track`, `retry_generation`, `delete_style`, `delete_generation`) | create |
| 010 | 5 | 2 (`generate_song`, `convert_take_voice`) | create |
| **Total** | **57** | **13 (+ large exports)** | |

### Selection per request (cap 32, research section 4)

All 57 tools are registered; `AgentToolRegistry.For(context)` offers the **core** group plus the
groups of the page context, then drops the unavailable ones (`+service`, `+mixer`), sorted by name
(stable for prompt caching). The ai-agent proves the cap per context in `RegistryTests` (T012).

| Group | Tools |
|---|---|
| core (14) | `set_plan`, `wait_for_job`, `search_library`, `get_track`, `import_from_url`, `delete_track`, `list_mixes`, `get_mix`, `open_track`, `open_mixer`, `get_processing_device`, `search_help`, `open_help`, `navigate` |
| mixer (10, `+mixer`) | `mixer_get_state`, `mixer_set_channel`, `mixer_set_master`, `mixer_reset`, `transport`, `set_loop`, `set_metronome`, `show_karaoke`, `save_mix_as`, `update_mix` |
| tracks (13) | `retry_track`, `duplicate_mix`, `rename_mix`, `delete_mix`, `start_detail_separation`, `cancel_detail_separation`, `get_artist_info`, `refresh_artwork`, `get_lyrics_status`, `fetch_lyrics`, `align_lyrics`, `export_audio`, `transcribe_stem` |
| library (5) | `export_stem_pack`, `list_exports`, `delete_export`, `list_scores`, `delete_score` |
| create (15) | the 10 tools of 009 and the 5 tools of 010 |

| Page context (`PageContext.page`) | Groups offered | Count |
|---|---|---|
| `import`, `tracks`, `track`, `mixes`, `settings`, `help`, unknown | core + tracks + `list_styles`, `restyle_track`, `generate_song`, `get_generation_status`, `list_generations` | 14 + 13 + 5 = **32** |
| `mixer` (mixer open) | core + mixer + `start_detail_separation`, `export_audio`, `transcribe_stem`, `get_lyrics_status`, `fetch_lyrics`, `align_lyrics`, `list_styles`, `restyle_track` | 14 + 10 + 8 = **32** |
| `library` | core + library + `export_audio`, `transcribe_stem`, `retry_track`, `delete_mix`, `rename_mix`, `duplicate_mix`, `get_lyrics_status` + `get_generation_status`, `list_generations`, `import_take`, `cancel_generation`, `retry_generation`, `delete_generation` | 14 + 5 + 7 + 6 = **32** |
| `styles`, `generations`, `lyrics`, `songs`, `voices` (009/010 pages) | core + create | 14 + 15 = **29** |

Outside its context a tool is reached with `navigate` (the next turn's context changes) or, on a
mixer page, by first answering from the offered tools; the evaluation set measures the selection.
