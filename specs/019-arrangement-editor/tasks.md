---

description: "Task list for feature 019 Arrangement editor (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Arrangement Editor (practice versions)

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.3.1, ADR-028.

**Status**: planning round done (2026-11-16): contract deltas validated (Redocly 2.78.0),
`render-vectors.json` 1.2.0 generated (`--check` green), ADR-028 Proposed. Phase 0 runs when 019
starts, on top of the merged 012-018 contracts (worker 1.9.0, backend 1.17.0, engine 2.7.0, UI ids
1.17.0, render-vectors 1.1.0 from 015).

**Tests**: REQUIRED (constitution I). In every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component. No task needs a model or a GPU.

**Organization**: by user story (US1..US7); the [execution plan](#execution-plan-per-owner)
regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: agent from `.claude/agents/`; writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-019-<checkpoint>.md`. **CTO** = orchestrator.
- beat-analysis has no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-019-<checkpoint>.md` at `foundational` (if it has
foundational tasks), each story it takes part in and `polish`, with red -> green commit hashes
per task pair, exact coverage numbers and lint/SIG output.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| audio-ingest (`render/timeline.py`) | request shape, law | `schemas.py` (T001), data-model 3/6, vectors `timeline.spans`/`samples` (T004) | n/a (pure functions + tmp FLAC fixtures) |
| stem-separation (`api/`) | timeline validation | `schemas.py` validators (T001) | fake executor |
| backend | worker timeline, sections of 014, beat grid | worker 1.10.0 (T003), merged 014 backend contract | stub `HttpMessageHandler`, fake section/grid repositories |
| frontend | arrangements API, engine 2.8.0 | backend 1.18.0 (T003) + DTOs (T002), `types.ts` (T005) | fake `IStemStudioApiClient`, fake engine interop |
| audio-engine | plan shape, law | `types.ts` (T005), vectors (T004) | mocked `AudioContext` |
| qa | UI, fake stack | `ui-test-ids.md` 1.18.0 (T003), fake sections/grid (T010) | suite authored in the wave, executed in T081 |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [ ] T001 [architect] Worker contract code: `schemas.py` `RenderTimeline`, `RenderSegment`, `RenderJobRequest.timeline` (default None), `RenderResult.duration_seconds <= 1800`, cross-field validators (even F <= 4410, `frame_count > crossfade_frames`, `muted_inputs` < len(inputs), total <= 79,380,000), `CONTRACT_VERSION = "1.10.0"`; skeleton tests `worker/tests/test_render_timeline_contract.py`; worker contract tests switched to the 019 document (FR-011)
- [ ] T002 [P] [architect] .NET contract code: `src/StemStudio.Contracts/Arrangements/*` (DELTA 2), `MixSettingsDto/MixDto.ArrangementId`, `CreateAudioExportRequest.ArrangementId/.Arrangement`, `ExportRecipeDto.Arrangement`, `ApiRoutes.Arrangements*`, `ContractLimits` (data-model 8); skeleton tests `tests/StemStudio.Api.Tests/Contract/ArrangementContractTests.cs`; test csproj links -> 019 contracts; operationIds staged in `PendingArrangementOperations` (FR-016)
- [ ] T003 [P] [architect] Full contracts: `worker-api.openapi.yaml` 1.10.0 (= 1.9.0 + worker delta), `backend-api.openapi.yaml` 1.18.0 (= 1.17.0 + backend delta incl. `x-stemstudio-schema-additions` applied; section endpoint names aligned with the merged 014 contract), `ui-test-ids.md` 1.18.0, `redocly.yaml`; Redocly lint clean
- [ ] T004 [P] [architect] `render-vectors.json` 1.2.0 regenerated on top of the merged 015 1.1.0 (generator copies its sections unchanged; `baseVersion` = 1.1.0); `--check` in `scripts/test-all.*` via devops T009 (FR-005, SC-002)
- [ ] T005 [P] [architect] Engine: `src/audio-engine/src/types.ts` 2.8.0 (types + methods), `ENGINE_CONTRACT_VERSION`, stubs throwing `TIMELINE_NOT_IMPLEMENTED`, full `contracts/audio-engine.md` 2.8.0; Web `EngineMethods.ContractVersion = "2.8.0"` and the new `[JSInvokable] OnTimelinePositionChanged` no-op (agreed one-time edit, frontend owns it afterwards)
- [ ] T006 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker pytest/ruff/mypy, engine tests, `tools/quality/run_all.py` 0 errors; ADR-028 -> Accepted; hand-off `docs/handoffs/architect-019-phase0.md`

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T007 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `worker/.../render/timeline.py` imports only `schemas`, `render.mixing`, `render.reader`; `StemStudio.Domain.Arrangements` references neither `StemStudio.Api` nor `StemStudio.Persistence`; `StemStudio.Web` does not reference `StemStudio.Domain`; `src/audio-engine/src/timeline/*` does not import `engine.ts`
- [ ] T008 [quality-gate] Implement the rules in `tools/quality/dependency_check.py`; re-run `run_all.py`
- [ ] T009 [P] [devops] CI `CONTRACTS_DIR` and `scripts/test-all.{ps1,sh}` -> `specs/019-arrangement-editor/contracts`; add `generate_render_vectors.py --check` and the Redocly lint of the 019 documents to CI (write the CI step so it fails on a deliberately stale vectors file first, then green)
- [ ] T010 [P] [qa] Fake-stack fixture check first (failing test `e2e/tests/fixtures-arrangement.spec.ts`: the seeded reference track has a reliable grid with downbeats and >= 4 sections), then extend `tests/fixtures/` (sections + grid of the fake track, bars every 2 s) until green
- [ ] T011 [P] [qa] `docs/testing/TEST-STRATEGY.md` section (019): vectors with four consumers, browser law check, fake-stack audio oracle for arranged exports, traceability US1..US7 -> test files, per-family mute matrix (SC-008)

---

## Phase 2: Foundational (blocking inside each owner)

### backend (tests first)

- [ ] T012 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Arrangements/BarLinesTests.cs` + `ArrangementValidatorTests.cs`: bar lines and bar numbers (pickup, dedup 1 ms, end line); every **error** case of `timeline.resolve` yields exactly its `{key, code}` list (loaded from the linked contracts folder); collected not first-fail (FR-002, FR-012)
- [ ] T013 [backend] Implement `Domain/Arrangements/{ArrangementDocument,Region,SnapMode,BarLines,ArrangementValidator}.cs` to pass T012
- [ ] T014 [P] [backend] Failing tests `TimelineResolverTests.cs`: every **valid** `timeline.resolve` case yields the expected segments, frames, F, bars and warnings exactly; stale detection (`bar-lines-changed`) when the grid moved; stored unknown mutes removed with `unknown-channel:<id>` (FR-002, FR-017, SC-002)
- [ ] T015 [backend] Implement `TimelineResolver`, `ResolvedTimeline`, `TimelineSegment` to pass T014
- [ ] T016 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/ArrangementPersistenceTests.cs`: JSON round trip of regions; unique `(TrackId, NameKey)`; cascade on track delete; `Mixes.ArrangementId` SET NULL on arrangement delete; a database migrated from the 1.17.0 schema loads with no arrangements and null mix references (file SQLite)
- [ ] T017 [backend] Implement configurations and migration `AddArrangements` to pass T016

### audio-engine (tests first)

- [ ] T018 [P] [audio-engine] Failing tests `src/audio-engine/tests/timeline/plan-runs.test.ts`: plan validation messages (contracts/audio-engine.md 2, every rule); running arranged starts; runs per channel equal every `timeline.spans` case exactly (FR-004, FR-005, SC-002)
- [ ] T019 [audio-engine] Implement `src/timeline/{plan,runs}.ts` to pass T018
- [ ] T020 [P] [audio-engine] Failing tests `timeline/curves-clock.test.ts`: fade curves equal data-model 3.4 to 1e-9 (F = 4, 442, 2206), cached per F; `source(t)` equals every `timeline.clock` entry; `seek(source)` rule of data-model 3.7 (first occurrence after the playhead, wrap, cut-out time -> next segment, end) (FR-007, FR-015)
- [ ] T021 [audio-engine] Implement `src/timeline/{curves,clock}.ts` to pass T020

### audio-ingest (tests first)

- [ ] T022 [P] [audio-ingest] Failing tests `worker/tests/render/test_timeline_law.py`: runs and gains equal `timeline.spans`; summing the synthetic signals equals every `timeline.samples` case to 1e-9; reads outside the file are silence (FR-005, SC-002)
- [ ] T023 [audio-ingest] Implement `render/timeline.py` (runs, gains, per-block contribution) to pass T022
- [ ] T024 [P] [audio-ingest] Failing tests `worker/tests/render/test_timeline_reader.py` (FLAC fixtures in `tmp_path`): `TimelineInputs.blocks()` over arranged frames equals a whole-array reference for several block sizes; seeks only inside runs; `mutedInputs` silences exactly those inputs; arranged range start/end in frames (clamped); memory bounded to one block per input + one run lookahead
- [ ] T025 [audio-ingest] Implement `TimelineInputs` in `render/timeline.py` (reuses `reader.py` opening/format checks) to pass T024

### frontend (tests first)

- [ ] T026 [P] [frontend] Failing tests `tests/StemStudio.Web.Tests/Arrange/ArrangeEditorStateTests.cs`: every command of data-model 5 changes the document as specified; each is one undo step; 100-step cap drops the oldest; redo cleared by a new command; label typing coalesced; local limit refusal with the data-model 2.3 codes (no HTTP call); dirty flag (FR-003, FR-009, FR-012)
- [ ] T027 [frontend] Implement `Web/Arrange/{ArrangeEditorState,ArrangeCommands,UndoHistory,BarRange,TimelineFormat}.cs` to pass T026
- [ ] T028 [P] [frontend] Failing tests `Api/ArrangementsClientTests.cs` (fake handler with the 1.18.0 examples): list/create/get/put/delete/duplicate/resolve; 409 conflict with `currentRevision`, 422 errors mapped to field messages
- [ ] T029 [frontend] Implement the API client methods to pass T028

**Checkpoint**: HO:foundational for backend, audio-engine, audio-ingest and frontend.

---

## Phase 3: User Story 1 - Build from sections and play gaplessly (P1) MVP

**Goal**: create (sections / whole song), play the arranged timeline, lane + playhead, metronome.
**Independent test**: quickstart A1-A3; E2E `arrangement.spec.ts` "build and play".

- [ ] T030 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Arrangements/CreateAndResolveTests.cs`: `POST .../arrangements` seed `song` / `sections` (regions per 014 section, snapped, labelled, `sectionId`), `no-sections` 409, `track-not-ready` 409, `arrangement-limit` 409 at 50, default unique name; `GET` list/get with timeline; `POST .../resolve` 200/422 without storing; responses validate against 1.18.0; un-stage the operationIds (FR-001, FR-003, FR-016)
- [ ] T031 [US1] [backend] Implement `Api/Arrangements/{ArrangementEndpoints,ArrangementService,SectionSeed}.cs` to pass T030
- [ ] T032 [P] [US1] [audio-engine] Failing tests `timeline/scheduler.test.ts`: on `setTimeline` + `play`, runs starting within 2 s are started with `start(when, offset, duration)` exactly per vectors, fades via `setValueCurveAtTime`, all channels from one anchor; later runs on later ticks only; a late tick starts a run with an advanced offset (no shift); with the 012 speed factor `r` context durations are divided by `r` - **first confirm which 012 shape shipped (research R-04) and record it in the hand-off**; song order: no change to any 2.7.0 test (FR-005, SC-003)
- [ ] T033 [US1] [audio-engine] Implement `timeline/scheduler.ts`, the transport timeline mode and `setTimeline`/`getTimelinePosition`/`getTimelineDuration`/`OnTimelinePositionChanged` (replace the T005 stubs) to pass T032
- [ ] T034 [P] [US1] [audio-engine] Failing tests `timeline/metronome-map.test.ts`: arranged clicks equal `timeline.metronome`; accents kept; `bpmOverride` on arranged time; 012 count-in uses the arranged grid; karaoke clock and `OnPositionChanged` report source time across a boundary (FR-007)
- [ ] T035 [US1] [audio-engine] Implement `timeline/metronome-map.ts` and the metronome/karaoke clock wiring to pass T034
- [ ] T036 [P] [US1] [frontend] Failing bUnit tests `Arrange/ArrangePanelTests.cs`: tab `mixer-tab-arrange`, `arrange-select` (song order + list), New from sections/song (disabled with reason when no sections), lane renders regions with data attributes, playhead and `aria-current` from `OnTimelinePositionChanged`, `setTimeline` called with the resolved plan, song order -> `setTimeline(null)` (FR-006)
- [ ] T037 [US1] [frontend] Implement `Components/Arrange/{ArrangePanel,ArrangeToolbar,RegionLane}.razor` and the mixer tab to pass T036
- [ ] T038 [P] [US1] [qa] E2E `e2e/tests/arrangement.spec.ts` (`@arrangement`) "build and play": A1, A2 (playhead passes every boundary, region highlight, metronome on), A3; page object `e2e/pages/arrange.page.ts`

---

## Phase 4: User Story 2 - Cut, reorder and repeat by bars (P1)

**Goal**: bar selection, add/split/move/duplicate/remove/repeat, live edits, undo/redo, loops.
**Independent test**: quickstart A4-A6.

- [ ] T039 [P] [US2] [audio-engine] Failing tests `timeline/live-edit.test.ts`: `setTimeline` while playing keeps the arranged position (clamped; beyond the end -> stopped at 0), old runs stopped at the switch time; `seekTimeline`; `setTimelineLoop` validation, wrap crossfade when not contiguous, 012 loop-pass event on wrap; `setLoop(enabled)` refused while arranged, `enabled: false` accepted; `loadTrack` clears the timeline (FR-008, FR-015)
- [ ] T040 [US2] [audio-engine] Implement live replanning, `seekTimeline`, `setTimelineLoop` and the `setLoop` guard to pass T039
- [ ] T041 [P] [US2] [frontend] Failing bUnit tests `Arrange/SongStripTests.cs` + `RegionEditTests.cs`: bar selection by click, drag, keyboard and the from/to fields; Add region; Split at the playhead bar; move left/right; duplicate; remove; repeat stepper; undo/redo buttons; limit messages in `arrange-error`; resolve debounced 150 ms and the previous plan kept on 422; "Loop this region" -> `setTimelineLoop` (FR-003, FR-008, FR-009, FR-012, FR-015)
- [ ] T042 [US2] [frontend] Implement `SongStrip.razor`, `RegionInspector.razor` (edges, repeat, move, duplicate, split, remove, play, loop) to pass T041
- [ ] T043 [P] [US2] [qa] E2E `arrangement.spec.ts` "edit": A5 (history), A6 (limits), split-then-play continuity check (no `arrange-error`, playhead continuous)

---

## Phase 5: User Story 3 - Drop an instrument in a part (P1)

**Goal**: per-region channel mutes for every family, parent/detail rule, lyrics kept.
**Independent test**: quickstart A7.

- [ ] T044 [P] [US3] [backend] Failing tests `ArrangementMutesTests.cs`: mutes validated against the track's channels (standard stems, kept detail stems of split parents), duplicates 422, detail of a non-split parent 422 on save, tolerated with a warning on read (FR-004)
- [ ] T045 [US3] [backend] Implement the channel-set provider and validator wiring to pass T044
- [ ] T046 [P] [US3] [audio-engine] Failing tests `timeline/mutes.test.ts`: parent mute silences an expanded group and the collapsed parent; detail mute only when expanded; mixer mute/solo x region mute (both must allow); expanding during a timeline builds runs for the details from the switch time; region mutes sit before the strip (meters/effects untouched) (FR-004)
- [ ] T047 [US3] [audio-engine] Implement mute handling in runs/scheduler/groups to pass T046
- [ ] T048 [P] [US3] [frontend] Failing bUnit tests `Arrange/RegionMutesTests.cs`: one `region-mute-<channel>` per channel of the track (six families + kept detail stems, same order as the mixer), badges in the lane and `aria-label`, `region-mute-note` for a stored missing channel, karaoke banner stays attached when vocals are muted (FR-004, FR-007)
- [ ] T049 [US3] [frontend] Implement the mute section of the inspector and lane badges to pass T048
- [ ] T050 [P] [US3] [qa] E2E `arrangement.spec.ts` "mutes": one region per family muted (vocals, drums, bass, guitar, piano, other) plus `drums.snare`, checked through engine meters (`data-peak-db` of the muted strip at silence inside its region, non-silent elsewhere) (SC-008)

---

## Phase 6: User Story 4 - Save, reuse and attach to a mix (P1)

**Goal**: save/rename/duplicate/delete, conflicts, unsaved guard, mix reference, stale flag.
**Independent test**: quickstart A8-A10, A16.

- [ ] T051 [P] [US4] [backend] Failing tests `ArrangementUpdateTests.cs`: PUT increments revision, stale revision -> 409 with `currentRevision`, name rules and case-insensitive uniqueness, duplicate naming " (copy)" + suffixes and limit, DELETE sets referencing mixes to null and is reported in `usedByMixes` before; mix PUT with an arrangement of another track -> 422; mix GET returns `arrangementId`; `stale` flag after a grid change (FR-001, FR-009, FR-010, FR-017)
- [ ] T052 [US4] [backend] Implement update/delete/duplicate and the mix extension to pass T051
- [ ] T053 [P] [US4] [frontend] Failing bUnit tests `Arrange/SaveFlowTests.cs`: save, save as, duplicate, delete confirmation listing the mixes, unsaved guard (save/discard/stay) on navigation and on switching arrangement, conflict dialog (reload/copy), save mix with the active saved arrangement (unsaved edits -> "save arrangement first"), opening a mix activates its arrangement, `arrange-snap-bar-lines` when stale (FR-009, FR-010, FR-017)
- [ ] T054 [US4] [frontend] Implement the save flow, `ArrangeGuardDialog.razor` and the mix integration (`mix-arrangement-<mixId>` badge) to pass T053
- [ ] T055 [P] [US4] [qa] E2E `arrangement.spec.ts` "save": A8, A9 (two browser contexts), A10

---

## Phase 7: User Story 5 - Export the arranged version (P2)

**Goal**: 007 export with a timeline, exact length, snapshot recipe.
**Independent test**: quickstart A11-A12.

- [ ] T056 [P] [US5] [backend] Failing tests `tests/StemStudio.Api.Tests/Assets/ArrangedExportTests.cs`: `TimelineExportMapper` yields every `timeline.spans` `workerSegments`; `arrangementId` vs inline `arrangement` (exactly one, 422 when both, 404 unknown, other track 422); worker request carries `timeline`; range validated against the arranged length; contents ", arranged (<name>)" and default name; recipe snapshot; retry renders from the snapshot after the arrangement was edited and after it was deleted; raw mode with a timeline (FR-011)
- [ ] T057 [US5] [backend] Implement `TimelineExportMapper` and the `ExportService`/worker client extension to pass T056
- [ ] T058 [P] [US5] [stem-separation] Failing API tests `worker/tests/api/test_render_timeline_routes.py`: a timeline request -> 202 and runs in the render lane; bad index / odd F / `frameCount <= F` / total too long -> 422 with field paths; responses validate against worker 1.10.0 (FR-011)
- [ ] T059 [US5] [stem-separation] Map the T001 validators to the 422 problem format in `api/render_routes.py` to pass T058
- [ ] T060 [P] [US5] [audio-ingest] Failing tests `worker/tests/render/test_executor_timeline.py`: output length = arranged length (or range in arranged time); two passes and clip protection over arranged audio; a single contiguous segment over the whole song renders bit-identical to the same request without a timeline (SC-004); progress monotonic; cancellation between blocks; MP3 length rule of 007 (FR-011, SC-004)
- [ ] T061 [US5] [audio-ingest] Wire `TimelineInputs` into `render/executor.py` to pass T060; measure a 10-minute arranged 6-stem MP3 320 render on the reference machine and record it in the hand-off (research R-07, SC-006)
- [ ] T062 [P] [US5] [frontend] Failing bUnit tests `Export/ExportArrangementTests.cs`: `export-arrangement` checked by default when a timeline is active (absent in song order), sends `arrangementId` for a saved clean arrangement and `arrangement` for unsaved edits, range fields in arranged time, library shows the contents text (FR-011)
- [ ] T063 [US5] [frontend] Implement the export dialog option and `arrange-export` shortcut to pass T062
- [ ] T064 [P] [US5] [qa] E2E `e2e/tests/arrangement-export.spec.ts`: A11 (WAV length +/- 1 ms, contents, audio oracle: expected arranged output computed from the served fake stems with the vector law), A12 (range), song-order identity export

---

## Phase 8: User Story 6 - Keyboard and screen reader (P2)

**Goal**: complete keyboard model, announcements, scoped shortcuts, non-drag alternatives.
**Independent test**: quickstart A13.

- [ ] T065 [P] [US6] [frontend] Failing bUnit tests `Arrange/ArrangeKeyboardTests.cs`: every key of research R-09 on lane and strip (focus, Alt+arrows, + / -, Ctrl+D, Delete, S, M, Enter, L, Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y, F1); roles/`aria-*` of lane, options, playhead; `arrange-live` announcement per command; no handler outside the timeline (Space keeps its global meaning) (FR-013)
- [ ] T066 [US6] [frontend] Implement `ArrangeKeyMap.cs`, roving focus and announcements to pass T065
- [ ] T067 [P] [US6] [qa] E2E `e2e/tests/arrangement-keyboard.spec.ts`: A13 keyboard only, focus visible on every step, axe scan of the open editor 0 violations (SC-007)

---

## Phase 9: User Story 7 - Levels, help, read-only view (P3)

**Goal**: level rules R19-1..R19-3, Beginner chip, help topics, record-disabled reason.
**Independent test**: quickstart A14, A15, A17.

- [ ] T068 [P] [US7] [frontend] Failing bUnit tests `Arrange/ArrangeLevelsTests.cs` + help catalogue tests: R19-1..R19-3; chip with "Play song order"; every topic of DELTA 6 exists, validates against the help schema and resolves its anchors; feature-catalogue ids; record button disabled with `record-disabled-reason` while arranged (FR-014, FR-015)
- [ ] T069 [US7] [frontend] Implement `ArrangeChip.razor`, level gates, help topics `Help/topics/mixer/arrange*.md` + glossary `arrangement`, record guard to pass T068
- [ ] T070 [P] [US7] [qa] E2E `e2e/tests/arrangement-levels.spec.ts`: A14, A15, A17, help topic opens from each anchor

---

## Phase 10: Polish (per owner)

- [ ] T071 [P] [qa] `e2e/tests/timeline-law.spec.ts` (no stack): built engine + `OfflineAudioContext` renders every `timeline.samples` case, compared at 1e-4 (research R-12; fallback test-only helper requested from audio-engine if needed) (SC-002)
- [ ] T072 [P] [audio-engine] Load test (mocked context): 256 segments x 22 channels -> at most 2 live sources per channel, scheduling cost per tick bounded; `dispose()` clears the plan; coverage >= 90 % line/branch; HO:polish
- [ ] T073 [P] [backend] Contract tests for every 1.18.0 operation and example, readable problem texts (limit names in `detail`), coverage; HO:polish
- [ ] T074 [P] [audio-ingest] Coverage, mypy strict, ruff, SIG for `render/timeline.py`; HO:polish
- [ ] T075 [P] [stem-separation] Coverage and contract-validation sweep of the render routes; HO:polish
- [ ] T076 [P] [frontend] Design review against 004 (lane, strip, inspector at 320 px and desktop, reduced motion, dark theme), coverage, SIG; HO:polish
- [ ] T077 [P] [quality-gate] `run_all.py` across the wave, `docs/quality/SIG-REPORT.md` update, refactor requests to owners
- [ ] T078 [P] [devops] CI matrix includes `@arrangement` and `timeline-law.spec.ts`; HO:polish

---

## Phase 11: Integration (CTO, after the wave)

- [ ] T079 [architect] Reconciliation: fold any wave refinements into data-model, contracts and ADR-028 (no wire change without a version note); hand-off `docs/handoffs/architect-019-reconciliation.md`
- [ ] T080 [CTO] Merge the wave branches; resolve additive contract merges with 017/018
- [ ] T081 [CTO] `scripts/test-all.*` green; E2E on the fake stack (`@arrangement`) and the full regression suite (song order unchanged)
- [ ] T082 [CTO] Manuals (constitution 1.3.1): user manual chapter "Arrangements: practice versions" (build, repeat, mutes per instrument, save with a mix, export, keyboard, levels, limits); technical manual (arrangement API, worker timeline, engine 2.8.0, limits, render-vectors 1.2.0); `cd tools/docs && npm ci && npm run browser && npm run build`; commit sources, PDFs and `build-manifest.json`
- [ ] T083 [CTO] Moderated checks on the reference machine: SC-001 (< 30 s chorus x4), SC-005 (edit-to-ear <= 200 ms), SC-006 (export <= 30 s), SC-007 (keyboard task), listening check of 10 boundaries per family
- [ ] T084 [CTO] `docs/STATUS.md`, ROADMAP note; backlog items: 011 agent tools for arrangements, recording while arranged (015), unrolled arranged score/lyrics for 020 handouts

---

## Dependencies

- Phase 0 -> everything. Phase 1 and Phase 2 per owner in parallel. Inside an owner: test task ->
  its implementation task; stories in order US1 -> US2 -> US3 -> US4 -> US5 -> US6 -> US7, but
  owners progress independently (fakes per the cross-owner rule).
- qa E2E tasks are authored in the wave against `ui-test-ids.md` and executed in T081.
- T061 measurement and T032 (012 shape) feed T079.

## Parallel example (wave start)

```text
backend:      T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T030 ...
audio-engine: T018 -> T019 -> T020 -> T021 -> T032 ...
audio-ingest: T022 -> T023 -> T024 -> T025 -> T060 ...
frontend:     T026 -> T027 -> T028 -> T029 -> T036 ...
qa:           T010, T011 -> T038 ...
quality-gate: T007 -> T008;  devops: T009
```

## Execution plan per owner

| Owner | Tasks | Count | Hand-offs |
|---|---|---|---|
| architect | T001-T006, T079 | 7 | phase0, reconciliation |
| backend | T012-T017, T030, T031, T044, T045, T051, T052, T056, T057, T073 | 15 | foundational, us1, us3, us4, us5, polish |
| audio-engine | T018-T021, T032-T035, T039, T040, T046, T047, T072 | 13 | foundational, us1, us2, us3, polish |
| audio-ingest | T022-T025, T060, T061, T074 | 7 | foundational, us5, polish |
| stem-separation | T058, T059, T075 | 3 | us5, polish |
| frontend | T026-T029, T036, T037, T041, T042, T048, T049, T053, T054, T062, T063, T065, T066, T068, T069, T076 | 19 | foundational, us1-us7, polish |
| qa | T010, T011, T038, T043, T050, T055, T064, T067, T070, T071 | 10 | setup, us1-us7, polish |
| quality-gate | T007, T008, T077 | 3 | setup, polish |
| devops | T009, T078 | 2 | setup, polish |
| CTO | T080-T084 | 5 | integration |
| **Total** | | **84** | |

MVP scope: Phase 0-3 (US1) gives a playable "chorus 4x" practice version; US2-US4 complete the
P1 set; US5-US7 follow.
