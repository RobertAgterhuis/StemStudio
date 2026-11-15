---

description: "Task list for feature 002 detail separation (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Detail Separation

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/](contracts/),
constitution 1.1.0, ADR-009, ADR-010.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off.

**Organization**: grouped by user story (US1..US5); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-002-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- audio-ingest and beat-analysis have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-002-<checkpoint>.md` at `foundational`, every story it
takes part in (`us1`..`us5`) and `polish`, with red -> green commit hashes per task pair, exact
coverage numbers and lint/SIG output. Marker `HO:<checkpoint>` in the execution plan.

## Cross-owner rule (binding)

No task needs another owner's code at runtime during the wave; every dependency goes through a
committed contract and is faked:

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`) | detail models | `ports.py` `DetailModel`/`DetailModelProvider` (T001) | fake models returning scaled copies |
| backend | worker detail API | `contracts/worker-api.openapi.yaml` 1.1.0 | stub `HttpMessageHandler` with the contract examples |
| frontend | backend detail API, engine v2 | `backend-api.openapi.yaml` 1.2.0 + Contracts DTOs (T003); `audio-engine.md` 2.0.0 + `types.ts` (T004) | fake `IStemStudioApiClient`; bUnit JSInterop |
| audio-engine | detail stem files | `types.ts` `DetailGroup` | stubbed `fetch`, mocked `AudioContext` |
| qa | UI | `contracts/ui-test-ids.md` 1.1.0 | suite authored in the wave, executed in the integration wave |
| devops | new env vars, deps | plan.md, quickstart.md, T001/T002 | skeleton |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] Worker contract code: `schemas.py` (`CONTRACT_VERSION = "1.1.0"`; `ParentStemName`, `DetailStemName`, `DetailJobState`, `ParentOutcomeKind`, `CreateDetailJobRequest`, `DetailJob`, `DetailStemInfo`, `OutcomeMetrics`, `ParentOutcome`, `DetailResult` mirroring worker-api 1.1.0); `ports.py` (`DetailModel` protocol `separate(audio, progress) -> dict[str, ndarray]`, `DetailModelProvider.get(model_id) -> DetailModel`, `DetailAdapters`, errors `DetailModelUnavailableError`, `DetailSeparationError`, `DetailCancelledError`); `config.py` (`model_dir` from `STEMSTUDIO_MODEL_DIR` default `data/models`, `fake_detail_step_seconds` from `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS` default 1.0); factory stub `detail/__init__.py::create_detail_adapters(settings) -> DetailAdapters` raising `NotImplementedError`; skeleton tests in `worker/tests/test_ports.py`, `worker/tests/test_schemas.py` (FR-002..FR-007)
- [x] T002 [P] [architect] Worker dependencies: `onnxruntime==1.32.0`, `rotary-embedding-torch==0.9.2` in `worker/pyproject.toml` + `uv.lock`; ruff `extend-exclude`, mypy `ignore_errors` and coverage `omit` for `src/stemstudio_worker/detail/vendor/**`; record licences in `docs/handoffs/architect-002-plan.md`
- [x] T003 [P] [architect] .NET contract DTOs in `src/StemStudio.Contracts`: `Tracks/{DetailPhaseDto, DetailPhaseStatusDto, ParentOutcomeDto, ParentOutcomeKindDto, DetailStemDto}.cs`, `TrackDto.Detail` (always serialized, defaults to the `none` phase), `Mixes/DetailChannelSettingsDto.cs`, `MixSettingsDto` + `Expanded` and `DetailChannels` (default empty), `DetailNames.cs` (parents, detail names, display order, allowed names per parent), `ApiRoutes.{StartDetail, CancelDetail, DetailStem}`, `ContractLimits` additions; XML docs (FR-007, FR-015)
- [x] T004 [P] [architect] Engine contract code `src/audio-engine/src/types.ts` 2.0.0 (`ParentStemName`, `DetailStemName`, `ChannelId`, `DetailGroup`, `LoadTrackRequest.groups?`, `setExpanded`, `ChannelId` in `setChannel`/`attachMeter`/`detachMeter`); one-time agreed edit outside the lane: `ENGINE_CONTRACT_VERSION = '2.0.0'` in `index.ts` and a `setExpanded` stub that rejects "not implemented" in `engine.ts` so the skeleton compiles (FR-012..014)
- [x] T005 [architect] Verify the skeleton: `dotnet build` zero warnings, `uv run pytest`, `npm test` green; Redocly lint of the 002 contracts; hand-off `docs/handoffs/architect-002-plan.md`

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T006 [P] [quality-gate] Write failing tests in `tools/quality/tests/` asserting that `sig_check.py`, `duplication.py` and `dependency_check.py` skip `worker/src/stemstudio_worker/detail/vendor/**` and that `detail` may import only `ports`, `schemas`, `config` and its own `vendor` (plan.md Complexity Tracking)
- [ ] T007 [quality-gate] Implement the exclusion and the dependency rule; re-run `tools/quality/run_all.py` and update `docs/quality/SIG-REPORT.md`
- [ ] T008 [P] [devops] `docker-compose.yml`: worker `STEMSTUDIO_MODEL_DIR=/models` (existing `model-cache` volume), comment on the ~1.9 GB first download and the >= 20 GB Docker memory need; `scripts/dev.{ps1,sh}` pass `STEMSTUDIO_MODEL_DIR` and `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS`; CI `CONTRACTS_DIR` and `scripts/test-all.{ps1,sh}` -> `specs/002-detail-separation/contracts`; `.github/workflows/e2e-real.yml` also runs the detail integration test with the cached model volume and a 60 min timeout
- [ ] T009 [P] [qa] Write failing tests `tests/fixtures/tests/test_generate_detail_fixtures.py` (files exist, 44.1 kHz stereo, manifest values reproduced within 0.1 dB, deterministic hashes)
- [ ] T010 [qa] Implement `tests/fixtures/generate_detail_fixtures.py`: `detail_silent_parent.wav` (-80 dBFS noise), `detail_bleed_parent.wav` (4 % active frames), `detail_drums_{kick,snare,hihat}.wav` + their sum, `detail_vocals_{lead,backing}.wav` + sum, `detail_instruments_{strings,brass}.wav` + sum, `detail_10s_band.wav` (input of the integration test); `tests/fixtures/detail-manifest.json` with expected outcomes per parent (SC-002, SC-003)
- [ ] T011 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: detail fakes per port, second integration test, `@detail` E2E scope, traceability US1..US5 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

