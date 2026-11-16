# Tasks: Mixer Effects and Controllers (017)

**Input**: spec.md, plan.md, research.md, data-model.md, DELTA.md, contracts/, ADR-026.
**Tests**: mandatory (constitution I). Every implementation task is preceded by its failing test
task (Red -> Green -> Refactor); a lane shows the red run in its hand-off note.

Format: `- [ ] T### [P] [US#] (lane) description - files`. `[P]` = can run in parallel with the
other `[P]` tasks of the same phase (different files, no dependency on an unfinished task).
Lanes: `architect`, `audio-engine`, `backend`, `frontend`, `stem-separation`, `qa`, `devops`,
`quality-gate`, `cto`. Each lane ends each phase it worked in with
`docs/handoffs/<lane>-017-<phase>.md` (coverage >= 90 % line and branch, exact numbers).

## Phase 0: Contracts and spikes (architect; blocks everything)

- [ ] T001 (architect) Accept ADR-026 (status, renumber if 012-016 took 026) - `docs/decisions/ADR-026-*.md`
- [ ] T002 (architect) Full `backend-api.openapi.yaml` 1.16.0 = current 1.15.0 + `backend-api.delta.openapi.yaml` + DELTA.md section 2 (property additions, problem types, examples, changelog); Redocly 2.78.0 clean (warnings justified) - `specs/017-effects-controllers/contracts/backend-api.openapi.yaml`, `redocly.yaml`
- [ ] T003 [P] (architect) Engine contract 2.7.0: `contracts/audio-engine.md` (2.6.0 + delta) and `src/audio-engine/src/types.ts` types, `EngineContractVersion = '2.7.0'`, `index.ts` exports `createMidiController` stub; `tsc --noEmit`, `npm test` green - `src/audio-engine/src/{types.ts,index.ts,midi/controller.ts}`
- [ ] T004 [P] (architect) `contracts/ui-test-ids.md` 1.16.0 = 1.15.0 + delta, rule R10 - `specs/017-effects-controllers/contracts/ui-test-ids.md`
- [ ] T005 [P] (architect) Contract DTO stubs with XML docs: `ChannelEffectsDto`, `EqDto`, bands, `CompressorDto`, `MixEffectsDto`, `ReverbBusDto`, `PrerenderDto`, `ControllerProfileDto`/`InputDto`, `ControllerBindingDto`, `MidiControlDto`, `ControllerTargetDto`, enums; `ExportEffects` on export DTOs; `ContractLimits` constants - `src/StemStudio.Contracts/{Mixes,Controllers,Assets}/`
- [ ] T006 [P] (architect) Domain record stubs (no rules yet) and `Prerender`, `ControllerProfile` entities - `src/StemStudio.Domain/{Mixes/Effects,Controllers,Assets}/`
- [ ] T007 (architect) Spikes, results into research.md: (a) EF Core 10 optional complex type inside JSON collection loads a 1.15.0-shaped mix row (else switch data-model 2.5 to `JsonColumn<T>`); (b) IR generation time per kind; (c) worst-case offline render 30 s time in headless Chromium - `specs/017-effects-controllers/research.md`
- [ ] T008 (architect) Hand-off with `dotnet build` (0 warnings), `npm test`, Redocly output - `docs/handoffs/architect-017-phase0.md`

## Phase 1: Foundational (blocks the user stories)

