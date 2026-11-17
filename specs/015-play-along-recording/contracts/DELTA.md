# Contract delta: feature 015 (play-along recording)

Feature 015 adds the `recording` tag to the backend API, small additive fields to the 007 export
and mix schemas, the recording and take-channel surface of the audio engine, UI test ids, help
topics and render-vector cases. **Worker API, generator API and the agent tool catalogue are
unchanged.** Design: ADR-024.

## Status (Phase 0, 2026-11-17)

Phase 0 ran on `main` e8d6a5d8 (001-013 merged). 014 (backend 1.13.0, engine 2.4.0, UI ids
1.13.0) and 022/023 (1.22.0/1.23.0) are **not** on `main` yet, so every full document below is
**`main`'s newest full document + the 015 delta**; it is rebuilt in feature order once they land
(section "Re-merge"). The labels stay 015's allocated ones (ROADMAP "Full-document labels after an
out-of-order merge").

| Contract | Base used now | 015 document | State |
|---|---|---|---|
| Backend API | 1.12.0 (013, `specs/013-library-care/contracts/backend-api.openapi.yaml`) | **[backend-api.openapi.yaml](backend-api.openapi.yaml) 1.14.0** = 1.12.0 + [recording-api.openapi.yaml](recording-api.openapi.yaml) `1.14.0-delta.2`, built by [merge_recording_delta.py](merge_recording_delta.py); [check_superset.py](check_superset.py) passes against 1.12.0 and against the delta; Redocly 2.78.0: 0 errors (12 `operation-4xx-response` warnings, all inherited except `listInputCalibrations`) | **binding**; `x-stemstudio-contains` `[..., 1.12.0, 1.14.0]` |
| Audio engine | 2.3.0 (012) | **[audio-engine.md](audio-engine.md) 2.5.0** = 2.3.0 + [audio-engine.delta.md](audio-engine.delta.md); `types.ts` `EngineContractVersion = '2.5.0'` | **binding** (`types.ts` wins) |
| UI test ids | 1.12.0 (013) | **[ui-test-ids.md](ui-test-ids.md) 1.14.0** = 1.12.0 + [ui-test-ids.delta.md](ui-test-ids.delta.md) (level rule numbered R10 until the re-merge) | **binding** |
| render-vectors | 1.0.0 (007) | **[render-vectors.json](render-vectors.json) 1.1.0** = 1.0.0 unchanged + 8 take cases, from [generate_render_vectors.py](generate_render_vectors.py) (`--check`) | binding; consumers switch from 007's file when they implement the take (engine T018/T019, backend US6) |
| Help content | 004 framework | 14 topic ids (data-model 5) | binding ids; text by frontend |
| Worker API | 1.7.0 on `main` (014 makes it 1.8.0) | unchanged (the 007 render job is the FLAC writer; research R-08) | - |
| Generator API, agent tool catalogue | - | unchanged (`ChannelId` unchanged, so no agent tool accepts `take`) | - |
| C# | - | `src/StemStudio.Contracts/Recording/*` (DTOs, `RecordingLimits`, `RecordingProblemTypes`, `ApiRoutes.Recording`), additions `AssetOriginDto.Recording`, `ProvenanceDto.Recording`, `CreateAudioExportRequest.TakeId`, `MixSettingsDto.Take`, `TrackDto.TakeCount`, `ContractLimits.ExportChannelIdPattern` | done (T003) |

On any difference the full OpenAPI document wins for the backend, `types.ts` for the engine.

## Phase 0 decisions (changes against the plan-time delta)

