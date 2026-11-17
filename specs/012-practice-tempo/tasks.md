---

description: "Task list for feature 012 practice tempo (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Practice Tempo (speed, transpose, speed trainer, count-in)

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.4.0 (1.3.1 when specified), ADR-021 (and ADR-005, ADR-010, ADR-015, ADR-016).

**Status**: specified 2026-11-16; **Phase 0 done 2026-11-16** (architect, hand-off `docs/handoffs/architect-012-phase0.md`); the wave may start.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No unit test needs a real `AudioContext`, the
WASM module, a browser or the network: the stretch stage is a fake (`timeStretchFactory`), the
processor runs against a fake `AudioWorkletGlobalScope` and a TypeScript fake of the WASM ABI, the
clock is the mocked `AudioContext`. The real SoundTouch module is exercised by the Node suite
`npm run test:wasm` (CI, after the WASM build) and by Playwright in real browsers.

**Organization**: grouped by user story (US1 speed/transpose, US2 trainer, US3 count-in); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per lane.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on an
  incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-012-<checkpoint>.md`. Lane **web-interop** is the
  frontend agent working in `src/web-interop/**` (own coverage gate). **CTO** = orchestrator.
- Paths are relative to the repository root. Worker owners (audio-ingest, stem-separation,
  beat-analysis), generative and ai-agent have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-012-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us3`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`.
The frontend agent reports the web-interop lane in a separate section with its own coverage.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| audio-engine | practice types, ABI, measured constants | `types.ts` 2.3.0 (T005), audio-engine.md 2.3.0 (incl. DELTA.md 5 refinements: D = 5120, `c(p)`, pitch-apply rule), ABI header (T006), `soundtouch.lock` | mocked `AudioContext`, fake stage, fake scope + fake ABI |
| backend | DTOs, schema | `StemStudio.Contracts/Mixes/*Practice*` (T004), backend-api 1.11.0 (T003) | SQLite in-memory, WebApplicationFactory |
| frontend | DTOs, engine methods, ids, help rules | T004, T005, `ui-test-ids` 1.11.0 (T003), help-content 1.0.0 | fake `IStemStudioApiClient`, fake `IAudioEngine`, bUnit JS interop mock |
| web-interop (frontend) | command ids | ui-test-ids delta "Shortcut commands" | jsdom key events |
| qa | ids, engine status, static file names | T003, audio-engine.md "Packaging", data-model 6 | fake stack (`STEMSTUDIO_SEPARATOR=fake`), probes authored in T012 |
| devops | file names, versions, lock | data-model 1 and 6, `third_party/soundtouch/soundtouch.lock` (T001) | locally built image |

---

## Phase 0: Spikes, contract code and skeleton (architect, BEFORE the wave; done 2026-11-16)

- [x] T001 [architect] Spike P0-1 (research.md) in a scratch container only: build SoundTouch 2.4.1 (unmodified tarball, SHA-256 recorded) with `emscripten/emsdk:6.0.7` (digest recorded) and a throw-away shim; Node harness: wasm size, SIMD128 on/off throughput and output equality, `SETTING_INITIAL_LATENCY` for pitch factors {0.42, 0.5, 0.71, 1, 1.41, 2, 2.83, 4}, FFT pitch accuracy on a 440 Hz sine, two builds bit-identical; set `STRETCH_DELAY_FRAMES` (<= 10,112) and the SIMD decision in research.md "Phase 0 results" and ADR-021; write `third_party/soundtouch/soundtouch.lock`. Exit criteria failed -> fallback soundtouchjs (ADR-021 D7), plan/tasks amended before the wave (FR-006, SC-001, SC-004)
- [x] T002 [architect] Spike P0-2 in Chromium and Firefox on the reference machine (throw-away page outside the repo): 20-channel fixture at 50 % for 10 min through the spike processor -> underruns, process time per quantum, change latency from `setValueAtTime` to the probe's frequency change; results in research.md; ADR-021 status Accepted (SC-001, SC-004)
- [x] T003 [architect] Contract merge P0-3: `backend-api.openapi.yaml` `1.11.0-delta.1` -> `1.11.0` = 011's full 1.10.0 (or 1.9.0 + the 011 delta when 011 Phase 0 part 2 has not run yet; the 011 architect resolves `NotFound`/`ValidationProblem`) + 012 unchanged; `check_superset.py` against 1.10.0 (`superset: yes`) and Redocly lint; full `contracts/ui-test-ids.md` 1.11.0 (1.10.0 + `ui-test-ids.delta.md`); merge a 2.2.0 engine contract of 011 into `audio-engine.md` if it exists; DELTA.md "Phase 0 outcome". **Done**: merged onto the full 1.21.0 on `main` instead (011 follow-up shipped first); labels per the ROADMAP rule added in Phase 0
- [x] T004 [P] [architect] .NET contract code: `src/StemStudio.Contracts/Mixes/{PracticeSettingsDto,SpeedTrainerSettingsDto,CountInSettingsDto}.cs` (records with `Default`), `MixSettingsDto.Practice { get; init; } = PracticeSettingsDto.Default`; skeleton contract tests `tests/StemStudio.Api.Tests/Contract/PracticeContractTests.cs` (JSON shape and defaults against the 012 document); test project contract link -> `specs/012-practice-tempo/contracts` (agreed one-time edit) (FR-014)
- [x] T005 [P] [architect] `src/audio-engine/src/types.ts` 2.3.0 additions (audio-engine.md "Practice tempo": types, `setPractice`, `getPracticeStatus`, `OnPracticeChanged`, `EngineOptions.timeStretch`/`timeStretchFactory`, `TimeStretchStage`), `ENGINE_CONTRACT_VERSION` "2.3.0" in `index.ts`; engine stubs that reject/throw "not implemented" until T034; skeleton test for version and stub presence
- [x] T006 [P] [architect] `third_party/soundtouch/` skeleton: `README.md` (what the module is, ABI v1, how to rebuild and replace `soundtouch.wasm` - LGPL-2.1 section 6), `NOTICE.md`, `COPYING.TXT` (LGPL-2.1 text from the upstream tarball), `include/stemstudio_soundtouch.h` (ABI v1 of audio-engine.md), `.gitignore` for `upstream/` and build output; `sonar-project.properties` exclusion of `third_party/soundtouch/upstream/**` requested from quality-gate (T009)
- [x] T007 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, engine `npm test`/lint/tsc, web-interop tests, `tools/quality/run_all.py` 0 errors, Redocly lint; hand-off `docs/handoffs/architect-012-phase0.md` (dependencies: SoundTouch 2.4.1 LGPL-2.1, emsdk 6.0.13 build tool)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [x] T008 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `stretch/processor.ts` imports only `stretch/{constants,params,fifo,abi}`; `transport.ts`, `mixer.ts`, `metronome/**`, `karaoke/**` do not import `practice/**` or `stretch/**`; `practice/**` does not import `karaoke/**`; C++ under `third_party/soundtouch/shim/` is measured for unit size (<= 30 lines) and `third_party/soundtouch/upstream/**` is ignored
- [x] T009 [quality-gate] Implement the rules in `tools/quality/dependency_check.py` (+ the C++ unit-size scan in the SIG checker), Sonar exclusions; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [x] T010 [P] [devops] `scripts/build-soundtouch.{sh,ps1}`: read `third_party/soundtouch/soundtouch.lock`, download the tarball, verify SHA-256, run `emcc` in `EMSDK_IMAGE@EMSDK_DIGEST` of the lock (emsdk 6.0.13) with `EMCC_FLAGS` and `SOUNDTOUCH_SOURCES` of the lock (em++, plus `-ffile-prefix-map` for the source and shim directories) over upstream + `shim/`, write `src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm` (git-ignored); `docker/web.Dockerfile`: emsdk stage, copy the WASM and `/third-party/soundtouch/` (COPYING.TXT, NOTICE.md, tarball, shim, build script) into the web root; static serving of `.wasm` as `application/wasm` confirmed (FR-006) **Done** (devops, 60bff731 + c5119c66): bit-identical module `07c00b3a...819c` (30,090 B, stand-in shim) from the script and from two no-cache image builds; `--network=none` compile stage; `/third-party/soundtouch/` with SHA256SUMS + index.html
- [x] T011 [P] [devops] CI (`.github/workflows/*`): build the WASM once per run (cache keyed by the lock file), build it twice and compare SHA-256 (reproducibility), run `npm run test:wasm` after the build; `scripts/test-all.*` run `test:wasm` when the module exists; CI `CONTRACTS_DIR` -> `specs/012-practice-tempo/contracts` for the backend contract tests after T003 **Done** (devops, 176a5b77): CI job `soundtouch`; `CONTRACTS_DIR` kept at 008 (012 has no worker document; the .NET contract tests link the 012 document in their csproj), the 012 document is linted via `PRACTICE_CONTRACTS` + `check_superset.py` instead
- [x] T012 [P] [qa] `tests/fixtures/generate_fixtures.py` + manifest: `tone_440hz_20s.flac` (stereo sine, -12 dBFS, CC0 self-generated); `e2e/support/output-probe.ts` (init script tapping every connection to `destination` into an `AnalyserNode`; FFT peak helper) and `e2e/support/click-probe.ts` (records click `start(when)` times); self-tests of both probes on a page with known oscillators (like `audio-check.spec.ts`); `docs/testing/TEST-STRATEGY.md` traceability US1..US3 and the six instrument families (SC-008)

---

## Phase 2: Foundational (blocking inside each owner)

### audio-engine (tests first)

- [x] T013 [P] [audio-engine] Failing tests `tests/stretch/params.test.ts`: pitch factor `2^(st/12) / (speed/100)` for the corners (50 % +12 -> 4.0, 120 % -12 -> 0.4167), neutral detection, `CHANGE_LEAD_SECONDS`, `STRETCH_DELAY_FRAMES` multiple of 128 and <= 10,112 (FR-001, FR-002)
- [x] T014 [audio-engine] Implement `src/stretch/{constants,params}.ts` to pass T013
- [x] T015 [P] [audio-engine] Failing tests `tests/stretch/fifo.test.ts`: fixed delay D (input frame n appears at output n + D with a jittering producer), underrun -> silence + counter, overflow above D + 1024 -> oldest surplus dropped + counter, `reset()` refills D frames of silence, stereo interleave/deinterleave, no allocation per call (FR-003, SC-001)
- [x] T016 [audio-engine] Implement `src/stretch/fifo.ts` to pass T015
- [x] T017 [P] [audio-engine] Failing tests `tests/stretch/processor.test.ts` with a fake `AudioWorkletGlobalScope` (`registerProcessor`, `currentFrame`, `sampleRate`) and a fake ABI: silence until `ready`; `ready` posts `latencyFrames`; `{type:'pitch', value, atFrame}` applied from that input frame (also inside a block); `reset`; `dispose` frees the handle; instantiate error -> `{type:'error'}`; `stats` at most 4 Hz; no `st_malloc` after ready; settings of research.md R-04 sent through `st_set_setting` (FR-006)
- [x] T018 [audio-engine] Implement `src/stretch/{abi,processor}.ts` and the second Vite entry `time-stretch-processor.js` to pass T017
- [x] T019 [P] [audio-engine] Failing Node suite `tests-wasm/soundtouch.test.ts` (`npm run test:wasm`, skipped with a clear message when the module is missing): ABI version 1; 440 Hz sine at pitch factors 0.5/1/2 -> FFT peak within 1 %; factor 1 identity after the delay (max error < 1e-4); initial latency <= `STRETCH_DELAY_FRAMES - 256` over the factor range; >= 10x real time (SC-002, SC-004)
- [x] T020 [audio-engine] Implement `third_party/soundtouch/shim/stemstudio_soundtouch.cpp` (ABI v1 over `soundtouch::SoundTouch`, every function <= 15 lines) to pass T019 with the T010 build
- [x] T021 [P] [audio-engine] Failing tests `tests/transport-rate.test.ts`, `tests/metronome-rate.test.ts`: anchor `{when, offset, rate}`, `positionAt = offset + (t - when) * rate` wrapped into the loop; sources start at `when - latency` with `when >= now + latency + startDelay`; `addBuffers` on a rate/latency anchor; native loop with `playbackRate`; timeline maps clicks with `/ rate`; every existing transport, loop, metronome, groups and karaoke test stays green unchanged (rate 1, latency 0) (FR-004, FR-005, FR-007)
- [x] T022 [audio-engine] Implement the rate-aware anchor in `src/transport.ts` and `src/metronome/timeline.ts` to pass T021
- [x] T023 [P] [audio-engine] Failing tests `tests/stretch/worklet-stage.test.ts`: `addModule(processorUrl)` once per engine, `fetch(wasmUrl)` once, bytes in `processorOptions.wasmBytes`, resolves on `ready` with `latencyFrames`, rejects with `Time stretch unavailable: <reason>` (no `audioWorklet`, 404, instantiate error, processor error), `setPitch`/`reset`/`underruns`/`dispose` over the port; nothing requested while only neutral settings were set (FR-007, FR-009, SC-007)
- [x] T024 [audio-engine] Implement `src/stretch/{worklet-stage,fake-stage}.ts` to pass T023

### backend (tests first)

- [x] T025 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Mixes/PracticeSettingsTests.cs`: defaults, ranges and integer rules, `startPercent < targetPercent` (path `practice.trainer.targetPercent`), trainer/everyPass without a loop valid, `MixSettings.Validate` nests `practice` (FR-014)
- [x] T026 [backend] Implement `Domain/Mixes/{PracticeSettings,SpeedTrainerSettings,CountInSettings}.cs` and `MixSettings.Practice` to pass T025
- [x] T027 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/MixPracticePersistenceTests.cs`: JSON round trip of `Practice` (nested trainer/count-in), `NULL` reads as defaults, a database at the 010 schema migrates and its mixes load with defaults, duplicate copies `Practice`
- [x] T028 [backend] Implement the `MixConfiguration` complex property and migration `AddMixPractice` to pass T027

### frontend (tests first)

- [x] T029 [P] [frontend] Failing tests: `IAudioEngine.SetPracticeAsync(PracticeSettingsDto)`, `GetPracticeStatusAsync()`, `[JSInvokable] OnPracticeChanged(PracticeStatus)` raising an event on the UI thread; `JSException` "Time stretch unavailable" mapped to an unavailable status (no crash); `PracticeStatus` record mirrors the engine JSON (FR-009)
- [x] T030 [frontend] Implement `Audio/{IAudioEngine,AudioEngineInterop,EngineMethods,PracticeStatus}.cs` additions to pass T029
- [x] T031 [P] [frontend] Failing tests `MixerStatePracticeTests`: load from `MixDto` (old mix -> defaults), dirty tracking, DTO mapping both ways, neutral settings -> no `SetPracticeAsync` on open, coalescing to at most one engine call per 50 ms while dragging (UI-side throttle only, not audible timing), `effectiveBpm` and `qualityHint` rules of data-model 3 (FR-008, FR-014, SC-007)
- [x] T032 [frontend] Implement `Mixer/{MixerState.Practice.cs,PracticeRules.cs}` to pass T031

**Checkpoint**: HO:foundational for audio-engine, backend, frontend (incl. nothing for web-interop yet).

---

## Phase 3: User Story 1 - Slow down or transpose (P1) MVP

**Goal**: live speed 50-120 % and transpose +-12 for the whole mix, saved in the mix.
**Independent test**: quickstart P3-P7, P10-P12, P14.

- [x] T033 [P] [US1] [audio-engine] Failing tests `tests/practice/controller.test.ts` (fake stage): validation errors (`Invalid practice settings: ...`) leave everything unchanged; first non-neutral call while playing -> `loading` -> engage -> restart at the heard position with latency compensation; live change -> `playbackRate.setValueAtTime(r, T - L)` with the same `T` on every source incl. detail sources, `setPitch(p, frame(T - L))`, re-anchor at `T`; changes before `T` replace the pending one; return to neutral while engaged -> pitch 1, no restart; next restart at neutral -> bypass; stage failure -> `unavailable`, speed 100/0, count-in kept, playback continues; metronome reschedules with the new rate; karaoke `positionMs` follows the rate (no change to karaoke code); `OnPracticeChanged` <= 10 Hz; `getPracticeStatus()` snapshot; sample lock: all sources same start, offset, rate times after 20 changes and 50 loop wraps (FR-001..FR-005, FR-007, FR-009, FR-018, SC-001, SC-003)
- [x] T034 [US1] [audio-engine] Implement `src/practice/{settings,tempo,controller}.ts` and `Engine.setPractice`/`getPracticeStatus`, `callbacks.ts` (`OnPracticeChanged`) to pass T033
- [x] T035 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Mixes/MixPracticeEndpointTests.cs`: `POST`/`PUT` with `practice` round trip; absent -> defaults; `GET` of a pre-012 mix returns defaults; 400 validation problem paths; duplicate keeps `practice`; `MixSummary` unchanged; export requests and render plans identical with and without practice (ADR-016 vectors unchanged); contract test against the 1.11.0 document; the 1.21.0-era tests that round-trip `MixSettings`/`Mix` examples keep passing because serialized DTOs carry `practice` with defaults (FR-014, FR-015, SC-007)
- [x] T036 [US1] [backend] Implement `Api/Mixes/{MixMapper,MixValidator}.cs` additions to pass T035
- [x] T037 [P] [US1] [frontend] Failing bUnit tests `PracticePanelTests`, `SpeedControlTests`, `TransposeControlTests`: ids and attributes of ui-test-ids 1.11.0 (`practice-panel` `data-stretch`/`data-speed`/`data-transpose`, slider keys, `aria-valuetext` "75 percent" / "+2 semitones", buttons +-5 % / +-1, `practice-reset` disabled at neutral, `practice-speed-reset`), `practice-effective-bpm` only with a tempo, `practice-quality-hint` thresholds, `practice-unavailable` text and disabled controls, Expert number inputs and `practice-stretch-status`, level visibility (Beginner list of the delta), `data-help-id` markers, 004 tokens only (FR-001, FR-002, FR-008, FR-009, FR-016, FR-017)
- [x] T038 [US1] [frontend] Implement `Components/Mixer/Practice/{PracticePanel,SpeedControl,TransposeControl}.razor(.cs)`, `Mixer.Practice.cs` (wiring to `MixerState` and the engine), `FeatureCatalog` entries (`practice.speed-reset`, `practice.numeric`, `practice.stretch-status`), styles to pass T037
- [x] T039 [P] [US1] [frontend] Failing tests: save/save-as/load include `practice`; `unsaved-indicator` after a practice change; `export-practice-note` in the export dialog only when not neutral; the export request is unchanged (FR-014, FR-015)
- [x] T040 [US1] [frontend] Implement in `Mixer.Save.cs`, `Mixer.Export.cs` and the export dialog to pass T039
- [x] T041 [P] [US1] [frontend] Failing help tests (existing 004 suite extended): topics `practice-speed`, `practice-transpose`, `practice-sound-quality`, glossary `semitone` exist with valid front matter, anchors present in the rendered mixer, related links valid, tour `mixer-basics` v2 step targets `practice-speed` (FR-016)
- [x] T042 [US1] [frontend] Write the help topics (incl. the LGPL link to `/third-party/soundtouch/` and the limits: smearing below 70 %, no formant correction, meter lead, exports unchanged), related links in `mixer-screen`/`export-dialog`, tour step, to pass T041
- [x] T043 [P] [US1] [web-interop] Failing Vitest tests `src/web-interop/tests/shortcuts-practice.test.ts`: `-`, `=`, `,`, `.`, `r`, `c`, `t` -> `mixer.speed:-5`, `mixer.speed:5`, `mixer.transpose:-1`, `mixer.transpose:1`, `mixer.practice.reset`, `mixer.countin.toggle`, `mixer.trainer.toggle`; ignored in typing contexts/dialogs and with Ctrl/Alt/Meta; `G then T` still `go.tracks` and does not toggle the trainer (FR-017)
- [x] T044 [US1] [web-interop] Implement the bindings in `src/web-interop/src/shortcuts.ts` (+ shortcut overlay data "Practice") to pass T043
- [x] T045 [P] [US1] [frontend] Failing tests `MixerShortcutsPracticeTests`: the speed/transpose/reset commands call the same methods as the buttons (clamped), are ignored while `data-stretch="unavailable"`; palette actions "Original speed and key", "Speed +5 %", "Speed -5 %" (FR-017)
- [x] T046 [US1] [frontend] Implement in `Mixer.Shortcuts.cs` to pass T045
- [x] T047 [US1] [qa] Author `e2e/tests/practice-tempo.spec.ts` (P3 speed, P4 pitch with the output probe, P5 live changes + underruns, P6 neutral / zero module requests, P7 karaoke at 50/75/120 % and click spacing, P10 persistence, P11 unavailable via route 404 and without `AudioWorkletNode`), `practice-families.spec.ts` (P14, one case per instrument family) and the `export.spec.ts` addition (P12) (SC-001, SC-002, SC-003, SC-007, SC-008)

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Speed trainer (P2)

**Goal**: the loop starts slow and speeds up per pass to a target.
**Independent test**: quickstart P8.

- [x] T048 [P] [US2] [audio-engine] Failing tests `tests/practice/trainer.test.ts` (pure state machine, data-model 3.2): pass 1 at start; step after every `passesPerStep` completed passes; cap at target -> `target`; pause keeps; stop/loadTrack/loop change/trainer field change reset; disabled -> manual speed; `off` without loop or loop < 0.5 s (FR-010)
- [x] T049 [US2] [audio-engine] Implement `src/practice/trainer.ts` to pass T048
- [x] T050 [P] [US2] [audio-engine] Failing tests `tests/practice/passes.test.ts`: explicit passes only under the conditions of audio-engine.md; pass k sources `start(T_k - L, A, B - A)` with one shared time/rate; next pass created by the look-ahead >= 0.3 s ahead at `T_k + (B - A)/r_k`; no `stage.reset()` between gapless passes, reset after a gap; first pass from the current position inside `[A, B)`; detail group decoded mid-pass starts on the pass anchor; leaving the conditions -> native loop at the next boundary; `positionAt` over the anchor queue; trainer speed applied exactly at `T_{k+1}`; `OnPracticeChanged` per pass; transpose live during a pass (FR-010, FR-011, FR-018, SC-006)
- [x] T051 [US2] [audio-engine] Implement `src/practice/passes.ts` and the controller wiring to pass T050
- [x] T052 [P] [US2] [frontend] Failing bUnit tests `SpeedTrainerPanelTests`: `trainer-*` ids, disabled without a loop (`trainer-needs-loop` texts incl. the 0.5 s rule) or with the stretch unavailable, `trainer-error` for target <= start, `trainer-status` text/attributes and polite announcement only on speed steps, speed control read-only while running, Advanced/Expert only, `mixer.trainer.toggle` command, help topic `practice-speed-trainer` + tour `mixer-advanced` v2 step (FR-010, FR-016, FR-017)
- [x] T053 [US2] [frontend] Implement `Components/Mixer/Practice/SpeedTrainerPanel.razor(.cs)`, `FeatureCatalog` `practice.trainer`, help topic and tour step, the trainer command, to pass T052
- [x] T054 [US2] [qa] Author `e2e/tests/practice-trainer.spec.ts` (P8: speed sequence 60, 70, 80, 80; pause keeps, stop resets; no loop -> needs-loop; saved settings restored) (SC-006)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Count-in (P3)

**Goal**: 1-2 bars of clicks before play and optionally before every loop pass.
**Independent test**: quickstart P9.

- [x] T055 [P] [US3] [audio-engine] Failing tests `tests/practice/count-in.test.ts` (pure plan, data-model 3.3): click times `T0 + (b - A)/r - k*d/r`, accents per counted bar, bpmOverride vs grid interval, `A` on a beat and between beats, beatsPerBar 3/4/7, rate 0.5/1.2, unavailable without a usable tempo, `bars = 0` (FR-012, FR-013, SC-005)
- [x] T056 [US3] [audio-engine] Implement `src/practice/count-in.ts` to pass T055
- [x] T057 [P] [US3] [audio-engine] Failing tests `tests/practice/count-in-engine.test.ts`: `play()` with a count-in -> clicks via the metronome output at the metronome volume (also with the metronome off), sources start at `T0`, `getPosition()` = A until `T0`, music enters exactly one beat after the last click (mock clock, 1 ms), indicator beats through `OnPracticeChanged`; pause/stop/seek cancel future clicks; seeks while playing never count in; `everyPass` gaps of N beats at the next pass's rate with position A in the gap; with the stretch unavailable the count-in still works (FR-012, FR-013, FR-018, SC-005)
- [x] T058 [US3] [audio-engine] Implement the count-in in the controller and the pass scheduler to pass T057
- [x] T059 [P] [US3] [frontend] Failing bUnit tests `CountInControlTests`, `CountInIndicatorTests`: `countin-toggle` (Beginner on = 1 bar, Advanced restores the last bar count), `countin-bars`, `countin-every-pass` disabled without a loop, `countin-unavailable` text, `countin-indicator` with `countin-dot-<n>` states, announcements "Count-in" / "Go" only, no pulse with reduced motion, `mixer.countin.toggle` command, help topics `practice-count-in` and glossary `count-in` (FR-012, FR-013, FR-016, FR-017)
- [x] T060 [US3] [frontend] Implement `Components/Mixer/Practice/{CountInControl,CountInIndicator}.razor(.cs)`, `FeatureCatalog` `practice.count-in.options`, help topics, related link from `mixer-metronome`, the command, to pass T059
- [x] T061 [US3] [qa] Author `e2e/tests/practice-count-in.spec.ts` (P9: 1 bar at 120 BPM = 2.0 s +- 50 ms with the click probe; 2 bars + every pass at 50 % on a 4 s loop; unavailable without a grid; pause cancels) (SC-005)

**Checkpoint**: HO:us3.

---

## Phase 6: Polish (inside the wave, per owner)

- [x] T062 [P] [audio-engine] Refactor to SIG limits, TSDoc for every export, `npm run test:coverage` >= 90 % (lines, branches, functions, statements) incl. `stretch/` and `practice/`, `npm run test:wasm` output, HO:polish (dependencies: none new at run time; build tool via devops)
- [x] T063 [P] [backend] Refactor, XML docs, Domain/Persistence/Api coverage; if 011 is merged: test that the agent's mix-update tool round-trips `practice` (otherwise a request in the hand-off for the 011 owner); HO:polish
- [x] T064 [P] [frontend] Accessibility pass (keyboard order through the practice panel, focus visible, `aria-valuetext`, live regions limited to trainer steps and count-in start/end, contrast >= 4.5:1 in both themes, reduced motion), help review (`reviewed: "012"`), bUnit coverage of `StemStudio.Web` >= 90 %, HO:polish incl. *Docs impact* (user manual: practice tempo section; technical manual: time-stretch architecture, LGPL module, build step, CSP note)
- [x] T065 [P] [web-interop] Coverage of `src/web-interop` >= 90 %, lint/tsc clean, shortcut overlay text reviewed; reported in the frontend HO:polish
- [x] T066 [P] [qa] `e2e/tests/practice-levels.spec.ts` (P13 level visibility at Beginner/Advanced/Expert), axe a11y suite and visual baselines of the mixer with the practice panel (both themes, regenerated through the documented baseline flow), traceability matrix (FR/SC -> tests, six instrument families), HO:polish
- [x] T067 [P] [devops] Validate the web image: `/third-party/soundtouch/` complete and served, `soundtouch.wasm` SHA-256 equal across two builds, `application/wasm` content type, non-Docker path documented in the script help; HO:polish with *Docs impact* (setup chapter: build-soundtouch scripts) **Done** (devops, 4d002d3e): `scripts/check-soundtouch-served.{sh,ps1}`, run in the CI e2e job; validated against a locally built image with a stand-in shim (real shim: T020)
- [x] T068 [P] [quality-gate] `run_all.py` on the whole repo (incl. the C++ shim scan and the new dependency rules), refactor requests per owner, HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [x] T069 [CTO] Merge the owner branches; full build, lint, unit + coverage gates, `npm run test:wasm`, contract tests against backend-api 1.11.0 green **Done** (CTO, integration): six lanes merged `--no-ff`; Release build 0 warnings, `dotnet format` clean, all coverage gates met, `test:wasm` 26/26 against the real module (`SOUNDTOUCH_WASM_REQUIRED=1`), 012 contract lint + superset green (`docs/handoffs/cto-012-integration.md`)
- [x] T070 [CTO] Playwright default suite + `practice-*` specs + `karaoke`/`export`/`mixer` regressions on the fake stack (retries 0), Chromium and Firefox **Done** (CTO): own compose project `e2e012` on 18080/18081, retries 0, Chromium + Firefox: default suite 495 passed / 115 skipped / 0 failed (incl. 112 `@practice`), agent fake 22/22 and none 2/2, visual 116 passed (28 baselines regenerated and reviewed), a11y 151/154 (open: target-size under the sticky dock, see STATUS); fixes recorded in `docs/handoffs/cto-012-integration.md`
- [ ] T071 [CTO] Reference-machine validation (quickstart R1-R3): 20 channels at 50 % / +12 st for 10 min without underruns, CPU budget, listening notes at 85/70/50 % and +-7 st with vocals/guitar/drums soloed, LGPL bundle check incl. replacing the module with a rebuild; record in `docs/STATUS.md` **Partly done** (CTO): Phase 0 P0-2 (20 channels, 10 min, 0 underruns, Chromium + Firefox) stands; LGPL bundle served complete and the image module equals the script build; listening notes and replacing the module by hand are left to the owner
- [x] T072 [CTO] `docs/THIRD-PARTY-LICENSES.md`: SoundTouch 2.4.1 (LGPL-2.1, surina.net release URL (Codeberg tag 2.4.1 identical), tarball SHA-256, "separate replaceable WASM module; source shipped at /third-party/soundtouch/"), Emscripten SDK 6.0.13 (build tool, MIT/NCSA, not distributed) **Done** (CTO): SoundTouch 2.4.1 and the shim (LGPL-2.1-or-later), emsdk 6.0.13 (build-time), actions/download-artifact 8.0.2 and PyYAML 6.0.3 (CI/test-all)
- [x] T073 [CTO] README: practice tempo (speed, transpose, trainer, count-in), shortcuts, quality limits, exports keep the original speed and key, LGPL module note and the build-soundtouch step for the non-Docker path **Done** (CTO): README feature bullet, "Practice tempo" section, known limitation and licence note; website moved 012 out of "Coming soon"
- [x] T074 [CTO] `/speckit-analyze` consistency pass; `docs/STATUS.md`; mark M1-M3 delivered in `docs/MoSCoW.md`; backlog note: agent tool catalogue minor for practice tools (011), "export at practice tempo" with 015; PR **Done** (CTO): analyze pass (all 18 FR / 8 SC mapped, 0 critical, sign and URL text fixed), STATUS, MoSCoW M1-M3 delivered, backlog notes (agent practice tools; export at practice tempo with 015); agent tool catalogue 1.3.0 shows `practice` read-only. PR left to the coordinator
- [x] T075 [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1, last step of the integration wave, after `docs/STATUS.md` is updated): user manual (mixer: practice tempo, speed trainer, count-in, limits) and technical manual (architecture of the stretch stage, latency compensation, LGPL module, build step, CSP note), run `cd tools/docs && npm ci && npm run browser && npm run build`, commit the sources, both PDFs and `docs/public/build-manifest.json` together; `node tools/docs/check.mjs` passes **Done** (CTO): user manual (mixer: practice tempo, trainer, count-in, limits; shortcuts; levels; export; FAQ) and technical manual (architecture, latency compensation, LGPL module, build step, CSP note, API, data model, quality, operations); PDFs rebuilt; `node tools/docs/check.mjs` passes

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding; `[P]` pairs of one owner may be interleaved only as test-then-implementation.

| Lane | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| architect | T001 -> T002 -> T003 -> T004 -> T005 -> T006 -> T007 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files, `types.ts`, `StemStudio.Contracts/**`, `third_party/soundtouch/` (except `shim/`) | scratch container / page for the spikes |
| audio-engine | T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022 -> T023 -> T024 -> HO:foundational -> T033 -> T034 -> HO:us1 -> T048 -> T049 -> T050 -> T051 -> HO:us2 -> T055 -> T056 -> T057 -> T058 -> HO:us3 -> T062 (HO:polish) | `src/audio-engine/**` except `types.ts`; `third_party/soundtouch/shim/**` | mocked `AudioContext`/`AudioParam`/`audioWorklet`, fake stage, fake worklet scope + fake ABI; T020 needs the T010 script (or a local emsdk) |
| backend | T025 -> T026 -> T027 -> T028 -> HO:foundational -> T035 -> T036 -> HO:us1 -> T063 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | SQLite, WebApplicationFactory, stub worker |
| frontend | T029 -> T030 -> T031 -> T032 -> HO:foundational -> T037 -> T038 -> T039 -> T040 -> T041 -> T042 -> T045 -> T046 -> HO:us1 -> T052 -> T053 -> HO:us2 -> T059 -> T060 -> HO:us3 -> T064 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, fake `IAudioEngine`, bUnit JS interop mock |
| web-interop (frontend agent) | T043 -> T044 (in US1) -> T065 (polish) | `src/web-interop/**` | jsdom keyboard events |
| qa | T012 -> T047 -> HO:us1 -> T054 -> HO:us2 -> T061 -> HO:us3 -> T066 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | fake stack; specs executed in T070 |
| devops | T010 -> T011 -> HO:foundational -> T067 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | locally built web image |
| quality-gate | T008 -> T009 -> HO:foundational -> T068 (HO:polish) | `tools/quality/**`, `docs/quality/**`, `sonar-project.properties` | sample files |
| CTO | T069 -> T070 -> T071 -> T072 -> T073 -> T074 -> T075 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, `docs/MoSCoW.md`, `docs/public/**`, merges | n/a |

**Task count per lane**: architect 7, audio-engine 23, backend 7, frontend 17, web-interop 3,
qa 5, devops 3, quality-gate 3, CTO 7 (total **75**: T001-T075). Test-first pairs: audio-engine 11,
backend 3, frontend 8, web-interop 1, quality-gate 1.

### Parallelism

- Phase 0: T004, T005, T006 in parallel after T001 (T003 needs no spike; T007 last).
- Wave: all lanes in parallel. Inside audio-engine, the pairs T013/T014, T015/T016, T021/T022 do not
  depend on each other; T017/T018 needs T014 and T016; T019/T020 needs T010 (devops) for the build;
  T023/T024 needs T018. Frontend T037-T046 depend only on T030/T032; web-interop T043/T044 is
  independent of everything but the delta's command ids.
- US2 and US3 engine work both extend the controller; run them in the listed order inside the
  audio-engine lane (T058 builds on T051's pass scheduler).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T004 contract test link switch | backend test project file | one-time edit by the architect, recorded in the hand-off |
| T005 `types.ts` | audio-engine package | architect-owned per the skeleton rule (as in 001/002/006) |
| T006 Sonar exclusion | quality-gate file | requested from quality-gate, done in T009 |
| T020 shim build | devops script | audio-engine uses T010's script; until it exists, a local emsdk with the same flags |
| T063 agent tool round trip | 011 agent code | test only if 011 is merged; otherwise a hand-off request to the 011 owner |
| T072-T075 licences, README, manuals, MoSCoW | CTO-owned files | consolidated by the CTO (AGENT-PROTOCOL rules 9-11) |

## Dependencies & Execution Order

Phase 0 (a negative T001 result switches to the soundtouchjs fallback and amends plan/tasks before
the wave) -> wave (Setup + Foundational + stories per lane, parallel across lanes) -> Integration
wave. US1 is the MVP and can ship alone; US2 needs US1's controller (speed changes) and explicit
passes; US3 needs only the metronome output and, for "every pass", US2's pass scheduler.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component (engine, web-interop, API, Web), exact numbers in the hand-offs; `npm run test:wasm` green on CI (I)
- [ ] Stretch stage, worklet scope and WASM behind ports/fakes in unit tests; real module only in the Node suite and E2E (I)
- [ ] SIG limits respected incl. the C++ shim; `run_all.py` passes with the new dependency rules (II)
- [ ] Implementations match backend-api 1.11.0, audio-engine 2.3.0, ui-test-ids 1.11.0 and WASM ABI v1; `check_superset.py` says `superset: yes` against 1.21.0 (Phase 0: the base on `main`) (III)
- [ ] SoundTouch unmodified, pinned by SHA-256, loaded as a separate replaceable module; licence, source archive, shim and build script in every binary distribution; recorded in THIRD-PARTY-LICENSES (IV)
- [ ] Neutral settings load nothing and behave as 2.1.0; audible timing only on the audio clock; quality limits shown in the UI and help (V)
- [ ] No secrets; analyzers/formatters/linters clean; public APIs documented (XML docs, TSDoc)
- [ ] Help topics `practice-speed`, `practice-transpose`, `practice-count-in`, `practice-speed-trainer`, `practice-sound-quality`, glossary `semitone`, `count-in` present (004 framework); tours updated
- [ ] One E2E case per instrument family (MoSCoW principle, SC-008)
- [ ] docs/public user and technical manuals updated and the PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