### stem-separation (tests first)

- [ ] T012 [P] [stem-separation] Failing tests `worker/tests/detail/test_gating.py` on synthetic signals: frame levels (2048/512, mono), parent gate (skip when < 8 % of frames > -40 dBFS; boundary cases), sub-stem gate (energy >= -20 dB rel. parent and >= 5 % frames above max(-50 dBFS, parent level - 25 dB)) (FR-003, FR-004)
- [ ] T013 [stem-separation] Implement `detail/names.py` and `detail/gating.py` (named constants from research.md D-04) to pass T012
- [ ] T014 [P] [stem-separation] Failing tests `worker/tests/detail/test_folding.py` and `test_reassign.py`: DrumSep 6 -> 5 (`ride + crash -> cymbals`, `hh -> hihat`), Mega 53 -> 8 families (research.md D-05 table; unlisted outputs ignored), shares of a family over 1..3 parents sum to the family within 1e-5 and follow the parents' power (FR-002)
- [ ] T015 [stem-separation] Implement `detail/folding.py` and `detail/reassign.py` to pass T014
- [ ] T016 [P] [stem-separation] Failing tests `worker/tests/detail/test_checks.py`: rest = parent - sum(kept); model residual > -15 dB -> not-reliable; family sum > parent + 1 dB -> not-reliable; < 2 kept -> nothing-further; reconstruction of 16-bit FLAC round trip <= -30 dB, a clipped rest fails it (FR-005, FR-006, SC-002)
- [ ] T017 [stem-separation] Implement `detail/checks.py` to pass T016
- [ ] T018 [P] [stem-separation] Failing tests `worker/tests/detail/test_download.py` with a fake opener: first-use download to `<model_dir>/detail/<id>/<file>.part`, SHA-256 verified then renamed, existing verified file skips the network, mismatch deletes and raises "Detail model download was corrupt; try again.", network error raises "Detail model could not be downloaded; try again when online." (FR-018)
- [ ] T019 [stem-separation] Implement `detail/registry.py` (three `ModelSpec`s from research.md D-07) and `detail/download.py` to pass T018
- [ ] T020 [P] [stem-separation] Failing tests `worker/tests/jobs/test_detail_store.py`, `test_detail_service.py`: persist/load `detail-jobs/*.json`; create is idempotent per `audioSha256` for queued/running/completed, new job after failed/cancelled; unknown standard job -> 404; not completed or stems missing -> 409; cancel queued -> cancelled at once, running -> flag + cancelled, terminal -> 409 (FR-010, FR-016)
- [ ] T021 [stem-separation] Implement `jobs/detail_store.py`, `jobs/detail_service.py` to pass T020
- [ ] T022 [P] [stem-separation] Failing tests `worker/tests/jobs/test_scheduler.py` (fake step functions, fake clock): a submitted standard job runs before the next detail step; detail steps interleave in FIFO job order; stop() waits for the current unit; a crashing step does not stop the thread (FR-011, SC-006)
- [ ] T023 [stem-separation] Implement `jobs/scheduler.py` and switch `jobs/runtime.py` from `JobQueue` to the scheduler (standard behaviour unchanged; existing tests stay green)

