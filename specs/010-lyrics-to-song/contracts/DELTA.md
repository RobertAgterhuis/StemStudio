# Contract delta: feature 010 (own lyrics to song)

## Phase 0 outcome (architect, 2026-11-15)

Bound versions: **generator-api 1.1.0** = 009's 1.0.0 + section 1, **backend-api 1.9.0** = 1.8.0 +
section 2, **ui-test-ids 1.9.0** = 1.8.0 + section 3; worker-api 1.6.0 and the engine 2.1.0
unchanged. Full documents in this folder (Redocly 2.78.0 valid); the code (`schemas.py`
`CONTRACT_VERSION = "1.1.0"`, `StemStudio.Contracts`) follows them. Refinements (binding; details
in ADR-019 "Phase 0 outcome"):

- Lyrics-sheet operation ids: `listLyricsSheets`, `getLyricsSheet`, `createLyricsSheet`,
  `uploadLyricsSheet`, `updateLyricsSheet`, `deleteLyricsSheet` (006 owns `getLyrics`).
- Generator: `VoiceSampleCheck` has `reason` as a code (`tooShort`/`tooLong`/`unvoiced`) plus a
  readable `message`; song takes `profile`, conversion takes `hasVocalStem`; restyle takes
  `vocal: null`; `NotServable` (409 `gpuRequired` / `kindNotServed`); the singer example health is
  `SingerReadyHealth`.
- Backend: `Generation.song` (`SongDetails`) and `Generation.voiceConversion`
  (`VoiceConversionDetails`); `LyricsSection.number`; `SongRequest.timeSignature` is a string
  (`"2"`, `"3"`, `"4"`, `"6"`); `VoiceProfile.convertedTakeCount`; problem `type` values
  `consent-required`, `consent-outdated`, `voice-name-taken`, `voice-not-installed`,
  `voice-unavailable`, `no-vocals`, `too-short`, `too-long`, `unvoiced`, `library-audio`.
- `GPU need SONG_PLANNED` = 12,288 MiB with the LM loaded directly in bf16 (re-measured).
- UI ids: also `nav-songs`, `nav-lyrics`, `nav-voices`, `song-lyrics-count`,
  `song-time-signature`, `song-generations`, `lyrics-song-<lyricsId>`, `lyrics-search`,
  `lyrics-empty`, `lyrics-delete-confirm`, the lyrics editor ids, `voice-record-timer`,
  `voice-preview`, `voice-delete-confirm`, `take-voice-convert-cancel`, `take-voice-pitch-shift`,
  `take-voice-auto-shift`, `take-vocal-play-*`, `track-vocal-badge`, and `data-voice-state` on
  `generation-status`. Help topic ids without the `help.` prefix.

On top of the 009 contracts (`specs/009-style-transfer/contracts/`). Final versions are assigned at
feature start (tasks.md T002): generator `G` (1.0.0 after 009) -> `G+0.1`; backend `B9` (after
009) -> `B9+0.1`; UI ids `U9` -> `U9+0.1`. All changes are **additive**. Worker API and audio
engine: **unchanged**.

## 1. Generator API 1.0.0 -> 1.1.0

