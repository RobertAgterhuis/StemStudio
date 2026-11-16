# Implementation Plan: Play-along Recording

**Branch**: `015-play-along-recording` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/015-play-along-recording/spec.md`; constitution 1.3.1;
MoSCoW S3; ROADMAP allocation (backend-api 1.14.0, audio-engine 2.5.0, ui-test-ids 1.14.0);
007 (asset library, render jobs, mixing law), 010 (browser recording, consent, audio-hash
guard), 012 (practice tempo, count-in; written in parallel); ADR-005, ADR-010, ADR-015,
ADR-016, ADR-019; this feature's [research.md](research.md) and ADR-024.

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). 015 is built in the
{014, 015} wave after {012, 013} are merged. Contract documents are deltas on top of 014's
versions; Phase 0 merges them into full documents when 014's are fixed
([contracts/DELTA.md](contracts/DELTA.md)).

## Summary

1. **Capture in the engine** (audio-engine 2.5.0): `getUserMedia` with all voice processing off
   -> `MediaStreamAudioSourceNode` -> capture `AudioWorkletNode` in the engine's own 44.1 kHz
   context; every captured block carries its context frame, so it maps exactly onto the
   transport (ADR-024 D1).
2. **Latency**: a loopback click test measures the round trip `L` (8 seeded chirps, normalised
   cross-correlation, median, 6-of-8 and 1 ms spread rule); manual value and browser estimate
   as fallbacks; stored per device label in the backend. Each take is cut so that its first
   sample is song sample `pos(f - L - P)` (D2). Recording only at 100 % / 0 st (D3).
3. **Takes**: the engine reports captured takes (one per loop pass, D6), plays them immediately
   from memory in a new `take` channel strip (gate, compare, D5), and encodes 24-bit WAV; Blazor
   streams it (JS stream interop) to the API.
4. **Storage** (backend 1.14.0): the API validates and pads the WAV to song sample 0, creates an
   Asset (origin `recording`) and a normal 007 render job that writes a bit-exact 24-bit FLAC
   (no worker change, D4); `RecordingTakes` holds the take metadata; `InputCalibrations` the
   latencies.
5. **Export**: "take only" and "take + mix" are ordinary 007 exports with one extra input
   (`ChannelId` `take`, `takeId`, `MixSettings.take`).
6. **UI** (frontend): recording panel (permission flow, device, mode, meter, part, mute
   original, count-in, monitoring, latency status, Record/Stop), calibration dialog, take strip
   with compare, Takes panel, export presets, help topics per instrument family, levels,
   shortcuts, a11y.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (API, Domain, Persistence, Web), TypeScript 6.0 (engine,
Vite 8, Vitest 5), Python 3.12 (worker, regression test only).

**Primary Dependencies (new)**: **none**. Web Audio `AudioWorklet`, `MediaDevices`,
`Permissions` and Blazor's `IJSStreamReference` are platform APIs; FLAC is written by the
worker's existing libsndfile path (007).

**Storage**: tables `RecordingTakes`, `InputCalibrations`; column `Mixes.TakeChannel` (JSON);
asset origin `recording`; migration `AddRecordingTakes`; files
`api-data/assets/<assetId>/audio.flac` (+ transient `capture.wav`).

**Testing**: xUnit v3 + coverlet (Domain: WAV reader, padding, take rules, ms/samples rounding,
calibration rules; Api: endpoints with a stub worker handler, multipart streaming, track-delete
cascade, export planner with `take`; Persistence: migration and JSON column); bUnit (panel,
dialog, take strip, Takes panel, permission states, levels, live region) with JS-interop mocks;
Vitest (capture mapping, latency estimator, WAV encoder, slicing, compare gate, state machine,
worklet processor) with the extended `AudioContext` mock; worker pytest regression (24-bit mono
WAV -> 24-bit FLAC bit-exact through the render job, no contract change); Playwright in the
Chromium full build with the fake capture device (one case per instrument family), digital
loopback alignment (SC-001), permission denied, network privacy assertion (SC-007), a11y and
visual baselines.

**Target Platform**: as before (Docker Desktop on Windows 11, reference machine); recording in
Chromium-based browsers on localhost.

**Performance Goals**: SC-001..SC-005 (alignment <= 2 samples digital, <= 1 ms cable;
calibration <= 10 s; playable <= 0.5 s after Stop; 5-min stereo take stored <= 15 s; 10 min
recording without dropouts).

**Constraints**: recordings never leave the machine; no worker contract change; no new
dependency; additive contracts only; record only at 100 % / 0 st.

**Scale/Scope**: 200 takes per track, 50 passes or 30 min per session, takes up to 15 min.

## Constitution Check

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % | Pure units for every algorithm (estimator, mapping, encoder, reader, padder, gate); fakes for the microphone, worklet, worker and JS interop; Playwright with a fake device and a digital loopback; failing test first per task | PASS |
| II | SIG 4 stars | Engine folder `src/recording/` with small modules (capture, mapping, calibration, take-channel, wav); backend `Recording/` folders per layer; units <= 15 LOC; parameter objects (`RecordOptions`, `TakeUploadCommand`) | PASS |
| III | Contracts first | backend delta 1.14.0 (Redocly valid), engine 2.5.0 types, UI ids 1.14.0, render-vectors 1.1.0, C# DTOs in Phase 0; ADR-024 | PASS |
| IV | Open source, lawful use | No new dependency; recordings local only (FR-017, SC-007 network assertion); takes excluded from voice samples (010 guard) and from the AI agent | PASS |
| V | Simplicity and honesty | Reuse of the 007 renderer as FLAC writer; one take strip; record at 100 % only; limits (two input channels, Firefox, hardware gain, acoustic loopback accuracy) stated in help; all timing on `AudioContext` frames (no timers for audio) | PASS |
| - | Tech & security | WAV parsed with bounds checks, streamed (no full buffering), size-limited per endpoint; no path from user input (asset ids are server GUIDs); CORS stays GET-only; mic opened only on demand and released | PASS |
| - | Workflow | Owners: architect, audio-engine, backend, frontend, qa, devops, quality-gate, stem-separation (regression test only), CTO; hand-offs per checkpoint | PASS |

**Post-design re-check**: PASS (no deviation needs justification beyond Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/015-play-along-recording/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                     # what changes in every contract
    ├── recording-api.openapi.yaml   # backend 1.14.0-delta.1 (standalone, Redocly valid)
    ├── audio-engine.delta.md        # engine 2.5.0
    ├── ui-test-ids.delta.md         # UI ids 1.14.0
    └── redocly.yaml
docs/decisions/ADR-024-play-along-recording-and-latency.md
```