- [ ] T010 [P] (audio-engine) Test: strip chain topology with mocked nodes - dry path is identity, inserts pre-fader in order EQ -> compressor -> make-up, send tap post-panner, meter tap unchanged; existing mixer tests still pass - `src/audio-engine/tests/effects/strip-chain.test.ts`, `tests/fakes/fake-audio-nodes.ts`
- [ ] T011 (audio-engine) Implement `buildStripChain` and wire it into `Channel`/`Mixer` (no behaviour change without effects) - `src/audio-engine/src/effects/strip-chain.ts`, `channel.ts`, `mixer.ts`
- [ ] T012 [P] (audio-engine) Test: effects input validation/clamping and error messages - `tests/effects/validate.test.ts`
- [ ] T013 (audio-engine) Implement - `src/audio-engine/src/effects/validate.ts`
- [ ] T014 [P] (audio-engine) Test: `EqSettings` -> biquad sections (types, Q, cascade for 24 dB, HP/LP only when enabled) - `tests/effects/eq.test.ts`
- [ ] T015 (audio-engine) Implement - `src/audio-engine/src/effects/eq.ts`
- [ ] T016 [P] (audio-engine) Test: compressor mapping (ms -> s, make-up dB -> gain) - `tests/effects/compressor.test.ts`
- [ ] T017 (audio-engine) Implement - `src/audio-engine/src/effects/compressor.ts`
- [ ] T018 [P] (backend) Test: domain validation of `ChannelEffects`, `Eq`, `Compressor`, `MixEffects` (every range, slope, preset id pattern, readable messages, JSON paths) - `tests/StemStudio.Domain.Tests/Mixes/Effects/*Tests.cs`
- [ ] T019 (backend) Implement domain rules; hook into `MixSettings.Validate` for channels, detail channels and mix effects - `src/StemStudio.Domain/Mixes/Effects/*.cs`, `Mixes/MixSettings.cs`
- [ ] T020 [P] (backend) Test: persistence round trip with effects; a mix row written in the 1.15.0 shape (no `Effects` members, no column value) loads with `Effects == null` - `tests/StemStudio.Persistence.Tests/MixEffectsPersistenceTests.cs`
- [ ] T021 [P] (stem-separation) Test only: a render job with one input `assets/<uuid>/prerender.wav` (float32 stereo 44.1 kHz) at gain 1, pan 0, master 1 produces FLAC-24/WAV-24 equal to the input within 1 LSB and MP3 of the right length; clip protection applies to peaks > 1.0 - `worker/tests/render/test_prerender_input.py`
- [ ] T022 (backend) Implement persistence: `MixConfiguration` optional nested complex types, `Mixes.Effects` column, `PrerenderConfiguration`, `ControllerProfileConfiguration`, migration `AddEffectsAndControllers` - `src/StemStudio.Persistence/**`
- [ ] T023 [P] (backend) Test: mix endpoints accept/return `effects`, refuse invalid values with 422 paths, `effects` absent stays absent in responses - `tests/StemStudio.Api.Tests/Mixes/MixEffectsEndpointTests.cs`
- [ ] T024 (backend) Implement mapper/validator changes - `src/StemStudio.Api/Mixes/{MixMapper,MixValidator}.cs`
- [ ] T025 [P] (frontend) Test: `IAudioEngine` wrapper calls for the 2.7.0 methods (names, argument shapes, camelCase) - `tests/StemStudio.Web.Tests/Interop/AudioEngineEffectsInteropTests.cs`
- [ ] T026 (frontend) Implement wrappers - `src/StemStudio.Web/Interop/{IAudioEngine,AudioEngineInterop}.cs`
- [ ] T027 [P] (frontend) Test: `EffectsState` (per-strip effects, mix effects, neutral -> null normalisation, unsaved indicator on change, global bypass transient, clipboard) - `tests/StemStudio.Web.Tests/Services/EffectsStateTests.cs`
- [ ] T028 (frontend) Implement - `src/StemStudio.Web/Services/EffectsState.cs`
- [ ] T029 [P] (qa) Fixtures and helpers: pink-noise stem set (all six stems + one split parent), `band-energy.ts` (FFT band energy in dB), `wav-decode.ts`, `fake-midi.ts` and `no-midi.ts` init scripts (research R-13) with a self-test spec - `e2e/fixtures/`, `e2e/helpers/`, `e2e/tests/fixtures-selftest.spec.ts`

**Checkpoint**: an empty-effects mix plays and saves exactly as before (existing suites green).

## Phase 2: User Story 1 - Practice EQ (P1) MVP