| # | Decision | Why |
|---|---|---|
| P0-1 | The take's recipe is **`Provenance.recording`** (`RecordingRecipe` or null, absent otherwise), not a third `Provenance.recipe` variant | same pattern as 1.8.0 `generation`; `ProvenanceDto.Recipe` stays the typed `ExportRecipeDto` for every reader |
| P0-2 | **`ChannelId` is unchanged**; the export channel lists (`CreateAudioExportRequest.channels`, `ExportRecipe.channels`) reference the new **`ExportChannelId`** (`ChannelId` or `take`); C# `ContractLimits.ExportChannelIdPattern` | `ChannelId` is also used by scores (`getScoreOptions`, `AudioSource`) and agent tools; widening it would let `take` reach them (the agent catalogue must stay unchanged) |
| P0-3 | `MixSettings.take`, `CreateAudioExportRequest.takeId`, `Provenance.recording` are **absent when null**, `Track.takeCount` **absent when 0** | every existing example, client and test stays byte-identical; there is no `MixSettingsInput` schema (MixSettings is request and response) |
| P0-4 | **Write gate** (1.12.0) on `deleteRecordingTake`, `deleteRecordingTakes`, `replaceRecordingTakeAudio` (`409 backup-running`, `503 library-maintenance`); upload, update, retry and calibrations are not gated (like 013's creates) | 013 gates operations that delete or replace library files |
| P0-5 | Delta responses that collide with existing names are `Take*` in the full document (`TakeConflict`, `TakeBadRequest`, `TakePayloadTooLarge`, `TakeUnsupportedMediaType`, `TakeUnprocessable`, `TakeWorkerUnavailable`); `NotFound`, `ProblemDetails`, `ValidationProblemDetails` keep 1.12.0's; new `ManagedByTake` (409 on rename/delete of a take's asset; retry documents it in its 409) | one meaning per component name |
| P0-6 | Engine: `RecordingSupport.reasonCode` (`insecureContext`, `noMediaDevices`, `noAudioWorklet`, `rateMismatch`) next to the readable `reason` | stable value for `rec-unavailable` `data-reason` and the help topic |
| P0-7 | Recording only in a secure context (localhost/127.0.0.1 on the StemStudio computer); a LAN address over http reports `insecureContext` | measured: no `mediaDevices` and no `audioWorklet` there (audio-engine.md "Browser constraints"); FR-017 |
| P0-8 | Mapping uses the **heard** transport anchors, which contain the tempo-stage latency `P`: compensation is `L` only; `CapturedTake.latencySamples` reports `L + P` | avoids applying `P` twice (audio-engine.md "Phase 0 outcome") |

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
| `Provenance` | adds optional `recording: RecordingRecipe { kind: "recording", takeId, captureFile: "capture.wav" }` (P0-1) |
| `listAssets` | optional query `origin`; **without it, `recording` assets are excluded** (they did not exist before, so existing clients see no change) |
| `renameAsset`, `deleteAsset`, `retryAsset` | `409 managed-by-take` for `recording` assets |
| `ExportChannelId` (new) | `^(take|(vocals|...)(\.[a-z][a-z-]{0,31})?)$`, used by `CreateAudioExportRequest.channels` and `ExportRecipe.channels`; `ChannelId` unchanged (P0-2) |
| `CreateAudioExportRequest` | adds `takeId: uuid | null` (raw take export; default range = the take's span); errors `404`, `409 take-not-ready`, `422 take-not-in-track`, `422 take-required` |
| `MixSettings` (request and response) | adds optional `take: TakeChannelSettings | null` (`{ takeId, volume, pan, mute, solo }`) |
| `Track` | adds optional `takeCount` (0..200, absent = 0) |
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
- **013 implementation notes** (backend lane, task T025a): stored provenance origin value
  `Recording` (enums by name; `tests/fixtures/backup/recording-asset.json` already uses it, no
  change); `BackupScrubber` with `recordings=false` also deletes the `RecordingTakes` rows of the
  removed assets and clears `Mixes.TakeChannel` that point at them; `InputCalibrations` travel with
  the `database` category; the merge restore (`MergePlanner`) adds `RecordingTakes` only when their
  asset was added and `InputCalibrations` by `DeviceLabel` (the live row wins on a clash); backups
  select only `audio.flac` of ready takes, never the transient `capture.wav`; the storage overview
  already counts origin `recording` as `recordings`; the three gated endpoints use
  `RequireGateOpen`, the asset-ready hook that deletes `capture.wav` touches no backed-up file, and
  the track-delete cascade runs inside the already gated `deleteTrack`. `KnownSchema` reads the new
  migration's tables automatically.
- **022** (library folders, lands on `main` before the wave): `LibraryFilingRules.IsFileable` must
  return false for origin `recording` (today it is `Enum.IsDefined`, which would file takes once the
  enum member exists), and `LibraryRepository.CountPendingAsync` / `PendingAsync` must exclude
  `recording` assets in SQL, else the backfill counts takes as pending forever (022 backend note;
  checked on `r22-lane-be022`). Task T025a.

## Re-merge (after 022/023 and 014 land on `main`)

The round branch merges `main` (with 022/023) first, then the 014 design head; the 015 full
documents are then rebuilt **in feature order** so every label contains exactly its deltas:

1. **Backend**: base = 014's full `specs/014-practice-flow/contracts/backend-api.openapi.yaml`
   (1.13.0 = newest `main` document + 014). Run
   `python -I specs/015-play-along-recording/contracts/merge_recording_delta.py specs/014-practice-flow/contracts/backend-api.openapi.yaml`
   (text-based; fails on a missing or ambiguous anchor: `AssetOrigin`, `Provenance`, `Track`,
   `MixSettings`, `CreateAudioExportRequest`, `ExportRecipe`, `listAssets`, `renameAsset`,
   `deleteAsset`, `retryAsset`, the `health` tag). Then `check_superset.py <014 full>
   backend-api.openapi.yaml` and with the delta (set `OLDER` to 014's document), Redocly, and in
   the tests: the `Contract/013` link of `StemStudio.Api.Tests.csproj` becomes the 014 document
   (`Backend013` -> the 1.13.0 document), `x-stemstudio-contains` in `RecordingContractTests`
   becomes the merged list in merge order (`..., 1.12.0, 1.22.0, 1.23.0, 1.13.0, 1.14.0`), and
   `Changed1120Entries` gains whatever 014 itself changed in the same schemas (none expected).
2. **Engine**: `types.ts` conflicts only in `EngineContractVersion` (take `'2.5.0'`) and the
   `DotNetCallbacks` union (keep 014's `OnLoopPass` and 015's five); `engine.ts` keeps both method
   groups. Rebuild `audio-engine.md` as 014's 2.4.0 document + this document's "Play-along
   recording" section and 2.5.0 changelog paragraph unchanged ("2.3.0" -> "2.4.0" in the
   merge-order note). The recorder does not use `OnLoopPass` (it reads the internal segment log).
3. **UI test ids**: 014's full 1.13.0 + `ui-test-ids.delta.md` exactly as in T002 (changelog line
   after 014's, the 015 section after 014's); R10 renumbered after 014's level rules;
   `RecordingUiTestIdsTests` reads the 014 document as `Base`.
4. **render-vectors**: unchanged (ROADMAP allocates 1.1.0 to 015; 014 does not touch it).
5. Re-run the Phase 0 checks: contract tests, engine tests, Redocly, both superset runs, vectors
   `--check`, `dotnet build` 0 warnings.