| Change | Detail |
|---|---|
| `CreateGenerationRequest.kind` | adds `song` (`SongParams`, no audio parts) and `voiceConversion` (`VoiceConversionParams` + parts `target` (1), `prompt` (1), `accompaniment` (0-5)) |
| `GET /v1/generations/{id}/takes/{index}/vocal` | new, `getTakeVocal`: converted vocal stem of a voice-conversion take (`audio/flac`, range); `404` for other kinds |
| `POST /v1/voice-samples/check` | new, `checkVoiceSample`: multipart `sample` -> `200 VoiceSampleCheck { checkId, durationSeconds, voicedRatio, pcmSha256, ok, reason? }`; `400` unreadable; `413` > 20 MiB; works without a GPU (CPU F0) |
| `GET /v1/voice-samples/{checkId}/audio` | new, `getCheckedVoiceSample`: normalised 48 kHz mono FLAC of an `ok` check, available once for 10 minutes; `404` afterwards |
| `GeneratedTake` | adds `vocal: "none" \| "synthetic" \| "converted"`, `hasVocalStem: bool`, `profile?: "planned" \| "direct"` (song takes) |
| `GeneratorHealth` | adds `kinds: ("restyle"\|"song"\|"voiceConversion")[]` (job kinds this instance serves) and `voiceSampleCheck: bool`; models add `soulx-singer-svc`, `rmvpe`, `whisper-base` |
| Deployment | the same document is served by two services: `generator` (kinds `restyle`, `song`) and `singer` (kind `voiceConversion` + voice-sample endpoints; SoulX needs transformers 4.41, ADR-019 D2). A kind not in `kinds` -> `409` `code: "kindNotServed"` |
| `GenerationErrorCode` | adds `unvoicedTarget` (target vocal stem < 10 % voiced), `promptInvalid` |
| Examples | `QueuedSong`, `RunningSong` (stage "Planning song structure"), `CompletedSong`, `CompletedInstrumental`, `CompletedVoiceConversion`, `FailedUnvoicedTarget`, `VoiceSampleOk`, `VoiceSampleUnvoiced` |

`SongParams` and `VoiceConversionParams`: data-model.md 2.1.

## 2. Backend API `B9` -> `B9+0.1`

| Method + path | operationId | Body / query | Responses |
|---|---|---|---|
| `GET /api/lyrics` | `listLyrics` | `?q=` | `200 LyricsSheet[]` (by `updatedAt` desc) |
| `GET /api/lyrics/{lyricsId}` | `getLyrics` | - | `200`; `404` |
| `POST /api/lyrics` | `createLyrics` | `LyricsInput { title, language, text }` | `201`; `400` (field + reason) |
| `POST /api/lyrics/upload` | `uploadLyrics` | multipart `file` (`.txt`, `.lrc`, `.md`, UTF-8, <= 64 KiB), `title?`, `language?` | `201 LyricsSheet` (normalised); `400`; `413`; `415` |
| `PUT /api/lyrics/{lyricsId}` | `updateLyrics` | `LyricsInput` | `200`; `400`; `404` |
| `DELETE /api/lyrics/{lyricsId}` | `deleteLyrics` | - | `204`; `404` |
| `POST /api/lyrics/sections` | `proposeSections` | `{ text }` | `200 { text, sections: [ { marker, lineCount } ], wordsPerSecondAt120s }` (pure helper) |
| `POST /api/songs` | `createSong` | `SongRequest { lyricsId? \| lyricsText, title?, language, styleId?, freeCaption?, leadVoice: "female"\|"male"\|"any", delivery?, instrumental = false, bpm?, keyScale?, timeSignature?, durationSeconds = 120, takes = 1 }` | `202 Generation` (kind `song`); `400` (incl. guard `code: "imitation"`, density warning is **not** an error); `409` not ready |
| `GET /api/voice-profiles` | `listVoiceProfiles` | - | `200 VoiceProfile[]` |
| `POST /api/voice-profiles` | `createVoiceProfile` | multipart: `sample` (WAV/FLAC/MP3/WebM-Opus from the browser recorder, <= 20 MiB), `displayName`, `personName`, `consentConfirmed=true`, `consentStatementVersion="consent.v1"`, `source="recorded"\|"uploaded"` | `201 VoiceProfile`; `400` (missing consent: `code: "consentRequired"`; wrong statement version: `code: "consentOutdated"`); `409` duplicate name; `422` sample rejected (`code: "tooShort"\|"tooLong"\|"unvoiced"\|"libraryAudio"`, reason); `503` generator unreachable (sample check needs the generator; CPU is enough) |
| `GET /api/voice-profiles/{profileId}` | `getVoiceProfile` | - | `200`; `404` |
| `GET /api/voice-profiles/{profileId}/sample` | `getVoiceSample` | `Range` | `200`/`206 audio/flac` |
| `DELETE /api/voice-profiles/{profileId}` | `deleteVoiceProfile` | `?deleteTakes=false` | `204`; `404` |
| `GET /api/consent/voice` | `getVoiceConsentStatement` | - | `200 { version: "consent.v1", text, textNl }` |
| `POST /api/generations/{generationId}/takes/{index}/voice-conversions` | `createVoiceConversion` | `{ profileId, pitchShift = 0, autoShift = true }` | `202 Generation` (kind `voiceConversion`, status `queued` or `waitingForStems`); `404`; `409` source not a completed song take with vocals, or not ready |
| `GET /api/generations/{generationId}/takes/{index}/vocal` | `getTakeVocal` | `Range` | `200`/`206`; `404` |

