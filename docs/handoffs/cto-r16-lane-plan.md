# Lane plan: round 016 (instrument views)

Architect, 2026-11-18. Round branch `016-round` (worktree `.claude/worktrees/r16`), design head = the commit of this
note (on top of `main` 7b5cd39e with 014+015 and the fixed mixer dock). Agent budget: **5 lanes** in parallel (owner
limit 6 agents incl. the orchestrator). Every lane branches `016-round-lane-<id>` from the design head. Design
hand-off: `docs/handoffs/architect-016-design.md`.

## 0. Contract state every lane builds against (frozen for the wave)

| Contract | Label | File | Notes |
|---|---|---|---|
| worker-api | **1.9.0** | `specs/016-instrument-views/contracts/worker-api.openapi.yaml` (+ delta `worker-api.delta.openapi.yaml` 1.9.0-delta.2) | = 014's 1.8.0 + 016 (`merge_full.py --check`); linked into `StemStudio.Api.Tests` as `Contract/worker-api.openapi.yaml` |
| backend-api | **1.15.0** | `specs/016-instrument-views/contracts/backend-api.openapi.yaml` (+ delta 1.15.0-delta.2) | = 015's 1.14.0 + 016; `x-stemstudio-contains` ends `1.13.0, 1.14.0, 1.15.0`; 016 additions to existing schemas are optional on the wire |
| audio-engine | **2.6.0** | `specs/016-instrument-views/contracts/audio-engine.md`, binding `src/audio-engine/src/types.ts` | reconciliation table in section "Instrument views" |
| ui-test-ids | **1.15.0** | `specs/016-instrument-views/contracts/ui-test-ids.md` | = 1.14.0 + the 016 table ("Additions in 1.15.0") |
| help-content | **1.1.0** | `specs/016-instrument-views/contracts/help-content.delta.md`, `help-topic.schema.json` | area `instruments` |
| chord spelling vectors | **1.0.0** | `specs/016-instrument-views/contracts/chord-spelling-vectors.json` (generator + `--check`) | shared by Domain (C#) and engine (TS) |
| web-interop / render-vectors / key catalogue | 1.1.0 / 1.1.0 / unchanged | 014 / 015 | 016 adds no key command and no render case |

Frozen for every lane (changes only through the orchestrator, who asks the architect): `specs/016-*/**` (except
ticking own task rows, see section 3), `src/StemStudio.Contracts/**`, `src/audio-engine/src/types.ts`, the contract
parts of `worker/src/stemstudio_worker/{schemas,ports,config}.py`, `docs/decisions/**`. Shared logs owned by **no
lane**: `docs/STATUS.md`, `docs/ROADMAP.md`, `docs/decisions/CTO-DECISIONS-*.md`, `docs/THIRD-PARTY-LICENSES.md`,
changelogs, `docs/public/**`. Lanes write decisions and notes only in `docs/handoffs/<role>-016-<checkpoint>.md`.

## 1. Lanes

Task ids are those of `specs/016-instrument-views/tasks.md` (read its "Phase 0 ready-check amendments" first).

| Lane | Role file(s) (`.claude/agents/`) | Tasks | Count |
|---|---|---|---|
| **L1 engine** | `audio-engine.md` | T053-T056, T066-T069, T088-T089, T097-T104, T118-T119, T125-T126, T134 | 23 |
| **L2 worker analysis + test data** | `beat-analysis.md`, `audio-ingest.md`, then `qa.md` | T015-T028, T130 (beat-analysis); T035-T038, T131 (audio-ingest); T013, T014 (qa strategy + fixtures) | 22 |
| **L3 worker jobs + platform** | `stem-separation.md`, then `devops.md`, `quality-gate.md` | T029-T034, T061-T063, T077-T078, T084-T085, T093-T094, T132; T011-T012, T144, T137; T009-T010, T138 | 25 |
| **L4 backend** | `backend.md` | T039-T052, T064-T065, T072-T073, T079-T080, T086-T087, T095-T096, T108-T109, T116-T117, T123-T124, T133 | 31 |
| **L5 frontend + E2E** | `frontend.md`, then `qa.md` | T057-T060, T070-T071, T074-T075, T081-T082, T090-T091, T105-T106, T110-T111, T113-T114, T120-T121, T127-T128, T135; E2E authoring T076, T083, T092, T107, T112, T115, T122, T129; T136 | 32 |

New task (LESSONS 014/015, added in Phase 0): **T144 [devops]** split the CI E2E job before 016 adds eight specs: the
default suite and the feature passes (recording, practice-flow, agent, lyric, restart, gpu, instrument, fake-mic) run as
parallel jobs, each under 90 minutes; acceptance: `ci.yml` validates (`actionlint` or the existing workflow check), each
job lists its `--grep`/project set, the union equals today's set plus the 016 projects.

Integration (CTO, after the wave): T139-T143. Critical path: **L4** (31 backend tasks, migration and four endpoint
groups) and **L5** (UI for eight stories plus the E2E specs); start them first with the strongest settings. L2 and L3
need no stack; their devops/quality-gate part may run on a lighter effort.

## 2. Owned write paths (each shared file has exactly one owner)

| Lane | Owns | Must not edit (request instead) |
|---|---|---|
| L1 | `src/audio-engine/**` except `src/types.ts` (incl. `engine.ts`, `index.ts`, `callbacks.ts`, new `src/{timeline,chords,drums,pitch,singing}/**`, `src/views/pending.ts` (remove when done), `src/karaoke/**` (clock moves onto `timeline/`, T054), `vite*.ts`, `package.json` scripts, `tests/**`) | `types.ts` (frozen) |
| L2 | `worker/src/stemstudio_worker/harmony/{chroma_hmm,postprocess,labels,key}.py`, `analysis/vocal_pitch.py`, `notation/{tablature,parts,chord_symbols,drum_midi}.py` and the notator wiring of the new steps in `notation/**`, `analysis_inputs/**`, `engraving/tablature.py`, their `worker/tests/**` (`tests/harmony/test_{postprocess,labels,chroma_hmm,key}.py`, `tests/analysis/test_vocal_pitch.py`, `tests/notation/**`, `tests/analysis_inputs/**`, `tests/engraving/test_tablature_engraving.py`); `docs/testing/**`; `tests/fixtures/**` (T014 generator + manifest) | `harmony/__init__.py`, `harmony/btc.py`, `jobs/**`, `api/**`, `main.py` (L3) |
| L3 | `worker/src/stemstudio_worker/harmony/{__init__,btc}.py`, `harmony/vendor/**`, `views_fake.py`, `jobs/instrument_analysis_*.py`, `api/instrument_analysis_routes.py`, the transcription request/response wiring in `jobs/`/`api/` (T077/T078), `main.py`, `config.py` wiring only, their tests (incl. the `/health` expectations in `worker/tests/test_smoke.py`, `worker/tests/api/test_jobs_api.py`), `worker/pyproject.toml`/`uv.lock` only for the vendor coverage/SIG excludes; `docker/**`, `docker-compose*.yml`, `.env.example`, `.github/**`, `scripts/**`; `e2e/playwright.config.ts` and `e2e/package.json` scripts (fake-mic projects, `test:instrument`); `tools/quality/**`, `docs/quality/**` | `schemas.py`/`ports.py` contract shapes; `notation/**` (L2) |
| L4 | `src/StemStudio.Domain/**`, `src/StemStudio.Persistence/**` (owner of `StemStudioDbContext*.cs`, `Migrations/**` incl. the snapshot), `src/StemStudio.Api/**`, `tests/StemStudio.{Domain,Persistence,Api}.Tests/**` incl. `Contract/BackendContractTests.InstrumentViews.cs` (un-staging) and the test csproj files | Contract DTOs (frozen); the Domain.Tests vector link exists |
| L5 | `src/StemStudio.Web/**` (incl. `Help/TopicParser.cs` area order, `Help/topics/instruments/**`, `Mixer/PracticePreset.cs`, `Api/*` client partials, `Audio/*` interop), `src/web-interop/**` (only if needed; no change planned), `tests/StemStudio.Web.Tests/**` (incl. `EngineContractDriftTests.PendingCallbacks`); E2E: `e2e/tests/instrument-*.spec.ts`, `e2e/a11y/instrument*.a11y.spec.ts`, every `e2e/pages/**` and `e2e/support/**` file | `e2e/playwright.config.ts`, `e2e/package.json` (L3); fixture generator (L2) |

**Rule M (migrations)**: only L4 changes the EF model; one migration `AddTrackAnalyses` (T048). No second lane touches
`Migrations/**`; a later model change goes through the orchestrator.

**Hook requests (one line each, through the orchestrator, answered within the next checkpoint)**: L2 -> L3: the
`ChordRecognizer` factory entry for `chroma-hmm`, `PitchAnalyzer` and `KeyEstimator` construction in `main.py`/
`harmony/__init__.py` (L2 delivers the classes and their constructor signatures in its HO:foundational). L5 (qa part) ->
L3: Playwright projects `fake-mic-on-pitch`, `fake-mic-semitone`, `fake-mic-silence` (WAVs from L2's T014) and the
`test:instrument` script. L5 -> L4: none expected (client built from the contract). L3 -> L2: the fixture paths for the
fake-mic projects (L2 T014 delivers them first, at HO:foundational).

## 3. Ordered tasks and hand-off checkpoints

Each lane ticks only its own rows in `tasks.md` (union-per-row on merge, LESSONS 022/023) and writes
`docs/handoffs/<role>-016-<checkpoint>.md`.

- **L1**: T053 -> T054 (timeline, karaoke clock moved without behaviour change) -> T055 -> T056 (spelling vectors) ->
  **HO:foundational** -> T066-T069 (lane, banner chords, `setChordDisplay`) -> **HO:us1** -> T088-T089 (hit lanes) ->
  **HO:us3** -> T097-T104 (YIN, worklet, feedback, scorer, pitch view, `scoreTake`) -> **HO:us4** -> T118-T119 ->
  **HO:us7** -> T125-T126 -> **HO:us8** -> T134 (frame budget, remove `views/pending.ts`) **HO:polish**.
- **L2**: T014 (fixtures, first: L3 and L5 need the WAVs) -> T013 -> T015-T020 (labels, post-processing, chroma-hmm,
  key) -> T035-T036 (mixdown) -> **HO:foundational** (with constructor signatures for L3) -> T021-T022 (vocal pitch) ->
  T023-T024 (tablature) -> T037-T038 (TAB engraving check, "Capo n" direction) -> T025-T028 (parts, chord symbols, drum
  MIDI) -> T130, T131 **HO:polish**.
- **L3**: T029-T034 (BTC adapter with fake torch, vendor, fakes, store) -> T009-T010 (quality rules) -> T011-T012 and
  T144 (worklet asset, env, CI incl. `CONTRACTS_DIR` move, E2E job split) -> **HO:foundational** (devops part must be in
  before the mid-wave checkpoint) -> T061-T063 (routes, composition, `/health`, integration test with real BTC) ->
  **HO:us1** -> T077-T078 -> **HO:us2** -> T084-T085 -> **HO:us3** -> T093-T094 -> **HO:us4** -> T132, T137, T138
  **HO:polish**.
- **L4**: T039-T052 (domain, spelling/key/capo/catalogue, persistence + migration, worker client, service) ->
  **HO:foundational** -> T064-T065, T072-T073 -> **HO:us1** -> T079-T080 -> **HO:us2** -> T086-T087 -> **HO:us3** ->
  T095-T096 -> **HO:us4** -> T108-T109 -> **HO:us5** -> T116-T117 -> **HO:us7** -> T123-T124 -> **HO:us8** -> T133
  **HO:polish**.
- **L5**: T057-T060 (family mix service + preset fix, API client) -> **HO:foundational** -> T070-T071, T074-T075, T076 ->
  **HO:us1** -> T081-T082, T083 -> **HO:us2** -> T090-T091, T092 -> **HO:us3** -> T105-T106, T107 -> **HO:us4** ->
  T110-T111, T112 -> **HO:us5** -> T113-T114, T115 -> **HO:us6** -> T120-T121, T122 -> **HO:us7** -> T127-T128, T129 ->
  **HO:us8** -> T135, T136 **HO:polish**.

## 4. What each lane reads (no broad exploration)

| Lane | Spec sections | Contract files | Code paths to start from |
|---|---|---|---|
| L1 | spec US1, US3, US4, US7, US8 (FR-003, FR-013, FR-014, FR-031, FR-041..FR-046, FR-071, FR-073, FR-075, SC-004, SC-007), data-model 2.5, 2.8, 2.9, 3.2-3.5, 5.2, 9; research R-05, R-07, R-12 | `audio-engine.md` 2.6.0 section "Instrument views" + "Callbacks", `types.ts`, `chord-spelling-vectors.json` | `src/audio-engine/src/{engine,frame-loop,transport}.ts`, `src/karaoke/{clock,index}.ts`, `src/recording/{input,surface}.ts`, `src/practice/controller.ts`, `src/score-follow/follower.ts`, `src/views/pending.ts`, `tests/views/instrument-views-contract.test.ts`, `tests/mocks/**` |
| L2 | spec US1, US2, US4, US5, US7 (FR-010, FR-011, FR-017, FR-020..FR-023, FR-040, FR-050..FR-054, FR-070), data-model 2.1-2.4, 2.8, 3.1, 4.1-4.8; research R-03, R-04, R-06, R-08, R-09, R-10, R-13 (Phase 0 outcomes: key HMM start values) | `worker-api.openapi.yaml` 1.9.0 (schemas `ChordChart`, `KeyRegion`, `VocalPitch`, `TablatureSettings`, `PartSettings`, `ScoreChordSymbol`, `*Summary`), `ports.py` (feature 016 section), `schemas.py` | `worker/src/stemstudio_worker/{notation,engraving,analysis}/`, `ports.py` `Notator`/`NotationSettings`/`ScoreDocument`, `worker/tests/contract/test_instrument_views_contract.py`; qa: `docs/testing/TEST-STRATEGY.md`, `tests/fixtures/` (015 recording fixtures as the pattern) |
| L3 | spec US1-US4 worker parts (FR-002, FR-010, FR-030, FR-040, FR-063), data-model 1, 2.3, 2.7, 5.1; research R-02 (+ outcome: safe globals), R-11; tasks amendments T062/T063; LESSONS devops and the CI split lesson | `worker-api.openapi.yaml` 1.9.0 (tag `instrument-analysis`, `Health`), DELTA.md 1 | `worker/src/stemstudio_worker/{main,config}.py`, `jobs/practice_*.py` and `api/practice_routes.py` (the 014 lane as the pattern), `devices/`, model manager of 002/008, `transcription/`; `docker/*`, `.github/workflows/ci.yml` (contracts job, E2E job), `scripts/test-all.*`, `tools/quality/{components,dependency_check}.py` |
| L4 | spec all US backend parts (FR-002, FR-010..FR-017, FR-020..FR-023, FR-030, FR-040, FR-050..FR-053, FR-060, FR-064, FR-070..FR-077), data-model 2.5-2.9, 4.1-4.3, 5.3, 7; research R-14 | `backend-api.openapi.yaml` 1.15.0 (tag `instrument-views`, changed score/track schemas), worker 1.9.0 examples in `tests/StemStudio.Api.Tests/Worker/Examples/analysis-*.json`, `chord-spelling-vectors.json` | `src/StemStudio.Contracts/InstrumentViews/**`, `ApiRoutes.InstrumentViews`, `ContractLimits.InstrumentViews`, `InstrumentViewProblemTypes`, `src/StemStudio.Api/{Scores,Sync,Worker,Tracks}/`, `ApiComposition.cs`, `StemStudioDbContext.cs`; LESSONS backend (`Mode=ReadWrite`, WAL, containment, one lock around check+insert+start) |
| L5 | spec all US UI parts (FR-012..FR-017, FR-020, FR-023, FR-031..FR-034, FR-041..FR-047, FR-051, FR-052, FR-060..FR-064, FR-071..FR-078), data-model 5.3, 6, 8; quickstart V2-V14 for the E2E specs | `ui-test-ids.md` 1.15.0 "Additions in 1.15.0", DELTA.md 5-6, help-content 1.1.0, backend 1.15.0 DTOs, `audio-engine.md` 2.6.0 | `src/StemStudio.Web/{Mixer,Audio,Api,Help,Experience}/`, `Components/{Mixer,Lyrics,Scores,Practice,Recording}/`, `wwwroot/css/components/{mixer,karaoke}.css`, `wwwroot/js/dock-inset.js`, `e2e/support/{stack,fake-microphone,env}.ts`, `e2e/pages/mixer-page.ts`; LESSONS frontend + qa |

## 5. Mid-wave checkpoint

When: all five `HO:foundational` are in, plus `HO:us1` of L1, L3, L4, L5 and L3's devops part (worklet asset, env,
fake-mic projects). The orchestrator merges the lane heads onto a throwaway `016-round-checkpoint` (L4 alone touches the
model, so no merge order is forced; merge L3 before L5 for the Playwright config) and runs in its own compose project
(own ports, images, volumes; torn down with `down -v`; never during another E2E or a11y run):

1. `dotnet build StemStudio.slnx -c Release` 0 warnings, `dotnet format StemStudio.slnx --verify-no-changes`, contract
   tests (`FullyQualifiedName~.Contract.`), engine `npm test`, worker `pytest`, `specs/016-instrument-views/contracts/
   merge_full.py --check` and `generate_chord_spelling_vectors.py --check`.
2. E2E smoke (Chromium, retries 0): `e2e/tests/instrument-chords.spec.ts` (US1) and `instrument-practise-as.spec.ts`
   if L5 has it; regression `main-flow.spec.ts`, `mixer.spec.ts`, `practice-levels.spec.ts`, `score-follow.spec.ts`,
   `recording.spec.ts` (project `recording`), `lyrics*.spec.ts` (banner chords sit in the karaoke dock).
3. Accessibility: `a11y/focus-not-obscured.a11y.spec.ts` and `a11y/practice.a11y.spec.ts` on the mixer with the chord
   lane and banner chords on (the dock grows by one row).

Failures go to the owning lane; afterwards every lane merges the checkpoint head into its branch.

## 6. Ready check (cross-artifact pass over 016 spec/plan/tasks/data-model/contracts, 2026-11-18)

Fixed in this design head:

| # | Finding | Fix |
|---|---|---|
| 1 | 014/015 placeholder names in the deltas (score view, sections, count-in, transpose, input, latency, take URL) | Real names in DELTA.md "Phase 0 outcome" and `audio-engine.md` 2.6.0 reconciliation table |
| 2 | Engine had no calibrated-latency state (015 keeps calibrations in the backend) | `SingingFeedbackRequest.roundTripMs` + `latencyMethod` from Blazor; sources = 015 `LatencyMethod` |
| 3 | `scoreTake` had 5 positional parameters (SIG max 4) and no take position | `scoreTake(request: TakeScoreRequest)` with `startSample` |
| 4 | Worklet file `audio-engine-pitch.worklet.js` and "second entry in vite.config.ts" contradict 015's pattern | `audio-engine-pitch-worklet.js`, `vite.pitch.config.ts`, `EngineOptions.pitchWorkletUrl` (built in Phase 0) |
| 5 | Required 016 fields on existing backend schemas would invalidate 1.14.0 producers/fixtures | Optional on the wire (`JsonIgnore` when unset); worker summary/health stay required |
| 6 | `TrackKeySummary` example "E-flat major" contradicts the spelling rule | "Eb major" |
| 7 | UI ids: `capo-select` "replaces `chords-capo`" (does not exist); `capo-suggestions` role unclear (LESSONS nested-interactive); `score-tab-layout` `none` unmapped; `singing-latency` "calibrated" vs 015 methods | Fixed in the delta ("Phase 0 changes") and merged into 1.15.0 |
| 8 | `STEMSTUDIO_ANALYSIS_THREADS` default "empty = automatic" (tasks) vs 4 (data-model) | 4; empty = default (`config.py`) |
| 9 | CI `CONTRACTS_DIR` names 015 files; moving it to 016 would break those steps | T012 amended: 015 steps under `RECORDING_CONTRACTS`, 016 steps added |
| 10 | Help area `instruments` not in `TopicParser.AreaOrder`, schema enum 1.0.0 | help-content 1.1.0 schema linked; AreaOrder in T070/T071 |
| 11 | ADR-033 D6 undecided (Verovio TAB) | Native Verovio; "Capo n" direction text (Verovio does not print `<capo>`) |
| 12 | Tasks without a red test lacked acceptance (E2E authoring, vendoring, polish, integration) | "Acceptance of tasks without a red test" block in `tasks.md` |
| 13 | The CI E2E job is at its 150-minute stopgap (LESSONS 014/015) and 016 adds eight specs | New task T144 (L3) |
| 14 | Mixer dock is now a fixed bar (014 integration fix); banner chords live in it | Layout row in the amendments; checkpoint a11y step |

Checked without change: every other task is "failing tests ... -> implement to pass"; FR/SC references resolve;
ADR-031..033 are unique (034-037 belong to other features); no new dependency (vendored BTC subset and MIT weights are
recorded for the CTO); task counts per owner as in tasks.md plus T144.

## 7. Flake list carried over

| Test | Symptom | State |
|---|---|---|
| `e2e/tests/export.spec.ts:67` | intermittent wait on the export job | fixed in r14 (wait on the job id) - watch at the checkpoint |
| Api.Tests agent detail re-queue | detail phase re-queued over a concurrent cancel | fixed on `main` (`004196f3`, merged `99e4b0e9`) |
| Api.Tests agent-executor timeouts (`ClaudeKey*`, `AgentEndpointsTests.Executor_*`) | 1-2 time out per full run | fixed in 022/023 (`70b2b515`, `af7e735f`); none seen in this Phase 0 run (see hand-off) |

No open flake; a lane that sees one records test, symptom and cause guess in its hand-off and tells the orchestrator.

## 8. Brief items for every lane (from LESSONS.md and the skill)

- Never touch the owner's stack (`stemstudio`) or another agent's stack (an a11y stack may be running); lanes start
  **no** Docker stack and run no E2E (write specs against ids and contracts, typecheck + lint them).
- Quiet tests, full logs to the scratchpad, summaries only; at most 1-2 heavy processes per lane; poll a background run
  and stop it after 10 minutes without progress.
- `dotnet format StemStudio.slnx --verify-no-changes` on the whole solution before every hand-off; the jscpd step of
  `tools/quality/run_all.py` must actually run (check its own output).
- No scope cut or contract deviation without the orchestrator; blocked > 20 min -> message the orchestrator.
- Backend: `Mode=ReadWrite` runtime connections, WAL, containment of every stored or worker path, one lock around
  check + insert + start (one analysis per kind and track); security self-review of the worker paths and the chord
  edit label (Harte pattern), ChordPro text (no injection into the attachment name), large JSON documents (90,000 frames).
- Worker: `torch.load(weights_only=True)` with `ports.BTC_SAFE_GLOBALS` only; no subprocess for downloads; path guard
  for every input.
- Frontend: WCAG 2.2 AA in bUnit (accessible name contains visible text, target size, focus not under the fixed dock),
  no awaiting the engine in rapid-click handlers, rail at 720 px / 320 px, no `listbox` with buttons inside, `@bind:get`/
  `@bind:set` for text fields.
- QA: wait on the job/analysis id you started, not on a status already set at load.
- Commits: Conventional Commits, identity `ragterhuis@agterhuis.net`, trailer
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; no push, no merge to `main`.