### backend (tests first)

- [ ] T024 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Tracks/DetailPhaseTests.cs`: state machine data-model 4.1 (forward only, cancel from queued/running, start/retry from none/failed/cancelled, only when the track is completed), progress never decreases, error iff failed, parents/stems validated against the allowed names per parent with `rest` last, completed requires every split parent to have >= 2 stems + rest; detail changes never touch `Track.Status`/`Stems` (FR-001, FR-007, FR-008)
- [ ] T025 [backend] Implement `src/StemStudio.Domain/Tracks/Detail/{DetailPhase,ParentOutcome,DetailStem,DetailPhaseStatus,ParentOutcomeKind}.cs` and `Track` members (`DetailPhase`, `RequestDetail`, `ApplyDetailProgress`, `CompleteDetail`, `FailDetail`, `CancelDetail`, `CanDelete`) to pass T024
- [ ] T026 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Mixes/MixDetailTests.cs`: `Expanded` only parents with outcome split, unique; `DetailChannels` at most one per (parent, stem), stem must exist on the track, value ranges as `ChannelSettings`; old mixes (empty) valid; duplicate copies both (FR-015, SC-005)
- [ ] T027 [backend] Implement `src/StemStudio.Domain/Mixes/DetailChannelSettings.cs` and the `Mix`/`MixSettings` extensions to pass T026
- [ ] T028 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/DetailPersistenceTests.cs`: JSON round trip of `DetailPhase`, `Expanded`, `DetailChannels`; a database migrated from the 001 schema with existing rows loads with null detail and empty mix detail settings (file-based SQLite)
- [ ] T029 [backend] Implement configurations and migrations `AddDetailPhase`, `AddMixDetailSettings` (`dotnet ef migrations add ...`) to pass T028
- [ ] T030 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerDetailClientTests.cs` with the worker 1.1.0 examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/detail-*.json`: create 200/202, get, cancel 200/409, result, detail stem stream, 404/409 problems, unreachable -> `WorkerUnavailableException`
- [ ] T031 [backend] Implement `IWorkerClient` detail methods, `WorkerDetailJob`, `WorkerDetailResult` to pass T030
- [ ] T032 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Storage/DetailStemFileStoreTests.cs`: path `<DataDir>/tracks/<id>/detail/<parent>/<name>.flac` from enum names only, temp + rename, delete removes `detail/`, unknown names rejected
- [ ] T033 [backend] Implement `src/StemStudio.Api/Storage/DetailStemFileStore.cs` to pass T032

