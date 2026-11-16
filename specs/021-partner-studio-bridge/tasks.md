---

description: "Task list for feature 021 practice partner and studio bridge (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Practice Partner and Studio Bridge

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.3.1, ADR-025, ADR-030, ADR-034, the merged 009/010/014/015/016/017/019 artefacts.

**Tests**: REQUIRED (constitution I). Every implementation task follows its failing-test task
(red -> green -> refactor), >= 90 % line AND branch per component. Nothing in CI needs a GPU, an
audio device, Emscripten or the internet: ACE-Step is replaced by the fake engine and a fake
`acestep` module, the NAM WASM binary is committed (golden vectors run it in Node), the browser input
is Chromium's fake device, catalogue downloads hit a fake host. Marked exceptions: one generator
GPU integration test (CI-skipped), the NAM WASM rebuild job (own CI job), and the CTO reference
checks (Q7-Q10).

**Status**: design written 2026-11-16 (architect). Phase 0 starts when 012-020 are merged (their
contracts are the base of the 021 deltas).

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task).
- **[USn]**: user story of spec.md. Owners: architect, generative, stem-separation, beat-analysis,
  backend, audio-engine, frontend, qa, devops, quality-gate, CTO. audio-ingest has no tasks.

## Checkpoint hand-offs (binding, ADR-008)

`docs/handoffs/<owner>-021-<checkpoint>.md` at `foundational`, every story and `polish`, with real
test and coverage output.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| generative | partner kind | `generator-api` 1.2.0, `schemas.py`/`ports.py` (T007) | fake `acestep` module, fake gate |
| stem-separation, beat-analysis | conform job | `worker-api` 1.12.0, worker ports (T008) | synthetic fixtures |
| backend | generator + worker additions | the OpenAPI documents + examples (T001) | stub HTTP handlers |
| audio-engine | part, rig, recorder additions | `audio-engine` 2.9.0 + `types.ts` (T002), `nam_shim.h` (T010) | mocked `AudioContext`, Node WASM |
| frontend | backend endpoints, engine interop | backend 1.20.0 + DTOs (T006), engine 2.9.0 | fake API client, bUnit JS-interop mock |
| qa | UI | `ui-test-ids` 1.20.0 (T003), fake stack (T015), fixtures (T017) | - |
| devops | CSP, fake catalogue host, CI WASM job | DELTA.md section 5 | - |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [ ] T001 [architect] Merge the three OpenAPI deltas into full documents in `specs/021-partner-studio-bridge/contracts/`: `backend-api.openapi.yaml` 1.20.0 (on 020's 1.19.0; enum/member additions of DELTA.md 1; reconcile the 015 take path and take channel id), `worker-api.openapi.yaml` 1.12.0 (on 1.11.0), `generator-api.openapi.yaml` 1.2.0 (on 010's 1.1.0); external `$ref`s become local; Redocly lint 0 errors
- [ ] T002 [P] [architect] `contracts/audio-engine.md` 2.9.0 (full document = 2.8.0 + delta) and `src/audio-engine/src/types.ts` additions (`EngineContractVersion '2.9.0'`, `PartChannelId`, `PartSource`, `RigSettings`, `AmpProfileInfo`, `RigStatus`, method signatures, `OnRigStatus`); type-level tests (`tsc --noEmit`)
- [ ] T003 [P] [architect] Merge `ui-test-ids.delta.md` into the full `ui-test-ids.md` 1.20.0; feature catalogue ids in the 004 design-spec catalogue resource
- [ ] T004 [architect] S-04 catalogue curation: select 6-10 captures (guitar clean/crunch/high gain/overdrive pedal, bass clean/drive) meeting ADR-030 D4 with `licenceEvidence`; write `src/StemStudio.Domain/Amp/catalogue.v1.json` (validates against `amp-catalogue.schema.json`); list entries for THIRD-PARTY-LICENSES in the hand-off; fewer entries are acceptable and recorded (FR-017)
- [ ] T005 [P] [architect] Vendor DAWproject 1.0 `Project.xsd`, `MetaData.xsd` and `LICENSE` (pinned commit + SHA-256) as embedded resources `src/StemStudio.Domain/DawProject/Schemas/`; skeleton test compiles them into an `XmlSchemaSet` (FR-025)
- [ ] T006 [P] [architect] .NET contract code: `src/StemStudio.Contracts/{Partner,Amp,DawExport}/*` DTOs, enum additions (`GenerationKind.Partner`, `AssetKind.DawProject`, `AssetOrigin.DawExport`, `AssetFileRole.Dawproject/Report`), `ApiRoutes`, `ContractLimits` (data-model 5); serialisation tests against the OpenAPI examples
- [ ] T007 [P] [architect] Generator skeleton: `schemas.py` (`kind: partner`, `PartnerParams`, `AceStepTrackName`, `ModelId` + `acestep-v15-base`, `GeneratedTake.track`), `ports.py` (`LEGO_BASE`, `LEGO_BASE_LOWMEM`, `PARTNER_NEEDS`, `ACESTEP_BASE_WEIGHTS` pin, `MusicEngine.partner`, `EnginePartner`), `config.py` (`STEMSTUDIO_FAKE_PART_OFFSET_MS`, fake mode `quiet-part`); skeleton tests; contract subset test (1.2.0 superset of 1.1.0)
- [ ] T008 [P] [architect] Worker skeleton: conform request/result schemas, `library_paths` parts pattern, ports `PartFitAnalyzer`, `PartConformWriter`; skeleton tests
- [ ] T009 [architect] S-01 spike with the generative agent on the reference machine: lego task string and track names at the pinned ACE-Step commit (move the pin if needed), lego output = track or mix (extract fallback decision), NVML peaks standard/offload, timings 30/60/180 s, DiT swap time, base checkpoint revision + SHA-256; update `ports.py` needs and pins, research.md S-01, ADR-025 "Phase 0 outcome"
- [ ] T010 [P] [architect] NAM shim contract `tools/nam-wasm/nam_shim.h` (C API of ADR-030 D2 with doc comments), `tools/nam-wasm/Dockerfile` (pinned `emscripten/emsdk` tag), `build.sh` skeleton, `src/audio-engine/vendor/nam/PROVENANCE.md` template
- [ ] T011 [architect] Verify the skeleton: `dotnet build` 0 warnings, all tests + coverage gates, ruff/mypy strict, eslint/tsc, `tools/quality` run; ADR-025/0212/0213 -> Accepted
- [ ] T012 [architect] Hand-off `docs/handoffs/architect-021-phase0.md` (contracts, lint output, spikes, dependencies + licences)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup

