# Contract delta: feature 015 (play-along recording)

Feature 015 adds the `recording` tag to the backend API, small additive fields to the 007 export
and mix schemas, the recording and take-channel surface of the audio engine, UI test ids, help
topics and render-vector cases. **Worker API, generator API and the agent tool catalogue are
unchanged.** Design: ADR-024.

## Status (plan, 2026-11-16)

| Contract | Base (ROADMAP allocation) | 015 document | State |
|---|---|---|---|
| Backend API | 1.13.0 (014) | [recording-api.openapi.yaml](recording-api.openapi.yaml) `1.14.0-delta.1`: standalone, additive, Redocly 2.78.0 valid (0 errors; warnings: 7 `no-unused-components` for the merge fragments, 1 `operation-4xx-response`) | **binding for the wave**; merged into the full `backend-api.openapi.yaml` **1.14.0** in Phase 0 T002 |
| Audio engine | 2.4.0 (014; 2.3.0 from 012) | [audio-engine.delta.md](audio-engine.delta.md) **2.5.0** | names and shapes binding; `types.ts` in Phase 0 T004 |
| UI test ids | 1.13.0 (014) | [ui-test-ids.delta.md](ui-test-ids.delta.md) **1.14.0** | binding; full document in T002 |
| render-vectors | 1.0.0 (007) | gate cases with `take` -> **1.1.0** (or the next free minor if 012/014 bump it first) | T002 |
| Help content | 004 framework | 14 topic ids (data-model 5) | binding ids; text by frontend |
| Worker API | 1.8.0 (014) | unchanged (the 007 render job is reused as the FLAC writer; research R-08) | - |
| Generator API, agent tool catalogue | - | unchanged | - |
| C# | - | `src/StemStudio.Contracts/Recording/*`, additions in `Assets/*`, `Mixes/*`, `Tracks/*`, `ApiRoutes`, `ContractLimits` (T003) | Phase 0 |

On any difference the OpenAPI document wins for the backend, `types.ts` (after T004) for the
engine.

## 1. Backend endpoints (tag `recording`)

| Method + path | operationId | Request | Responses |
|---|---|---|---|
| `GET /api/tracks/{trackId}/takes` | `listRecordingTakes` | - | `200 RecordingTakeList { items (newest first), totalSizeBytes }`; `404` |
| `POST /api/tracks/{trackId}/takes` | `createRecordingTake` | multipart `audio` (24-bit PCM WAV 44.1 kHz 1-2 ch, <= 256 MiB) + `metadata` (`RecordingTakeUpload`) | `202 RecordingTake` (`saving`, `Location`); `400 invalid-wav`; `404`; `409 track-not-ready`/`take-limit-reached`/`insufficient-disk-space`; `413`; `415 unsupported-wav-format`; `422 silent-take`/`take-outside-track`/`length-mismatch`/`recording-speed`/validation; `503` worker unreachable |
| `POST /api/tracks/{trackId}/takes/delete` | `deleteRecordingTakes` | `{ takeIds: uuid[1..200] }` | `200 { deleted }` (unknown ids ignored; ids of other tracks ignored); `404` track; `422` |
| `GET /api/takes/{takeId}` | `getRecordingTake` | - | `200`; `404` |
| `PATCH /api/takes/{takeId}` | `updateRecordingTake` | `{ name?, favorite?, part? }` (>= 1 field) | `200`; `404`; `422` |
| `DELETE /api/takes/{takeId}` | `deleteRecordingTake` | - | `204`; `404` |
| `PUT /api/takes/{takeId}/audio` | `replaceRecordingTakeAudio` | multipart `audio` + `metadata` (`ReplaceTakeAudioMetadata { startSample, lengthSamples, adjustmentSamples }`) | `202`; `400`; `404`; `409 take-saving`/`insufficient-disk-space`; `413`; `415`; `422` |
| `POST /api/takes/{takeId}/retry` | `retryRecordingTake` | - | `202`; `404`; `409 take-not-failed`/`capture-missing`; `503` |
| `GET /api/recording/calibrations` | `listInputCalibrations` | - | `200 InputCalibration[]` |
| `PUT /api/recording/calibrations` | `upsertInputCalibration` | `InputCalibrationInput` (upsert by `deviceLabel`) | `200`; `422` |
| `DELETE /api/recording/calibrations/{calibrationId}` | `deleteInputCalibration` | - | `204`; `404` |

Take audio: existing `GET /api/assets/{assetId}/files/audio` (Range, ETag) via
`RecordingTake.audioUrl` (padded FLAC; the take starts at `startSample`).

## 2. Additions to existing backend schemas and operations