### frontend (tests first)

- [x] T034 [P] [frontend] Failing tests `tests/StemStudio.Web.Tests/Api/DetailApiClientTests.cs`: `StartDetailAsync`, `CancelDetailAsync` (202/200, 409 and 503 problems -> `ApiException` with the readable detail), `TrackDto.Detail` deserialisation incl. `none`
- [x] T035 [frontend] Implement the client methods in `src/StemStudio.Web/Api/` to pass T034
- [x] T036 [P] [frontend] Failing tests `tests/StemStudio.Web.Tests/Mixer/MixerStateDetailTests.cs`: groups from parents with outcome split (display order, rest last), expand/collapse flags, audible-level gate mirror (FR-013 cases: solo on a detail stem, solo on a collapsed parent, hidden channel flags ignored), detail settings kept across collapse, `ToMixSettings` writes `expanded` + `detailChannels`, applying a mix without them collapses everything (FR-012, FR-013, FR-015)
- [x] T037 [frontend] Implement `src/StemStudio.Web/Mixer/{MixerState.Detail.cs, DetailChannelState.cs}` to pass T036

### audio-engine (tests first)

- [ ] T038 [P] [audio-engine] Failing tests `src/audio-engine/tests/gate.test.ts`: audible-level gate (contract 2.0.0 table) for collapsed/expanded parents, solo across levels, mute, hidden flags ignored
- [ ] T039 [audio-engine] Implement `src/audio-engine/src/gate.ts` and use it from `mixer.ts` (v1 behaviour unchanged without groups) to pass T038

**Checkpoint**: HO:foundational for every owner.

---

## Phase 3: User Story 1 - Automatic detail stems for a new import (P1) MVP

**Goal**: after the standard stems, the detail phase runs automatically and the track page shows
the per-parent outcomes. **Independent test**: quickstart D1-D3; E2E `@detail` "auto".

### stem-separation (tests first)

- [ ] T040 [P] [US1] [stem-separation] Failing tests `worker/tests/detail/test_steps.py` with fake models: step plan from the standard result (bass not-supported, 4-stem fallback without guitar/piano, instrument group = active parents among other/guitar/piano, single active parent runs directly), drums/vocals/instrument steps produce outcomes + FLACs, checkpoint written atomically and skipped on resume, progress weights from research.md D-03, cancel flag raises `DetailCancelledError` between chunks (FR-001..FR-006, FR-019)
- [ ] T041 [US1] [stem-separation] Implement `detail/steps.py` to pass T040
- [ ] T042 [P] [US1] [stem-separation] Failing tests `worker/tests/detail/test_fake.py`: `STEMSTUDIO_SEPARATOR=fake` gives deterministic fake models (drums: kick 0.5x, snare 0.3x, toms 0.2x of the parent -> split; vocals: lead 0.7x, backing 0.3x -> split; guitar: one audible family -> nothing-further; piano: outputs summing to 0.3x -> not-reliable; other: brass 0.6x, strings 0.4x -> split), no downloads, `fake_detail_step_seconds` delay per step via the injected clock/sleeper
- [ ] T043 [US1] [stem-separation] Implement `detail/fake.py` and `create_detail_adapters` to pass T042
- [ ] T044 [P] [US1] [stem-separation] Failing tests `worker/tests/jobs/test_detail_runner.py`: queued -> running -> completed with `result.json` written last and cached under `cache/by-audio/<sha>/detail/`; cache hit completes in one step (SC-007); errors mapped to the fixed texts of data-model.md 4.2 (`MemoryError`, ENOSPC, download errors, bug -> "Unexpected error"); failed/cancelled leave no `detail/`; start-up reconcile marks non-terminal detail jobs "interrupted by restart" and keeps checkpoints (FR-008, FR-010, FR-019)
- [ ] T045 [US1] [stem-separation] Implement `jobs/detail_runner.py` and the reconcile extension to pass T044
- [ ] T046 [P] [US1] [stem-separation] Failing contract tests `worker/tests/api/test_detail_routes.py` against `specs/002-detail-separation/contracts/worker-api.openapi.yaml` (switch `worker/tests/api/contract.py` to the 002 path): every status code and example of `/v1/detail-jobs/**`, `Location` header, FLAC streaming, `health.contractVersion == "1.1.0"`
- [ ] T047 [US1] [stem-separation] Implement `api/detail_routes.py` and wire scheduler + detail adapters in `main.py` to pass T046
- [ ] T048 [P] [US1] [stem-separation] Failing tests for the real runtimes with fake torch/onnx callables `worker/tests/detail/test_runtimes.py` (chunk callbacks -> progress, family accumulation inside demix, model loading from the downloaded path, lazy imports so fake mode never imports onnxruntime)
- [ ] T049 [US1] [stem-separation] Vendor MSST code into `detail/vendor/msst/` (pinned commit, MIT `LICENSE` + `NOTICE`, decorators and config loaders removed, hyper-parameters as constants) and implement `detail/runtimes.py` (MDX23C, KARA 2 STFT wrapper after python-audio-separator, Mega 53 with family accumulators) to pass T048
- [ ] T050 [US1] [stem-separation] Marked integration test `worker/tests/detail/test_detail_integration.py` (`@pytest.mark.integration`): real models on a generated 10 s band fixture; every split parent reconstructs <= -30 dB, gates hold, wall time logged (SC-002, SC-003)