- [ ] T030 [P] [US1] (audio-engine) Test: preset catalogue v1 matches data-model 5.2/5.4 (ids, scopes, families, variants, stable order); family mapping 5.1 for every `ChannelId`; for each family the cookbook magnitude response of Focus and Make room meets the 5.3 test bands (pure math, no browser) - tune values and update data-model 5.2 if a band fails - `tests/effects/presets.test.ts`, `tests/effects/families.test.ts`
- [ ] T031 [US1] (audio-engine) Implement catalogue and families - `src/audio-engine/src/effects/{presets,families}.ts`
- [ ] T032 [P] [US1] (audio-engine) Test: `getEffectPresets`, `familyOf` on the engine; unknown id error - `tests/engine-effects.test.ts`
- [ ] T033 [US1] (audio-engine) Implement - `src/audio-engine/src/engine.ts`
- [ ] T034 [P] [US1] (frontend) Test: `PracticeEqService` - Focus on the chosen strip; Make room on every other strip incl. hidden detail strips and siblings of the same family; Off clears only `practice-*` EQs; disabled with reason when only one audible strip; compressor/send untouched - `tests/StemStudio.Web.Tests/Services/PracticeEqServiceTests.cs`
- [ ] T035 [US1] (frontend) Implement - `src/StemStudio.Web/Services/PracticeEqService.cs`
- [ ] T036 [P] [US1] (frontend) Test (bUnit): `PracticeEqMenu` on every strip at all levels, item names per family, `data-practice-eq`, `strip-has-effects-<key>` marker for Beginner, keyboard menu pattern - `tests/StemStudio.Web.Tests/Mixer/PracticeEqMenuTests.cs`
- [ ] T037 [US1] (frontend) Implement menu and strip integration - `src/StemStudio.Web/Components/Mixer/Effects/PracticeEqMenu.razor`, `ChannelStrip.razor`
- [ ] T038 [US1] (qa) E2E `effects-practice-eq.spec.ts`: one strip of each of the seven families gets Focus and Make room (state via ids/attributes); band-energy check on offline renders for every family (SC-001, US1-4) at level Beginner and Advanced - `e2e/tests/effects-practice-eq.spec.ts`

## Phase 3: User Story 2 - Shape a strip (P1)

- [ ] T040 [P] [US2] (audio-engine) Test: IR generator deterministic per kind and `irVersion`, decay envelope within 10 % of RT60, stereo decorrelation; reverb bus topology, pre-delay, return ramp, meter `reverb` - `tests/effects/{impulse,reverb-bus}.test.ts`
- [ ] T041 [US2] (audio-engine) Implement - `src/audio-engine/src/effects/{impulse,reverb-bus}.ts`
- [ ] T042 [P] [US2] (audio-engine) Test: `setChannelEffects` lazy node creation/removal, parameter ramps, dry/wet cross-fade on on/off/bypass, hidden strip suspends wet path and resumes on expand, `setEffectsBypassed`, `setReverb`, errors - `tests/engine-effects.test.ts`
- [ ] T043 [US2] (audio-engine) Implement - `src/audio-engine/src/{engine,mixer}.ts`, `effects/strip-chain.ts`
- [ ] T044 [P] [US2] (audio-engine) Test: EQ curve SVG path from `getFrequencyResponse` (frame-coalesced), reduction meter attributes, detach - `tests/effects/{eq-curve,reduction-meter}.test.ts`
- [ ] T045 [US2] (audio-engine) Implement - `src/audio-engine/src/effects/{eq-curve,reduction-meter}.ts`
- [ ] T046 [P] [US2] (audio-engine) Test: `getEffectsLoad` cost units and `renderCapacity` passthrough - `tests/effects/load.test.ts`
- [ ] T047 [US2] (audio-engine) Implement - `src/audio-engine/src/effects/load.ts`
- [ ] T048 [P] [US2] (frontend) Test (bUnit): `EffectsPanel`, `EqSection`, `CompressorSection`, `SendSection` - ids, Advanced vs Expert controls, `aria-valuetext` units, numeric entry validation (`fx-error`), engine calls on change, curve and GR meter attach/detach - `tests/StemStudio.Web.Tests/Mixer/Effects/*Tests.cs`
- [ ] T049 [US2] (frontend) Implement - `src/StemStudio.Web/Components/Mixer/Effects/{EffectsPanel,EqSection,CompressorSection,SendSection,ReductionMeter}.razor`
- [ ] T050 [P] [US2] (frontend) Test (bUnit): `PresetMenu` (scope filter, apply, "modified", reset, copy/paste between strips) - `tests/StemStudio.Web.Tests/Mixer/Effects/PresetMenuTests.cs`
- [ ] T051 [US2] (frontend) Implement - `src/StemStudio.Web/Components/Mixer/Effects/PresetMenu.razor`
- [ ] T052 [P] [US2] (frontend) Test (bUnit): `ReverbBusSection`, `BypassAllToggle`, `EffectsLoad` in `MasterSection`; strip `fx-open`/`fx-bypass`; Beginner renders none of them (R10) - `tests/StemStudio.Web.Tests/Mixer/Effects/MasterEffectsTests.cs`
- [ ] T053 [US2] (frontend) Implement - `src/StemStudio.Web/Components/Mixer/{MasterSection.razor,ChannelStrip.razor,Mixer.Effects.cs}`, `Effects/{ReverbBusSection,BypassAllToggle,EffectsLoad}.razor`
- [ ] T054 [US2] (qa) E2E `effects-panel.spec.ts` and `effects-levels.spec.ts`: panel flow, presets, bypass and bypass-all, reverb bus, Expert-only controls, Beginner sees practice EQ only - `e2e/tests/effects-{panel,levels}.spec.ts`

