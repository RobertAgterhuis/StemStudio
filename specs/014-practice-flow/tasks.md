---

description: "Task list for feature 014 Practice flow (Phase 0 by the architect in two parts, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Practice Flow

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.3.1, ADR-023.

**Status**: **Phase 0 part 1 done** (2026-11-16, planning round): spec, research with spikes,
ADR-023 (Proposed), standalone contract deltas (backend `1.13.0-delta.1`, worker `1.8.0-delta.1`,
Redocly-valid), engine 2.4.0, web-interop 1.1.0 and UI-id deltas. **Phase 0 part 2 done**
(2026-11-17, branch `r14-p0-014` on `main` e8d6a5d8): full documents, contract code, key map catalogue,
spikes T010/T011 (hand-off `docs/handoffs/architect-014-phase0.md`, DELTA.md 7-8). **Round start done**
(2026-11-17, branch `014-015-round`): 022+023 pre-merged, documents re-merged onto 1.23.0 (DELTA.md 8.1), T107
decided; the combined wave follows `docs/handoffs/cto-r14-lane-plan.md` (lanes L1-L6, which override the owner
table below where they differ).

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No real ML model is involved; all worker tests
run in the default CI job (synthetic audio and MusicXML are generated in the tests).

**Organization**: grouped by user story (US1..US6); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on an
  incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-014-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-014-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us6`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`, composition) | analyser, score-view renderer, setlist renderer | `ports.py` `StructureAnalyzer`, `ScoreViewRenderer`, `SetlistDocumentRenderer` (T002) | fakes returning fixed results / bytes |