- [ ] T013 [P] [quality-gate] Failing rule tests: `StemStudio.Domain.DawProject` and `Domain.Amp` depend on nothing in `Api`/`Persistence`; `Api.Partner -> Api.Generation` one-way (Generation never references Partner); worker `conform/` imports only ports/schemas/config; engine `amp/` imports only `types.ts` and its own modules
- [ ] T014 [quality-gate] Implement the rules in `tools/quality/`; update `docs/quality/SIG-REPORT.md`
- [ ] T015 [P] [devops] E2E fake stack: fake catalogue host (static file server image already used by the repo, or the API test host when none exists), `StemStudio__Amp__CatalogueFile`, fake generator env (`STEMSTUDIO_FAKE_PART_OFFSET_MS=37`), Playwright Chromium flags for the fake audio input; CSP check for `'wasm-unsafe-eval'`
- [ ] T016 [P] [devops] CI job `nam-wasm` (pinned emsdk image): rebuild `nam-engine.wasm`, run the Node golden vectors against the rebuilt binary, compare exports; engine build copies `nam-worklet.js` + `nam-engine.wasm` to `wwwroot/js/`
- [ ] T017 [P] [qa] Fixtures (all synthetic, generated by committed scripts): `tests/fixtures/amp/guitar-di-10s.wav`, `bass-di-10s.wav` (Karplus-Strong plucks), `linear-test.nam` + `wavenet-tiny.nam` (`tools/nam-wasm/make_test_model.py`, fixed seed), `not-a-model.nam`, `ir-1s.wav`, `ir-3s.wav`, `catalogue.e2e.json` + files; `tests/fixtures/partner/click-offset-37ms.flac`, `bass-in-a-minor.flac`, chord timeline JSON; TEST-STRATEGY update (fake input, fake catalogue, golden DAW files)

---

## Phase 2: Foundational

### generative (tests first)

