---

description: "Task list for feature 015 play-along recording (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Play-along Recording

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md) (Phase 0 results 2026-11-17: [contracts/DELTA.md](contracts/DELTA.md) "Phase 0 decisions"),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
the merged 007, 012 and 014 artefacts, constitution 1.3.1, ADR-024.

**Round start done** (2026-11-17, branch `014-015-round`): 022+023 and 014 Phase 0 merged, the 015 documents
re-merged onto 014's 1.13.0 / 2.4.0 (DELTA.md "Re-merge outcome"). The combined wave follows
`docs/handoffs/cto-r14-lane-plan.md` (lanes L1-L6, which override the owner table below where they differ).

**Tests**: REQUIRED (constitution I). Every implementation task is preceded by a failing-test
task of the same owner (red commit first, then green). >= 90 % line AND branch per component.
No task needs real hardware except the CTO owner checks (T089): the microphone, the worklet,
the worker and JS interop are faked; E2E uses Chromium's fake capture device and the digital
loopback (`StemStudio:Recording:TestLoopback=true`).

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: can run in parallel with the other [P] tasks of the same phase (different files, no
  dependency on an unfinished task).
- **[USn]**: user story of spec.md. Owners: architect, audio-engine, backend, frontend, qa,
  devops, quality-gate, stem-separation, CTO. audio-ingest, beat-analysis and generative have
  no tasks.

## Checkpoint hand-offs (binding, ADR-008)

`docs/handoffs/<owner>-015-<checkpoint>.md` at `foundational`, every story the owner works on,
and `polish`, with real test and coverage output.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| frontend | engine recording API, callbacks | `types.ts` 2.5.0 (T004), audio-engine.delta.md | bUnit JS-interop mock |
| frontend | takes, calibrations, export/mix additions | backend 1.14.0 (T002), DTOs (T003) | fake API client / stub handler |
| backend | worker render job (unchanged 007) | worker-api 1.8.0 | stub worker handler |
| audio-engine | 012/014 transport segments, tempo-stage latency, count-in | DELTA.md section 3 requests (T001 check) | fakes of the transport/tempo stage |
| qa | UI ids, fake capture, loopback | ui-test-ids 1.14.0 (T002), fixtures (T009), TestLoopback (T010) | - |

---

