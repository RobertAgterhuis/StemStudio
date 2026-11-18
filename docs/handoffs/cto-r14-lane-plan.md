# Lane plan: round 014+015 (practice flow + play-along recording)

Architect, 2026-11-17. Round branch `014-015-round` (worktree `.claude/worktrees/r14`), design head = the
commit of this note. 022+023 are pre-merged (from `022-023-round` 7eac67f4), then `r14-p0-014` and
`r14-p0-015`; all full contract documents are re-merged (014 DELTA.md 8.1, 015 DELTA.md "Re-merge outcome").
Agent budget: **6 lanes** (owner limit 2026-11-17). Every lane branches `014-015-round-lane-<id>` from the
design head.

## 0. Contract state every lane builds against (frozen for the wave)

| Contract | Label | File | Notes |
|---|---|---|---|
| backend-api (current full) | **1.14.0** | `specs/015-play-along-recording/contracts/backend-api.openapi.yaml` | = 014's 1.13.0 + 015; contains `[..., 1.12.0, 1.22.0, 1.23.0, 1.13.0, 1.14.0]`; linked into `StemStudio.Api.Tests` as `Contract/backend-api.openapi.yaml` |
| backend-api 014 | 1.13.0 | `specs/014-practice-flow/contracts/backend-api.openapi.yaml` (+ delta `practice-api.openapi.yaml`) | = 023's 1.23.0 + 014 (`merge_full.py --check`) |
| backend-api 015 delta | 1.14.0-delta.2 | `specs/015-play-along-recording/contracts/recording-api.openapi.yaml` | merge: `merge_recording_delta.py --check` |
| worker-api | 1.8.0 | `specs/014-practice-flow/contracts/worker-api.openapi.yaml` (+ `worker-practice.openapi.yaml`) | 015 changes nothing in the worker |
| audio-engine | **2.5.0** | `specs/015-play-along-recording/contracts/audio-engine.md`, binding `src/audio-engine/src/types.ts` | = 2.4.0 (014: score follower, `OnLoopPass`) + 015 |
| web-interop | 1.1.0 | `specs/014-practice-flow/contracts/web-interop.md` | contains 1.0.0, 1.2.0 (022 `library`), 1.1.0; module constant stays `1.2.0` until L5 ships the key map, then `1.1.0` |
| ui-test-ids | **1.14.0** | `specs/015-play-along-recording/contracts/ui-test-ids.md` | = 1.13.0 (014) + 015; 015 level rule R10 |
| render-vectors | 1.1.0 | `specs/015-play-along-recording/contracts/render-vectors.json` | 007's 1.0.0 + 8 take cases |
| key catalogue | - | `src/StemStudio.Web/wwwroot/js/keymap-defaults.json` | 44 commands; `mixer.record.toggle` (`r`), `mixer.compare.cycle` (`c`) `reserved: true` until L5 flips them for L6 |

Frozen for every lane (changes only through the orchestrator, who asks the architect): `specs/014-*/**`,
`specs/015-*/**` (except ticking own task rows, see section 3), `src/StemStudio.Contracts/**`,
`src/audio-engine/src/types.ts`, the worker `schemas.py` / `ports.py` contract parts, `docs/decisions/**`.
Shared logs owned by **no lane**: `docs/STATUS.md`, `docs/ROADMAP.md`, `docs/decisions/CTO-DECISIONS-*.md`,
`docs/THIRD-PARTY-LICENSES.md`, changelogs, `docs/public/**`. Lanes write decisions and notes only in their
hand-offs `docs/handoffs/<lane-role>-<014|015>-<checkpoint>.md`.

## 1. Lanes

Task counts include the tests-first pairs; "014:" / "015:" name the feature's `tasks.md`.