### backend (tests first)

- [ ] T051 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Detail/DetailAutoQueueTests.cs`: when `TrackJobApplier` completes a track, the detail phase is `queued` in the same save; `DetailSynchronizer` submits pending phases (unreachable worker -> stays pending), maps running/progress/currentParent, on completed downloads the result and every detail stem then completes, worker 404 -> failed "interrupted by restart", worker cancelled -> cancelled, a failing stem download -> failed with reason and no stems offered (FR-001, FR-007, FR-008, FR-019)
- [ ] T052 [US1] [backend] Implement `src/StemStudio.Api/Detail/{DetailSynchronizer,DetailJobApplier}.cs`, hook into `JobSyncService` and `TrackJobApplier` to pass T051
- [ ] T053 [P] [US1] [backend] Failing contract tests (the `backend-api.openapi.yaml` link already points to `specs/002-detail-separation/contracts/` since Phase 0): remove the served operations from `PendingDetailOperations` in `BackendContractTests.StemsAndMixes.cs` as they are implemented and extend `BackendContractTests` for `Track.detail` (all statuses, examples `DetailQueuedTrack`, `DetailCompletedTrack`) and `getDetailStem` (200/206/304/404/416, CORS exposure) (FR-007)
- [ ] T054 [US1] [backend] Implement `TrackMapper` detail mapping and `GET /api/tracks/{id}/detail-stems/{parent}/{name}` (same `Results.File` range processing as stems) to pass T053

### frontend (tests first)

- [x] T055 [P] [US1] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Pages/TrackDetailDetailTests.cs`: `detail-section` per status, stage and progress (`aria-valuenow`), outcome texts per kind (data-model 2.2), `detail-stem-<parent>-<detail>` list, `detail-long-notice`, `detail-limitation-help` text (FR-017), polling continues while the detail phase is active even though the track is completed (FR-007, US3 AS1)
- [x] T056 [US1] [frontend] Implement `src/StemStudio.Web/Components/Pages/TrackDetail.Detail.razor(.cs)` and the polling change to pass T055

### qa

- [ ] T057 [US1] [qa] Author `e2e/tests/detail.spec.ts` "auto" (`@detail`, fake separator): import the fixture, standard completes, detail runs to completed, outcomes match the fake table, mixer usable during the phase (US1 AS1-AS3)

**Checkpoint**: HO:us1 (stem-separation, backend, frontend, qa).

---

## Phase 4: User Story 2 - Mix with detail stems (P1) MVP

**Goal**: expand a parent, mix its detail stems. **Independent test**: quickstart D4; E2E "mixer".