## Phase 4: User Story 3 - Saved in mixes (P1)

- [ ] T055 [P] [US3] (backend) Test: duplicate copies effects; SC-002 regression - every stored pre-1.16.0 mix fixture renders the same export plan as 007 (effects `ignore`) - `tests/StemStudio.Api.Tests/Mixes/MixEffectsDuplicateTests.cs`, `Assets/ExportPlannerLegacyTests.cs`
- [ ] T056 [US3] (backend) Implement duplicate/planner adjustments - `src/StemStudio.Api/Mixes/*`, `Assets/ExportPlanner.cs`
- [ ] T057 [P] [US3] (frontend) Test: mixer load applies effects to the engine (incl. detail strips, reverb bus); old mix -> no effects calls; save sends `effects: null` for neutral strips; unsaved indicator - `tests/StemStudio.Web.Tests/Mixer/MixerEffectsLoadSaveTests.cs`
- [ ] T058 [US3] (frontend) Implement - `src/StemStudio.Web/Components/Mixer/{Mixer.Effects.cs,Mixer.Save.cs}`
- [ ] T059 [US3] (qa) E2E `effects-persist.spec.ts`: save/reload/duplicate; a mix created through the API without effects loads neutral; invalid API values -> 422 - `e2e/tests/effects-persist.spec.ts`

## Phase 5: User Story 4 - Export as heard (P2)