## Phase 0: Contracts and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] ADR-024 -> Accepted; check the merged 012 (2.3.0) and 014 (2.4.0) engines against DELTA.md section 3 (segments, processing latency, extra channels, count-in API); record which hooks exist and which the audio-engine lane adds in T015 (ADR-024 "Phase 0 outcome")
- [x] T002 [architect] Full contracts in `specs/015-play-along-recording/contracts/`: `backend-api.openapi.yaml` 1.14.0 (= 014's 1.13.0 + `recording-api.openapi.yaml` + the `*Additions` merged into their schemas/operations), `ui-test-ids.md` 1.14.0, `render-vectors.json` 1.1.0 (+ `generate_render_vectors.py` take input, `--check`), `redocly.yaml`; Redocly 2.78.0 lint 0 errors
- [x] T003 [P] [architect] .NET contract code: `src/StemStudio.Contracts/Recording/{RecordingTakeDto, RecordingTakeListDto, RecordingTakeUploadDto, ReplaceTakeAudioMetadataDto, UpdateTakeRequest, DeleteTakesRequest, DeleteTakesResultDto, TakeLatencyDto, InputCalibrationDto, InputCalibrationInput, RecordingPartDto, InputChannelModeDto, LatencyMethodDto, CalibrationMethodDto, TakeStatusDto, TakeChannelSettingsDto, RecordingRecipeDto}.cs`; additions `AssetOriginDto.Recording`, `CreateAudioExportRequest.TakeId`, `MixSettings*.Take`, `TrackDto.TakeCount`; `ApiRoutes.Takes*`/`Calibrations*`; `ContractLimits` (data-model constants, `ExportChannelIdPattern` = `ChannelIdPattern` or `take`; `ChannelIdPattern` unchanged, P0-2); XML docs; skeleton serialisation tests
- [x] T004 [P] [architect] Engine skeleton: `src/audio-engine/src/types.ts` 2.5.0 (audio-engine.delta.md section 1-3), `ENGINE_CONTRACT_VERSION = "2.5.0"`, engine methods rejecting `not implemented: <method>`, `src/recording/capture-worklet.ts` stub + second Vite entry; skeleton tests keep coverage >= 90 %
- [x] T005 [architect] Spikes on the reference machine (research.md S-01 capture gap-free 10 min Chrome/Edge + Firefox rate-mismatch check; S-02 stream a 240 MB WAV through JS stream interop + API, time it); record results in research.md. **Phase 0 result**: S-01 60 s gap-free in Chromium and Firefox (fake devices) plus the secure-context matrix; S-02 browser half 0.2-0.3 s. The 10-minute real-interface run, CPU and the Firefox rate mismatch move to T089; the circuit + API half of S-02 to the frontend US1 timing test (T043) and T089
- [x] T006 [architect] Verify the skeleton (`dotnet build` 0 warnings, all tests + coverage gates, engine lint/typecheck/tests, `run_all.py`); hand-off `docs/handoffs/architect-015-phase0.md`

**Checkpoint**: skeleton green; the wave may start.

---

## Phase 1: Setup

- [ ] T007 [P] [quality-gate] Failing rule tests: `Api.Assets` and `Api.Tracks` must not depend on `Api.Recording` (only on `IAssetOriginPolicy` / `ITrackTakesCleanup` interfaces they own); engine `src/recording/*` imported only by `engine.ts` and each other
- [ ] T008 [quality-gate] Implement the rules in `tools/quality/`; update `docs/quality/SIG-REPORT.md`
- [x] T009 [P] [qa] Fixtures `tests/fixtures/recording/` generated by a committed script (no real person): one WAV per family for the fake capture device (sung vowels, plucked string, low pluck, stereo chord pad, stereo drum hits, bowed tone; 44.1 kHz, 20 s), a click-track stem fixture track (clicks every 0.5 s) for the loopback test, a silent WAV; `docs/testing/TEST-STRATEGY.md` section 19 (fake capture, loopback, Chromium channel, network assertion)
- [ ] T010 [P] [devops] E2E: Playwright project `recording` (Chromium full build `channel: 'chromium'`, fake-device flags, `microphone` permission) in CI and `scripts/test-all.*` (`npm run test:recording`); e2e compose override sets `StemStudio__Recording__TestLoopback=true` for the web service; API env `StemStudio__Recording__MaxUploadBytes` documented in `.env.example`
- [ ] T011 [P] [stem-separation] Regression test `worker/tests/render/test_take_conversion.py`: render jobs of padded 24-bit mono and stereo WAVs (`dither: false`, FLAC 24) produce FLAC whose decoded integers equal the input (mono duplicated), leading zeros preserved, all-zero input fails "Nothing to export" (research R-08; expected green on existing code, otherwise report as an open issue, no contract change)

---

## Phase 2: Foundational (blocking all stories)

### audio-engine (tests first)

- [x] T012 [P] [audio-engine] Failing tests `tests/recording/wav.test.ts`: `encodeWav24` header fields, quantisation `clamp(round(x*2^23))` incl. +-1.0 and NaN -> 0, mono/stereo interleave, length, little-endian vectors (FR-011)
- [x] T013 [audio-engine] Implement `src/recording/wav.ts` to pass T012
- [x] T014 [P] [audio-engine] Failing tests `tests/recording/mapping.test.ts`: `mapCaptureToTakes` without loop, with loop passes, with count-in gaps, negative start dropped, end of track, partial pass >= 1 s kept / < 1 s dropped, frame gaps filled and counted, compensation `L + P` (data-model 3.2, FR-008, FR-010)
- [x] T015 [audio-engine] Implement `src/recording/mapping.ts`; add the internal transport-segment log (T001 found the anchors pruned; tempo-stage latency is already in the heard anchors, P0-8) with its own unit tests (no public API change)
- [x] T016 [P] [audio-engine] Failing tests `tests/recording/calibration.test.ts`: seeded click schedule, chirp template, `estimateLatency` vectors of research R-04 (exact delays 0/128/1,000/4,410/20,000, noise -20/-40 dB, echo, missing clicks, clipping warning, jitter, every failure reason) (FR-007)
- [x] T017 [audio-engine] Implement `src/recording/calibration.ts` to pass T016
- [x] T018 [P] [audio-engine] Failing tests `tests/recording/compare.test.ts` and `tests/gate.test.ts` additions: `take` is an audible-level channel for `anySolo`; `compareGate` per mode incl. expanded detail channels and part `none` (research R-10)
- [x] T019 [audio-engine] Implement `src/recording/compare.ts`, gate and mixer changes to pass T018

### backend (tests first)

- [ ] T020 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Recording/TakeWavReaderTests.cs`: valid mono/stereo, `WAVE_FORMAT_EXTENSIBLE`, extra chunks, truncated, wrong rate/bits/channels (415 field), oversize, odd data size; streamed peak scan (all zero, clipped count, quiet) (data-model 2.3)
- [ ] T021 [backend] Implement `src/StemStudio.Domain/Recording/TakeWavReader.cs` to pass T020
- [ ] T022 [P] [backend] Failing tests `Domain.Tests/Recording/`: `TakeWavPadder` (header sizes, zero bytes, streamed copy), `LatencyConversion` (ms <-> samples rounding rule), `TakeRules` (200 limit, length bounds, outside-track tolerance 4,410, speed rule), `TakeNaming` (numbering never reused, default names per part, rename trimming), `InputCalibration` rules (data-model 2.1, 2.4, 2.5)
- [ ] T023 [backend] Implement `src/StemStudio.Domain/Recording/*` to pass T022
- [ ] T024 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/`: migration `AddRecordingTakes` (tables, indexes, unique `(TrackId, Number)`, cascade on track, set null on mix/calibration), `InputCalibrations` unique label, `Mixes.TakeChannel` JSON round trip
- [ ] T025 [backend] Implement persistence configuration and migration to pass T024
- [ ] T025a [P] [backend] Failing tests for the 013/022 integration (DELTA.md 7 "013 implementation notes" and "022"): origin `recording` stored as `Recording` and read back (fixture `tests/fixtures/backup/recording-asset.json` unchanged); backup scrub `recordings=false` removes the take rows and clears `Mixes.TakeChannel`; `InputCalibrations` in the `database` category; merge restore adds `RecordingTakes` only with their asset and `InputCalibrations` by label (live wins); backups never contain `capture.wav`; the three gated endpoints answer `409 backup-running` while a backup runs and `503` during maintenance; 022 `LibraryFilingRules.IsFileable` false for `recording` and `CountPendingAsync`/`PendingAsync` exclude takes (022 is merged into the round since the round start)
- [ ] T025b [backend] Implement T025a (Domain enum member `Recording`, scrubber, merge planner, file selection, `RequireGateOpen`, 022 filing rules and SQL)

### frontend (tests first)

- [x] T026 [P] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Recording/RecordingInteropTests.cs`: JS-interop calls and argument shapes for every 2.5.0 method, callback routing (`OnRecordingStateChanged`, `OnRecordingProgress`, `OnTakesCaptured`, `OnInputLost`, `OnInputDevicesChanged`), error-code parsing (`permissionDenied:` ...), `RecordingPreferences` defaults and persistence (data-model 4)
- [x] T027 [frontend] Implement `src/StemStudio.Web/Recording/{RecordingInterop,RecordingPreferences}.cs` to pass T026

**Checkpoint**: HO:foundational per owner.

---

## Phase 3: User Story 1 - Record myself over the mix and hear it back (P1)

- [x] T028 [P] [US1] [audio-engine] Failing tests `tests/recording/input.test.ts`, `monitoring.test.ts`: support reasons (insecure, no AudioWorklet, no getUserMedia), permission query incl. not queryable, constraints of research R-02, error mapping (NotAllowedError, NotFoundError, NotReadableError, OverconstrainedError, rate mismatch), mode availability, `devicechange`, track `ended` -> `OnInputLost`, monitoring routing/volume ramps, `closeInput` rules (FR-001..FR-003, FR-006)
- [x] T029 [US1] [audio-engine] Implement `src/recording/{input,monitoring}.ts` to pass T028
- [x] T030 [P] [US1] [audio-engine] Failing tests `tests/recording/capture-worklet.test.ts`, `capture.test.ts`: processor channel selection per mode, `currentFrame` stamps, 2,048-frame batches, zero-gain keep-alive connection; receiver gap fill, 30-minute limit, release
- [x] T031 [US1] [audio-engine] Implement `src/recording/{capture-worklet,capture}.ts` and the Vite entry to pass T030
- [x] T032 [P] [US1] [audio-engine] Failing tests `tests/recording/recorder.test.ts`: state machine, refusals (`recordingSpeed:`, `speedTrainer:`, input not open), 012 count-in start, locked transport controls, mute-original gate, take gated while recording, finishing tail `L + P` + one batch, stop at end of song, `OnRecordingProgress` <= 4 Hz, `captureWav`/`releaseCapture` (FR-004, FR-005, FR-009)
- [x] T033 [US1] [audio-engine] Implement `src/recording/recorder.ts` to pass T032
- [x] T034 [P] [US1] [audio-engine] Failing tests `tests/recording/take-channel.test.ts`: `loadTake` from capture and from URL, slicing and mono reduction, scheduling with the shared anchor (play, seek inside/before/after the take, pause, loop passes), meter and `setChannel('take')` before load, `unloadTake`, decode errors; playback through the tempo stage like stems (FR-009, FR-012)
- [x] T035 [US1] [audio-engine] Implement `src/recording/take-channel.ts`, wire everything in `engine.ts` and `callbacks.ts` to pass T034 (2.5.0 complete for US1)
- [ ] T036 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Recording/CreateTakeEndpointTests.cs`, `ListTakesEndpointTests.cs`: streamed multipart, metadata validation, every problem code of DELTA.md section 1 for create, padded `capture.wav` bytes, Asset (`recording`, provenance, recipe) and the exact worker render request, `202` + `Location`, per-endpoint body limit, list order and total size (FR-011)
- [ ] T037 [US1] [backend] Implement `src/StemStudio.Api/Recording/{TakeEndpoints,TakeUploadService,TakeStorage}.cs` to pass T036
- [ ] T038 [P] [US1] [backend] Failing tests `TakeAssetFinisherTests.cs`, `AssetOriginPolicyTests.cs`: asset ready -> `capture.wav` deleted, audio hash registered (010 guard refuses the take as a voice sample), status mapping, `listAssets` excludes `recording` without `origin`, `managed-by-take` refusals, `retryRecordingTake` (`take-not-failed`, `capture-missing`) (FR-011, FR-017)
- [ ] T039 [US1] [backend] Implement `TakeAssetFinisher`, `IAssetOriginPolicy` registration, retry to pass T038
- [x] T040 [P] [US1] [frontend] Failing bUnit tests `RecordingPanelTests.cs`, `PermissionGateTests.cs`, `InputSelectorTests.cs`: every permission/device state and `data-reason`, privacy note, allow button triggers `openInput`, device preselected by stored label, meter element, part and mute-original, count-in, monitoring with warning, disabled reasons with "Set to 100 %", live region texts, `R` shortcut, levels (FR-001..FR-006, FR-009, FR-020, FR-021)
- [x] T041 [US1] [frontend] Implement `Components/Recording/{RecordingPanel,PermissionGate,InputSelector}` and the toolbar toggle to pass T040
- [x] T042 [P] [US1] [frontend] Failing bUnit tests `TakeUploaderTests.cs`, `TakeStripTests.cs`: `IJSStreamReference` -> API client streaming (`maxAllowedSize`), sequential uploads, `local` -> `saving` -> `ready` with 1 s polling, refused upload -> `rec-error` + `rec-save-again`, capture released after the server copy loads; take strip ids and settings -> engine (FR-011, FR-012)
- [x] T043 [US1] [frontend] Implement `Recording/TakeUploader.cs`, API client methods and `Components/Recording/TakeStrip` to pass T042
- [x] T044 [P] [US1] [qa] E2E `e2e/tests/recording.spec.ts` (`@recording`, Chromium): one case per family with its fixture (SC-008): record 10 s, take row appears `local` then `ready`, plays in the strip (meter moves), stored FLAC is 24-bit (asset `audio` facts); speed 80 % disables Record and "Set to 100 %" re-enables it; monitoring warning; network assertion: only web and API origins contacted (SC-007); permission-denied context shows `rec-unavailable data-reason="denied"` and the mixer still plays

**Checkpoint**: US1 independently testable; HO:us1 per owner.

---

## Phase 4: User Story 2 - Calibrate latency (P1)

- [ ] T045 [P] [US2] [audio-engine] Failing tests `tests/recording/calibrate-engine.test.ts`: refuses while playing, forces monitoring off and mutes the mix, clicks scheduled on the destination, capture window, result object incl. `outputLatencySamples`, `testInput: 'loopback'` wiring (FR-007)
- [ ] T046 [US2] [audio-engine] Implement `calibrateLatency` and the loopback test input to pass T045
- [ ] T047 [P] [US2] [backend] Failing tests `CalibrationEndpointTests.cs`: list, upsert by label (create/replace), validation, delete (takes keep their latency, `calibrationId` set null)
- [ ] T048 [US2] [backend] Implement `Recording/CalibrationEndpoints.cs` to pass T047
- [x] T049 [P] [US2] [frontend] Failing bUnit tests `CalibrationDialogTests.cs`: instructions, running state (`aria-busy`), success and every failure reason with its tip, manual entry (0-1000, 0.1 step, rounding), save -> upsert, Expert samples, `rec-latency-status` per method, `rec-setup-changed` (> 220 samples) (FR-007, FR-020)
- [x] T050 [US2] [frontend] Implement `Components/Recording/CalibrationDialog` to pass T049
- [x] T051 [P] [US2] [qa] E2E `recording-loopback.spec.ts` (TestLoopback stack): calibration ok within 10 s (SC-003); record the click-track stem through the loopback; download the take, cross-correlate with the stem: offset <= 2 samples (SC-001); manual entry flow labels the next take "manual"

**Checkpoint**: HO:us2 per owner.

---

## Phase 5: User Story 3 - Loop recording, one take per pass (P2)

- [ ] T052 [P] [US3] [audio-engine] Failing tests `tests/recording/loop-recording.test.ts`: passes from transport segments, count-in before every pass not captured, partial last pass, 50-pass and 30-minute limits, loaded take replays in each pass (FR-010)
- [ ] T053 [US3] [audio-engine] Implement loop handling in recorder/take-channel to pass T052
- [ ] T054 [P] [US3] [backend] Failing tests `DeleteTakesEndpointTests.cs`: batch delete, unknown and other-track ids ignored, files and hashes gone, count returned (FR-014)
- [ ] T055 [US3] [backend] Implement `deleteRecordingTakes` to pass T054
- [x] T056 [P] [US3] [frontend] Failing bUnit tests: session groups, `rec-pass`, partial badge, star, `takes-keep-starred-<sessionId>` with confirmation (FR-010, FR-014)
- [x] T057 [US3] [frontend] Implement session grouping and keep-starred in `TakesPanel` to pass T056
- [x] T058 [P] [US3] [qa] E2E `recording-loop.spec.ts`: loop 4-8 s, three passes + 2 s partial -> four takes in one session; star pass 2, delete the others -> one take remains

**Checkpoint**: HO:us3 per owner.

---

## Phase 6: User Story 4 - Compare take and original (P2)

- [ ] T059 [P] [US4] [audio-engine] Failing tests: `setTakeCompare` swaps gates at one context time with <= 10 ms ramps, user mute/solo untouched, expanded detail channels, part `none` toggle (FR-013)
- [ ] T060 [US4] [audio-engine] Implement `setTakeCompare` to pass T059
- [ ] T061 [P] [US4] [backend] Failing tests `MixTakeChannelTests.cs`: `MixSettings.take` save/load, stale take id -> null, duplicate copies it, deleting the take nulls it (FR-012)
- [ ] T062 [US4] [backend] Implement the mix additions to pass T061
- [ ] T063 [P] [US4] [frontend] Failing bUnit tests `TakeCompareTests.cs`: radio group and toggle, `C` shortcut cycles, aria states, strip settings saved with the mix (FR-012, FR-013, FR-021)
- [ ] T064 [US4] [frontend] Implement `Components/Recording/TakeCompare` and mix saving to pass T063
- [ ] T065 [P] [US4] [qa] E2E `recording-compare.spec.ts`: bass take, "My take" -> `meter-bass` at -60 dB and `meter-take` moving; "Original" reverse; saved mix restores the take strip

**Checkpoint**: HO:us4 per owner.

---

## Phase 7: User Story 5 - Manage takes (P2)

- [ ] T066 [P] [US5] [backend] Failing tests `UpdateTakeEndpointTests.cs`, `DeleteTakeEndpointTests.cs`, `TrackDeleteCascadeTests.cs`: rename syncs the asset name, star, part change; delete cancels a running render, removes files, hashes and mix references; track delete removes its takes; `Track.takeCount` (FR-014, FR-018)
- [ ] T067 [US5] [backend] Implement `updateRecordingTake`, `deleteRecordingTake`, `TakeDeletion`, `ITrackTakesCleanup` to pass T066
- [ ] T068 [P] [US5] [frontend] Failing bUnit tests `TakesPanelTests.cs`, `TakeRowTests.cs`: newest first, details per level, select/load, play from start, rename, delete confirm, retry, warnings, total size, `track-delete-takes-note` (FR-014, FR-018)
- [ ] T069 [US5] [frontend] Implement `TakesPanel`, `TakeRow` and the track delete note to pass T068
- [ ] T070 [P] [US5] [qa] E2E `recording-manage.spec.ts`: three takes, rename, star, delete one (files gone via the asset endpoint 404), delete the track -> its takes are gone

**Checkpoint**: HO:us5 per owner.

---

## Phase 8: User Story 6 - Export take or take + mix (P2)

- [ ] T071 [P] [US6] [backend] Failing tests `Domain.Tests/Rendering/TakeExportPlannerTests.cs` and Api export tests: raw take (default range = span, explicit range intersected), mix with `take` (gate, volume, pan), explicit `["take", ...]`, `take-required`, `take-not-in-track`, `take-not-ready`, provenance `contents` and `sourceAssetIds`; render-vectors 1.1.0 consumer (FR-015)
- [ ] T072 [US6] [backend] Implement the planner and export request additions to pass T071
- [ ] T073 [P] [US6] [audio-engine] render-vectors 1.1.0 consumer test with the take cases (gate parity with the planner); fix the gate if red
- [ ] T074 [P] [US6] [frontend] Failing bUnit tests: export dialog presets from a take row (take only, take + mix), compare translated into explicit mutes, `export-channel-take`, `export-take-saving` (FR-015)
- [ ] T075 [US6] [frontend] Implement the export presets to pass T074
- [ ] T076 [P] [US6] [qa] E2E `recording-export.spec.ts`: take only as WAV 24-bit has the take's length and samples; take + mix MP3 appears in the Library with contents starting "Take n"

**Checkpoint**: HO:us6 per owner.

---

## Phase 9: User Story 7 - Adjust the timing of a take (P3)

- [ ] T077 [P] [US7] [audio-engine] Failing tests: `setTakeAdjustment` re-schedules at `currentTime + 0.01`; re-encoding the loaded span with a shifted start (positive later, clamped at 0) (FR-016)
- [ ] T078 [US7] [audio-engine] Implement to pass T077
- [ ] T079 [P] [US7] [backend] Failing tests `ReplaceTakeAudioEndpointTests.cs`: previous audio kept until ready, `alignmentRevision` + 1, `adjustmentSamples` summed, `take-saving` 409, failure keeps the old audio (data-model 2.4)
- [ ] T080 [US7] [backend] Implement `replaceRecordingTakeAudio` to pass T079
- [ ] T081 [P] [US7] [frontend] Failing bUnit tests `TakeAdjustTests.cs`: Expert only, slider and number (0.1 ms), live playback shift, apply/reset, note (FR-016, FR-020)
- [ ] T082 [US7] [frontend] Implement `TakeAdjust` to pass T081
- [ ] T083 [P] [US7] [qa] E2E (Expert): adjust -10.0 ms and apply -> `startSample` 441 lower, revision 1

**Checkpoint**: HO:us7 per owner.

---

## Phase 10: Polish and cross-cutting

- [ ] T084 [P] [frontend] Help topics (data-model 5, one connection topic per instrument family, privacy, permissions per browser, latency incl. the clap test, speed rule, limits), `FeatureCatalog` entries (data-model 6), shortcut overview rows; help marker tests (FR-019, FR-020)
- [ ] T085 [P] [qa] a11y (axe on panel, calibration dialog, Takes panel and take strip; keyboard-only record/compare/delete flow; live region; reduced motion), visual baselines of the new panels at 320 px and desktop, Firefox smoke (unsupported or permission states only) (SC-009)
- [ ] T086 [audio-engine, backend, frontend, qa, devops, stem-separation] HO:polish per owner with coverage evidence (counted once per owner)
- [ ] T087 [quality-gate] `run_all.py --report docs/quality/SIG-REPORT.md` over the 015 code; refactor requests to owners
- [ ] T088 [CTO] Integration run: all suites (legacy, ux, recording, recording-loopback, a11y, visual), `scripts/test-all.ps1`
- [ ] T089 [CTO] Owner checks on the reference machine: cable loopback 5 calibrations within 0.5 ms and a take within 1 ms (SC-002); 5-min stereo take stored <= 15 s (SC-004); 10 min recording without dropouts (SC-005); one real input per available family (vocal mic, guitar DI, stereo keys)
- [ ] T090 [CTO] `/speckit-analyze`; `docs/STATUS.md`; merge preparation
- [ ] T091 [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1): user manual chapter "Play-along recording" (setup per instrument family, calibration, loop takes, compare, export, privacy, limits), technical manual (endpoints, `StemStudio:Recording:*` settings, browser support, storage layout); `cd tools/docs && npm ci && npm run browser && npm run build`; commit sources, PDFs and `build-manifest.json`; `node tools/docs/check.mjs` passes

---

## Acceptance of tasks without a red test (ready check 2026-11-17)

- T084 (help, `FeatureCatalog`, overview rows): the 004 help-content tests list every 015 topic id of
  data-model 5 (one connection topic per instrument family), each `data-help-id` resolves, `FeatureCatalogTests`
  show the data-model 6 levels, and the shortcut overview test shows `mixer.record.toggle` / `mixer.compare.cycle`
  (`reserved: false` in `keymap-defaults.json`, flipped by lane L5 on request, `KeyMapDefaultsTests` updated).
- T009 (fixtures): `tests/fixtures/tests/` checks each generated file (rate, channels, length, family manifest)
  and that the generator output is reproducible (hash list).
- E2E authoring (T044, T051, T058, T065, T070, T076, T083, T085): typecheck and lint in `e2e/`; runs at the
  mid-wave checkpoint (T044) and in T088.
- T086: hand-off per owner with the coverage table; T087/T088/T090: commands green as listed; T089: each SC
  result recorded in `docs/STATUS.md`; T091: `node tools/docs/check.mjs` passes.
- T108 of 014 (`ManifestBuilder.BackendApiVersion` = `1.14.0`) belongs to lane L4 with the 015 backend tasks.

## Execution plan per owner (parallel wave)

| Owner | Ordered task sequence | May mock / fake |
|---|---|---|
| architect | T001 -> T002 -> (T003 \|\| T004) -> T005 -> T006 | n/a |
| audio-engine | T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> HO:foundational -> T028 -> T029 -> T030 -> T031 -> T032 -> T033 -> T034 -> T035 -> HO:us1 -> T045 -> T046 -> HO:us2 -> T052 -> T053 -> HO:us3 -> T059 -> T060 -> HO:us4 -> T073 -> HO:us6 -> T077 -> T078 -> HO:us7 -> T086 | AudioContext/worklet/media-devices mocks, fake transport segments |
| backend | T020 -> T021 -> T022 -> T023 -> T024 -> T025 -> T025a -> T025b -> HO:foundational -> T036 -> T037 -> T038 -> T039 -> HO:us1 -> T047 -> T048 -> HO:us2 -> T054 -> T055 -> HO:us3 -> T061 -> T062 -> HO:us4 -> T066 -> T067 -> HO:us5 -> T071 -> T072 -> HO:us6 -> T079 -> T080 -> HO:us7 -> T086 | stub worker handler, SQLite, temp data dir, `FakeTimeProvider` |
| frontend | T026 -> T027 -> HO:foundational -> T040 -> T041 -> T042 -> T043 -> HO:us1 -> T049 -> T050 -> HO:us2 -> T056 -> T057 -> HO:us3 -> T063 -> T064 -> HO:us4 -> T068 -> T069 -> HO:us5 -> T074 -> T075 -> HO:us6 -> T081 -> T082 -> HO:us7 -> T084 -> T086 | fake API client, bUnit JS-interop mock |
| qa | T009 -> T044 -> HO:us1 -> T051 -> HO:us2 -> T058 -> HO:us3 -> T065 -> HO:us4 -> T070 -> HO:us5 -> T076 -> HO:us6 -> T083 -> HO:us7 -> T085 -> T086 | fake capture device, digital loopback |
| devops | T010 -> HO:foundational -> T086 | - |
| stem-separation | T011 -> T086 | - |
| quality-gate | T007 -> T008 -> HO:foundational -> T087 | sample files |
| CTO | T088 -> T089 -> T090 -> T091 | n/a |

Parallelism: within a phase, every `[P]` failing-test task of different owners runs at once;
each owner's implementation task follows its own red task. Stories US3-US7 depend only on US1
(and US6 on the 007 planner); after US1 the backend, engine and frontend lanes may take the P2
stories in any order.

Task count per owner (T086 counted once per owner): architect 6 (T001-T006), audio-engine 26
(T012-T019, T028-T035, T045-T046, T052-T053, T059-T060, T073, T077-T078, T086), backend 25
(T020-T025, T025a-T025b, T036-T039, T047-T048, T054-T055, T061-T062, T066-T067, T071-T072, T079-T080,
T086), frontend 20 (T026-T027, T040-T043, T049-T050, T056-T057, T063-T064, T068-T069, T074-T075,
T081-T082, T084, T086), qa 10 (T009, T044, T051, T058, T065, T070, T076, T083, T085, T086),
devops 2 (T010, T086), stem-separation 2 (T011, T086), quality-gate 3 (T007, T008, T087), CTO 4
(T088-T091). **91 task ids** (T001-T091); 96 owner assignments.

## Dependencies & Execution Order

012 and 013 merged (014 may run in parallel) -> Phase 0 -> Phase 1 -> Phase 2 -> US1 -> US2 ->
{US3, US4, US5, US6} -> US7 -> Polish -> Integration (CTO).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch for engine, API, Web and worker (I)
- [ ] Microphone, worklet, worker and interop behind fakes; E2E with the fake device and loopback (I)
- [ ] SIG limits; `run_all.py` passes; new dependency rules green (II)
- [ ] backend 1.14.0, engine 2.5.0, UI ids 1.14.0, render-vectors 1.1.0 implemented; contract tests pass; worker contract unchanged (III)
- [ ] No new dependency; recordings local only (network assertion); takes refused as voice samples and absent from the agent (IV)
- [ ] Limits documented in help (100 % speed rule, two channels, browser support, hardware gain, acoustic loopback accuracy) (V)
- [ ] Help topics per instrument family present and linked; levels per data-model 6; a11y clean
- [ ] docs/public manuals updated and PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
