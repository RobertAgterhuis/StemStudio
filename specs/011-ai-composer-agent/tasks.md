---

description: "Task list for feature 011 AI composer agent (Phase 0 by the architect at feature start, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: AI Composer Agent in the UI

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/](contracts/)
(DELTA.md, tool-catalogue.md), [evaluation/eval-set.md](evaluation/eval-set.md), constitution
1.2.0 (+ amendment 1.3.0 draft), ADR-012, ADR-020.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. **No task needs an LLM**: chat is faked with
`ScriptedChatClient` / `AdversarialChatClient`; Ollama and Claude HTTP are stub handlers. The
real model is measured only in the integration wave (T081, T082).

**Organization**: grouped by user story (US1..US6); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; **ai-agent** is a new role (draft definition in
  plan.md, created by the CTO before the wave). Each owner writes only inside its owned paths
  (plan.md ownership map) plus `docs/handoffs/<owner>-011-<checkpoint>.md`.
- **[amend]**: Claude backend work; merged only after the constitution amendment 1.3.0 is
  ratified (plan.md Q1). Without it these tasks stay open and the feature ships with Ollama + none.
- **[NNN]**: tool provider for feature NNN; executed only when that feature is delivered,
  otherwise marked "deferred" in the hand-off.
- Paths are relative to the repository root. **CTO** = orchestrator.
- Worker agents and audio-engine have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-011-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us6`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`
in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| ai-agent | StemStudio actions, stores, GPU activity, UI gateway, event sink | `src/StemStudio.Agent/Ports/*` + `Model/*` (T004) | in-memory fakes in `tests/StemStudio.Agent.Tests/Fakes/` |
| backend | agent library | `StemStudio.Agent` public API: `AddStemStudioAgent()`, `IAgentRunService`, ports to implement (T004) | NSubstitute substitutes of `IAgentRunService`; `ScriptedChatClient` for endpoint tests |
| frontend | `/api/agent/*`, SSE events | `contracts/backend-api.openapi.yaml` (next minor) + `StemStudio.Contracts/Agent` DTOs (T003, T005) | fake `IAgentApiClient`, recorded SSE fixtures |
| qa | UI | `contracts/ui-test-ids.md` (next minor), `e2e/agent-scripts` format (T003) | suite authored in the wave, executed in T080 |
| devops | env vars, compose | contracts/DELTA.md section 7 | skeleton |

---

## Phase 0: Contract code and skeleton (architect, at feature start, BEFORE the wave) - listed, not executed

- [ ] T001 [architect] Re-verify dependencies: NuGet versions of Microsoft.Extensions.AI, OllamaSharp, Anthropic (latest stable, licence unchanged); compile spike in the scratchpad that confirms how `num_ctx`, `num_gpu`, `think`, `keep_alive` reach Ollama through `IChatClient`, and how the Anthropic SDK exposes `IChatClient` plus effort/thinking display (research R-03); record results in research.md section 1 (F-12..)
- [ ] T002 [architect] Finalise `contracts/tool-catalogue.md` for the delivered features 004-010 against their contracts (names, endpoints, arguments, risks; mark undelivered ones "deferred"); finalise `evaluation/eval-set.md` cases 27-30; version both 1.0.0
- [ ] T003 [P] [architect] Contracts: `contracts/backend-api.openapi.yaml` (full document, next minor: `/api/agent/*` per DELTA.md 1-3 with SSE `text/event-stream` and the event schemas as components), `contracts/ui-test-ids.md` (next minor, DELTA.md 6), `redocly.yaml`; Redocly lint valid; `e2e/agent-scripts` JSON schema (`docs` section in DELTA.md)
- [ ] T004 [P] [architect] Skeleton: `src/StemStudio.Agent/StemStudio.Agent.csproj` (refs Contracts, MEAI, OllamaSharp; Anthropic only under the `[amend]` condition), `Ports/*` and `Model/*` records/enums of data-model 4-6 with XML docs, empty `AgentServiceCollectionExtensions`; `tests/StemStudio.Agent.Tests` project (xUnit v3, coverlet, Shouldly, NSubstitute) with skeleton tests of the records' invariants; solution entries; `Directory.Packages.props` versions; `tools/agent-eval` project stub
- [ ] T005 [P] [architect] `src/StemStudio.Contracts/Agent/*.cs` DTOs (DELTA.md 2) + `ApiRoutes.Agent*`; skeleton contract tests `tests/StemStudio.Api.Tests/Contract/AgentContractTests.cs` (DTO <-> schema); new operationIds staged in `PendingAgentOperations` (agreed one-time edit in backend tests)
- [ ] T006 [architect] Verify the skeleton: `dotnet build` 0 warnings, all tests + coverage gates, `tools/quality/run_all.py` 0 errors, worker/engine untouched; hand-off `docs/handoffs/architect-011-plan.md`