| Lane | Role file(s) (`.claude/agents/`) | Tasks | Count |
|---|---|---|---|
| **L1 engine** | `audio-engine.md` | 014: T042-T043, T080-T081; 015: T012-T019, T028-T035, T045-T046, T052-T053, T059-T060, T073, T077-T078, T086 | 30 |
| **L2 worker + platform** | `beat-analysis.md`, `audio-ingest.md`, `stem-separation.md`, then `devops.md`, `quality-gate.md` | 014: T020-T033, T050-T053, T076-T077, T086-T087, T018, T014, T015-T016; 015: T011, T010, T007-T008 | 30 |
| **L3 backend 014 + E2E 014** | `backend.md`, then `qa.md` | 014: T034-T041, T054-T055, T070-T071, T078-T079, T088-T091, T096-T097; qa: T060, T068, T074, T084, T094, T100 + the 012 key-move regression | 27 |
| **L4 backend 015** | `backend.md` | 015: T020-T025, T025a-T025b, T036-T039, T047-T048, T054-T055, T061-T062, T066-T067, T071-T072, T079-T080, T086; 014: T056-T057, T107, T108 | 29 |
| **L5 frontend 014** | `frontend.md` | 014: T044-T049, T058-T059, T061-T067, T069, T072-T073, T075, T082-T083, T085, T092-T093, T095, T098-T099, T101 | 28 |
| **L6 frontend 015 + qa 015** | `frontend.md`, `qa.md` | 015: T026-T027, T040-T043, T049-T050, T056-T057, T063-T064, T068-T069, T074-T075, T081-T082, T084, T086; qa: T009, T044, T051, T058, T065, T070, T076, T083, T085; 014: T017 | 30 |

Integration (CTO, after the wave): 014 T102 (quality-gate run), T103, T104, T105, T106; 015 T087, T088-T091.
Critical path: L1 (30 engine tasks, recording is the riskiest code) and L5 (28 UI tasks); start them first
with the strongest settings. L2 needs no stack; its devops/quality-gate part may run on a lighter effort.

## 2. Owned write paths (each shared file has exactly one owner)

