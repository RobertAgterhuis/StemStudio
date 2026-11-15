# Contract delta: Sheet Music per Instrument

Changes on top of the 007 contract documents. The full documents are produced by the architect
in Phase 0 (T001-T006) on the versions current when 008 starts. Every change below is
**additive** -> next **minor** of each document. Audio-engine contract: unchanged.

## Phase 0 outcome (2026-11-15, combined 006 + 008 Phase 0)

Final documents in this folder: `worker-api.openapi.yaml` **1.6.0** (= 006's 1.5.0 + transcription),
`backend-api.openapi.yaml` **1.7.0** (= 006's 1.6.0 + scores), `ui-test-ids.md` **1.7.0**,
`redocly.yaml` (valid, 6 warnings: the 4 known 4xx warnings plus the idempotent DELETEs of render
and transcription jobs). Engine contract: 006's 2.1.0 (unchanged by 008). The code (worker
`CONTRACT_VERSION = "1.6.0"`, Contracts DTOs = backend 1.7.0) follows these documents.
Refinements against the delta above:

- **Worker**: `TranscriptionJobRequest.{profile, quality (default accurate), input |
  drumInputs, range (RenderRange), grid, resolution (null = profile default), keyOverride,
  titles}`; pitched profiles need `input` only, `drums` needs `drumInputs` only (model validator
  -> 422). `DrumInputs` has five nullable paths, at least one set (`pieces()` keeps the order).
  `TempoGrid` = `oneOf` `BeatGridTempo {kind: beat-grid, bpm, beatsPerBar, beats, downbeats}` |
  `ManualTempo {kind: manual, bpm, beatsPerBar, firstDownbeatSeconds}` (discriminator `kind`);
  beat times may be **negative** after the API's shift (data-model 2.4 keeps earlier beats; 2.8's
  ">= 0" is superseded), strictly ascending, -900..900. `KeyName` = 30 music21 names
  ("C major" ... "A- minor"). `TranscriptionJob.status` reuses `RenderJobStatus`; `stage` is null
  while queued and after the end. Result: exactly three `TranscriptionFile`s in the order pdf,
  musicxml, midi with fixed names/media types (`SCORE_FILES`). `ScoreSummary.notes >= 1`.
  New warning code **`missing-drum-piece`** ("No {piece} stem was available; that part is
  empty.", data-model 4.4). Texts: `SCORE_WARNING_TEXTS`, `NO_NOTES_TEXT`,
  `MUSIC_FONT_MISSING_TEXT`, `PIANO_ATTRIBUTION`. File route 409 type `transcription-not-completed`.
- **Health**: `transcription {models: {basic-pitch-icassp2022, piano-crnn-note-pedal: ready |
  not-downloaded}, font: {leipzig}}` and `lanes.transcribe` are required; Python defaults are
  "not-downloaded" / false / idle.
- **Settings**: `WorkerSettings.transcription = TranscriptionSettings(transcriber, threads)` from
  `STEMSTUDIO_TRANSCRIBER` (unset = `fake` with `STEMSTUDIO_SEPARATOR=fake`, else `real`) and
  `STEMSTUDIO_TRANSCRIBE_THREADS` (1..64, default 8).
- **Ports**: `NoteEvent`, `DrumHit`, `Transcription(notes, device)`, `PitchTranscriber.{engine,
  attribution, transcribe(audio_path, window, progress)}`, `DrumTranscriber.transcribe(inputs,
  window, progress)`, `NotationSettings`, `ScoreDocument`, `Notator.{notate, notate_drums}`,
  `EngravedScore`, `ScoreEngraver.engrave(musicxml, title, subject)`, `TranscriptionProgress`
  (= `RenderProgress`), errors `TranscriptionError` > `NoNotesError`,
  `TranscriptionCancelledError`; `PIANO_GPU_NEED` (1280 MiB), `BASIC_PITCH_WEIGHTS`,
  `PIANO_WEIGHTS`. `TempoGrid` stays the schema union (no second dataclass).
- **Backend**: `GET /api/scores/options?trackId&channel | ?assetId` -> `ScoreOptions` (adds
  `source` and `durationSeconds`); `POST /api/assets/scores` -> 202 `Asset` (409 `ScoreConflict`:
  track-not-ready, drums-not-split, insufficient-disk-space; 422 `ScoreUnprocessable`:
  validation, tempo-required). The manual tempo is `ScoreTempo` (DELTA's `ManualTempoDto` is
  `ScoreTempoDto`), the range `ScoreRange` (>= 2 s, `RangeDto` in .NET). **Provenance**: instead of
  turning `recipe` into a `oneOf`, score items set the new `Provenance.scoreRecipe`
  (`ScoreRecipe`, `kind: score`) and `recipe: null`; `ProvenanceDto.Recipe` is therefore nullable
  (additive for every 007 writer; 007 readers null-check). `Asset.score` = `ScoreFacts` (summary +
  `device`). `getAssetFile` keeps the 1.5.0 semantics for every role: attachment by default,
  `inline=true` for the PDF preview (the delta's "PDF inline by default" is dropped to keep one
  rule).
- **.NET**: `Contracts/Scores/{CreateScoreRequest, ScoreOptionsDto, ScoreFactsDto, ScoreRecipeDto,
  KeyInfoDto, KeyModeDto, KeySourceDto, ScoreWarningDto, ScoreWarningCodeDto, ScoreProfileDto,
  ScoreQualityDto, ScoreResolutionDto, ScoreEngineDto, GridStatusDto, ScoreTempoDto,
  ScoreEstimateDto, ProfileLimitDto}`, `AssetDto.Score` (`ScoreFactsDto?`),
  `AssetFileRoleDto.{Pdf, MusicXml, Midi}`, `AssetOriginDto.Transcription`,
  `ApiRoutes.{Scores, ScoreOptions, ScoreOptionsForChannel, ScoreOptionsForAsset, AssetFilePdf,
  AssetFileMusicXml, AssetFileMidi}`, `ContractLimits.{MinScoreRangeSeconds, ScoreProfiles,
  ScoreResolutions, KeyNames}`; the two operation ids are staged in `PendingScoreOperations`.
- **Font**: `worker/scripts/install_music_font.py <out_dir> [--css]` writes `Leipzig.ttf`
  (TrueType outlines; data-model 1 updated) and prints its SHA-256; needs `fontTools[woff]`
  (brotli) in the build stage only.

## 1. Worker API (`worker-api.openapi.yaml`, minor bump)

New tag `transcription`.

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| POST | `/v1/transcription-jobs` | `createTranscriptionJob` | `TranscriptionJobRequest` (data-model 2.8) | `202 TranscriptionJob`; `400 invalid-input-path`; `404 input-not-found`; `422` validation (profile/inputs mismatch, grid, range, key) |
| GET | `/v1/transcription-jobs/{transcriptionJobId}` | `getTranscriptionJob` | - | `200 TranscriptionJob`; `404` |
| GET | `/v1/transcription-jobs/{transcriptionJobId}/files/{role}` | `getTranscriptionFile` | `role` = `pdf`\|`musicxml`\|`midi` | `200` binary; `409 transcription-not-completed`; `404` |
| DELETE | `/v1/transcription-jobs/{transcriptionJobId}` | `deleteTranscriptionJob` | - | `204` (cancel + remove, idempotent) |

Schemas (new): `ScoreProfile`, `ScoreQuality`, `ScoreResolution`, `TempoGrid` (`oneOf`
`BeatGridTempo` / `ManualTempo`), `DrumInputs`, `ScoreTitles`, `TranscriptionJobRequest`,
`TranscriptionStage`, `TranscriptionJob`, `TranscriptionResult`, `ScoreSummary`, `KeyInfo`,
`ScoreWarning`. `TranscriptionJob.device` reuses `JobDevice` (003).

`Health` (additive): `lanes.transcribe {queued, running}`; `transcription { models:
{ "basic-pitch-icassp2022": "ready"|"not-downloaded", "piano-crnn-note-pedal": ... }, font:
{ leipzig: boolean } }`.

Examples: a bass request with the beat grid, a drums request with `drumInputs`, a piano request
with a manual tempo and a range, a running job in `transcribing`, a completed job with three files
and warnings, a failed "No notes were detected in this audio.".

## 2. Backend API (`backend-api.openapi.yaml`, minor bump)

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| GET | `/api/scores/options` | `getScoreOptions` | query: `trackId` + `channel`, or `assetId` | `200 ScoreOptions` (data-model 4.5); `404`; `409 track-not-ready` |
| POST | `/api/assets/scores` | `createScore` | `CreateScoreRequest` | `202 Asset` (kind `score`); `404`; `409 track-not-ready`, `409 drums-not-split`, `409 insufficient-disk-space`; `422` validation, `422 tempo-required` |

`CreateScoreRequest`:

```yaml
source: AudioSource            # 007: {trackId, channel} | {assetId}
profile: vocals|bass|guitar|keys|piano|drums | null
quality: accurate|fast | null
resolution: sixteenth|eighth | null
keyOverride: string | null     # e.g. "E minor", "B- major" (music21 names, enum in the schema)
tempo: { bpm, beatsPerBar, firstDownbeatSeconds } | null
range: { startSeconds, endSeconds } | null
name: string | null
```

`ScoreOptions`: `suggestedProfile`, `allowedProfiles[]`, `drumsRequireSplit` (bool),
`gridStatus` (`reliable`|`unreliable`|`none`), `defaultQuality`, `estimates[{profile, quality,
seconds}]`, `limits[{profile, text}]` (the quality note per profile, data-model 2.1/2.7),
`attribution` (piano model text).

`Asset` (additive): `score: ScoreFacts | null` (data-model 4.1); `files[].role` gains `pdf`,
`musicxml`, `midi`; `provenance.origin` gains `transcription`. `listAssets` filter `kind=score`.
`getAssetFile` serves the new roles (`application/pdf` inline by default for the preview,
`?inline=false` for download; MusicXML and MIDI as attachments).

`StemStudio.Contracts` (Phase 0): `Scores/{CreateScoreRequest, ScoreOptionsDto, ScoreFactsDto,
KeyInfoDto, ScoreWarningDto, ScoreProfileDto, ScoreQualityDto, ScoreResolutionDto,
ManualTempoDto}.cs`, `AssetDto.Score`, `ApiRoutes.Scores`, `ContractLimits` additions.

## 3. Audio engine

Unchanged.

## 4. UI test ids (`ui-test-ids.md`, minor bump)

| Screen | New `data-testid` |
|---|---|
| Track detail | `score-stem-<stem>`, `score-detail-<parent>-<detail>` (pitched detail stems only) |
| Library | `asset-score-<assetId>` (row action "Sheet music" on audio items), `asset-preview-<assetId>` (PDF viewer, score items), `asset-download-pdf-<assetId>`, `asset-download-musicxml-<assetId>`, `asset-download-midi-<assetId>`, `asset-score-key-<assetId>` (text e.g. "A-flat major (83 %)"), `asset-score-tempo-<assetId>`, `asset-score-warnings-<assetId>`, `asset-score-limits-<assetId>`, `asset-attribution-<assetId>` (piano items) |
| Sheet music dialog | `score-dialog`, `score-profile`, `score-quality` (piano), `score-estimate` (text with seconds/minutes), `score-limits` (profile quality note), `score-resolution`, `score-key-override`, `score-tempo-bpm`, `score-tempo-beats-per-bar`, `score-tempo-first-downbeat`, `score-tempo-required` (notice), `score-range-start`, `score-range-end`, `score-drums-split-required` (notice) with `score-drums-split-start` (button), `score-submit`, `score-cancel`, `score-error`, `score-saved`, `score-saved-open` |

## 5. In-app help (004 framework)

Topics: `sheet-music` (what you get, practice lead sheet, files), `sheet-music-profiles` (one
paragraph per profile with its limits), `sheet-music-key-tempo` (grid, key confidence, manual
tempo; Advanced), `sheet-music-drums` (detail stems needed, notation legend), `sheet-music-piano`
(accurate vs fast, CPU time, CC BY 4.0 attribution). Levels: profile + simplified = Beginner;
quality, key override, manual tempo, range = Advanced.

## Reconciliation (architect, 2026-11, after the 008 implementation wave)

- Backend 1.7.0 `createScore` declares `503 WorkerUnavailable` (worker unreachable at creation;
  nothing stored) and `ScoreConflict` lists `source-unavailable` (worker 400/404 at creation),
  as the 007 exports do. The 006 lyrics reconciliation is mirrored unchanged (see the 006 DELTA).
- Worker 1.6.0: `BeatGridTempo.downbeats` description notes bar 1 with more than one bar of
  lead-in. data-model 2.3 (chords per staff cut at the next onset; drum gate on the linear onset
  peak), 2.4 (pickup longer than a bar keeps the meter phase), 2.8 (beats may be negative) and
  the Leipzig font facts (119,356 B, SHA-256 `773c72dc...584a4b`, deterministic) updated.