### audio-engine (tests first)

- [ ] T058 [P] [US2] [audio-engine] Failing tests `src/audio-engine/tests/groups.test.ts`: `loadTrack` with groups creates hidden channels without fetching; first `setExpanded(true)` fetches/decodes the group once, starts its sources with `start(t, offsetAt(t))` on the shared anchor (also inside a running loop), swaps gates at one time with <= 10 ms ramps; collapse is immediate; play/seek/stop/loop include detail sources; `setChannel`/`attachMeter` accept `<parent>.<detail>`; unknown ids and parents without group throw; failed group load rejects and keeps the parent audible (FR-012..FR-014, SC-004)
- [ ] T059 [US2] [audio-engine] Implement `src/audio-engine/src/groups.ts`, extend `engine.ts`, `transport.ts`, `meters.ts`, `stem-loader.ts` to pass T058
- [ ] T060 [P] [US2] [audio-engine] Failing test: after 50 loop repetitions with a group expanded mid-playback all `start(when, offset)` pairs of audible channels map to the same transport position within 1 ms; v1 suite still green
- [ ] T061 [US2] [audio-engine] Fix/refine to pass T060; `npm run test:coverage` >= 90 %

### frontend (tests first)

- [x] T062 [P] [US2] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Mixer/StemGroupTests.cs`: `expand-<parent>` only for split parents with `aria-expanded`; group strips with all controls and meters (`*-<parent>-<detail>` ids); engine receives `loadTrack` with `groups`, `setExpanded`, `setChannel("<parent>.<detail>", ...)`; parent strip marked `data-expanded`; a rejected `setExpanded` shows the error and stays collapsed (FR-012, FR-013)
- [x] T063 [US2] [frontend] Implement `src/StemStudio.Web/Components/Mixer/{StemGroup,ExpandToggle}.razor` and the mixer wiring to pass T062

### qa

- [ ] T064 [US2] [qa] Author `detail.spec.ts` "mixer": expand drums, mute snare -> `meter-drums-snare` silent while others move, solo kick, collapse/expand keeps settings, 50 loop repetitions alignment check through the engine debug hook already used by the 001 suite (US2 AS1-AS5, SC-004)

**Checkpoint**: HO:us2 (audio-engine, frontend, qa).

---

## Phase 5: User Story 3 - Progress, failure, retry and cancel (P2)

**Goal**: the detail phase can fail, be cancelled and retried without touching the standard stems.
**Independent test**: quickstart D5-D7, D11; E2E "restart", "priority".

- [ ] T065 [P] [US3] [stem-separation] Failing tests: a standard job submitted while a detail step runs starts within 5 s after that step (fake clock), queued detail jobs never delay it (SC-006); cancel during a step reports cancelled within one chunk; retry after cancel/failure reuses checkpoints (stage text skips finished parents)
- [ ] T066 [US3] [stem-separation] Implement the remaining scheduler/runner behaviour to pass T065
- [ ] T067 [P] [US3] [backend] Failing tests `tests/StemStudio.Api.Tests/Detail/DetailEndpointTests.cs`: `POST .../detail` 202 from none/failed/cancelled, 409 otherwise and when the track is not completed; `POST .../detail/cancel` 200 from queued/running (pending submission cancels locally), 409 otherwise, 503 when the worker is unreachable; `DELETE /api/tracks/{id}` 409 while queued/running with the reason, removes `detail/` otherwise (FR-008, FR-009, FR-016)
- [ ] T068 [US3] [backend] Implement `src/StemStudio.Api/Detail/{DetailEndpoints,DetailService}.cs` and the `TrackDeletionService` rule to pass T067
- [ ] T069 [P] [US3] [frontend] Failing bUnit tests: `detail-retry` (failed, cancelled) and `detail-cancel` (queued, running) call the client and refresh; `detail-error` shows the reason; delete refused shows `track-delete-blocked` (FR-007, FR-008, FR-016)
- [ ] T070 [US3] [frontend] Implement the actions in `TrackDetail.Detail.razor.cs` / `TrackDetail.Actions.cs` to pass T069
- [ ] T071 [US3] [qa] Author `detail.spec.ts` "restart" (`STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`: restart the worker container mid-phase -> "interrupted by restart" -> Retry completes; standard stems play throughout), "cancel" (cancel -> delete succeeds) and "priority" (detail of track A running, import B -> B's standard separation completes before A's detail phase)

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Detail-separate existing tracks (P2)

- [ ] T072 [P] [US4] [backend] Failing tests: tracks created before the migration expose `detail.status = none`; `POST .../detail` on them queues the phase; a second track with the same audio completes from the worker cache (fake worker returns completed on create) (FR-009, FR-010, SC-007)
- [ ] T073 [US4] [backend] Implement anything missing to pass T072
- [ ] T074 [P] [US4] [frontend] Failing bUnit test: `detail-split-further` visible only for status none on a completed track, click queues and shows progress
- [ ] T075 [US4] [frontend] Implement to pass T074
- [ ] T076 [US4] [qa] Author `detail.spec.ts` "split further": cancel the auto phase, Split further, detail completes, mixer offers expand

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Mixes with detail settings (P3)

- [ ] T077 [P] [US5] [backend] Failing tests `tests/StemStudio.Api.Tests/Mixes/MixDetailEndpointTests.cs`: create/update/get/duplicate round-trip `expanded` + `detailChannels` exactly; 400 with JSON paths (`settings.detailChannels[0].stem`, `settings.expanded[0]`) for unknown stems or unsplit parents; a 1.1.0-shaped body without the fields is accepted and returned with empty arrays (FR-015, SC-005)
- [ ] T078 [US5] [backend] Implement `MixValidator`/`MixMapper`/`MixService` extensions to pass T077
- [ ] T079 [P] [US5] [frontend] Failing bUnit tests: save sends expansion + detail settings; loading a mix applies them (engine `setExpanded` before `setChannel`); loading an old mix collapses all groups and restores 001 settings exactly; dirty flag on detail changes
- [ ] T080 [US5] [frontend] Implement to pass T079
- [ ] T081 [US5] [qa] Author `detail.spec.ts` "mixes": save "No snare", reload, load -> expanded + snare muted; an old-style mix (created through the API without the fields) loads collapsed

**Checkpoint**: HO:us5.

---

## Phase 8: Polish (inside the wave, per owner)

- [ ] T082 [P] [stem-separation] Add `worker/scripts/check_reconstruction.py` (quickstart D3); refactor to SIG limits; HO:polish with coverage of `detail`, `jobs`, `api`
- [ ] T083 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [ ] T084 [P] [frontend] Accessibility of group strips (labels, keyboard expand), HO:polish
- [ ] T085 [P] [audio-engine] TSDoc, memory release of groups on `dispose()`, HO:polish
- [ ] T086 [P] [qa] Traceability matrix update, HO:polish
- [ ] T087 [P] [devops] Validate compose/CI/e2e-real changes (`docker compose config -q`, `actionlint`), HO:polish
- [ ] T088 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T089 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green
- [ ] T090 [CTO] `docs/THIRD-PARTY-LICENSES.md`: three detail models with licence status, URL, SHA-256 (research.md D-07), credit lines for UVR and ZFTurbo MSST, new libraries (`onnxruntime`, `rotary-embedding-torch`); README notice "model weights are for local, personal, non-commercial use; commercial use and redistributing images that contain them are not covered" (FR-018, constitution IV)
- [ ] T091 [CTO] Playwright `@detail` + 001 suite against the fake stack (retries 0)
- [ ] T092 [CTO] Real-model validation on the reference machine: quickstart D1-D6, D12, D14; record the SC-001 wall time of the reference song and the peak worker RAM in `docs/STATUS.md`; if SC-001 fails, apply the research.md D-03 levers in order and re-measure
- [ ] T093 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| stem-separation | T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022 -> T023 -> HO:foundational -> T040 -> T041 -> T042 -> T043 -> T044 -> T045 -> T046 -> T047 -> T048 -> T049 -> T050 -> HO:us1 -> T065 -> T066 -> HO:us3 -> T082 (HO:polish) | `worker/src/stemstudio_worker/{detail,jobs,api}/**`, `main.py`, `worker/scripts/**`, `worker/tests/{detail,jobs,api}/**` | fake `DetailModel`s, fake opener, fake clock/sleeper, synthetic numpy signals |
| backend | T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> T032 -> T033 -> HO:foundational -> T051 -> T052 -> T053 -> T054 -> HO:us1 -> T067 -> T068 -> HO:us3 -> T072 -> T073 -> HO:us4 -> T077 -> T078 -> HO:us5 -> T083 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests; the one-line csproj link in T053 | stub `HttpMessageHandler` with worker 1.1.0 examples, `FakeTimeProvider`, SQLite in-memory/file, temp DataDir |
| frontend | T034 -> T035 -> T036 -> T037 -> HO:foundational -> T055 -> T056 -> HO:us1 -> T062 -> T063 -> HO:us2 -> T069 -> T070 -> HO:us3 -> T074 -> T075 -> HO:us4 -> T079 -> T080 -> HO:us5 -> T084 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, bUnit JSInterop, fake `IPollingTimer` |
| audio-engine | T038 -> T039 -> HO:foundational -> T058 -> T059 -> T060 -> T061 -> HO:us2 -> T085 (HO:polish) | `src/audio-engine/**` except `src/types.ts`, configs | mocked `AudioContext`, stubbed `fetch`, fake timers |
| qa | T009 -> T010 -> T011 -> HO:foundational -> T057 -> HO:us1 -> T064 -> HO:us2 -> T071 -> HO:us3 -> T076 -> HO:us4 -> T081 -> HO:us5 -> T086 (HO:polish) | `tests/fixtures/**`, `e2e/**`, `docs/testing/**` | suite executed in T091 |
| devops | T008 -> HO:foundational -> T087 (HO:polish) | `docker/**`, `docker-compose.yml`, `.github/**`, `scripts/**` | skeleton |
| quality-gate | T006 -> T007 -> HO:foundational -> T088 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T089 -> T090 -> T091 -> T092 -> T093 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: architect 5, stem-separation 26, backend 21, frontend 15, audio-engine 7,
qa 9, devops 2, quality-gate 3, CTO 5, audio-ingest 0, beat-analysis 0 (total 93, T001-T093).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T004 engine skeleton | audio-engine code (`index.ts`, `engine.ts`) | one-time two-line stub by the architect, recorded in the architect hand-off |
| T053 backend contract test | architect-owned csproj | link switched by the architect in Phase 0 (T003) |
| T047 `main.py` wiring | real detail models | `create_detail_adapters` (T001) with fakes in unit tests; real wiring checked in T050/T092 |
| T057-T081 Playwright | frontend markup | UI test ids 1.1.0; executed in T091 |
| T090 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 -> wave (Setup + Foundational + stories per owner, parallel across owners) -> Integration
wave. Inside the wave every owner follows its sequence; stories are independent at the contract
level (fakes), so P1 stories of all owners finish before P2/P3 work starts in each sequence.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component, exact numbers in the hand-off (I)
- [ ] Detail models behind `DetailModel` with fakes; the two marked integration tests (Demucs, detail) are the only real-model tests (I, Complexity Tracking)
- [ ] SIG limits respected outside `detail/vendor/**`; `tools/quality/run_all.py` passes (II)
- [ ] Implementations match worker 1.1.0, backend 1.2.0, engine 2.0.0, UI ids 1.1.0; contract tests pass (III)
- [ ] Only OSI libraries; detail weights listed with status, URL, SHA-256; downloaded at first use, never committed; README/THIRD-PARTY notice (IV)
- [ ] Limitations shown in the UI (FR-017); audible timing only from `AudioContext.currentTime` (V)
- [ ] No secrets; names from enums only (no path traversal); SHA-256 verified downloads (Security)
- [ ] Analyzers/formatters/linters clean; public APIs documented
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