- [ ] T018 [P] [generative] Failing tests `generator/tests/engines/test_fake_partner.py`: fake partner part (pattern at BPM inside the range, silent outside, length = context length, pitched families on the key root, drums as clicks), offset `STEMSTUDIO_FAKE_PART_OFFSET_MS`, mode `quiet-part` (-60 dBFS), take `track` and `vocal` (`synthetic` for `backing_vocals`)
- [ ] T019 [generative] Implement the fake in `engines/fake.py` to pass T018
- [ ] T020 [P] [generative] Failing API tests: `kind: partner` multipart (`params` + 1-6 `context`, same rate/length), validation (range 10-480 inside context -> `422 sourceTooShort`, trackName enum, seeds = takes, steps 30-60, guidance 1-15), `409 kindNotServed` on the singer variant, `/health.kinds` contains `partner`, responses validate against generator-api 1.2.0
- [ ] T021 [generative] Implement the partner kind in `api/` and `jobs/runner.py` to pass T020

### worker (tests first)

- [ ] T022 [P] [beat-analysis] Failing tests `worker/tests/conform/test_fit.py`: onset alignment (click fixture -> offset 37 +- 1 ms, confidence >= 2), +-250 ms window, drift slope, Krumhansl-Schmuckler key and agreement classes, chord agreement per beat (pitched only; drums -> null), loudness of non-silent frames, gain clamp +-12 dB, `veryQuiet`, verdict rules and notes, no beat grid -> offset 0 + note (FR-006)
- [ ] T023 [beat-analysis] Implement `conform/{onset_align,key_chords,fit}.py` to pass T022
- [ ] T024 [P] [stem-separation] Failing tests conform job: `POST/GET/DELETE /v1/conform-jobs`, `.../file`, render-lane FIFO, allow-list (asset take path, parts pattern; traversal rejected), mode check (no file) vs write (44.1 kHz FLAC 24-bit, exact track frames, placement at range start minus offset, peak guard -0.1 dBFS, FLAC tags copied), restart -> failed "interrupted by restart", contract validation against 1.12.0 (FR-007)
- [ ] T025 [stem-separation] Implement `api/routes_conform.py`, `jobs/conform_runner.py`, `conform/writer.py`, `library_paths.py` to pass T024

### backend (tests first)