### Source Code (additions)

```text
src/audio-engine/src/recording/                                                   (audio-engine)
├── input.ts            # support check, devices, openInput/closeInput, permission, devicechange, track ended
├── capture-worklet.ts  # AudioWorkletProcessor 'stemstudio-capture' (second Vite entry)
├── capture.ts          # receives stamped blocks, gap fill, ring storage, limits
├── mapping.ts          # mapCaptureToTakes (data-model 3.2), pure
├── calibration.ts      # click template, seeded schedule, estimateLatency (research R-04), pure core
├── recorder.ts         # state machine, count-in via 012, speed checks, finishing tail
├── wav.ts              # encodeWav24, pure
├── take-channel.ts     # loadTake/slice, scheduling with the transport anchor, adjustment
├── compare.ts          # compareGate, pure
├── monitoring.ts
└── limits.ts
src/audio-engine/src/{types.ts, engine.ts, gate.ts, mixer.ts, callbacks.ts}        (types: architect; rest: audio-engine)

src/StemStudio.Contracts/Recording/*.cs, Assets/*, Mixes/*, Tracks/* additions    (architect)
src/StemStudio.Domain/Recording/{RecordingTake,TakeRules,TakeWavReader,TakeWavPadder,
    LatencyConversion,InputCalibration,TakeNaming}.cs                             (backend)
src/StemStudio.Domain/Rendering/* (planner: channel 'take', raw take range)        (backend)
src/StemStudio.Persistence/{Configurations/RecordingTakeConfiguration.cs,
    Migrations/*_AddRecordingTakes.cs}                                            (backend)
src/StemStudio.Api/Recording/{TakeEndpoints,CalibrationEndpoints,TakeUploadService,
    TakeStorage,TakeAssetFinisher,TakeDeletion}.cs                                (backend)
src/StemStudio.Api/{Assets,Mixes,Tracks}/* additions                               (backend)
src/StemStudio.Web/Components/Recording/{RecordingPanel,PermissionGate,InputSelector,
    CalibrationDialog,TakeStrip,TakeCompare,TakesPanel,TakeRow,TakeAdjust}.razor(.cs) (frontend)
src/StemStudio.Web/Recording/{RecordingInterop,TakeUploader,RecordingPreferences}.cs (frontend)
src/StemStudio.Web/Help/topics/recording/*.md                                      (frontend)
src/StemStudio.Web/Experience/FeatureCatalog.cs (entries)                          (frontend)
worker/tests/render/test_take_conversion.py                                       (stem-separation)
tests/fixtures/recording/*  + generator script                                     (qa)
e2e/tests/recording*.spec.ts, e2e/pages/recording-panel.ts, e2e/support/fake-capture.ts (qa)
e2e/playwright.config.ts project 'recording' (Chromium channel), CI job wiring     (qa / devops)
```

