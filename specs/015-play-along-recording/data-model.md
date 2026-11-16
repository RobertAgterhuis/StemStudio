# Data Model: Play-along Recording

Builds on the 001-014 data models, in particular 007 (assets, render jobs, mixing law) and 012
(speed, transpose, count-in). Wire shapes: [contracts/DELTA.md](contracts/DELTA.md) and
[contracts/recording-api.openapi.yaml](contracts/recording-api.openapi.yaml); engine types:
[contracts/audio-engine.delta.md](contracts/audio-engine.delta.md). Design: ADR-024.

Constants (binding; `ContractLimits` and engine `recording/limits.ts`):

| Name | Value |
|---|---|
| Sample rate | 44,100 Hz (the engine context and every take) |
| Take bit depth | 24-bit PCM upload, 24-bit FLAC stored |
| Min take length | 22,050 samples (0.5 s) |
| Max take length | track length (at most 15 min); upload <= 256 MiB |
| Takes per track | 200 |
| Passes per session | 50; capture per session <= 30 min |
| Partial pass kept | >= 44,100 samples (1 s) |
| Latency range | 0..44,100 samples (0-1000 ms) |
| Timing adjustment | -8,820..+8,820 samples (+/-200 ms), entered in 0.1 ms steps |
| Setup-change threshold | 220 samples (5 ms) of output latency |
| Take name | 1..80 characters, trimmed, no control characters |
| Device label | 0..200 characters (stored as given, control characters stripped) |
| Quiet take | peak < -50 dBFS (label only) |

Rounding rule (ms -> samples): `samples = sign(ms) * floor(abs(ms) * 44.1 + 0.5)`.

## 1. Configuration

| Where | Name | Default | Meaning |
|---|---|---|---|
| API | `StemStudio:Recording:MaxUploadBytes` | `268435456` (256 MiB) | Request body limit of the two take upload endpoints (endpoint metadata; the global upload limit is unchanged) |
| API | `StemStudio:MinFreeDiskBytes` | 1 GiB (007) | Also applies to take uploads (room for the padded WAV + FLAC) |
| Web | `StemStudio:Recording:TestLoopback` | `false` | E2E only: the engine's input is its own output (research R-13) |
| Web | `StemStudio:Recording:MaxStreamBytes` | `268435456` | `maxAllowedSize` of the JS stream reference |

No worker configuration changes.

## 2. Backend entities

### 2.1 RecordingTake (table `RecordingTakes`)

| Column / field | Type | Rule |
|---|---|---|
| `Id` | GUID | - |
| `TrackId` | GUID FK -> `Tracks` | cascade delete (FR-018) |
| `AssetId` | GUID FK -> `Assets` | the library item holding `audio.flac`; deleted with the take; unique |
| `Number` | int | per track, starts at 1, never reused (`max + 1`); unique `(TrackId, Number)` |
| `Name` | string 1..80 | default `"Take <n>"` or `"Take <n> - <Part label>"` |
| `Part` | enum `none`, `vocals`, `guitar`, `bass`, `piano`, `drums`, `other` | the stem compare mode mutes; label "Keys" for `piano` |
| `SessionId` | GUID | one Record press; all passes of a loop session share it |
| `PassIndex` | int? 1..50 | null when not loop-recorded |
| `Partial` | bool | last pass stopped early (>= 1 s) or device unplugged |
| `StartSample` | long >= 0 | song sample of the first take sample (after compensation and adjustment) |
| `LengthSamples` | long | 22,050..track length; `StartSample + LengthSamples <= TrackSamples + 4,410` |
| `InputMode` | enum `mono-1`, `mono-2`, `stereo` | - |
| `InputChannels` | int 1..2 | channels of the uploaded WAV (1 for mono modes) |
| `LatencySamples` | int 0..44,100 | `L + P` used at capture |
| `LatencyMethod` | enum `loopback`, `manual`, `estimated` | - |
| `CalibrationId` | GUID? FK -> `InputCalibrations` | set null when the calibration is deleted |
| `DeviceLabel` | string? <= 200 | - |
| `MixId` | GUID? FK -> `Mixes` | mix loaded while recording (info); set null on mix delete |
| `Favorite` | bool | the star |
| `PeakDbfs` | double | computed by the API from the upload, floored at -150 |
| `ClippedSamples` | int | samples with `abs(v) >= 2^23 - 1` (API count) |
| `Dropouts` | int | capture gaps reported by the engine (filled with silence) |
| `AdjustmentSamples` | int | total timing adjustment applied so far (US7), informational |
| `AlignmentRevision` | int >= 0 | +1 per applied timing adjustment |
| `RecordedAt` | UTC | from the engine (`Date.now()` at capture start) |
| `CreatedAt`, `UpdatedAt` | UTC | - |

Derived (not stored): `Status` = asset status mapped `queued`/`rendering` -> `saving`, `ready`
-> `ready`, `failed` -> `failed`; `Error` = asset error; `SizeBytes` = asset size;
`StartSeconds = StartSample / 44100`, `DurationSeconds = LengthSamples / 44100`;
`Quiet = PeakDbfs < -50`; `Clipped = ClippedSamples > 0`.