| Lane | Owns | Must not edit (request instead) |
|---|---|---|
| L1 | `src/audio-engine/**` except `src/types.ts` (incl. `engine.ts`, `index.ts`, `callbacks.ts`, `src/recording/**`, `src/score-follow/**`, `src/loop-pass.ts`, `vite.config.ts`, `package.json`, `tests/**`; `tests/render-vectors.test.ts` switches to the 015 vectors in T073) | `types.ts` (frozen) |
| L2 | `worker/src/stemstudio_worker/{analysis/structure/**,engraving/score_view.py,documents/**,jobs/practice_*.py,api/practice_routes.py,main.py,config.py (wiring only)}` and the matching `worker/tests/**` (incl. `worker/tests/render/test_take_conversion.py`), `worker/pyproject.toml`/`uv.lock` only if a lock refresh is needed; `docker/**`, `docker-compose*.yml`, `.env.example`, `.github/**`, `scripts/**`; `e2e/playwright.config.ts`, `e2e/package.json` scripts (`test:practice-flow`, `test:recording`, project `recording`); `tools/quality/**`, `docs/quality/**`; `tests/fixtures/practice-flow/**` and `tests/fixtures/tests/test_practice_flow_fixture.py` (T018) | `schemas.py`/`ports.py` contract shapes |
| L3 | `src/StemStudio.Domain/{Sections,Setlists,Practice}/**`; `src/StemStudio.Persistence/**` **except** `Configurations/{MixConfiguration,AssetConfiguration}.cs` and L4's recording files (owner of `StemStudioDbContext.cs` - make it `partial` in its first commit - `Migrations/**` incl. the model snapshot, `DesignTimeDbContextFactory.cs`); `src/StemStudio.Api/{Sections,ScoreFollow,Setlists,Practice}/**`, `src/StemStudio.Api/Worker/WorkerClient.Practice.cs` + practice job records, `src/StemStudio.Api/Sync/**`, `src/StemStudio.Api/ApiComposition.cs`, `src/StemStudio.Api/Program.cs`; tests: `tests/StemStudio.Domain.Tests/{Sections,Setlists,Practice}/**`, `tests/StemStudio.Persistence.Tests/PracticeFlowPersistenceTests.cs` (+ shared Persistence test helpers), `tests/StemStudio.Api.Tests/{Sections,ScoreFollow,Setlists,Practice,Worker}/**`, `Contract/BackendContractTests.PracticeFlow.cs`, `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj`; E2E: `e2e/tests/{sections,shortcuts-pedals,sections-edit,score-follow,setlists,practice-log}.spec.ts`, new page objects `e2e/pages/{section-lane,section-editor,score-panel,setlists-page,setlist-editor,setlist-bar,shortcut-settings,practice-log-panel,practice-page}.ts`, new support files `e2e/support/practice-flow*.ts`, and the edit of `e2e/tests/practice-levels.spec.ts` (012 keys `r`/`c` -> `0`/`Shift+K`, after L5's T065) | `e2e/pages/mixer-page.ts` and other existing shared page objects/support (L6); `MixConfiguration`/`AssetConfiguration` (L4) |
| L4 | `src/StemStudio.Domain/{Recording,Rendering,Mixes,Assets,Tracks,LibraryCare}/**`, the 022 filing rules (`src/StemStudio.Domain/Library/LibraryFilingRules*.cs` and the pending-filing queries in `src/StemStudio.Api/Library/**` that T025b changes); `src/StemStudio.Persistence/{StemStudioDbContext.Recording.cs,Configurations/Recording*.cs,Configurations/MixConfiguration.cs,Configurations/AssetConfiguration.cs}` and its migration `AddRecordingTakes` (see rule M); `src/StemStudio.Api/{Recording,Assets,Tracks,Mixes,LibraryCare}/**` (incl. `ManifestBuilder.cs`, `MergePlanner`, `LibraryMerger`, scrubber; export requests live under `Assets/`); tests: `tests/StemStudio.Domain.Tests/{Recording,Rendering,Mixes,Assets,Tracks,LibraryCare}/**`, `tests/StemStudio.Persistence.Tests/Recording*Tests.cs`, `tests/StemStudio.Api.Tests/{Recording,Assets,Tracks,Mixes,LibraryCare}/**`, `Contract/BackendContractTests.StemsAndMixes.cs` (`PendingRecordingOperations`) | `ApiComposition.cs`, `Program.cs`, `Sync/**`, the csproj (request one line each from L3) |
| L5 | `src/web-interop/**`; `src/StemStudio.Web/**` **except** L6's paths (owner of `Components/Mixer/Mixer.razor`, `Mixer.razor.cs`, `Mixer.Shortcuts.cs`, `Components/Layout/**`, `Components/Shell/**` incl. `ShortcutOverlay.razor`, `Program.cs`, `Help/*.cs` (catalogue code), `Help/topics/**` except `recording/`, `Experience/FeatureCatalog*`, `Mixer/MixerState*.cs`, `Api/IStemStudioApiClient.cs`, `Api/StemStudioApiClient.cs`, `wwwroot/js/keymap-defaults.json`, `wwwroot/css/**`); `tests/StemStudio.Web.Tests/**` except L6's folders | `src/StemStudio.Web/Audio/EngineMethods.cs` is shared read-only (version already 2.5.0); `EngineContractDriftTests.cs` - L5 removes `OnLoopPass` from `PendingCallbacks` (T049/T099) |
| L6 | `src/StemStudio.Web/{Recording,Components/Recording}/**`, `Components/Mixer/Mixer.Recording.cs` (new partial), `Api/{IStemStudioApiClient,StemStudioApiClient}.Recording.cs` (new partials), `Help/topics/recording/**`; `tests/StemStudio.Web.Tests/{Recording,Components/Recording}/**`; the five recording callbacks in `tests/StemStudio.Web.Tests/Audio/EngineContractDriftTests.cs` `PendingCallbacks` (edit only those names, after L5's change is merged, or ask L5); E2E: `e2e/tests/recording*.spec.ts`, every existing `e2e/pages/**` and `e2e/support/**` file (owner of shared helpers such as `mixer-page.ts`, `stack.ts`, `env.ts`, `fixtures.ts`, `fake-microphone.ts`), new recording page objects; `tests/fixtures/recording/**`, `tests/fixtures/tests/test_recording_fixtures.py`; `docs/testing/**` (TEST-STRATEGY: 014 T017 and 015 T009 sections) | Mixer markup, rail, overlay, FeatureCatalog, key catalogue (request from L5) |

**Rule M (migrations, one linear model snapshot)**: L3's model is complete at L3 `HO:foundational` (T039) and
frozen afterwards (a later model change goes through the orchestrator). L4 creates its migration only after
merging L3's `HO:foundational` head; until then L4 tests persistence against its configuration with an
in-memory/`EnsureCreated` context. L4 then owns the snapshot for its migration only.

**Hook requests (one line each, through the orchestrator, answered within the next checkpoint)**: L4 -> L3:
`MapRecordingEndpoints()` / `AddRecording()` in `ApiComposition.cs`, the take-asset finisher and the 022/013
hooks in `Sync/**` if no extension point exists, csproj links. L6 -> L5: recording panel toggle in the mixer
toolbar, the `take` strip slot and Takes panel slot in `Mixer.razor`, a mixer command registration hook so L6
routes `mixer.record.toggle` / `mixer.compare.cycle` in its own files, `reserved: false` for those two rows
(with `KeyMapDefaultsTests`), FeatureCatalog rows (015 data-model 6), shortcut overview rows, help
catalogue registration of `topics/recording/`. L5 delivers a command registration hook at its
`HO:foundational` (`IMixerCommandSource` or similar, documented in its hand-off) so the other hooks stay
single lines.

## 3. Ordered tasks and hand-off checkpoints

Each lane ticks only its own rows in the two `tasks.md` (union-per-row on merge, LESSONS 022/023) and writes
`docs/handoffs/<role>-<feature>-<checkpoint>.md`.

- **L1**: 014 T042 -> T043 (`OnLoopPass`, unblocks L5's recorder) -> 015 T012-T019 (wav, mapping + internal
  segment log, calibration, compare/gate) -> **HO:foundational** -> 015 T028-T035 -> **HO:us1 (015)** -> 014
  T080 -> T081 (score follower) -> **HO:us4 (014)** -> 015 T045-T046 (US2) -> T052-T053 (US3) -> T059-T060
  (US4) -> T073 (US6, vectors 1.1.0) -> T077-T078 (US7) -> T086 **HO:polish**.
- **L2**: 014 T030-T033 (lane store, fake analyser, `Health.structure`) -> T020-T025 (structure features,
  boundaries, labels) -> T026-T029 (score view, setlist PDF) -> **HO:foundational** -> T050-T053 (US1) ->
  **HO:us1** -> T076-T077 (US4) -> T086-T087 (US5) -> 015 T011 -> 014 T018 (fixture) -> devops 014 T014 + 015
  T010 -> quality-gate 014 T015-T016 + 015 T007-T008 -> **HO:polish**. The devops part must be in before the
  mid-wave checkpoint (fonts in the worker image, `STEMSTUDIO_STRUCTURE_ANALYZER`, project `recording`, e2e
  override `StemStudio__Recording__TestLoopback=true`).
- **L3**: T034-T041 (+ make `StemStudioDbContext` partial first) -> **HO:foundational** (model frozen, rule M)
  -> T054-T055 (US1) -> T060 (E2E sections) -> **HO:us1** -> T070-T071, T074 (US3) -> T078-T079, T084 (US4)
  -> T088-T091, T094 (US5) -> T096-T097, T100 (US6) -> T068 (E2E keys/pedals, after L5 HO:us2) and the
  `practice-levels.spec.ts` key update -> **HO:polish**.
- **L4**: 015 T020-T025 (migration after merging L3 HO:foundational) -> T025a-T025b -> **HO:foundational** ->
  T036-T039 (US1) -> 014 T056-T057 (`LoopRegion.sectionId`) -> **HO:us1** -> T047-T048 (US2) -> T054-T055
  (US3) -> T061-T062 (US4) -> T066-T067 (US5) -> T071-T072 (US6) -> T079-T080 (US7) -> 014 T107, T108 ->
  T086 **HO:polish**.
- **L5**: T044-T049 (key map, preferences v2, navigator, recorder; deliver the command registration hook) ->
  **HO:foundational** -> T058-T059, T061 (US1) -> **HO:us1** -> T062-T067, T069 (US2) -> T072-T073, T075 (US3)
  -> T082-T083, T085 (US4) -> T092-T093, T095 (US5) -> T098-T099, T101 (US6) -> hook requests of L6 as they
  arrive -> **HO:polish**.
- **L6**: 014 T017 + 015 T009 (strategy, fixtures) -> T026-T027 -> **HO:foundational** -> T040-T043, T044
  (E2E) -> **HO:us1** -> T049-T050, T051 -> T056-T057, T058 -> T063-T064, T065 -> T068-T069, T070 ->
  T074-T075, T076 -> T081-T082, T083 -> T084, T085 -> T086 **HO:polish**.

## 4. What each lane reads (no broad exploration)

| Lane | Spec sections | Contract files | Code paths to start from |
|---|---|---|---|
| L1 | 014 spec US4, FR-010/011/025, data-model 4; 015 spec US1-US7, FR-001..FR-016, data-model 3, research R-02..R-10, ADR-024 | 015 `audio-engine.md` ("Score follower", "Loop passes", "Play-along recording" incl. Phase 0 outcome, "Callbacks"), `render-vectors.json` 1.1.0, `types.ts` | `src/audio-engine/src/{engine,transport,anchors,gate,mixer,frame-loop}.ts`, `src/{practice,karaoke,stretch}/`, `src/recording/pending.ts` (replace), `tests/practice-flow-contract.test.ts`, `tests/recording/*` skeletons |
| L2 | 014 spec US1, US4, US5 (FR-001..FR-003, FR-014, FR-018, SC-001/002/008), data-model 1-2, research R-10/R-11, ADR-023 D1-D4; 015 tasks T007-T011, research R-08 | 014 `worker-api.openapi.yaml` 1.8.0, `worker-practice.openapi.yaml`; DELTA.md 2 and 7 | `worker/src/stemstudio_worker/{schemas,ports,config,main}.py`, `jobs/` (existing lanes), `engraving/`, `analysis/` (beat), `tests/contract/test_practice_contract.py`; `docker/worker.Dockerfile`, `scripts/test-all.*`, `.github/workflows/*`, `tools/quality/{components,dependency_check}.py` |
| L3 | 014 spec US1, US3-US6 (FR-001..FR-008, FR-009, FR-014..FR-018, FR-025..FR-029, SC-007/009), data-model 3, ADR-023; ui-test-ids 1.13.0 section (for the E2E part) | 014 `backend-api.openapi.yaml` 1.13.0 (tags sections, scores, setlists, practice), `practice-api.openapi.yaml`, worker 1.8.0 examples in `tests/StemStudio.Api.Tests/Worker/Examples/`; `ui-test-ids.md` 1.14.0 "Additions in 1.13.0" | `src/StemStudio.Contracts/{Sections,ScoreFollow,Setlists,Practice}/**`, `ApiRoutes.PracticeFlow`, `ContractLimits.PracticeFlow`, `PracticeFlowProblemTypes`, `src/StemStudio.Api/{Scores,Sync,Worker}/`, `StemStudioDbContext.cs`, `e2e/support/{stack,practice}.ts` |
| L4 | 015 spec US1-US7 (FR-007, FR-011..FR-018), data-model 1-2, DELTA.md 1-2 + 7 (013/022 notes) + Phase 0 decisions P0-1..P0-8; 014 FR-005 (T056/T057), DELTA.md 7 R-5 (T107) | 015 `backend-api.openapi.yaml` 1.14.0 (tag `recording`, changed schemas), `recording-api.openapi.yaml`, `render-vectors.json` 1.1.0 | `src/StemStudio.Contracts/Recording/**`, `ContractLimits.ExportChannelIdPattern`, `src/StemStudio.Api/{Assets,Tracks,Mixes,LibraryCare}/`, `src/StemStudio.Domain/{Rendering,Library}/`, `ManifestBuilder.cs`, LESSONS backend (containment, `Mode=ReadWrite`, WAL, one lock around check+insert+start) |
| L5 | 014 spec US1-US6 UI parts, FR-004..FR-007, FR-009..FR-013, FR-015..FR-031, data-model 5-6; 012 help topic `shortcuts` | 014 `web-interop.md` 1.1.0 (section 5 + 022's 1.2.0 note), `ui-test-ids.md` 1.14.0 "Additions in 1.13.0" + "Changed default keys", 014 backend DTOs, `audio-engine.md` "Score follower"/"Loop passes" | `src/web-interop/src/{shortcuts,preferences,types}.ts`, `src/StemStudio.Web/{Practice,Mixer,Interop,Help,Experience}/`, `Components/{Mixer,Shell,Layout}/`, `wwwroot/js/keymap-defaults.json`, LESSONS frontend (WCAG 2.2 AA, dock-inset, RadzenButton, rail at 720/320 px) |
| L6 | 015 spec all US (UI), FR-001..FR-021, data-model 4-6, research R-01/R-02 (browser constraints), S-02 results (T043 timing test); qa: 015 quickstart, 014 spec SC list for T017 | 015 `ui-test-ids.md` 1.14.0 "Additions in 1.14.0" (R10, `data-reason` mapping), `audio-engine.md` "Play-along recording", 1.14.0 recording DTOs | `src/StemStudio.Web/{Audio,Mixer}/`, `Components/Mixer/ChannelStrip.razor`, `e2e/support/{stack,fake-microphone,env}.ts`, `e2e/pages/mixer-page.ts`, `docs/testing/TEST-STRATEGY.md`, LESSONS qa + frontend |

## 5. Mid-wave checkpoint

When: all six `HO:foundational` are in, plus `HO:us1` of L1, L3, L4, L5, L6 and L2's devops part. The
orchestrator merges the lane heads onto a throwaway `014-015-round-checkpoint` (L3 before L4, rule M) and runs
in its own compose project (own ports, images, volumes; torn down with `down -v`; never during another E2E
run):

1. `dotnet build StemStudio.slnx -c Release` 0 warnings, `dotnet format --verify-no-changes`, the contract
   tests (`FullyQualifiedName~.Contract.`), engine and web-interop `npm test`, worker `pytest`.
2. E2E smoke (Chromium, retries 0): new `e2e/tests/sections.spec.ts` (014 US1) and `e2e/tests/recording.spec.ts`
   (015 US1, project `recording`, fake capture device); regression `main-flow.spec.ts`, `mixer.spec.ts`,
   `practice-tempo.spec.ts`, `practice-levels.spec.ts` (moved keys), `library-search.spec.ts` (`/` from the
   catalogue), `help.spec.ts`.
3. Accessibility: `a11y/practice.a11y.spec.ts` and `a11y/focus-not-obscured.a11y.spec.ts` on the mixer with the
   section lane and the recording panel open.

Failures go to the owning lane; afterwards every lane merges the checkpoint head into its branch.

## 6. Ready check (cross-artifact pass over 014 and 015 spec/plan/tasks, 2026-11-17)

Fixed in this design head:

| # | Finding | Fix |
|---|---|---|
| 1 | 014 T107 scope "pending" | Decided (sections and score views follow the track; setlists/practice log not merged); acceptance includes "none of them arrive" |
| 2 | 014 T108 bumped `BackendApiVersion` to 1.13.0, 015 had no bump task | One task in L4: version = `info.version` of the current full document (1.14.0), test-first |
| 3 | 014 T014 and 015 T010 both pointed CI `CONTRACTS_DIR` at their own folder | Combined: `CONTRACTS_DIR` -> 015 (newest full backend document); both features' lint, superset and `--check` steps |
| 4 | 014 T002/T003 named files that Phase 0 built under other names | Text follows the code (`worker/tests/contract/...`, `PracticeFlowContractTests`, `PendingPracticeFlowOperations`) |
| 5 | 015 T003 said `ChannelIdPattern` gains `take` (contradicts P0-2) | `ExportChannelIdPattern`; `ChannelIdPattern` unchanged |
| 6 | 015 T015 "add hooks if missing" | T001 found the anchors pruned: the segment log is required, with its own tests |
| 7 | Help, documentation, E2E-authoring and CTO tasks had no explicit acceptance | "Acceptance of tasks without a red test" block in both `tasks.md` |
| 8 | 012's E2E `practice-levels.spec.ts` presses `r`/`c`, which move to `0`/`Shift+K` | Assigned to L3 (after L5 T065); checkpoint smoke includes it |
| 9 | Both frontends need the mixer markup and the key catalogue; 015's `R`/`C` are `reserved` | L5 owns them; hook requests and the `reserved` flip listed in section 2 |
| 10 | Mix settings carry both `loop.sectionId` (014) and `take` (015) | `Mixes/**` and `MixConfiguration` in one lane (L4, incl. 014 T056-T057) |
| 11 | web-interop module constant after a lower-labelled re-merge | Stays `1.2.0` until 014's key map ships, then `1.1.0` (ROADMAP label rule; recorded in CTO-DECISIONS) |

Checked without change: every remaining task of both features is "failing tests ... -> implement to pass"
or has an acceptance in the new blocks; FR/SC references resolve; no UI id, operation id, path, tag or schema
collides between 014, 015, 022 and 023 (superset and id checks above); 015 adds no dependency, 014 adds only
ReportLab 5.1.0 (BSD, locked in Phase 0; DejaVu fonts via devops T014; both for `THIRD-PARTY-LICENSES.md` at
integration).

## 7. Brief items for every lane (from LESSONS.md and the skill)

- Never touch the owner's stack (`stemstudio`) or round 022+023's `r22` worktree/stack; lanes start **no**
  Docker stack and run no E2E (write specs against ids and contracts, typecheck + lint them).
- Quiet tests, full logs to the scratchpad, summaries only; at most 1-2 heavy processes per lane.
- `dotnet format StemStudio.slnx --verify-no-changes` on the whole solution before every hand-off; jscpd step
  of `tools/quality/run_all.py` must actually run (check its own output).
- No scope cut or contract deviation without the orchestrator; blocked > 20 min -> message the orchestrator.
- Backend: `Mode=ReadWrite` runtime connections, containment of every stored path, WAL, one lock around
  check + insert + start; L4 security self-review for multipart WAV uploads and file paths (hostile WAV
  headers, oversize, path traversal in names).
- Frontend: WCAG 2.2 AA in bUnit (accessible name contains visible text, target size, focus not under the
  fixed bars - dock-inset), no awaiting the engine in rapid-click handlers, rail at 720 px / 320 px.
- QA: wait on the job id you started; E2E written, not run, in the lanes.
- Commits: Conventional Commits, configured git identity, trailer
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; no push, no merge to `main`.