**Dependency direction**: `Web.Recording -> Web.Api client -> API`; `Api.Recording ->
Domain.Recording`, `Api.Recording -> Api.Assets` (create asset, render job, file store) one way;
`Api.Assets` does not know `Recording` except through an `IAssetOriginPolicy` registered by
Recording (managed-by-take refusals, listing filter) to keep the direction one-way;
`Api.Tracks` deletion calls `ITrackTakesCleanup` (interface in Api.Tracks, implemented in
Api.Recording). Engine: `recording/*` depends on `transport`, `mixer`, `gate`; nothing depends on
`recording/*` except `engine.ts`.

### Ownership map

| Owner | Paths (write) |
|---|---|
| architect | `specs/015-*/**`, ADR-024, `src/audio-engine/src/types.ts`, `src/StemStudio.Contracts/**` (Recording + additions), skeleton stubs |
| audio-engine | `src/audio-engine/**` except `types.ts` |
| backend | `src/StemStudio.{Domain,Persistence,Api}/**`, `tests/StemStudio.{Domain,Persistence,Api}.Tests/**` |
| frontend | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` |
| stem-separation | `worker/tests/render/test_take_conversion.py` (test only) |
| qa | `e2e/**`, `tests/fixtures/recording/**`, `docs/testing/TEST-STRATEGY.md` section 19 |
| devops | `.github/workflows/**`, `docker-compose.e2e*.yml` overrides, `scripts/test-all.*` |
| quality-gate | `tools/quality/**`, `docs/quality/**` |
| CTO | `docs/public/**`, `docs/STATUS.md`, `docs/THIRD-PARTY-LICENSES.md` (no change expected) |

## Key flows

1. **Open the panel**: `recordingSupport()` -> not supported: `rec-unavailable` with reason ->
   permission `granted`: `listInputDevices()` + `openInput(last device)`; `prompt`: privacy
   note + `rec-allow-mic` -> `openInput(null)` (browser prompt) -> labels -> select the stored
   device label; `denied`: instructions + retry. Calibrations are loaded
   (`listInputCalibrations`) and matched by label; output latency checked (> 220 samples
   difference -> `rec-setup-changed`).
2. **Calibrate**: dialog -> `calibrateLatency()` -> result -> `upsertInputCalibration` (loopback)
   or manual entry -> upsert (manual).
3. **Record**: Record -> checks (speed/transpose 1/0, trainer, limit, input open) ->
   `startRecording({ latencySamples: L, ... })` -> 012 count-in -> capture -> Stop (or end of
   song, pass limit, device lost) -> `finishing` (tail `L + P`) -> `OnTakesCaptured` -> each take
   shown as `local` and loadable at once (`loadTake({ captureKey })`) -> uploads one by one:
   `captureWav` (Blob) -> `IJSStreamReference` -> `createRecordingTake` multipart -> `saving` ->
   polling (existing 007 asset sync on the server; the Web polls `listRecordingTakes` every 1 s
   while any take is `saving`) -> `ready` -> `releaseCapture` after the server copy is loaded or
   the panel closes.
4. **Server save**: validate metadata -> checks (track, limit, disk) -> stream-parse WAV
   (`TakeWavReader`, peak scan) -> write `capture.wav` padded (`TakeWavPadder`) -> create Asset
   (`recording`, `queued`) + RecordingTake -> 007 `createRenderJob` (single input, FLAC 24, no
   dither) -> 007 sync stores `audio.flac`, sets `ready` -> `TakeAssetFinisher` (asset-ready
   hook) deletes `capture.wav` and registers the audio hash.
5. **Listen back / compare**: select take -> `loadTake({ url, startSample, lengthSamples })` ->
   strip `take`; compare radio -> `setTakeCompare`; strip settings -> `setChannel('take')`;
   saved with the mix (`MixSettings.take`).
6. **Export**: take row -> 007 dialog preset "Take only" (`mode: raw`, `channels: ["take"]`,
   `takeId`) or "Take + mix" (`mode: mix`, `mix` = current settings incl. `take`, compare
   translated into explicit mutes) -> 007 flow unchanged.
7. **Delete**: one (`deleteRecordingTake`) or the other passes (`deleteRecordingTakes`) ->
   cancel render job, delete asset files and row, null mix references, remove hashes.
8. **Adjust timing** (Expert): nudge -> `setTakeAdjustment` -> Apply -> engine re-encodes the
   span from the loaded buffer with the shifted start -> `replaceRecordingTakeAudio` -> new
   render -> `alignmentRevision + 1`.

## Phasing

- **Phase 0 (architect)**: ADR-024 accepted; full contract documents on 014's versions; C#
  contracts; `types.ts` 2.5.0; skeleton stubs (engine methods that reject with
  `Error('not implemented: <method>')`, covered by skeleton tests so every gate stays green, as
  in earlier Phase 0s); spikes S-01 (capture gap-free, Firefox) and S-02 (stream 240 MB).
- **Wave (parallel)**: audio-engine, backend, frontend, qa, devops, stem-separation (one
  regression test), quality-gate.
- **Integration (CTO)**: full stack with the fake capture device; owner checks SC-002, SC-004,
  SC-005 on the reference machine with a cable loopback and a real interface (guitar DI, vocal
  mic, stereo keys); manuals.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Takes padded with leading silence to song sample 0 | 007 renderer aligns inputs at sample 0 and has no offset; keeps worker unchanged and exports trivial | an offset in `RenderInput` is a worker contract change not allocated to 015; unpadded storage cannot be exported as "take + mix" |
| 007 render job reused as WAV -> FLAC writer | lossless FLAC without a new dependency, bit-exact path already proven (ADR-016) | a .NET FLAC encoder (none maintained with a permissive licence) or browser WASM FLAC (new dependency, worker still needs the file) |
| Takes are hidden library assets (origin `recording`) | reuse of asset storage, sync, Range serving, hashes and the worker path allow-list | a separate take store would duplicate the asset pipeline and need a worker path pattern |
| Upload through Blazor stream interop instead of direct browser POST | keeps CORS GET-only and the Web server as the only caller of the API | direct fetch is faster but widens the API's attack surface for the first time |
| Second Vite entry (capture worklet) | AudioWorklet modules must be separate files | ScriptProcessorNode is deprecated and runs on the main thread |