Indexes: `(TrackId, CreatedAt DESC)`, `(TrackId, SessionId)`, unique `(TrackId, Number)`,
unique `AssetId`.

**Rules**
- Create: track `completed` (else 409 `track-not-ready`); fewer than 200 takes (409
  `take-limit-reached`); free disk >= 1 GiB + 2 x upload size (409 `insufficient-disk-space`);
  speed 1 and transpose 0 (422 `recording-speed`); WAV valid (2.3); not silent (422
  `silent-take`); inside the track (422 `take-outside-track`); `lengthSamples` equals the WAV's
  frame count (422 `length-mismatch`).
- Delete: removes the asset row and `assets/<assetId>/` (007 `AssetFileStore.DeleteAll`),
  cancels a running render job (007 `DELETE /v1/render-jobs/{id}`), nulls `Mixes.TakeChannel`
  that refer to it, removes its audio-hash entries.
- Track delete: deletes every take as above (in the same unit of work as the 001 delete).

### 2.2 Take asset (table `Assets`, 007)

| Field | Value |
|---|---|
| `Kind` | `audio` |
| `Name` | the take name (kept in sync on rename) |
| `Provenance.origin` | `recording` (new `AssetOrigin` value) |
| `Provenance.trackId/trackTitle/trackArtist` | of the take's track |
| `Provenance.contents` | `"Take <n> - <Part label> (recording)"` |
| `Provenance.recipe` | `RecordingRecipe { kind: "recording", takeId, captureFile: "capture.wav" }` (new member of the 008 `oneOf`) |
| `Files` | `[{ role: audio, fileName: "audio.flac", contentType: "audio/flac" }]` when ready |
| `Audio` | `{ format: flac, bitDepth: 24, sampleRate: 44100, channels: 2, durationSeconds = (StartSample + LengthSamples) / 44100, peakDbfs, appliedGainDb: 0 }` |

`listAssets` excludes `origin = recording` unless the query has `origin=recording`
(additive filter `origin`). Rename, delete and retry of a take asset through the asset
endpoints are refused with 409 `managed-by-take` (the Takes panel owns them); takes use
`POST /api/takes/{takeId}/retry` (needs `capture.wav`).

### 2.3 Upload validation (`TakeWavReader`, Domain, pure)

Accepted: RIFF/WAVE, first `fmt ` chunk `audioFormat = 1` (PCM) or `0xFFFE` with PCM subformat,
`channels` 1 or 2, `sampleRate` 44,100, `bitsPerSample` 24, `blockAlign = 3 x channels`; optional
chunks before `data` are skipped (<= 64 KiB in total); `data` size a multiple of `blockAlign`
and <= `MaxUploadBytes`. Anything else -> 400 `invalid-wav` (unreadable) or 415
`unsupported-wav-format` (readable but another format; `detail` names the field).

Peak scan streams the `data` chunk once (signed 24-bit little-endian), computing `peak`,
`ClippedSamples` and "all zero".

### 2.4 Padded capture and timing adjustment

`capture.wav` = canonical header for `StartSample + LengthSamples` frames + `StartSample *
blockAlign` zero bytes + the uploaded `data` bytes. `PUT /api/takes/{takeId}/audio` (US7) carries
the engine's re-encoded span with the new `startSample` (the engine shifts by the adjustment:
positive = later); the API rewrites `capture.wav`, re-runs the render, increments
`AlignmentRevision`, adds the delta to `AdjustmentSamples`, keeps the previous `audio.flac` until
the new one is ready (status `saving` meanwhile; on failure the previous audio stays and the take
shows the error with "Try again").

### 2.5 InputCalibration (table `InputCalibrations`)

| Column | Type | Rule |
|---|---|---|
| `Id` | GUID | - |
| `DeviceLabel` | string 1..200 | unique (ordinal, trimmed) |
| `DeviceIdHint` | string? <= 512 | last `deviceId` seen |
| `LatencySamples` | int 0..44,100 | - |
| `Method` | enum `loopback`, `manual` | - |
| `SpreadSamples` | int? 0..44 | loopback only |
| `DetectedClicks` | int? 6..12 | loopback only (8 clicks by default) |
| `OutputLatencySamples` | int 0..44,100 | snapshot at measurement |
| `MeasuredAt` | UTC | - |

Upsert by `DeviceLabel` (`PUT /api/recording/calibrations`); delete by id.

### 2.6 Mix take channel (table `Mixes`, column `TakeChannel`, JSON, nullable)

`TakeChannelSettings { takeId: GUID, volume: 0..1 (default 1), pan: -1..1 (default 0), mute: bool,
solo: bool }`. On load, a `takeId` that no longer exists (or belongs to another track) is
ignored with no error (the mixer shows no take). `MixSettings` and `MixSettingsInput` (007) gain
the optional `take` field; duplicating a mix copies it.

### 2.7 Export additions (007 planner)

