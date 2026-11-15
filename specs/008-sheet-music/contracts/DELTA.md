# Contract delta: Sheet Music per Instrument

Changes on top of the 007 contract documents. The full documents are produced by the architect
in Phase 0 (T001-T006) on the versions current when 008 starts. Every change below is
**additive** -> next **minor** of each document. Audio-engine contract: unchanged.

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