Schema changes:

- `Generation.kind` adds `song`, `voiceConversion`; `status` adds `waitingForStems` (API only).
  Song fields: `lyrics { lyricsId?, title, language, text }`, `style?: StyleSnapshot`,
  `freeCaption?`, `voice { leadVoice, delivery? }`, `instrumental`, `bpm?`, `keyScale?`,
  `timeSignature?`, `durationSeconds`. Conversion fields: `sourceGenerationId`, `sourceTakeIndex`,
  `voiceProfileId?`, `voiceProfileName`, `pitchShift`, `autoShift`.
- `GenerationTake` adds `vocal`, `voiceProfileName?`, `vocalAudioUrl?`.
- `Track.origin` adds `vocal`, `voiceProfileName?`, `lyricsId?`.
- New: `LyricsSheet { id, title, language, text, createdAt, updatedAt }`, `VoiceProfile { id,
  displayName, consent { statementVersion, personName, consentedAt }, source, durationSeconds,
  voicedRatio, sampleUrl, createdAt, convertedTakeCount }`.
- `GET /api/generation/status` gains `voice { state: "notInstalled"|"unavailable"|"ready", reason? }`
  (singer service) next to the existing generator state; models add the 010 models;
  `downloadBytesNeeded` covers them.

## 3. UI test ids `U9` -> `U9+0.1`

| Screen | `data-testid` |
|---|---|
| Song page (`/songs/new`) | `song-lyrics-select`, `song-lyrics-text`, `song-lyrics-upload`, `song-lyrics-save`, `song-sections-propose`, `song-density-warning`, `song-title-input`, `song-language-select`, `song-style-select`, `song-free-caption`, `song-lead-voice`, `song-delivery-input`, `song-instrumental`, `song-bpm`, `song-key`, `song-duration` (`aria-valuenow` seconds), `song-takes`, `song-generate`, `song-error`, `song-personal-use-notice` |
| Lyrics list (`/lyrics`) | `lyrics-row-<lyricsId>`, `lyrics-edit-<lyricsId>`, `lyrics-delete-<lyricsId>`, `lyrics-new` |
| Voice profiles (`/voices`) | `voice-new`, `voice-record-start`, `voice-record-stop`, `voice-record-unavailable`, `voice-upload`, `voice-consent-text`, `voice-consent-checkbox`, `voice-person-name`, `voice-display-name`, `voice-save`, `voice-error`, `voice-row-<profileId>`, `voice-play-<profileId>`, `voice-consent-<profileId>`, `voice-delete-<profileId>`, `voice-delete-takes-<profileId>` (checkbox in the confirm dialog) |
| Take actions (all take lists) | `take-use-my-voice-<generationId>-<index>`, `take-voice-profile-select`, `take-voice-convert-start`, `take-vocal-badge-<generationId>-<index>` (attribute `data-vocal`) |
| Track detail | `track-lyrics-link` (generated tracks with lyrics) |

## 4. Help entries (004 framework, keys)

`help.lyrics.format` (markers, length, LRC), `help.lyrics.sections`, `help.song.options`,
`help.song.language`, `help.voice.profiles` (how to record, 20-30 s dry singing),
`help.voice.consent` (only your own voice; what is stored; deletion), `help.voice.conversion`
(limits, languages, quality), plus the 009 keys `help.generation.*`.