- [ ] T060 [P] [US4] (audio-engine) Test: offline gate against every gate case of `specs/007-export-library/contracts/render-vectors.json` (live rule and selected-channels rule) - `tests/render/offline-gate.test.ts`
- [ ] T061 [US4] (audio-engine) Implement - `src/audio-engine/src/render/offline-gate.ts`
- [ ] T062 [P] [US4] (audio-engine) Test: float32 WAV encoder (header fields, chunking, exact samples, size limit) - `tests/render/wav-encoder.test.ts`
- [ ] T063 [US4] (audio-engine) Implement - `src/audio-engine/src/render/wav-encoder.ts`
- [ ] T064 [P] [US4] (audio-engine) Test: `renderPrerender` - live and offline graphs equal (node types, order, params) for the same state; pre-roll `min(3 s, start)` trimmed; progress non-decreasing <= 10 Hz; abort; upload error message; metronome/karaoke excluded; global bypass ignored - `tests/render/prerender.test.ts`
- [ ] T065 [US4] (audio-engine) Implement - `src/audio-engine/src/render/prerender.ts`, `engine.ts`
- [ ] T066 [P] [US4] (backend) Test: `WavHeaderReader` (format 3/32/2/44100 accepted; every other field -> readable 415 detail; truncated data; odd chunk sizes) - `tests/StemStudio.Api.Tests/Prerenders/WavHeaderReaderTests.cs`
- [ ] T067 [US4] (backend) Implement - `src/StemStudio.Api/Prerenders/WavHeaderReader.cs`
- [ ] T068 [P] [US4] (backend) Test: prerender endpoints - 201 body and file location, 411, 413 at limit + 1, 415, 409 not ready/disk, CORS preflight for the path only, delete idempotent and 409 consumed, TTL sweep with `FakeTimeProvider`, track delete removes pre-renders - `tests/StemStudio.Api.Tests/Prerenders/PrerenderEndpointTests.cs`
- [ ] T069 [US4] (backend) Implement endpoints, store, sweeper, Kestrel limit, CORS - `src/StemStudio.Api/Prerenders/*.cs`, `Program.cs`
- [ ] T070 [P] [US4] (backend) Test: `createAudioExport` with `apply` - file moved to `assets/<assetId>/prerender.wav`, worker request has one input gain 1 pan 0 master 1 range null; errors `prerender-required`/`-not-found`/`-consumed`/`-mismatch`/`effects-not-allowed`; `recipe.effects`; contents suffix; retry from pre-render and `409 prerender-missing`; pre-render deleted after completion and on asset delete; `effects` absent behaves as 1.5.0 - `tests/StemStudio.Api.Tests/Assets/EffectsExportTests.cs`
- [ ] T071 [US4] (backend) Implement - `src/StemStudio.Api/Assets/*`, `Domain/Assets/ExportEffectsRule.cs`
- [ ] T072 [P] [US4] (frontend) Test (bUnit): export dialog effects option (shown only for mix modes with effects, default on, disabled under bypass-all, Beginner implicit with note, long-track warning, progress, cancel deletes the pre-render, upload/API errors) - `tests/StemStudio.Web.Tests/Export/ExportEffectsOptionTests.cs`
- [ ] T073 [US4] (frontend) Implement - `src/StemStudio.Web/Components/Export/{ExportDialog.razor.cs,EffectsOption.razor}`, `Components/Mixer/Mixer.Export.cs`
- [ ] T074 [P] [US4] (frontend) Test (bUnit): library badge `asset-effects-<assetId>` - `tests/StemStudio.Web.Tests/Library/AssetEffectsBadgeTests.cs`
- [ ] T075 [US4] (frontend) Implement - `src/StemStudio.Web/Components/Library/*`
- [ ] T076 [US4] (qa) E2E `effects-export.spec.ts`: decoded FLAC-24 equals a second offline render within 1 LSB (SC-003); practice EQ band energy in the file; "without effects" equals the 007 export; raw/stem pack have no option; range export pre-roll; cancel leaves no item - `e2e/tests/effects-export.spec.ts`

## Phase 6: User Story 5 - MIDI controller (P2)