| beat-analysis (`analysis/structure/`) | request/result shapes | `schemas.py` (T002), data-model 2.1-2.4 | synthetic stems generated in the tests |
| audio-ingest (`engraving/score_view.py`, `documents/`) | MusicXML, grids, setlist content | `ports.py`, `schemas.py` (T002), data-model 2.5-2.6 | MusicXML written by music21 in the test |
| backend | worker practice jobs | worker-api 1.8.0 (T005; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`) | stub `HttpMessageHandler` |
| audio-engine | types | `types.ts` 2.4.0 (T004) | mocked `AudioContext`, jsdom host |
| frontend | backend API, engine, key map | backend-api 1.13.0 + Contracts DTOs (T003), `types.ts` (T004), web-interop 1.1.0 (T005) | fake `IStemStudioApiClient`, fake engine module |
| qa | UI | `ui-test-ids.md` 1.13.0 (T005), fake stack with the fake analyser | suite authored in the wave, executed in T104 |
| devops | font, env vars, CI paths | data-model 1, plan | skeleton |

---

## Phase 0: Contracts and skeleton (architect, BEFORE the wave)

### Part 1 (done, 2026-11-16)

- [x] T001 [architect] spec.md, checklists/requirements.md, research.md (spikes S-01 structure, S-02 Verovio boxes), ADR-023, data-model.md, plan.md, quickstart.md; contract deltas `practice-api.openapi.yaml` 1.13.0-delta.1, `worker-practice.openapi.yaml` 1.8.0-delta.1 (Redocly lint: valid, 3 accepted warnings), `audio-engine.delta.md` 2.4.0, `web-interop.delta.md` 1.1.0, `ui-test-ids.delta.md`, `redocly.yaml`, `DELTA.md` (assumed 012 surface, reconciliation R-1..R-5); complete shortcut catalogue with owners (FR-020, CTO decisions 2026-11-16)

### Part 2 (after 012 and 013 are merged into main)

- [x] T002 [architect] Worker contract code: `schemas.py` 1.8.0 (`SectionJobRequest`, `StemPaths`, `DetailPath`, `DetectedSection`, `SectionResult`, `SectionJob`, `ScoreViewJobRequest`, `BarBox`, `ScoreViewResult`, `ScoreViewJob`, `SetlistSongContent`, `SetlistDocumentRequest`, `SetlistDocumentJob`, `Health.lanes.practice`, `Health.structure`); `ports.py` (`StructureAnalyzer`, `ScoreViewRenderer`, `ScoreView`, `SetlistDocumentRenderer`, errors, texts, allow-list entry `assets/<uuid>/score.musicxml`); `config.py` (`structure_analyzer`); lock `reportlab` 5.1.x in `pyproject.toml` + `uv.lock` (record what it adds); mypy overrides; skeleton tests `worker/tests/contract/test_practice_contract.py` (schemas round-trip the delta examples) (FR-001, FR-014, FR-018)
- [x] T003 [P] [architect] .NET contract code: `src/StemStudio.Contracts/{Sections,ScoreFollow,Setlists,Practice}/*`, `LoopRegionDto.SectionId`, `ApiRoutes.*`, `ContractLimits.*` (DELTA 1); skeleton tests `tests/StemStudio.Api.Tests/Contract/PracticeFlowContractTests.cs` (delta document additive: no removed path/schema/property); operation ids staged in `PendingPracticeFlowOperations` (`BackendContractTests.PracticeFlow.cs`) (FR-008, FR-032)
- [x] T004 [P] [architect] `src/audio-engine/src/types.ts` 2.4.0 (`ScoreViewPage`, `ScoreViewBar`, `ScoreViewMap`, `ScoreFollowOptions`, `LoopPassInfo`, `DotNetCallbacks` + `OnLoopPass`, the three methods as interface members with TSDoc); engine stubs throwing "not implemented" so `tsc` passes; reconcile A-012-1..A-012-5 (DELTA R-1, R-3)
- [x] T005 [architect] Full documents: `backend-api.openapi.yaml` 1.13.0 (= 013's 1.12.0 + delta), `worker-api.openapi.yaml` 1.8.0, `audio-engine.md` 2.4.0, `web-interop.md` 1.1.0, `ui-test-ids.md` 1.13.0; Redocly lint clean (known warnings only); ADR-023 -> Accepted; DELTA "Phase 0 outcome" section; R-4, R-5 closed (R-2 decided by the CTO 2026-11-16: metronome `K`, count-in `Shift+K`)
- [x] T006 [P] [architect] `keymap-defaults.json` (complete catalogue of FR-020 with `owner`, `group`, `reserved`; metronome `k`, count-in `shift+k` as decided by the CTO) under `src/StemStudio.Web/wwwroot/js/` as a project-file-level asset, plus `KeyMapDefaults` loader stub in `StemStudio.Web/Practice/` with a test that every FR-020 row is present and no chord is duplicated (FR-019, FR-020)
- [x] T007 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, engine `npm test`/lint/typecheck, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-014-phase0.md` (versions, ReportLab licence record, reconciliation results)

### Part 2 measurements (architect, throw-away scripts, results into research.md)

- [x] T010 [P] [architect] Segmentation spike on the 6-form synthetic set (one solo per family: guitar, piano, bass, woodwinds detail, vocals-led bridge, drums-only break): tune novelty kernel/peak and vocal-switch thresholds, merge length, cluster threshold; record recall/precision/solo labels and runtime for 5 min; freeze the constants in data-model 2.2 (SC-001, SC-002)
- [x] T011 [P] [architect] Score-view font check: Verovio page with an embedded Leipzig `data:` `@font-face` rendered as `<img>` in Chromium and Firefox (Playwright screenshot of a tempo mark); record result and page size overhead; if the glyph fails, switch data-model 2.5 step 4 to the fallback (strip the Leipzig text, tempo in the panel header) (FR-010)

**Checkpoint**: skeleton green; the wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T014 [P] [devops] Failing check first: `scripts/check-worker-fonts.{ps1,sh}` also requires `fc-list | grep -i "DejaVu Sans"` and `python -c "import reportlab"` as the worker user; then `docker/worker.Dockerfile` installs `fonts-dejavu-core` (both image variants); `docker-compose.yml` passes `STEMSTUDIO_STRUCTURE_ANALYZER` (default empty = automatic); CI and `scripts/test-all.*` (combined round: `CONTRACTS_DIR` -> `specs/015-play-along-recording/contracts`, the newest full backend document 1.14.0, together with 015 T010): Redocly lint of the four 014 documents with `specs/014-practice-flow/contracts/redocly.yaml`, both `check_superset.py` runs (default `OLDER` 1.23.0 -> 1.13.0, `practice-api.openapi.yaml` -> 1.13.0) and `python -I specs/014-practice-flow/contracts/merge_full.py --check`; acceptance: the new CI steps fail on a deliberately broken copy and pass on the branch (SC-008)
- [ ] T015 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `analysis/structure` imports only `ports`, `schemas`, numpy, scipy, sklearn, librosa, soundfile; `engraving/score_view.py` only `ports`, `schemas`, verovio, stdlib xml; `documents/**` only `ports`, `schemas`, reportlab; `jobs`/`api` import none of them (composition in `main.py`); TS: `src/audio-engine/src/score-follow/**` imports no Blazor interop module; .NET: `Domain.{Sections,Setlists,Practice}` reference nothing from `Api`/`Persistence`
- [ ] T016 [quality-gate] Implement the rules of T015 in `tools/quality/`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [x] T017 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: synthetic structured audio for sections (one solo per family, SC-002), score-view and PDF assertions, fake analyser for E2E, a11y (axe-core) and keyboard-only E2E for the new pages, request-log check for SC-009; traceability US1..US6 -> test files
- [ ] T018 [P] [qa] Fixtures `tests/fixtures/` generator: a synthetic structured song (60-90 s, known form and grid, separated-stem layout for the fake stack) with a manifest of expected sections and bar times; `tests/fixtures/tests/` checks the manifest (FR-033)

---

## Phase 2: Foundational (blocking inside each owner)

### beat-analysis (`analysis/structure/`, tests first)

- [ ] T020 [P] [beat-analysis] Failing tests `worker/tests/analysis/structure/test_features.py`: frames from a reliable grid (bars, lead-in frame), an unreliable grid (beats grouped), no grid (2 s); chroma/MFCC/RMS shapes per frame; vocal activity ratio on a synthetic vocals stem (on/off sections exact); stem energy shares sum to 1; 4-stem fallback without guitar/piano (FR-001, FR-003)
- [ ] T021 [beat-analysis] Implement `analysis/structure/features.py` to pass T020
- [ ] T022 [P] [beat-analysis] Failing tests `worker/tests/analysis/structure/test_novelty_boundaries.py`: checkerboard novelty peaks at block changes of a synthetic self-similarity matrix; peaks >= 4 frames apart; vocal switches holding >= 2 frames become boundaries; union dedupe within 1 frame; merge of segments < 4 frames (first/last < 2) into the more similar neighbour; limit to `maxSections` with `merged-to-limit`; snap to downbeat/beat/0.1 s; first section starts at 0, last ends at the duration (FR-002, FR-003)
- [ ] T023 [beat-analysis] Implement `analysis/structure/{novelty,boundaries}.py` to pass T022
- [ ] T024 [P] [beat-analysis] Failing tests `worker/tests/analysis/structure/test_clusters_labels.py`: clustering gives the same letter to repeated synthetic segments; every rule of data-model 2.3 in order (intro/outro, chorus by energy among repeating vocal clusters, verse, bridge after the first chorus, solo with the dominant stem or detail stem, instrumental, `few-repetitions` -> "Part A/B", `no-vocals`), numbering per kind, solo labels with the 004 display names (FR-002)
- [ ] T025 [beat-analysis] Implement `analysis/structure/{clusters,labels}.py` to pass T024

### audio-ingest (`engraving/score_view.py`, `documents/`, tests first)

- [ ] T026 [P] [audio-ingest] Failing tests `worker/tests/engraving/test_score_view.py`: a music21 MusicXML of N bars (single staff, grand staff, percussion, with and without pickup) renders to pages whose bar count is N; every box is inside 0..1 and inside its page; grand-staff boxes span both staves; bar times from a `BeatGridTempo` (uneven beats) and a `ManualTempo`; pickup start; last bar ends at the duration; each page contains Verovio's Leipzig `@font-face` (T011: kept, not added); no `<script>`/`foreignObject`/external href; `ScoreViewError` text on unreadable input (FR-010, FR-014, SC-003)
- [ ] T027 [audio-ingest] Implement `engraving/score_view.py` (`VerovioScoreViewRenderer`) to pass T026
- [ ] T028 [P] [audio-ingest] Failing tests `worker/tests/documents/test_setlist_pdf.py`: pypdf text of a 3-song request has the overview table in order, per-song headings, facts lines, notes, sections, lyrics stanzas, "No lyrics", "Lyrics shortened"; options switch parts off; `pageBreakPerSong` changes the page count; footer "page n of m"; a CJK character becomes U+FFFD with the overview note; DejaVu missing -> `DocumentError` (fonts check patched); 20 songs with 10,000-character lyrics under 10 s (FR-018, SC-008)
- [ ] T029 [audio-ingest] Implement `documents/{setlist_pdf,fonts}.py` (`ReportLabSetlistRenderer`) to pass T028

### stem-separation (`jobs/`, tests first)

- [ ] T030 [P] [stem-separation] Failing tests `worker/tests/jobs/test_practice_lane_store.py`: the `practice` lane runs one job at a time FIFO across the three kinds; stores persist `section-jobs/<id>.json`, `score-view-jobs/<id>/...`, `setlist-document-jobs/<id>/...`; restart -> `failed` "interrupted by restart"; delete removes files; 24 h sweep (fake clock); `Health.lanes.practice` counts (FR-001)
- [ ] T031 [stem-separation] Implement `jobs/practice_{lane,store}.py` to pass T030
- [ ] T032 [P] [stem-separation] Failing tests `worker/tests/analysis/structure/test_fake.py` (owned by stem-separation for the composition, file under its tests): the fake analyser follows data-model 2.4 for grids with and without reliability and for short songs
- [ ] T033 [stem-separation] Implement `analysis/structure/fake.py` and the factory (real/fake by settings) in `main.py` to pass T032 (fake file agreed with beat-analysis: stem-separation owns `fake.py` only)

### backend (tests first)

- [x] T034 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Sections/SectionRulesTests.cs`: 1..64 sections, ordering, overlap, bounds, min 1 s, label length/trim, instrument only for solo and only channels of the track, id ownership, revision rule; `SectionSnapper` (downbeat, beat, 0.1 s) (FR-002, FR-003, FR-006)
- [x] T035 [backend] Implement `Domain/Sections/*` to pass T034
- [x] T036 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Setlists/SetlistRulesTests.cs` and `.../Practice/PracticeRulesTests.cs`: setlist name/note/entry limits, mix-track match, duplicates allowed; session rules (12 h, future start, played <= span + 5, sections sum, kinds incl. `none`, percents 50..120); `PracticeSummaryCalculator` (local-day buckets with offsets -840..840, split at local midnight, last 7 days, per-section grouping by id or label+kind, best trainer percent, tempo points per day) (FR-015, FR-025, FR-027)
- [x] T037 [backend] Implement `Domain/Setlists/*`, `Domain/Practice/*` to pass T036
- [x] T038 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/PracticeFlowPersistenceTests.cs`: migration `AddPracticeFlow` applies on a 1.12.0 database; round trips of every new table; cascades (track delete -> sections, setlist entries, practice sessions; mix delete -> entry `MixId` null, session `MixId` null; asset delete -> score view); mix loop JSON with and without `sectionId` (FR-005, FR-028)
- [x] T039 [backend] Implement entities, configurations and the migration to pass T038
- [x] T040 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerPracticeClientTests.cs` with the worker delta examples: create/get/delete per job kind, page and file downloads, problem mapping (400/404/409/422), `Health.lanes.practice` parsed
- [x] T041 [backend] Implement `Worker/WorkerClient.Practice.cs` and job records to pass T040

### audio-engine (tests first)

- [x] T042 [P] [audio-engine] Failing tests `src/audio-engine/tests/loop-pass.test.ts`: `OnLoopPass` fires once per heard wrap with region, pass number and speed; seek or loop change resets; disabled loop never fires; at 50 % and 120 % speed (A-012-1) (FR-025)
- [x] T043 [audio-engine] Implement `src/audio-engine/src/loop-pass.ts` and wire it into the transport/frame loop to pass T042

### frontend (tests first)

- [x] T044 [P] [frontend] Failing tests `src/web-interop/tests/keymap.test.ts`: data-driven matching per scope; guard contexts (text, slider, listbox, grid, menu, dialog; Space/Enter on buttons/links); repeats ignored; Alt/meta never; `singleKey: false` drops non-Mod chords; pedal layer in pedal mode fires on non-text controls but not in text fields; reserved chords via `isReservedChord`; duplicates dropped; reserved (015) bindings never dispatched; `captureChord` reports once, Escape cancels, timeout; `registerShortcuts` without a map keeps the 1.0.0 behaviour (FR-019..FR-023)
- [x] T045 [frontend] Implement `src/web-interop/src/{keymap,capture,shortcuts}.ts` (web-interop 1.1.0) to pass T044
- [x] T046 [P] [frontend] Failing tests `src/web-interop/tests/preferences-v2.test.ts` and `tests/StemStudio.Web.Tests/Practice/KeyMapValidatorTests.cs`: schemaVersion 1 -> 2 defaults; invalid/conflicting overrides dropped and reported; 16 KB limit; validator rules equal to the TS rules on the shared `keymap-defaults.json`; pedal presets (FR-019, FR-022, FR-023)
- [x] T047 [frontend] Implement `preferences.ts` v2, `Practice/{KeyMapValidator,KeyMapDefaults,PedalPresets}.cs` to pass T046
- [x] T048 [P] [frontend] Failing tests `tests/StemStudio.Web.Tests/Practice/SectionNavigatorTests.cs` and `PracticeRecorderTests.cs` (fake `TimeProvider`): current/next/previous/restart rules (data-model 5.3) incl. "next" while looping moves the loop; recorder states, per-section split from positions, loop passes, trainer percent only for full section passes with the trainer on, 30 s flushes, flush on stop/leave, 10-minute session end, 409 stops the recorder, other errors retry; accuracy <= 2 s per 10 min (FR-004, FR-025, FR-029, SC-007)
- [x] T049 [frontend] Implement `Practice/{SectionNavigator,PracticeRecorder}.cs` to pass T048

**Checkpoint**: HO:foundational for beat-analysis, audio-ingest, stem-separation, backend, audio-engine, frontend.

---

## Phase 3: User Story 1 - Loop a section with one click (P1)

- [ ] T050 [P] [US1] [beat-analysis] Failing test `worker/tests/analysis/structure/test_synthetic_songs.py`: the 6-form synthetic set (generated in the test, T010 forms) through `StemsNoveltyAnalyzer`: recall >= 0.80 and precision >= 0.75 within one bar over the set, solo labelled with the right instrument in >= 5 of 6, runtime < 10 s for a 5-minute song (SC-001, SC-002, FR-033)
- [ ] T051 [US1] [beat-analysis] Implement `analysis/structure/analyzer.py` (`StemsNoveltyAnalyzer`, progress callbacks) to pass T050
- [ ] T052 [P] [US1] [stem-separation] Failing tests `worker/tests/api/test_section_routes.py`: create/get/delete per the delta; path guard 400 for a non-stem path; 404 missing input; 422 schema errors; completed result shape; progress; analyser errors -> `failed` with the text (FR-001)
- [ ] T053 [US1] [stem-separation] Implement `jobs/practice_service.py` (section part), `api/practice_routes.py` (section routes), composition and `Health.structure` to pass T052
- [x] T054 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Sections/SectionEndpointsTests.cs`: `getSections` (none/queued/ready), `detectSections` (202, 409 `track-not-ready`, `sections-edited`, `detection-running`, 503), the detect trigger after a completed separation (config on/off), `SectionJobApplier` (completed -> new ids, revision + 1; failed -> reason), worker job deleted after applying, re-separation keeps sections; contract tests against the delta (FR-001, FR-007, FR-008)
- [x] T055 [US1] [backend] Implement `Api/Sections/{SectionEndpoints,SectionService,SectionJobApplier,SectionDetectTrigger}.cs` to pass T054
- [x] T056 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Mixes/LoopSectionIdTests.cs`: save/load a mix with `loop.sectionId`; unknown id read back as null with the loop kept; 1.12.0 clients without the field unaffected (FR-005)
- [x] T057 [US1] [backend] Implement the `LoopRegion.SectionId` mapping to pass T056
- [x] T058 [P] [US1] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Practice/SectionLaneTests.cs`: lane renders sections with kind/label/bars and test ids; status texts (detecting, grid uncertain, failed + retry); first open with status `none` calls `detectSections` once; click loops the section (`MixerState` loop + `sectionId`, looping on, position moved into the section when outside); current section marked from positions; previous/next/restart buttons; keyboard (list of buttons, `aria-pressed`); trainer region label shows the section (A-012-3) (FR-004, FR-005, FR-007)
- [x] T059 [US1] [frontend] Implement `Components/Practice/SectionLane.razor`, `MixerState.Sections` and the section commands (`mixer.loop.section`, `mixer.section.*`) to pass T058
- [x] T060 [P] [US1] [qa] E2E `e2e/tests/sections.spec.ts` (fake stack): sections appear after import (<= 30 s), click loops, N/Shift+N/Backspace, older track triggers detection on mixer open, unreliable-grid note (fixture with `reliable: false`) (US1 scenarios 1-5)
- [x] T061 [US1] [frontend] Help topics `mixer-sections`, glossary `section`, `speed-trainer-region`; tour `practice-section` v1; help tests updated (FR-030)

**Checkpoint**: HO:us1.

## Phase 4: User Story 2 - Hands-free control with keys and pedals (P1)

- [x] T062 [P] [US2] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Shell/ShortcutOverlayTests.cs`: groups and rows from the live key map with `data-keys`, remapped keys and pedal keys shown, reserved 015 rows hidden, reachable at every level from help (`help-shortcuts`) and Ctrl+/ (FR-024)
- [x] T063 [US2] [frontend] Extend `ShortcutOverlay.razor` and the 004 `shortcuts` help topic (reviewed "014") to pass T062
- [x] T064 [P] [US2] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Pages/ShortcutSettingsTests.cs`: edit a command via capture, conflict message names the other command, reserved message, clear, reset all; pedal setup presets, learn left/right, pedal mode switch, test indicator; single-key switch in the Preferences panel at every level; saved to preferences and applied with `updateShortcuts` (FR-019, FR-021..FR-023)
- [x] T065 [US2] [frontend] Implement `Components/Pages/ShortcutSettings.razor`, the Preferences switch and the layout wiring (`registerShortcuts` with the key map; `mixer-keys.js` retired) to pass T064
- [x] T066 [P] [US2] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Mixer/MixerCommandsTests.cs`: every mixer command id of data-model 5.2 dispatches to the same `MixerState` action as its control (012 commands via the 012 handlers, A-012-2..4); unknown/reserved ids ignored (FR-019, FR-020)
- [x] T067 [US2] [frontend] Implement the command dispatcher to pass T066
- [x] T068 [P] [US2] [qa] E2E `e2e/tests/shortcuts-pedals.spec.ts`: every FR-020 command by keyboard; typing in a text field types; slider keeps arrows; remap with conflict and reset; pedal mode with PageDown/PageUp while a mute button has focus; single-key off; axe-core on the shortcut settings page (US2 scenarios 1-5, SC-006)
- [x] T069 [US2] [frontend] Help topics `shortcuts-custom`, `foot-pedals`; tour `hands-free` v1 (FR-030)

**Checkpoint**: HO:us2.

## Phase 5: User Story 3 - Edit the sections (P2)

- [x] T070 [P] [US3] [backend] Failing tests `tests/StemStudio.Api.Tests/Sections/ReplaceSectionsTests.cs`: `replaceSections` 200 with kept and new ids, origin `edited`, revision + 1; 409 `sections-changed`; 422 field errors per rule; detect with/without `overwrite` after edits (FR-006)
- [x] T071 [US3] [backend] Implement `replaceSections` to pass T070
- [x] T072 [P] [US3] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Practice/SectionEditorTests.cs`: split at playhead (snapped), merge keeps the first id/kind/label, rename, change kind (instrument select only for solo), move boundary by field and keyboard with the 1 s minimum, delete, add; renumbering unless named; Done saves, Cancel discards; 409 shows the reload prompt; overwrite confirmation for re-detect (Expert); confidence shown at Expert (FR-006, FR-007)
- [x] T073 [US3] [frontend] Implement `Components/Practice/SectionEditor.razor` to pass T072
- [x] T074 [P] [US3] [qa] E2E `e2e/tests/sections-edit.spec.ts`: split/merge/rename/re-kind/move, reload persists, two-tab conflict, re-detect confirmation (US3 scenarios 1-5)
- [x] T075 [US3] [frontend] Help topics `mixer-sections-edit`, `mixer-sections-detection` (FR-030)

**Checkpoint**: HO:us3.

## Phase 6: User Story 4 - The score follows the playback (P2)

- [ ] T076 [P] [US4] [stem-separation] Failing tests `worker/tests/api/test_score_view_routes.py`: create/get/pages/delete; allow-list accepts `assets/<id>/score.musicxml` and rejects other files; 409 before completion; result shape (FR-014)
- [ ] T077 [US4] [stem-separation] Implement the score-view part of `jobs/practice_service.py` and routes to pass T076
- [x] T078 [P] [US4] [backend] Failing tests `tests/StemStudio.Api.Tests/ScoreFollow/ScoreFollowEndpointsTests.cs`: `listTrackScores` (direct scores and scores from exported audio of the track, not others); view created automatically when a 008 score becomes ready; `createScoreView` idempotent, 409 `not-a-score`/`score-not-ready`, 503; applier stores pages and bars shifted by the range offset (008 range, exported asset range, both); `getScoreViewPage` headers (CSP, nosniff, cache) and 409 before ready; asset delete removes view files (FR-009, FR-014)
- [x] T079 [US4] [backend] Implement `Api/ScoreFollow/*` to pass T078
- [x] T080 [P] [US4] [audio-engine] Failing tests `src/audio-engine/tests/score-follow.test.ts`: per data-model 4 and the 2.4.0 delta - bar changes at boundaries on the heard clock, loop wrap jump, seek binary search, speed 50/75/100/120 % (A-012-1), none before the first/after the last bar, highlight percentages, host attributes, scroll once per system change, manual-scroll pause (fake timers), reduced motion, validation errors keep the old follower, detach cleanup, frame cost counter (FR-010, FR-011, SC-003, SC-004)
- [x] T081 [US4] [audio-engine] Implement `src/audio-engine/src/score-follow/{follower,cursor,dom,scroll}.ts` and the engine methods to pass T080
- [x] T082 [P] [US4] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Practice/ScorePanelTests.cs`: list and select, empty state with link to 008, preparing state polls, pages as `<img>` in `score-page-<n>`, attach/detach on select and track change, follow toggle, transpose note while transpose != 0, manual-tempo note, click a bar seeks, Shift+click sets a bar loop (Advanced), "Read bar" announcement (FR-009..FR-013)
- [x] T083 [US4] [frontend] Implement `Components/Practice/ScorePanel.razor` and `mixer.score.*` commands to pass T082
- [x] T084 [P] [US4] [qa] E2E `e2e/tests/score-follow.spec.ts`: one score per family (vocals, bass, guitar, piano, drums; fake transcriber + real view) - bar number at known times, loop wrap, 50 % and 120 % speed, range score, manual scroll pause; Chromium trace for SC-004 (US4 scenarios 1-6, FR-033)
- [x] T085 [US4] [frontend] Help topic `mixer-score-follow` (FR-030)

**Checkpoint**: HO:us4.

## Phase 7: User Story 5 - Setlists (P2)

- [ ] T086 [P] [US5] [stem-separation] Failing tests `worker/tests/api/test_setlist_document_routes.py`: create/get/file/delete; 422 for >100 songs or long lyrics lines; PDF media type (FR-018)
- [ ] T087 [US5] [stem-separation] Implement the setlist part of `jobs/practice_service.py` and routes to pass T086
- [x] T088 [P] [US5] [backend] Failing tests `tests/StemStudio.Api.Tests/Setlists/SetlistEndpointsTests.cs`: CRUD, `replaceSetlistEntries` (kept ids, order, field errors `track-not-found`, `mix-track-mismatch`), total duration, `removedEntries` after a track delete and its acknowledgement, mix delete -> `mixId` null (FR-015, FR-016)
- [x] T089 [US5] [backend] Implement `Api/Setlists/{SetlistEndpoints,SetlistService}.cs` to pass T088
- [x] T090 [P] [US5] [backend] Failing tests `tests/StemStudio.Api.Tests/Setlists/SetlistPdfTests.cs`: `SetlistContentBuilder` (facts, sections, lyrics lines, 10,000-character cut with `shortened`), options from the query, waits for the worker job with polling and timeout (fake time) -> 504 `document-timeout`, 409 `setlist-empty`, 503, file name sanitised, worker job deleted afterwards (FR-018)
- [x] T091 [US5] [backend] Implement `Api/Setlists/{SetlistPdfService,SetlistContentBuilder}.cs` to pass T090
- [x] T092 [P] [US5] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Pages/SetlistsTests.cs` and `.../Practice/SetlistBarTests.cs`: list/create/delete; editor add (track picker with mix), reorder by drag and by up/down buttons, note editing (Advanced), remove, removed-entries note, PDF dialog options (Advanced) and default download (Beginner); play mode opens the first entry's mixer with its mix, setlist bar position and note, next/previous with the unsaved-changes guard, `setlist.next/previous` commands, exit; "Add to setlist" on track and mix (FR-015..FR-018)
- [x] T093 [US5] [frontend] Implement `Components/Pages/{Setlists,SetlistEditor}.razor`, `Components/Practice/SetlistBar.razor`, rail link and actions to pass T092
- [x] T094 [P] [US5] [qa] E2E `e2e/tests/setlists.spec.ts`: create with 3 songs, reorder by keyboard, play through with next/previous (button, Ctrl/Cmd+Right, pedal preset "next-previous-song"), PDF download and pypdf text check in the test (order, notes, sections, lyrics), track delete note; axe-core on both pages (US5 scenarios 1-5, SC-008)
- [x] T095 [US5] [frontend] Help topics `setlists`, `setlist-play`, `setlist-pdf` (FR-030)

**Checkpoint**: HO:us5.

## Phase 8: User Story 6 - Practice log and progress (P2)

- [x] T096 [P] [US6] [backend] Failing tests `tests/StemStudio.Api.Tests/Practice/PracticeEndpointsTests.cs`: settings get/put; upsert create/replace with sections, 409 `practice-log-off`, 404 unknown track, 422 rules; list with paging; delete per track and all (`deleted` count); delete one; summary and overview with offsets; no outgoing HTTP call from any practice endpoint (handler spy) (FR-025..FR-028, SC-009)
- [x] T097 [US6] [backend] Implement `Api/Practice/{PracticeEndpoints,PracticeService}.cs` to pass T096
- [ ] T098 [P] [US6] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/Practice/PracticePanelTests.cs` and `.../Pages/PracticePageTests.cs`: today/week/total, per-section table (Advanced), day chart and tempo chart with table toggles and text summaries, off note with turn-on, first-use notice (ok / turn off), delete with confirmation; Practice page overview, enabled switch, delete all, sessions list (Expert); recorder wired to the mixer (state, positions, `OnLoopPass`, trainer state) (FR-025, FR-027, FR-028)
- [ ] T099 [US6] [frontend] Implement `Components/Practice/{PracticePanel,PracticeCharts}.razor`, `Components/Pages/PracticePage.razor`, rail link and recorder wiring to pass T098
- [x] T100 [P] [US6] [qa] E2E `e2e/tests/practice-log.spec.ts`: play with pauses -> totals; section loop with trainer passes at rising speed -> tempo reached; switch off -> nothing recorded (409 in the network log); delete -> zeros; the whole run's request log contains only the app's own origin (US6 scenarios 1-5, SC-009)
- [ ] T101 [US6] [frontend] Help topics `mixer-practice-log`, `practice-privacy` (FR-030)

**Checkpoint**: HO:us6.

---

## Phase 9: Polish and integration

- [ ] T102 [P] [quality-gate] Run `tools/quality/run_all.py` on the merged wave; refactor requests to owners; SIG report update
- [ ] T103 [P] [qa] Visual baselines for section lane, score panel, setlist pages, practice panel and shortcut settings (light/dark, 320 px and desktop); a11y suite extended (axe-core, keyboard-only walk of every new page) (FR-031, SC-006)
- [ ] T104 [qa] Execute the full E2E suite on the fake stack (all 014 specs + regression); hand-off with results
- [ ] T105 [CTO] Moderated checks: SC-005 (first-time chorus loop), NVDA browse mode on the mixer (SC-006), label quality on the owner's reference songs (record in STATUS)
- [x] T107 [backend] (added Phase 0 part 2; **decided 2026-11-17**, CTO-DECISIONS 014 "merge restore"; lane L4 of the combined round, after merging L3's HO:foundational) Failing tests first in `tests/StemStudio.Api.Tests/LibraryCare/`: a merge restore brings the `TrackSections`/`Sections` of every merged track and the `ScoreViews` row plus `assets/<id>/view/` files of every merged score asset; skipped parents skip them; setlists and practice sessions of the archive are **not** merged (test asserts none arrive and the live ones stay); then extend 013's `MergePlanner`/`LibraryMerger` (FR-005, FR-014)
- [x] T108 [backend] (added Phase 0 part 2; lane L4) Failing test first: `ManifestBuilder.BackendApiVersion` equals the `info.version` of the current full backend document linked as `Contract/backend-api.openapi.yaml` (combined round: `1.14.0`; backups record the API version); then bump it
- [ ] T106 [CTO] Manuals (constitution 1.3.1): user manual chapters "Song sections", "Score follows playback", "Setlists", "Keyboard and foot pedals", "Practice log and privacy"; technical manual: `practice` lane, `STEMSTUDIO_STRUCTURE_ANALYZER`, fonts, API list; rebuild PDFs and `build-manifest.json`; `docs/THIRD-PARTY-LICENSES.md` (ReportLab BSD, DejaVu fonts); `docs/ROADMAP.md` (web-interop 1.1.0 for 014); STATUS; README

---

## Acceptance of tasks without a red test (ready check 2026-11-17)

- Help-topic and tour tasks (T061, T063 help part, T069, T075, T085, T095, T101): the 004 help-content tests
  (`tests/StemStudio.Web.Tests/Help/**`) list every new topic id of ui-test-ids 1.13.0 / data-model 6, each
  `data-help-id` marker resolves to a topic, and the help text check passes; tours pass the tour schema test.
- Documentation tasks (T017, T106): the files named in the task exist with the listed sections;
  T106 additionally `node tools/docs/check.mjs` passes after the PDF rebuild.
- E2E authoring tasks (T060, T068, T074, T084, T094, T100): lanes typecheck and lint the spec
  (`npm run typecheck`, `npm run lint` in `e2e/`); the run against a stack is T104 (or the mid-wave checkpoint).
- CTO tasks (T105): the result per SC is recorded in `docs/STATUS.md`.

## Dependencies and execution order

- Phase 0 part 2 (T002-T011) needs 012 and 013 merged; T005 needs T002-T004; T007 last.
- The wave (Phase 1-8) starts after T007. Inside each owner: foundational before stories; each
  story's test task before its implementation task. Stories are independent except: US4 needs the
  008 score assets (present) and T043 (`OnLoopPass`) is used by US6; US5 PDF sections need US1's
  sections (degrades to "no sections").
- Frontend US1..US6 need the backend endpoints only through the fake API client; real wiring is
  verified in the E2E tasks.
- Polish after all stories.

## Execution plan per owner (parallel wave)

| Owner | Tasks (in order) | Hand-offs |
|---|---|---|
| architect | T001 (done); T002, T003, T004, T006, T010, T011 (parallel) -> T005 -> T007 | HO:phase0 |
| beat-analysis | T020 -> T021, T022 -> T023, T024 -> T025, T050 -> T051 | HO:foundational, HO:us1, HO:polish |
| audio-ingest | T026 -> T027, T028 -> T029 | HO:foundational, HO:polish |
| stem-separation | T030 -> T031, T032 -> T033, T052 -> T053, T076 -> T077, T086 -> T087 | HO:foundational, HO:us1, HO:us4, HO:us5, HO:polish |
| backend | T034 -> T035, T036 -> T037, T038 -> T039, T040 -> T041, T108, T054 -> T055, T056 -> T057, T070 -> T071, T078 -> T079, T088 -> T089, T090 -> T091, T096 -> T097, T107 (if decided) | HO:foundational, HO:us1, HO:us3..us6, HO:polish |
| audio-engine | T042 -> T043, T080 -> T081 | HO:foundational, HO:us4, HO:polish |
| frontend | T044 -> T045, T046 -> T047, T048 -> T049, T058 -> T059, T061, T062 -> T063, T064 -> T065, T066 -> T067, T069, T072 -> T073, T075, T082 -> T083, T085, T092 -> T093, T095, T098 -> T099, T101 | HO:foundational, HO:us1..us6, HO:polish |
| qa | T017, T018, T060, T068, T074, T084, T094, T100, T103, T104 | HO:us1..us6, HO:polish |
| devops | T014 | HO:polish |
| quality-gate | T015 -> T016, T102 | HO:polish |
| CTO | T105, T106 | STATUS |

## Task counts

| Lane | Tasks | Test-first pairs |
|---|---|---|
| architect | 9 (T001-T007, T010, T011) | - (contract skeleton tests inside T002/T003/T006) |
| beat-analysis | 8 | 4 |
| audio-ingest | 4 | 2 |
| stem-separation | 10 | 5 |
| backend | 24 | 11 (+ T107, T108 test-first inside the task) |
| audio-engine | 4 | 2 |
| frontend | 28 | 11 (+ 6 help-content tasks) |
| qa | 10 | E2E authored before integration |
| devops | 1 | check-first inside the task |
| quality-gate | 3 | 1 |
| CTO | 2 | - |
| **Total** | **103** | |