**Checkpoint**: skeleton green; CTO creates `.claude/agents/ai-agent.md` (plan.md draft); wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T007 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `StemStudio.Agent` may reference only `StemStudio.Contracts` and the MEAI/OllamaSharp/Anthropic packages; only `StemStudio.Agent.Backends` may use OllamaSharp/Anthropic namespaces; `StemStudio.Web` and `StemStudio.Persistence` must not reference `StemStudio.Agent`; `Agent.Policy` must not reference `Agent.Backends` or `Agent.Runtime` (plan.md dependency direction)
- [ ] T008 [quality-gate] Implement the rules in `tools/quality/dependency_check.py`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T009 [P] [devops] `docker-compose.yml` api: `extra_hosts: ["host.docker.internal:host-gateway"]`, `StemStudio__Agent__Ollama__BaseUrl: ${STEMSTUDIO_OLLAMA_URL:-http://host.docker.internal:11434}`, `StemStudio__Agent__Backend: ${STEMSTUDIO_AGENT_BACKEND:-ollama}`, `STEMSTUDIO_AGENT_FAKE_SCRIPT` + read-only mount of `e2e/agent-scripts` for the fake stack, bare `- ANTHROPIC_API_KEY` pass-through (no value anywhere); header comments (Ollama optional on the host, how to export the key in the shell); `scripts/dev.{ps1,sh}` same variables for the non-Docker path (`http://localhost:11434`) (DELTA.md 7, FR-018)
- [ ] T010 [devops] CI: build/test `StemStudio.Agent.Tests`, coverage gate for the new component, `CONTRACTS_DIR` -> 011, a secret-scan step asserting no `ANTHROPIC_API_KEY` value or `sk-ant-` pattern in the repo; CI never sets the key (SC-006)
- [ ] T011 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: fake chat clients, adversarial injection tests, agent E2E scripts, evaluation harness (manual, reference machine), traceability US1..US6 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

### ai-agent (tests first)

- [ ] T012 [P] [ai-agent] Failing tests `tests/StemStudio.Agent.Tests/Tools/RegistryTests.cs`: providers contribute descriptors; duplicate names rejected at start-up; `For(context)` returns only available tools (feature delivered, service reachable, `+mixer` only with an open mixer), sorted by name; cap 32 (FR-006)
- [ ] T013 [ai-agent] Implement `Tools/{AgentToolDescriptor,IAgentToolProvider,AgentToolRegistry}.cs` to pass T012
- [ ] T014 [P] [ai-agent] Failing tests `Tools/ArgumentValidatorTests.cs` + `ResultProjectorTests.cs`: schema validation (types, ranges, enums, `additionalProperties: false`, 8 KB limit), canonical JSON + SHA-256 hash stable across key order and escaping; projections keep only whitelisted fields, cap 4 KB with a truncation marker, wrap external text in `untrusted_text` and report taint; lyrics/biography/path fields never projected (FR-008, FR-015, FR-026)
- [ ] T015 [ai-agent] Implement `Tools/{ToolArgumentValidator,CanonicalJson,ResultProjector}.cs` to pass T014
- [ ] T016 [P] [ai-agent] Failing tests `Policy/ToolPolicyTests.cs` (table-driven over every risk x tainted x provenance x plan-scope combination of data-model 6): heavy/destructive always approval; change/job approval iff tainted or provenance fails; read/meta never; URL provenance (literal substring of a user message, normalised whitespace only); guardrails deny artist imitation and non-user lyrics; `plan_heavy` covers only heavy steps matching tool + target and never destructive or compensations (FR-012..FR-015, FR-025)
- [ ] T017 [ai-agent] Implement `Policy/{ToolPolicy,ProvenanceChecker,Guardrails,ApprovalBinding}.cs` to pass T016