- [ ] T077 [P] [US5] (audio-engine) Test: parser (note on/off, velocity-0 note-off, CC, pitch bend, program, channels, system messages ignored, cc14 pairing and MSB-only fallback) - `tests/midi/parser.test.ts`
- [ ] T078 [US5] (audio-engine) Implement - `src/audio-engine/src/midi/parser.ts`
- [ ] T079 [P] [US5] (audio-engine) Test: bindings (all modes incl. three relative encodings, invert, toggle/momentary/trigger) and pick-up (tolerance, crossing, re-sync) - `tests/midi/{bindings,pickup}.test.ts`
- [ ] T080 [US5] (audio-engine) Implement - `src/audio-engine/src/midi/{bindings,pickup}.ts`
- [ ] T081 [P] [US5] (audio-engine) Test: coalescer (latest-wins per binding, <= 30 Hz flush on the frame loop, buttons immediate, timestamps kept) and learn (first message, inferred mode incl. cc14 detection, 10 s timeout, cancel) - `tests/midi/{coalescer,learn}.test.ts`
- [ ] T082 [US5] (audio-engine) Implement - `src/audio-engine/src/midi/{coalescer,learn}.ts`
- [ ] T083 [P] [US5] (audio-engine) Test: `createMidiController` with the fake MIDI access - availability states, connect only on call with `sysex: false`, devices and statechange callbacks, `setBindings` per device, dispose removes listeners - `tests/midi/controller.test.ts`
- [ ] T084 [US5] (audio-engine) Implement - `src/audio-engine/src/midi/{controller,availability}.ts`, `index.ts`
- [ ] T085 [P] [US5] (backend) Test: profile domain rules - validation matrix 3.5, slot/command presence, control uniqueness incl. cc14 occupancy, limits, readable paths - `tests/StemStudio.Domain.Tests/Controllers/*Tests.cs`
- [ ] T086 [US5] (backend) Implement - `src/StemStudio.Domain/Controllers/*.cs`
- [ ] T087 [P] [US5] (backend) Test: `/api/controller-profiles` CRUD, ordering, `409 profile-exists` with `profileId`, 422, idempotent delete, persistence - `tests/StemStudio.Api.Tests/Controllers/ControllerProfileEndpointTests.cs`
- [ ] T088 [US5] (backend) Implement - `src/StemStudio.Api/Controllers/ControllerProfileEndpoints.cs`, DI
- [ ] T089 [P] [US5] (frontend) Test: `MidiControllerInterop` + `ControllerSession` (availability, connect from a user action only, auto-activate the profile of a connected device, create a profile on first learn, `syncValues` after UI changes) - `tests/StemStudio.Web.Tests/Controllers/ControllerSessionTests.cs`
- [ ] T090 [US5] (frontend) Implement - `src/StemStudio.Web/Interop/{IMidiController,MidiControllerInterop}.cs`, `Services/{ControllerSession,ControllerProfilesClient}.cs`
- [ ] T091 [P] [US5] (frontend) Test: `ControllerDispatcher` + `StripSlots` - every target kind and command calls the same mixer method as the on-screen control; slots follow mixer order with expanded groups; bank next/previous; empty slot ignored; 012 targets ignored with "not available here" when absent - `tests/StemStudio.Web.Tests/Controllers/ControllerDispatcherTests.cs`
- [ ] T092 [US5] (frontend) Implement - `src/StemStudio.Web/Services/{ControllerDispatcher,StripSlots}.cs`, `Components/Mixer/Mixer.Midi.cs`
- [ ] T093 [P] [US5] (frontend) Test (bUnit): `MidiLearnLayer`, `MidiStatusChip`, bank indicator - learn by click and keyboard, live-region text, conflict confirmation, Escape/timeout, `data-midi-learn`/`data-midi-bound` - `tests/StemStudio.Web.Tests/Mixer/MidiLearnTests.cs`
- [ ] T094 [US5] (frontend) Implement - `src/StemStudio.Web/Components/Mixer/{MidiLearnLayer,MidiStatusChip}.razor`
- [ ] T095 [P] [US5] (frontend) Test (bUnit): Controllers page - device list, profile cards (enable, rename, delete), binding table (re-learn, delete, Expert mode/invert/pick-up/bank size), add binding via target picker, monitor (Expert), errors; screen help topic - `tests/StemStudio.Web.Tests/Controllers/ControllersPageTests.cs`
- [ ] T096 [US5] (frontend) Implement - `src/StemStudio.Web/Components/Pages/Controllers.razor`, `Components/Controllers/*.razor`, navigation entry
- [ ] T097 [US5] (qa) E2E `midi-learn.spec.ts` and `midi-control.spec.ts` with the fake device: connect, learn fader/mute/play/speed, move -> mixer follows, pick-up, bank switching, conflict, reload re-activates the profile, latency p95 < 50 ms from fake `timeStamp` to the engine call (SC-004) - `e2e/tests/midi-{learn,control}.spec.ts`

## Phase 7: User Story 6 - Fallback and permissions (P3)

- [ ] T098 [P] [US6] (frontend) Test (bUnit): reasons and remedies for `unsupported`, `insecure-context`, `prompt`, `denied` (Try again), `no-device`; Beginner `controllers-level-note`; no prompt without a click - `tests/StemStudio.Web.Tests/Controllers/ControllerFallbackTests.cs`
- [ ] T099 [US6] (frontend) Implement - `src/StemStudio.Web/Components/Controllers/*`, `MidiStatusChip.razor`
- [ ] T100 [US6] (qa) E2E `midi-fallback.spec.ts`: API absent (0 prompts, 0 console errors, SC-007), denied, unplug during playback and re-plug, Beginner with an enabled profile still works; Firefox project runs the unsupported path - `e2e/tests/midi-fallback.spec.ts`

## Phase 8: Polish and cross-cutting