- [ ] T026 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Partner/`: `PartFamily` table (data-model 2.2), `PartnerCaptionBuilder` vectors (order, 512 limit drop order, guard before and after), `ChordProgressionText` vectors (bar symbol, `x2` collapse, 160 truncation), `PartnerRequestResolver` (context channels per mode incl. detail -> parent, consecutive sections, 480 cap, defaults from grid/key/chords, `IncludeChords` flag), `FitRanking` (FR-001..FR-004, FR-006)
- [ ] T027 [backend] Implement `src/StemStudio.Domain/Partner/*` to pass T026
- [ ] T028 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Amp/`: `NamFileValidator` (valid fixtures, missing weights, unknown architecture, non-finite numbers, depth > 32, weights > 4,000,000, sample rates), `WavHeaderReader` (PCM 16/24/32, float 32, extensible PCM/float, other codecs, > 2 s, rates), `RigSettings` ranges + fallback substitution, catalogue loader validates `catalogue.v1.json` against the schema and checks unique ids/hosts (FR-016..FR-019)
- [ ] T029 [backend] Implement `src/StemStudio.Domain/Amp/*` to pass T028
- [ ] T030 [P] [backend] Failing persistence tests: migration `AddPartnerAmpDaw` from the 1.19.0 schema, round trips of `TrackParts`, `AmpProfiles`, `CabinetIrs`, `RigPresets`, `Generations.Partner_*`, `GenerationTakes.FitJson/PartId`, `Takes.WetFileName/RigJson/PlayWet`; seeded presets idempotent; part cascade on track delete
- [ ] T031 [backend] Implement configurations, migration and seed to pass T030
- [ ] T032 [P] [backend] Failing client tests with the 1.2.0/1.12.0 examples: generator client partner multipart (1-6 `context` parts, routing to the generator URL), worker client conform jobs (create, poll, file, delete)
- [ ] T033 [backend] Implement the client additions to pass T032

### audio-engine (tests first)

- [ ] T034 [P] [audio-engine] Failing Vitest `parts.test.ts`: `addPart`/`removePart`, duplicate id rejected, part as top-level channel in the gate (render-vectors 1.1.0 still pass), sample-locked start at the transport position, settings applied, meters
- [ ] T035 [audio-engine] Implement `src/parts.ts` to pass T034 (FR-007)
- [ ] T036 [P] [audio-engine] Failing Node tests `test/nam-wasm.test.ts`: `linear-test.nam` output matches the FIR vector within 1e-6, `wavenet-tiny.nam` output hash, `nam_latency_frames` > 0 only when resampling 48 kHz -> 44.1 kHz, invalid JSON -> `nam_last_error` text, repeated `nam_process` does not grow WASM memory
- [ ] T037 [audio-engine] Implement `tools/nam-wasm/shim.cpp` + `build.sh` (core pinned), build in the container, commit `vendor/nam/nam-engine.wasm` + `PROVENANCE.md` to pass T036 (FR-013)
- [ ] T038 [audio-engine] S-03: development-only benchmark page/harness, measure p50/p99 per architecture and size class and the resampler latency on the reference machine; record in research.md S-03 and set the "heavy" rule (SC-004)

**Checkpoint**: HO:foundational for generative, stem-separation, beat-analysis, backend, audio-engine.

---

## Phase 3: User Story 1 - Generate a practice partner part (P1) MVP

- [ ] T039 [P] [US1] [generative] Failing tests `generator/tests/engines/test_acestep_partner.py` (fake `acestep` module): `task_type="lego"`, `track_name` per family, caption/bpm/keyscale/timesignature, repaint window = range, 50 steps + guidance, base DiT loaded (turbo unloaded, stage "Loading the partner model"), `extract` fallback when configured by S-01, profile choice `PARTNER_NEEDS` + low-memory, one OOM retry, download phase, FLAC tags incl. `STEMSTUDIO_PART_FAMILY` (FR-005, FR-010)
- [ ] T040 [US1] [generative] Implement `engines/acestep_partner.py`, `engines/profiles.py` partner needs to pass T039
- [ ] T041 [US1] [generative] `@pytest.mark.integration @pytest.mark.gpu` test: real 30 s bass part over a short fixture on the GPU (skipped without CUDA)
- [ ] T042 [P] [US1] [backend] Failing tests `PartnerEndpointTests.cs`: `getPartnerContext` (available/reason for not installed, no GPU, track not ready; families; sections; base-model download note), `createPartnerGeneration` (202 with snapshot and caption, `400 imitation`, `409 not-installed/gpu-required/track-not-ready/too-many-parts`, `422` validation incl. `replacesChannel` iff alternative, range vs sections exclusive) (FR-001..FR-005)
- [ ] T043 [US1] [backend] Implement `Api/Partner/{PartnerEndpoints,PartnerService}.cs` to pass T042
- [ ] T044 [P] [US1] [backend] Failing tests: automatic fit check after completion (conform `check` per take -> `GenerationTake.fit`, `recommended`), `addPartFromTake` (conform `write`, hashing copy into `parts/`, worker job deleted, mute of the replaced channel in `mixId`, `part-exists`, `too-many-parts`), parts list/get/rename/delete (mix channel entries removed), audio Range/ETag, retry; `RenderPlanner` maps part channels; library take registration with family (FR-006..FR-011)
- [ ] T045 [US1] [backend] Implement `Api/Partner/{PartSync,PartsEndpoints,PartService}.cs` and the `RenderPlanner` addition to pass T044
- [ ] T046 [P] [US1] [frontend] Failing bUnit tests `PartnerDialogTests.cs`, `PartnerTakesTests.cs`, `PartStripTests.cs`: families (10), add/alternative + replaces, sections (consecutive only), prefilled tempo/key, hint error, download note, limits note, generate/cancel, job progress, takes with verdict/recommended/AI badge, play with the song, add to mixer (engine `addPart` interop), strip badge/rename/delete/conforming, levels of data-model 9 (FR-001..FR-011, FR-029)
- [ ] T047 [US1] [frontend] Implement `Components/Partner/*` and the mixer wiring to pass T046
- [ ] T048 [US1] [qa] Author `e2e/tests/partner.spec.ts` (`@generative`, fake stack): Q1 flow, imitation refusal, mix save/reload with the part, one alternative case per family group (drums, bass, guitar, keys, strings, backing vocals)

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Play guitar or bass through an amp simulation (P1)

- [ ] T049 [P] [US2] [audio-engine] Failing Vitest `amp/rig.test.ts`, `builtin-amps.test.ts`, `cabinets.test.ts`: chain order, built-in amp curves (4x oversampled shaper), tone stack biquads, synthesised cabinets, gate, gains, bypass 10 ms cross-fade, parameter ramps, unknown ids -> fallback + `missing`, `loadNamRuntime` false path (no WASM) keeps built-ins working, 017 effects inserted unchanged (FR-012, FR-016)
- [ ] T050 [US2] [audio-engine] Implement `src/amp/{rig,builtin-amps,cabinets}.ts` to pass T049
- [ ] T051 [P] [US2] [audio-engine] Failing Vitest `amp/nam-node.test.ts`, `nam-worklet.test.ts`, `load-meter.test.ts`: module passed in `processorOptions`, message protocol, model swap 20 ms equal-power cross-fade, load benchmark at model load (mock worker), overload rule (2 % of quanta > 90 % in 2 s, underrun stats), `OnRigStatus` throttling (4/s + state changes), latency report incl. resampler (FR-013..FR-015)
- [ ] T052 [US2] [audio-engine] Implement `src/amp/{nam-node,nam-worklet,load-meter}.ts` to pass T051
- [ ] T053 [P] [US2] [backend] Failing tests `RigPresetEndpointTests.cs`: seeded read-only presets (`409 preset-read-only`), CRUD, name unique, limit 100, `missingRefs` after a profile/IR delete (FR-019)
- [ ] T054 [US2] [backend] Implement `Api/Amp/RigPresetEndpoints.cs` to pass T053
- [ ] T055 [P] [US2] [frontend] Failing bUnit tests `AmpPanelTests.cs`: toggle (disabled with reason), rig picker, gains, gate, pickers, built-in tone/drive only for built-ins, bypass, latency badge levels (<= 20 ok, <= 30 usable, > 30 high + warning with tips), load/overload/missing warnings, record-dry-only, save rig, levels, interop calls (`setRig`, `setRigBypass`, `loadAmpProfile`, `loadCabinetIr`) (FR-012..FR-015, FR-029)
- [ ] T056 [US2] [frontend] Implement `Components/Amp/{AmpPanel,RigPicker,LatencyBadge,LoadBadge}.razor` in the 015 input panel to pass T055
- [ ] T057 [US2] [qa] Author `e2e/tests/amp.spec.ts` (fake audio input): Q3 incl. the spectral page probe and bypass

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Export the song to my DAW (P2)

- [ ] T058 [P] [US3] [backend] Failing tests `tests/StemStudio.Domain.Tests/DawProject/`: `TempoMapBuilder` (one point per bar, hold, pickup before bar 1, constant mode median, time-signature points), `ArrangementClipMapper` (019 segments -> clips with `playStart`, repeats, cuts, fades), `MixMapper` (volume, pan `(p+1)/2`, mute, solo, master, 017 EQ band types, compressor units, report entries for reverb/practice EQ/amp/practice settings), markers from sections, track order/colours/slugs/ids (FR-022..FR-024, FR-027)
- [ ] T059 [US3] [backend] Implement `Domain/DawProject/{DawProjectModel,TempoMapBuilder,ArrangementClipMapper,MixMapper}.cs` to pass T058
- [ ] T060 [P] [US3] [backend] Failing tests: `DawProjectBuilder` + XML writers produce the golden `project.xml`/`metadata.xml` (fixtures in `tests/StemStudio.Domain.Tests/DawProject/Golden/`), `DawSchemaValidator` accepts the goldens and fails a broken document with its first message, property test (100 random arrangements/mixes) always validates, ZIP writer deterministic (same bytes twice), audio entries stored, XML deflated (FR-021, FR-025, SC-005)
- [ ] T061 [US3] [backend] Implement `Domain/DawProject/{DawProjectBuilder,DawXmlWriter*,DawSchemaValidator,DawPackageWriter}.cs` to pass T060
- [ ] T062 [P] [US3] [backend] Failing tests `DawExportEndpointTests.cs`: request normalisation to the recipe, `409 insufficient-disk-space` (estimate x 2 + 1 GiB), `track-not-ready`, `arrangement-mismatch`, `nothing-to-export`, one 007 raw render per channel (worker stub), packaging stage text, files `dawproject` + `report`, asset kind/origin/provenance, validation failure -> failed, retry, skipped channels in the report; bit identity of a channel WAV vs the 007 raw WAV 24 export (FR-021..FR-026, SC-006)
- [ ] T063 [US3] [backend] Implement `Api/DawExport/{DawExportEndpoints,DawExportService}.cs` to pass T062
- [ ] T064 [P] [US3] [frontend] Failing bUnit tests `DawExportDialogTests.cs`, `DawReportTests.cs`: channels checklist, mix and arrangement selects, format/bit depth/tempo mode/effects, FLAC warning, size estimate, compatibility note (Ableton), start/error, library kind filter value, report in the item details by level (FR-021..FR-026, FR-029)
- [ ] T065 [US3] [frontend] Implement `Components/DawExport/*` and the library additions to pass T064
- [ ] T066 [US3] [qa] Author `e2e/tests/daw-export.spec.ts` (Q6) and the helper `tools/dawproject-check` (.NET console: unzip, XSD-validate, print counts; uses the domain validator)

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Manage amp profiles and cabinets (P2)

- [ ] T067 [P] [US4] [backend] Failing tests `AmpProfileEndpointTests.cs`, `CatalogueInstallerTests.cs`: catalogue list with install state, install (HTTPS only, allowed hosts, no cross-host redirect, 30 s timeout, size cap, `checksum-mismatch`, `already-installed`, `502 catalogue-download-failed`), `.nam` upload (validator, `413`, name unique, limit 200), IR upload/rename/delete, file endpoints ETag/304, profile delete -> presets `missingRefs` (FR-017, FR-018)
- [ ] T068 [US4] [backend] Implement `Api/Amp/{CatalogueEndpoints,CatalogueInstaller,AmpProfileEndpoints,CabinetIrEndpoints}.cs` to pass T067
- [ ] T069 [P] [US4] [frontend] Failing bUnit tests `AmpProfilesPageTests.cs`: catalogue entries with licence/attribution/size, install, upload with errors, profile rows with metadata by level, rename/delete with the rig warning, IRs (FR-017..FR-019)
- [ ] T070 [US4] [frontend] Implement `Pages/AmpProfiles.razor` (route `/amp`, nav entry Advanced+) to pass T069
- [ ] T071 [US4] [qa] Extend `amp.spec.ts` with Q4 (install, tamper -> checksum error, uploads, delete fallback)

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Record with the amp, keep the dry signal (P2)

- [ ] T072 [P] [US5] [audio-engine] Failing Vitest `recorder-wet.test.ts`, `reamp.test.ts`: `captureWet` records dry and wet in the same quantum, drops the chain latency frames, equal lengths, `wet` result; `setTakeReamp` plays the dry buffer through a rig and back; print via `OfflineAudioContext` (FR-020)
- [ ] T073 [US5] [audio-engine] Implement `src/{recorder-wet,reamp}.ts` to pass T072
- [ ] T074 [P] [US5] [backend] Failing tests `TakeWetEndpointTests.cs`: `putTakeWet` (FLAC/WAV, 44.1 kHz, duration within 1 ms of dry, 300 MiB, replace deletes the old file, rig stored, `playWet` true), `getTakeWetAudio` Range, `deleteTakeWet`, `setTakePlayback` (`409 no-wet-file`), 015 take DTO `wet` member (FR-020)
- [ ] T075 [US5] [backend] Implement `Api/Amp/TakeWetEndpoints.cs` to pass T074
- [ ] T076 [P] [US5] [frontend] Failing bUnit tests `TakeRowAmpTests.cs`: wet badge, play wet toggle, re-amp toggle + rig picker, print, levels (FR-020, FR-029)
- [ ] T077 [US5] [frontend] Implement the take-row additions to pass T076
- [ ] T078 [US5] [qa] Author Q5 in `amp.spec.ts` (dry/wet lengths, re-amp, print)

**Checkpoint**: HO:us5.

---

## Phase 8: User Story 6 - Helpful at every level (P3) and cross-cutting

- [ ] T079 [P] [US6] [frontend] Failing tests `HelpTopics021Tests.cs`: every topic of data-model 10 exists, validates against `help-topic.schema.json`, its anchors exist on the screens, `related` ids resolve; feature catalogue ids and the level table of data-model 9 (FR-028, FR-029)
- [ ] T080 [US6] [frontend] Write the help topics (`Help/topics/{mixer,mixes,tracks,glossary}/*.md`, limits stated honestly: lego quality, browser latency, Ableton, unmappable settings) and the catalogue entries to pass T079
- [ ] T081 [US6] [qa] Accessibility and journeys: axe checks for the partner dialog, amp panel, profiles page, export dialog; keyboard-only paths; `journey-levels.spec.ts` cases for the new controls (FR-029, FR-030)
- [ ] T082 [P] [backend] Failing tests for the agent tools `generate_partner_part` (risk heavy, job), `add_part_to_mixer` (change, undo = delete part), `export_to_daw` (job) in the 011 registry with schemas mirroring the endpoints (FR-031)
- [ ] T083 [backend] Implement the three tools and the tool-catalogue minor entry to pass T082

**Checkpoint**: HO:us6.

---

## Phase 9: Polish and integration

- [ ] T084 [quality-gate] SIG and dependency checks over all 021 changes; refactor requests to owners
- [ ] T085 [P] [generative] Polish hand-off with coverage
- [ ] T086 [P] [stem-separation] Polish hand-off with coverage (incl. beat-analysis modules, reviewed by beat-analysis)
- [ ] T087 [P] [backend] Polish hand-off with coverage
- [ ] T088 [P] [audio-engine] Polish hand-off with coverage
- [ ] T089 [P] [frontend] Polish hand-off with coverage
- [ ] T090 [CTO] Public manuals: user manual (practice partner, amp chain, profiles and licences, re-amp, DAW export and compatibility), technical manual (base model download, CSP, catalogue file, NAM WASM build); regenerate PDFs, `tools/docs/check.mjs` green
- [ ] T091 [CTO] `docs/THIRD-PARTY-LICENSES.md`: ACE-Step base weights (revision, SHA-256), NAM core and bundled libraries, emsdk (build), DAWproject XSDs, every catalogue entry; README notice
- [ ] T092 [CTO] Reference validation Q7-Q10 (incl. S-02 chord caption A/B and S-05 DAW import); record results in research.md, ADR Phase 0 outcomes and the help topic `daw-compatibility`
- [ ] T093 [CTO] Integration wave: full suites + E2E on the fake stack, `docs/STATUS.md`, merge

## Dependencies and order

- Phase 0 -> everything. T009 (S-01) blocks T039/T040 values but not T018-T021 (fakes).
- Foundational per lane before its stories; stories are independent of each other except:
  US5 needs US2's rig (T050/T052); US3 exports parts (US1) and wet takes (US5) when present, but its
  tests use fixtures, so it can run in parallel.
- Frontend story tasks need the backend DTOs (T006) and engine types (T002), not the backend
  implementation (fake API client).
- qa E2E tasks run when their story's frontend and backend tasks are green.

## Parallel example (wave start, after T012)

```text
generative: T018 -> T019, T020 -> T021        beat-analysis: T022 -> T023
stem-separation: T024 -> T025                   backend: T026, T028, T030, T032 (all [P])
audio-engine: T034 -> T035, T036 -> T037 -> T038  quality-gate: T013 -> T014
devops: T015, T016                              qa: T017
```

## Task counts per lane

| Lane | Tasks | Ids |
|---|---|---|
| architect | 12 | T001-T012 |
| quality-gate | 3 | T013, T014, T084 |
| devops | 2 | T015, T016 |
| qa | 9 | T017, T048, T057, T066, T071, T078, T081 (+ reviews in T066 helper) |
| generative | 8 | T018-T021, T039-T041, T085 |
| beat-analysis | 2 | T022, T023 |
| stem-separation | 3 | T024, T025, T086 |
| backend | 25 | T026-T033, T042-T045, T053, T054, T058-T063, T067, T068, T074, T075, T082, T083, T087 |
| audio-engine | 14 | T034-T038, T049-T052, T072, T073, T088 (+ T038 spike) |
| frontend | 14 | T046, T047, T055, T056, T064, T065, T069, T070, T076, T077, T079, T080, T089 |
| CTO | 4 | T090-T093 |
| **Total** | **93** | |