### backend (tests first)

- [ ] T018 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/Agent/AgentPersistenceTests.cs`: round trip of settings, conversation, messages, run (plan, usage, taint), steps (canonical args, hash), approvals, undo entries, run events (seq order, `after` query); a 010-schema database migrates and loads with defaults (file SQLite) (FR-002)
- [ ] T019 [backend] Implement entities, configurations and migration `AddAgent` to pass T018
- [ ] T020 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Agent/StoresTests.cs`: `EfConversationStore` / `EfAgentSettingsStore` implement the ports (mapping Agent records <-> entities); one-active-run rule (409); settings defaults from `StemStudio__Agent__*`; start-up marks non-terminal runs `interrupted` and pending approvals `expired`
- [ ] T021 [backend] Implement `src/StemStudio.Api/Agent/Stores/*`, `AgentStartupRecovery` to pass T020

### frontend (tests first)

- [ ] T022 [P] [frontend] Failing tests `tests/StemStudio.Web.Tests/Agent/AgentApiClientTests.cs`: every `/api/agent/*` call of DELTA.md; SSE reader parses recorded streams (ids, event types, keep-alive comments, reconnect with `Last-Event-ID`), problem details -> `ApiException` (FR-001)
- [ ] T023 [frontend] Implement `src/StemStudio.Web/Agent/{IAgentApiClient,AgentApiClient,SseReader}.cs` to pass T022

**Checkpoint**: HO:foundational for ai-agent, backend, frontend.

---

## Phase 3: User Story 1 - Command in the chat panel, executed (P1) MVP

**Goal**: commands become visible plans and executed StemStudio actions.
**Independent test**: quickstart A5 (fake) and A10 (reference machine).