- `ChannelId` pattern adds the literal `take`:
  `^(take|(vocals|drums|bass|guitar|piano|other)(\.[a-z][a-z-]{0,31})?)$`.
- `CreateAudioExportRequest.takeId: uuid | null`.
- Raw mode: `channels: ["take"]` + `takeId` -> one input `assets/<assetId>/audio.flac` (gain 1,
  pan 0), `range` default = `[StartSample, StartSample + LengthSamples) / 44100` (whole take;
  an explicit range is intersected with it); `dither: false`. Raw mode with `take` and without
  `takeId` -> 422 `take-required`.
- Mix mode: `mix.take` (or the saved mix's `TakeChannel` when `mixId` is used) adds channel
  `take` to the gate evaluation as an audible-level channel with its volume/pan/mute/solo; with
  `channels` explicit, `take` may be listed (requires a take in the mix, else 422
  `take-required`). The take input path is `assets/<takeAssetId>/audio.flac`.
- Errors: take not found 404; take of another track 422 `take-not-in-track`; take not ready 409
  `take-not-ready`.
- `Provenance.sourceAssetIds` of the export includes the take's asset id; `contents` starts with
  the take name (`"Take 2 - Bass + drums + vocals, mixed"`).
- render-vectors (next minor, 1.1.0 unless taken): gate cases with `take` (no solo; take solo;
  take muted; take part muted by the frontend's compare translation; explicit selection with
  take; raw take with its range).

## 3. Engine state (audio-engine 2.5.0, browser only)

### 3.1 Recording state machine

```text
closed --openInput--> open --startRecording--> countIn --(012 count-in ends)--> recording
  ^                     ^                         |                               |
  |                     |                         +--stopRecording--> finishing <-+ (end of song,
  +----closeInput-------+<-------- takes reported (OnTakesCaptured) ---+            pass limit,
                                                                                    device lost)
```

`finishing` waits `L + P` plus one batch (<= 2,048 frames) after the stop time, so the
compensated tail is complete, then reports every captured take.

### 3.2 Capture mapping (binding for engine tests)

For a captured frame `f` (context frame) and compensation `C = latencySamples`:
`songSample(f) = transportSampleAt(f - C)` where `transportSampleAt` evaluates the transport
segment active at context frame `f - C` (segment = `{ whenFrame, offsetSample, loopStart,
loopEnd, pass }` from play start, loop pass boundaries and 012 count-in gaps; inside a count-in
gap it is undefined and the frame is dropped). A take is the maximal run of frames with the same
`pass` (or the whole run without a loop) whose `songSample >= 0` and `< trackSamples`; its
`startSample` = `songSample` of its first frame. With the loop on, a pass take is cut to
`[loopStart, loopEnd)`. Gaps in the worklet's frame stamps are filled with zeros and counted as
`dropouts`.

### 3.3 CapturedTake (engine -> .NET, `OnTakesCaptured`)

`{ captureKey, sessionId, passIndex | null, partial, startSample, lengthSamples, channels,
inputMode, latencySamples, latencyMethod, peakDbfs, clippedSamples, dropouts, recordedAt }`;
the audio stays in the engine until `releaseCapture(captureKey)`; `captureWav(captureKey)`
returns the WAV `Blob` for upload.

### 3.4 Calibration result

`{ ok, latencySamples | null, latencyMilliseconds | null, detectedClicks, spreadSamples | null,
outputLatencySamples, inputPeakDbfs, reason: noSignal | tooFewClicks | inconsistent | tooLong |
null, warnings: ("clipped")[] }`.

## 4. Web preferences (004 preferences store, frontend only)

`recording.deviceLabel`, `recording.inputMode`, `recording.part`, `recording.muteOriginal`
(default true), `recording.countInBars` (0-2, default 1), `recording.monitoring` (default false),
`recording.monitoringVolume` (0..1, default 0.7). No contract change.

## 5. Help topics (004 framework)

`recording`, `recording-permissions`, `recording-connect-vocals`, `recording-connect-guitar-bass`,
`recording-connect-keys`, `recording-connect-drums`, `recording-connect-acoustic`,
`recording-latency`, `recording-monitoring`, `recording-loop-takes`, `recording-compare`,
`recording-export`, `recording-speed`, `recording-timing` (Expert).

## 6. Feature catalogue entries (004 `FeatureCatalog`)

| Id | Min level | Help |
|---|---|---|
| `recording.input-mode` | Advanced | `recording` |
| `recording.monitoring-volume` | Advanced | `recording-monitoring` |
| `recording.manual-latency` | Advanced | `recording-latency` |
| `recording.loop` | Advanced | `recording-loop-takes` |
| `recording.take-pan` | Advanced | `recording-compare` |
| `recording.export-options` | Advanced | `recording-export` |
| `recording.latency-samples` | Expert | `recording-latency` |
| `recording.timing-adjust` | Expert | `recording-timing` |
| `recording.diagnostics` | Expert | `recording` (dropouts, clipped count, device id hint) |

Everything else in the panels is shown at every level (Beginner: guided calibration wizard,
record, input, meter, count-in, part, monitoring on/off, takes, compare).