- [ ] T101 [P] (frontend) Help: 13 topics, 6 glossary entries, tour `practice-eq` per contracts/help-topics.md; help tests (anchors coverage, schema) green - `src/StemStudio.Web/Help/**`
- [ ] T102 [P] (qa) a11y: axe on effects panel, practice EQ menu, export dialog with effects, Controllers page (SC-008); keyboard-only learn walkthrough - `e2e/a11y/{effects,controllers}.a11y.spec.ts`
- [ ] T103 [P] (qa) Budget: `effects-budget.spec.ts` worst-case graph (28 strips, full EQ 24 dB + compressor, Hall) offline 30 s <= 10 s on CI; manual 5-minute playback on the reference machine and the SC-006 one-minute bind check recorded in the hand-off - `e2e/tests/effects-budget.spec.ts`, `docs/handoffs/qa-017-polish.md`
- [ ] T104 [P] (devops) Compose/proxy body limit >= 320 MB for the pre-render path, CORS origin env documented, new E2E specs in the Chromium project and the fake-MIDI init scripts in the CI run; CI green - `docker-compose*.yml`, `.github/workflows/*`, `scripts/test-all.*`
- [ ] T105 [P] (quality-gate) SIG check and Sonar on the new engine, backend and web modules; refactor requests to owners - `docs/quality/`
- [ ] T106 [P] (audio-engine) Contract test: exported names and `ENGINE_CONTRACT_VERSION === '2.7.0'`; coverage >= 90 % line/branch for `effects/`, `render/`, `midi/` - `tests/contract.test.ts`
- [ ] T107 [P] (backend) Contract test: every 1.16.0 operation and schema member exists with the documented status codes - `tests/StemStudio.Api.Tests/Contract/Backend1160ContractTests.cs`
- [ ] T108 (cto) User manual: Effects, Practice EQ (per family), Export with effects, Controllers (learn, banks, modes), browser support and troubleshooting - `docs/public/src/user/**`
- [ ] T109 (cto) Technical manual: effect chain, CPU budget, pre-render flow and limits, MIDI architecture and permissions, data model additions - `docs/public/src/technical/**`
- [ ] T110 (cto) Rebuild PDFs and `build-manifest.json` (`node tools/docs/check.mjs` green); `docs/STATUS.md`; THIRD-PARTY-LICENSES "no new dependencies"; integration items with 012/014/015 (plan "Integration"); ADR number check - `docs/public/**`, `docs/STATUS.md`

## Dependencies and order

- Phase 0 blocks all. Phase 1 blocks the stories. US1, US2 and US5 can start in parallel after
  Phase 1 (different files); US3 needs T019/T022 (Phase 1) and the panel (US2) only for its E2E;
  US4 needs T043 (effects in the engine) and T024; US6 needs T090.
- Within a lane: test task before its implementation task (listed pairs).
- qa E2E tasks (T038, T054, T059, T076, T097, T100) run after the lanes' implementation of the
  same story; fixtures T029 first.
- Polish after the stories; CTO tasks last (manuals before the integration wave closes).

## Parallel example (after Phase 1)

```text
audio-engine: T030 -> T031 | T040 -> T041 | T077 -> T078   (different files)
backend:      T066 -> T067 | T085 -> T086
frontend:     T034 -> T035 | T048 -> T049
qa:           T038 prepared against fixtures while lanes implement
```

## Task counts per lane

| Lane | Tasks | Ids |
|---|---|---|
| architect | 8 | T001-T008 |
| audio-engine | 35 | T010-T017, T030-T033, T040-T047, T060-T065, T077-T084, T106 |
| backend | 19 | T018-T020, T022-T024, T055-T056, T066-T071, T085-T088, T107 |
| frontend | 31 | T025-T028, T034-T037, T048-T053, T057-T058, T072-T075, T089-T096, T098-T099, T101 |
| stem-separation | 1 | T021 (test only) |
| qa | 9 | T029, T038, T054, T059, T076, T097, T100, T102, T103 |
| devops | 1 | T104 |
| quality-gate | 1 | T105 |
| cto | 3 | T108-T110 |
| **Total** | **108** | 41 test -> implementation pairs, 1 worker test-only task, 2 contract-test tasks, 9 qa E2E/a11y/budget tasks, 8 Phase 0, 6 other (help content, devops, quality-gate, cto) |

MVP scope: Phase 0, Phase 1 and US1 (practice EQ) - every instrument family benefits first.