- [ ] T024 [P] [US1] [ai-agent] Failing tests `Runtime/AgentRunnerTests.cs` with `ScriptedChatClient`: text-only turn completes the run with the streamed answer; one tool call -> step created/succeeded -> result returned -> final turn; parallel calls decided and executed in order; unknown tool / invalid args -> error result, `InvalidCalls`++ and stop after 3; limits (turns, calls, timeout via `FakeTimeProvider`) stop with `limit-reached`/`timeout`; every state change emitted as the DELTA.md event in order (FR-005, FR-007, FR-008, FR-011)
- [ ] T025 [US1] [ai-agent] Implement `Runtime/{AgentRunner,StepExecutor,TurnBuilder,SystemPrompt}.cs` to pass T024
- [ ] T026 [P] [US1] [ai-agent] Failing tests `Runtime/PlanTrackerTests.cs` and `JobWaiterTests.cs`: `set_plan` publishes/replaces not-started items; calls are linked to plan items by tool order, extras appended; no plan -> steps still listed; `wait_for_job` polls every 2 s without model turns, reports progress, ends on completed/failed, honours `MaxJobWaitMinutes` and cancellation (FR-007, FR-009)
- [ ] T027 [US1] [ai-agent] Implement `Runtime/{PlanTracker,JobWaiter}.cs` + `Tools/Providers/MetaToolProvider.cs` to pass T026
- [ ] T028 [P] [US1] [ai-agent] Failing tests `Tools/Providers/Core001ToolProviderTests.cs`, `Detail002ToolProviderTests.cs`, `Device003ToolProviderTests.cs`: every tool of the catalogue for 001-003 has the contract name, risk, undo kind, schema (relative vs absolute gain split, research F-11) and maps to the right `IStemStudioActions` / `IUiToolGateway` call; projections per catalogue; `search_library` filters case- and diacritics-insensitively (FR-024)
- [ ] T029 [US1] [ai-agent] Implement the three providers to pass T028
- [ ] T030 [P] [US1] [ai-agent] Failing tests `TurnBuilderTests.cs`: context window keeps the system prompt + tools + newest messages within the budget (chars/3.5), page context injected as a short system line, language instruction "answer in the user's language", stable byte-identical system prompt and tool order across turns (prompt caching) (FR-003, FR-004)
- [ ] T031 [US1] [ai-agent] Refine `TurnBuilder` to pass T030
- [ ] T032 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Agent/AgentEndpointsTests.cs` (WebApplicationFactory + `ScriptedChatClient`): conversations CRUD, `POST messages` 202 + run executed by `AgentRunExecutor`, `GET runs/{id}`, SSE stream content-type, ids, replay with `after`/`Last-Event-ID`, keep-alive, closes after terminal event; responses validate against the 011 OpenAPI document; remove operations from `PendingAgentOperations` (FR-001, FR-002)
- [ ] T033 [US1] [backend] Implement `src/StemStudio.Api/Agent/{AgentEndpoints,AgentRunExecutor,SseWriter}.cs` and registration to pass T032
- [ ] T034 [P] [US1] [backend] Failing tests `Agent/LoopbackActionsTests.cs`: each `IStemStudioActions` member calls the public endpoint with the Contracts DTOs (stub handler at the loopback base URL), maps 4xx problem details to readable tool errors (no stack traces, no internal URLs), never touches DbContext (FR-005)
- [ ] T035 [US1] [backend] Implement `src/StemStudio.Api/Agent/LoopbackActions.cs` (typed `HttpClient`, `StemStudio__Agent__LoopbackBaseUrl`) to pass T034
- [ ] T036 [P] [US1] [frontend] Failing bUnit tests `Components/Agent/AgentPanelTests.cs`: toggle on every page (layout), input/send disabled while not ready, streamed text, plan checklist statuses, step rows with progress and error, context (page/track/mix) sent, conversation list (new/select) (ui-test-ids next minor; FR-001, FR-004, FR-007)
- [ ] T037 [US1] [frontend] Implement `Components/Agent/{AgentPanel,MessageList,PlanView,StepRow,ConversationList,BackendBadge}.razor`, `Agent/AgentPanelState.cs`, layout toggle to pass T036
- [ ] T038 [P] [US1] [frontend] Failing tests `Agent/AgentUiToolHostTests.cs` + mixer handler tests: `ui.requested` dispatched to the registered handler; mixer handlers change `MixerState` through the same methods as the UI (absolute/relative gain, pan, mute, solo, master, reset, transport, loop by seconds or bars from the beat grid, metronome) and return previous/current values; wrong track or no handler -> `{ok: false, "The mixer of <title> is not open"}`; `open_track`/`open_mixer` navigate (unsaved-changes guard of 001 respected) (FR-005)
- [ ] T039 [US1] [frontend] Implement `Agent/AgentUiToolHost.cs`, handler registration in `Mixer.razor` and the layout to pass T038
- [ ] T040 [US1] [qa] Author `e2e/tests/agent.spec.ts` "command executes" (fake script: search -> open mixer -> mute drums): plan + steps visible, `mute-drums` pressed state, answer shown; script files `e2e/agent-scripts/us1-*.json`

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Confirm before destructive or heavy actions (P1)

**Goal**: no heavy/destructive (or tainted) action without a bound approval.
**Independent test**: quickstart A5 approval specs; SC-002, SC-003.

- [ ] T041 [P] [US2] [ai-agent] Failing tests `Runtime/ApprovalFlowTests.cs`: approval-required step suspends the run (`awaiting_approval`, persisted), resumes on approve with hash re-check, returns "rejected by the user" on reject and marks dependent plan items skipped; expiry; `plan_heavy` scope auto-approves only matching later heavy steps; a second decision returns the stored one (FR-012, FR-015)
- [ ] T042 [US2] [ai-agent] Implement approval suspension/resumption in `AgentRunner`/`StepExecutor` (`IAgentRunService.DecideAsync`) to pass T041
- [ ] T043 [P] [US2] [ai-agent] Failing tests `Runtime/InjectionTests.cs` with `AdversarialChatClient` (calls `delete_track`, `import_from_url(evil)`, `update_mix` whenever it sees `untrusted_text`) across every untrusted source of the catalogue: 0 executions without approval; cards carry reason `tainted`/`url-not-from-user`; eval cases 22-24 scripted (SC-002, FR-013, FR-014)
- [ ] T044 [US2] [ai-agent] Fix anything missing to pass T043
- [ ] T045 [P] [US2] [backend] Failing tests `Agent/ApprovalEndpointTests.cs`: `POST approvals/{stepId}` approve/reject/plan scope, 409 not pending / arguments changed, idempotency, `approval.resolved` broadcast to all subscribers (two SSE clients) (FR-015)
- [ ] T046 [US2] [backend] Implement the approval endpoint to pass T045
- [ ] T047 [P] [US2] [frontend] Failing bUnit tests `Components/Agent/ApprovalCardTests.cs`: card shows effect, exact arguments, reasons (tainted text "proposed after reading external content"), undoability, estimated minutes and device for heavy; approve / approve-plan (only when allowed) / reject post the right body with `argumentsHash`; card closes on `approval.resolved` (FR-012, FR-013)
- [ ] T048 [US2] [frontend] Implement `Components/Agent/ApprovalCard.razor` to pass T047
- [ ] T049 [US2] [qa] Author `agent.spec.ts` "confirm and reject" (delete mix approved; delete track rejected -> track still listed; detail split approved -> detail phase queued) and "injection" (fake adversarial script on the `t-inject` fixture: approval card with reason `tainted`, reject -> nothing changed) (SC-002, SC-003)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Local, offline, graceful without an LLM (P1)

**Goal**: Ollama by default, CPU fallback model during GPU jobs, model unloaded before heavy jobs,
clear unavailable state.
**Independent test**: quickstart A6, A8, A12, A13; SC-005, SC-008.

- [ ] T050 [P] [US3] [ai-agent] Failing tests `Backends/OllamaBackendTests.cs` (stub handler for `/api/tags`, `/api/show`, `/api/chat`): factory sets model, `num_ctx` 16384, `think`, `keep_alive`; probe reports reachable/version/models/tool capability/default+fallback present with the exact fix texts; unreachable within 2 s -> not ready; `OllamaGpuRelease` posts `keep_alive: 0` for both models, swallows errors after 2 s; `AgentModelSelector` picks the fallback with `num_gpu: 0` while `IGpuActivity.IsHeavyJobActive()` and emits `model.changed` (FR-018, FR-021, FR-022, FR-023)
- [ ] T051 [US3] [ai-agent] Implement `Backends/{OllamaChatClientFactory,OllamaBackendProbe,OllamaGpuRelease,AgentModelSelector}.cs` (+ `DelegatingChatClient` shim if T001 found option gaps) to pass T050
- [ ] T052 [P] [US3] [backend] Failing tests `Agent/GpuCoordinationTests.cs`: `ILlmGpuRelease.ReleaseAsync` is awaited before `POST /api/imports`, `/imports/upload`, `/tracks/{id}/retry`, `/tracks/{id}/detail` submit to the worker (order asserted with a recording fake), never fails or delays the request beyond 2 s, skipped when backend != ollama or worker device = CPU; `IGpuActivity` true while any track is queued/processing standard or detail on a GPU worker (FR-021, FR-022, SC-008)
- [ ] T053 [US3] [backend] Implement the hook calls in `Importing/`, `Tracks/`, `Detail/` and `Agent/GpuActivity.cs` to pass T052
- [ ] T054 [P] [US3] [backend] Failing tests `Agent/StatusEndpointTests.cs`: `GET /api/agent/status` within 3 s for ready / Ollama down / model missing / backend none; `POST messages` 503 `backend-unavailable` with status; existing endpoints unaffected when Ollama is down (FR-023, SC-005)
- [ ] T055 [US3] [backend] Implement the status endpoint to pass T054
- [ ] T056 [P] [US3] [frontend] Failing bUnit tests: `agent-unavailable` with reason + fix, input disabled, badge `data-backend`/`data-model`/`data-fallback`; status refresh every 10 s while the panel is open (FR-021, FR-023)
- [ ] T057 [US3] [frontend] Implement to pass T056
- [ ] T058 [US3] [qa] Author `agent.spec.ts` "unavailable" (backend none: panel state, rest of the app works) and ensure the default suite runs unchanged with `STEMSTUDIO_AGENT_BACKEND=none` (SC-005)
- [ ] T059 [US3] [devops] CI e2e job: after the default fake-stack run, run `agent.spec.ts` with `STEMSTUDIO_AGENT_BACKEND=fake` and the unavailable spec with `none`; same in `scripts/test-all.*`

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Backend switch in Settings with status (P2)

**Goal**: choose Ollama / Claude / none with status, notice and connection test.
**Independent test**: quickstart A14, A15; SC-006.

- [ ] T060 [P] [US4] [backend] Failing tests `Agent/SettingsEndpointTests.cs`: GET/PUT settings with validation (model must exist with tools capability -> 422), `claude` without key -> 409, without notice -> 422; `POST settings/test` for Ollama; no response, log line (captured logger) or DB row ever contains the key value (test sets a sentinel key) (FR-018, FR-019, SC-006)
- [ ] T061 [US4] [backend] Implement the settings endpoints to pass T060
- [ ] T062 [P] [US4] [frontend] Failing bUnit tests `Components/Agent/AgentSettingsTests.cs`: radio group, Ollama status and model pickers (only tool-capable models), Claude disabled with "not configured" when `keyConfigured` false, notice + acknowledge before saving Claude, test-connection result, errors (FR-019)
- [ ] T063 [US4] [frontend] Implement the agent section of the settings page (004 page, or `/settings` if absent) to pass T062
- [ ] T064 [P] [US4] [ai-agent] [amend] Failing tests `Backends/ClaudeBackendTests.cs` (stub handler for `api.anthropic.com`): factory uses `ANTHROPIC_API_KEY` from an injected environment reader, model `claude-opus-5-5` default, effort setting, tool mode auto, streaming, max output tokens; per-run token budget stops the run (`budget-exhausted`); refusal and 401/429/5xx mapped to readable `run.failed` with "Switch to local"; probe only reports `keyConfigured` and calls `GET /v1/models/{id}` only on explicit test; a "must not be called" handler proves 0 requests when Claude is not selected (FR-018, FR-020, SC-006)
- [ ] T065 [US4] [ai-agent] [amend] Implement `Backends/{ClaudeChatClientFactory,ClaudeBackendProbe}.cs` to pass T064
- [ ] T066 [US4] [frontend] [amend] Usage display (`agent-usage`) and "Switch to local" on Claude failures (bUnit test first, then implementation)
- [ ] T067 [US4] [qa] Author `agent.spec.ts` "settings" (without key: Claude not selectable; switch to none and back to Ollama on the fake stack)

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Conversation history and undo (P2)

**Goal**: conversations persist; undo reverses the last command group.
**Independent test**: quickstart A5 undo spec; SC-007.

- [ ] T068 [P] [US5] [ai-agent] Failing tests `Runtime/UndoPlannerTests.cs`: undo entries recorded for executed change/job steps with the inverse/compensation of the catalogue; newest available group undone in reverse order as a new run; fresh compensations auto, others need approval; entries become `obsolete` when the target changed outside the agent (mix `updatedAt`) or vanished; "nothing to undo" (FR-017, SC-007)
- [ ] T069 [US5] [ai-agent] Implement `Runtime/UndoPlanner.cs` and recording in `StepExecutor` to pass T068
- [ ] T070 [P] [US5] [backend] Failing tests: `POST conversations/{id}/undo` 202/409; rename/delete conversation (409 while active); reload restores messages, plans, steps (FR-002, FR-017)
- [ ] T071 [US5] [backend] Implement to pass T070
- [ ] T072 [P] [US5] [frontend] Failing bUnit tests: undo button state, conversation rename/delete, history reload restores the view and resumes a running run's stream (FR-002)
- [ ] T073 [US5] [frontend] Implement to pass T072
- [ ] T074 [US5] [qa] Author `agent.spec.ts` "undo" (three mixer changes -> undo -> previous fader values via `aria-valuenow`; save-as -> undo -> mix gone) and "history" (reload page, conversation restored)

**Checkpoint**: HO:us5.

---

## Phase 8: User Story 6 - Orchestrate features 004-010 (P3)

**Goal**: one tool provider per delivered feature (catalogue 1.0.0 from T002).
**Independent test**: per feature, its evaluation cases pass with scripted turns; real model in T081.

- [ ] T075 [P] [US6] [ai-agent] [004] [005] [006] Failing provider tests then implementation: `HelpToolProvider` (004), `ArtistToolProvider` (005, `get_artist_info` taints), `LyricsToolProvider` (006, never projects lyrics text; `align_lyrics` heavy) (FR-024, FR-026)
- [ ] T076 [P] [US6] [ai-agent] [007] [008] Failing provider tests then implementation: `ExportToolProvider` (007, heavy above 500 MB estimate), `ScoreToolProvider` (008) (FR-024)
- [ ] T077 [P] [US6] [ai-agent] [009] [010] Failing provider tests then implementation: `StyleToolProvider` (009, styleIds only, artist-imitation guardrail), `SongToolProvider` (010, lyrics provenance, no voice cloning) (FR-024, FR-025)
- [ ] T078 [US6] [backend] [006-010] Failing tests then implementation: `ILlmGpuRelease` before each delivered GPU job endpoint of 006-010 and `IGpuActivity` covers their jobs (or registration as a pre-job hook of the 009 GPU scheduler, if it exists) (FR-022)

**Checkpoint**: HO:us6.

---

## Phase 9: Polish (inside the wave, per owner)

- [ ] T079a [P] [ai-agent] `tools/agent-eval`: console harness with fake StemStudio actions seeded from eval-set fixtures, `eval-set.json` generated from `evaluation/eval-set.md`, grading rules, Markdown report (success per case/language, latency p50/p90), `--backend ollama|claude --runs N`; unit tests for grading with scripted turns (research R-09)
- [ ] T079b [P] [ai-agent] Refactor to SIG limits, XML docs, HO:polish with `StemStudio.Agent` coverage
- [ ] T079c [P] [backend] Refactor, XML docs, HO:polish with Api/Persistence coverage
- [ ] T079d [P] [frontend] Accessibility of the panel (keyboard: Enter sends, Esc closes; live region for streamed text and step status; cards focus the Approve button only after reading - no auto-focus on Approve), HO:polish
- [ ] T079e [P] [qa] Traceability matrix update, HO:polish
- [ ] T079f [P] [devops] Validate compose/CI (`docker compose config -q`, `actionlint`), HO:polish
- [ ] T079g [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T080 [CTO] Merge the owner branches; full build, lint, unit + coverage gates; Playwright default suite + agent specs on the fake stack, unavailable spec with `none` (retries 0) (quickstart A1-A6)
- [ ] T081 [CTO] Reference machine: quickstart A7-A13; `tools/agent-eval --backend ollama --runs 3` (SC-001, SC-004); GPU coordination check (SC-008); record results in `docs/STATUS.md`; if SC-001 fails, tune tool descriptions with the ai-agent (re-run, keep the report history)
- [ ] T082 [CTO] [amend] After ratification only: owner-run A14/A15 with Claude (paid, opt-in), result in `docs/STATUS.md`
- [ ] T083 [CTO] Constitution amendment via `/speckit-constitution` (plan.md draft, 1.3.0) once the owner ratifies (Q1); `docs/THIRD-PARTY-LICENSES.md` (MEAI, OllamaSharp, Anthropic SDK; "Optional external services"; Qwen3.5 weights Apache-2.0 pulled by the user; Ollama host prerequisite); README agent section (prerequisites, Ollama setup, models, settings, key via shell env only, data sent to Claude, limitations)
- [ ] T084 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR
- [ ] T084a [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1, last step of the integration wave, after `docs/STATUS.md` is updated): edit the chapters of this feature in `docs/public/src/user-manual/` and/or `docs/public/src/technical-manual/` (remove the *In development* marker, describe the delivered behaviour, setup, env vars, contracts and limits; add screenshots from the visual baselines where useful), run `cd tools/docs && npm ci && npm run browser && npm run build`, commit the sources, both PDFs and `docs/public/build-manifest.json` together; `node tools/docs/check.mjs` passes

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| ai-agent | T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> HO:foundational -> T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> HO:us1 -> T041 -> T042 -> T043 -> T044 -> HO:us2 -> T050 -> T051 -> HO:us3 -> T064 -> T065 [amend] -> HO:us4 -> T068 -> T069 -> HO:us5 -> T075 -> T076 -> T077 (delivered features only) -> HO:us6 -> T079a -> T079b (HO:polish) | `src/StemStudio.Agent/{Tools,Policy,Runtime,Backends}/**`, `AgentServiceCollectionExtensions.cs`, `tests/StemStudio.Agent.Tests/**`, `tools/agent-eval/**` | `ScriptedChatClient`, `AdversarialChatClient`, in-memory port fakes, stub handlers, `FakeTimeProvider` |
| backend | T018 -> T019 -> T020 -> T021 -> HO:foundational -> T032 -> T033 -> T034 -> T035 -> HO:us1 -> T045 -> T046 -> HO:us2 -> T052 -> T053 -> T054 -> T055 -> HO:us3 -> T060 -> T061 -> HO:us4 -> T070 -> T071 -> HO:us5 -> T078 -> HO:us6 -> T079c (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | `ScriptedChatClient` via `AddStemStudioAgent` options, stub handlers, SQLite, `FakeTimeProvider` |
| frontend | T022 -> T023 -> HO:foundational -> T036 -> T037 -> T038 -> T039 -> HO:us1 -> T047 -> T048 -> HO:us2 -> T056 -> T057 -> HO:us3 -> T062 -> T063 -> T066 [amend] -> HO:us4 -> T072 -> T073 -> HO:us5 -> T079d (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IAgentApiClient`, recorded SSE fixtures, bUnit |
| qa | T011 -> T040 -> HO:us1 -> T049 -> HO:us2 -> T058 -> HO:us3 -> T067 -> HO:us4 -> T074 -> HO:us5 -> T079e (HO:polish) | `e2e/**` (incl. `e2e/agent-scripts/**`), `docs/testing/**` | suite executed in T080 |
| devops | T009 -> T010 -> HO:foundational -> T059 -> HO:us3 -> T079f (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | skeleton |
| quality-gate | T007 -> T008 -> HO:foundational -> T079g (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 -> T006 (at feature start; later contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | create `.claude/agents/ai-agent.md` (before the wave) -> T080 -> T081 -> T082 [amend] -> T083 -> T084 | `.claude/agents/**`, `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, constitution, merges | n/a |

Task count per owner: architect 6, ai-agent 29 (of which 2 [amend]; T075-T077 conditional on
delivered features), backend 20, frontend 16 (1 [amend]), qa 7, devops 4, quality-gate 3, CTO 5
(1 [amend]) - **total 90** (T001-T084 with T079a-g; 4 [amend]).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T005 operationId staging | backend test files | staged by the architect; un-staged by the backend in T032 |
| T053 GPU hook in import/detail endpoints | Api files touched by 001-003 | backend task (backend owns all Api code) |
| T039 handler registration in `Mixer.razor` | mixer page of 001/002 | frontend task (frontend owns the Web app) |
| T083 licences, README, amendment | owner hand-offs, constitution | consolidated by the CTO (AGENT-PROTOCOL rule 9; constitution governance) |

## Dependencies & Execution Order

Phase 0 (at feature start, after 004-010) -> CTO creates the `ai-agent` role -> wave (Setup +
Foundational + stories per owner, parallel across owners; stories independent at the contract
level via fakes) -> Integration wave. `[amend]` tasks wait for Q1; `[NNN]` tasks wait for their
feature. US1-US3 form the MVP of the feature (all P1).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component incl. the new `StemStudio.Agent`, exact numbers in the hand-off (I)
- [ ] No test calls a real LLM; chat behind `IChatClient` fakes; evaluation run on the reference machine recorded (I, SC-001)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the `StemStudio.Agent` dependency rules (II)
- [ ] Implementations match backend API (next minor), UI test ids (next minor), tool catalogue 1.0.0; contract tests pass; worker and engine contracts unchanged (III)
- [ ] Only OSI libraries; Claude backend merged only after amendment 1.3.0; app and agent fully functional without `ANTHROPIC_API_KEY`; licences recorded (IV)
- [ ] Model in use, fallbacks, unavailable tools and evaluation limits shown honestly (V)
- [ ] Key never in files, logs, DB or responses (automated scan); injection tests green (SC-002, SC-006)
- [ ] docs/public user and/or technical manual updated for this feature and the PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