| Where | Change |
|---|---|
| `AssetOrigin` | adds `recording` |
| `Provenance.recipe` (`oneOf`) | adds `RecordingRecipe { kind: "recording", takeId, captureFile: "capture.wav" }` |
| `listAssets` | optional query `origin`; **without it, `recording` assets are excluded** (they did not exist before, so existing clients see no change) |
| `renameAsset`, `deleteAsset`, `retryAsset` | `409 managed-by-take` for `recording` assets |
| `ChannelId` pattern | adds the literal `take` (`^(take|(vocals|...)(\.[a-z][a-z-]{0,31})?)$`) |
| `CreateAudioExportRequest` | adds `takeId: uuid | null` (raw take export; default range = the take's span); errors `404`, `409 take-not-ready`, `422 take-not-in-track`, `422 take-required` |
| `MixSettings`, `MixSettingsInput` | add `take: TakeChannelSettings | null` (`{ takeId, volume, pan, mute, solo }`) |
| `Track` | adds `takeCount` (0..200) |
| `deleteTrack` | also deletes the track's takes (behaviour; response unchanged) |
| Problem `type` values | `invalid-wav`, `unsupported-wav-format`, `length-mismatch`, `silent-take`, `take-outside-track`, `take-limit-reached`, `recording-speed`, `take-not-ready`, `take-not-in-track`, `take-required`, `take-not-failed`, `take-saving`, `capture-missing`, `managed-by-take` |

The worker render job request built for a take is a normal 007 request (one input
`assets/<assetId>/capture.wav`, gain 1, pan 0, `masterGain` 1, FLAC 24-bit, `dither: false`);
the worker path allow-list already accepts it.

## 3. Audio engine 2.4.0 -> 2.5.0

See [audio-engine.delta.md](audio-engine.delta.md): `ChannelId` adds `take`, `MeterTarget` adds
`input`; methods `recordingSupport`, `listInputDevices`, `openInput`, `closeInput`,
`setMonitoring`, `calibrateLatency`, `startRecording`, `stopRecording`, `captureWav`,
`releaseCapture`, `loadTake`, `unloadTake`, `setTakeAdjustment`, `setTakeCompare`; callbacks
`OnRecordingStateChanged`, `OnRecordingProgress`, `OnTakesCaptured`, `OnInputLost`,
`OnInputDevicesChanged`; option `testInput: 'loopback'`; second build entry (capture worklet).

### Requests to 012 / 014 (internal engine hooks)

1. Transport segments (play start, loop pass start, end of each count-in gap) with context frame
   and song sample.
2. `processingLatencySamples()` of the tempo stage (0 when bypassed).
3. Extra channels (the take) go through the tempo stage like stems.
4. Count-in startable with an explicit bar count; "before every loop pass" honoured.

Phase 0 T004 checks the merged 012/014 engine and adds any missing hook inside the 2.5.0 work
(audio-engine T014), without changing their public API.

## 4. UI test ids 1.13.0 -> 1.14.0

See [ui-test-ids.delta.md](ui-test-ids.delta.md): recording panel (`rec-*`), calibration dialog
(`cal-*`), take strip (`*-take` in the stem patterns, `take-compare-*`, `take-adjust-*`), Takes
panel (`takes-*`, `rec-take-*-<takeId>`), export dialog presets, the track-delete note, shortcut
rows.

## 5. render-vectors 1.0.0 -> 1.1.0

New `gate` cases (inputs listed in mixer order, the take last): no solo with take; take solo
only; take muted; part stem muted (compare "My take" as sent by the frontend); explicit selection
`["take", "bass"]`; raw take with `expected.range` = the take span; take volume 0 dropped. The
generator `generate_render_vectors.py` gains a `take` input (path
`assets/<uuid>/audio.flac`).

## 6. Help topics (004 framework)

`recording`, `recording-permissions`, `recording-connect-vocals`,
`recording-connect-guitar-bass`, `recording-connect-keys`, `recording-connect-drums`,
`recording-connect-acoustic`, `recording-latency`, `recording-monitoring`,
`recording-loop-takes`, `recording-compare`, `recording-export`, `recording-speed`,
`recording-timing`. Feature catalogue entries: data-model.md 6.

## 7. Cross-feature notes

- **010**: take files are registered in the audio-hash index (`AudioHashes`), so a take is
  refused as a voice sample (`422 library-audio`); takes are never offered in the voice page.
- **011**: no agent tool for takes in 015 (a later catalogue minor may add list/delete/export,
  never recording); takes are never part of an LLM request.
- **013** (library care): takes are library assets with origin `recording` under
  `assets/<assetId>/` (plus a transient `capture.wav`); backup includes `RecordingTakes`,
  `InputCalibrations` and the files; the storage overview counts them per track as "recordings".
- **014** (shortcuts, practice log): 015 registers `R` (record/stop) and `C` (compare) in 014's
  shortcut registry when merged, else in `mixer-keys.js`; recording time is not logged as
  practice time in 015.
- **016** (singing feedback, drum trainer): reuse `openInput`, the input meter and the capture
  worklet; 016 adds its own analysis on the input node.
