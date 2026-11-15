# Hand-off: architect / 011 Phase 0 part 1 (AI composer agent)

Base: tip of `006-008-lyrics-sheet-music` (57b5190; 004 complete, 005-008 backend/worker complete).
Scope: the Phase 0 work that does not depend on 009/010. Nothing here reads or depends on the
009/010 contracts (backend API 1.8.0/1.9.0). No existing Web page, worker or engine file changed.
The Claude API was never called; Ollama was not called (stub handlers only).

## What was built

- **`.claude/agents/ai-agent.md`** created from the plan draft (CTO-approved), updated for
  constitution 1.3.0 (Claude opt-in, no automatic switching, no `fallbacks`) and the verified
  backend option facts.
- **Packages** (T001) pinned in `Directory.Packages.props` (group `Agent`). The requested versions
  were less than two weeks old, so the newest qualifying releases were pinned:
  Microsoft.Extensions.AI **10.11.0**, OllamaSharp **5.5.0**, Anthropic **13.3.0**. A
  scratchpad compile + stub-handler spike confirmed (research F-12..F-15): Ollama `num_ctx`/`num_gpu`
  via `ChatOptions.AddOllamaOption`, `think`/`keep_alive` via `RawRepresentationFactory` ->
  `ChatRequest`; Claude via `AnthropicClient.AsIChatClient(model, maxTokens, AnthropicThinkingMode.Adaptive)`,
  `ChatOptions.Reasoning.Effort` -> `output_config.effort`, `ToolMode.Auto` -> `tool_choice auto`.
  No `DelegatingChatClient` shim is needed.
- **`src/StemStudio.Agent`** skeleton (T004): csproj (refs Contracts + MEAI + OllamaSharp +
  Anthropic), `Model/*` records (AgentSettings with defaults, Conversation, ConversationMessage,
  AgentRun, PlanItem, RunStep, JobRef, Approval with 24 h expiry, UndoEntry/UndoAction/UndoEntryStatus,
  RunEvent, TokenUsage, `AgentStateRules`), `Ports/*` (ITrackActions, IMixActions, IArtistActions,
  ILyricsActions (text-free `LyricsSummary`), IAssetActions behind `IStemStudioActions`;
  IUiToolGateway, IConversationStore, IRunStore, IUndoStore, IRunEventSink, IAgentSettingsStore,
  IGpuActivity, ILlmGpuRelease, IEnvironmentReader; inbound IAgentRunService, IAgentStatusService;
  AgentActionException, AgentConflictException), empty `AddStemStudioAgent()`, and the fake
  **`Backends/Fakes/ScriptedChatClient`** (+ `ChatScript`/`ScriptedTurn` records).
  Agent enums are not duplicated: the model uses the Contracts enums.
- **`tests/StemStudio.Agent.Tests`** (xUnit v3, coverlet 90 % line+branch gate inherited from
  `Directory.Build.props`): state rules, record invariants, port exceptions, DI entry point,
  ScriptedChatClient (streaming, non-streaming, parallel calls, exhausted script, cancellation,
  script parsing, schema).
- **`tools/agent-eval`** console skeleton (`StemStudio.AgentEval.csproj`, option parsing,
  exits 2 "not implemented"; T079a fills it). In the solution under `/tools/`.
- **Contracts** (T005 part 1): `src/StemStudio.Contracts/Agent/*` - 15 enums, 40 records (incl.
  event payloads), `AgentEventTypes`, `AgentProblemTypes`, `AgentFailureCodes`,
  `AgentDecisionReasons`, `AgentContractLimits`, `ApiRoutes.Agent*` (`ApiRoutes` made `partial`).
  `tests/StemStudio.Api.Tests/Contract/AgentContractTests.cs` (78 cases): delta is additive to
  1.7.0 (no path/operationId collision), operation list, enums and constants vs schema, routes vs
  paths, every DTO validates against its schema and writes exactly its properties, every SSE event
  validates as `AgentRunEvent`.
- **Agent API contract** (T003 part 1): standalone additive
  `specs/011-ai-composer-agent/contracts/agent-api.openapi.yaml` **1.10.0-delta.1** (17 operations,
  SSE stream with decoded-event `oneOf`, problem responses), `redocly.yaml`. Fix vs the DELTA draft:
  the 503 problem carries the status in the extension member `agentStatus` (RFC 9457 `status` is the
  HTTP code).
- **Tool catalogue 1.0.0** (T002): finalised for 001-008 against the actual contracts
  (004 help tools are `ui` - no help endpoint exists; 005 `refresh_artwork` = metadata lookup;
  006 lyrics status without text; 007 `export_audio` per `CreateAudioExportRequest` + new
  `export_stem_pack`; 008 `transcribe_stem` per `CreateScoreRequest` + new `delete_score`;
  `save_mix_as`/`update_mix` are `+mixer`; 32-tool cap rule per page). 42 binding tools; 009/010
  (3 tools) marked **pending 009/010**. Claude backend: no Wikipedia summary in `get_artist_info`.
- **Eval set 1.0.0**: cases 27-28 finalised; 29-30 pending 009 (28 scored cases: 15 nl, 13 en).
- **UI test ids delta draft** `contracts/ui-test-ids.delta.md` (additive to 1.7.0; minor number
  fixed at the merge).
- **Script format** `contracts/agent-script.schema.json` 1.0.0 (E2E `e2e/agent-scripts`) + DELTA.md
  section 8; DELTA.md status table; research.md F-12..F-15 and licence table; plan.md; tasks.md
  (T001, T002, T004 ticked; T003/T005 annotated "part 1 done", T006 open).

## Files changed

- New: `.claude/agents/ai-agent.md`; `src/StemStudio.Agent/**`; `src/StemStudio.Contracts/Agent/**`;
  `tests/StemStudio.Agent.Tests/**`; `tests/StemStudio.Api.Tests/Contract/AgentContractTests.cs`;
  `tools/agent-eval/**`; `specs/011-ai-composer-agent/contracts/{agent-api.openapi.yaml,redocly.yaml,agent-script.schema.json,ui-test-ids.delta.md}`.
- Modified: `Directory.Packages.props`, `StemStudio.slnx`, `src/StemStudio.Contracts/ApiRoutes.cs`
  (`partial` only), `src/StemStudio.Contracts/StemStudio.Contracts.csproj` (comment),
  `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` (links the delta as `Contract/011/`),
  `specs/011-ai-composer-agent/{plan,research,tasks}.md`, `contracts/{DELTA,tool-catalogue}.md`,
  `evaluation/eval-set.md`.

## Tests and coverage

`dotnet build StemStudio.slnx` -> 0 warnings, 0 errors.
`dotnet test StemStudio.slnx -p:CollectCoverage=true` (all gates 90 % line AND branch):

| Project | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Agent.Tests -> StemStudio.Agent | 48 passed | 100 % | 100 % |
| StemStudio.Api.Tests -> Api / Contracts | 1005 passed | 99.54 % / 100 % | 94.97 % / 100 % |
| StemStudio.Domain.Tests | 555 passed | 98.94 % | 95.82 % |
| StemStudio.Persistence.Tests | 77 passed | 98.4 % | 92.85 % |
| StemStudio.Web.Tests | 798 passed | 99.27 % | 92.56 % |

Worker (`pytest`) and audio engine (`npm test`) were not run: `git diff` shows no change under
`worker/`, `src/audio-engine/` or `src/StemStudio.Web/`.

## Quality checks

- `dotnet format StemStudio.slnx --verify-no-changes` -> clean.
- `uv run --project tools/quality python tools/quality/run_all.py` -> **0 errors**, 337 warnings
  (exit 0). New: one warning `StemStudio.Agent is not in the dependency map` (quality-gate T007/T008).
  A duplication error between `RunStep` and `AgentStepDto` was fixed; the new test data members were
  split to stay within the unit-size limit.
- `npx @redocly/cli@2.78.0 lint --config specs/011-ai-composer-agent/contracts/redocly.yaml specs/011-ai-composer-agent/contracts/agent-api.openapi.yaml`
  -> valid, 2 warnings (`operation-4xx-response` on `getAgentStatus` / `getAgentSettings`, same
  accepted pattern as `getHealth` in 1.7.0).

## Dependencies added

| Name | Version | Licence | URL |
|---|---|---|---|
| Microsoft.Extensions.AI (+ .Abstractions transitive) | 10.11.0 | MIT | https://github.com/dotnet/extensions |
| OllamaSharp | 5.5.0 | MIT (licence file in the package) | https://github.com/awaescher/OllamaSharp |
| Anthropic (official C# SDK) | 13.3.0 | Apache-2.0 | https://github.com/anthropics/anthropic-sdk-csharp |

Newer releases (11.0.0 / 5.5.2 / 13.8.0) can be taken once they are two weeks old (from 2026-11-25).

## Open issues / requests (what remains after 009/010 - Phase 0 part 2)

1. **Backend API 1.10.0 merge**: write the full `specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml`
   = 010's 1.9.0 + `agent-api.openapi.yaml` unchanged (version 1.10.0, changelog line, tag);
   Redocly over worker + backend; link it as `Contract/backend-api.openapi.yaml` in Api.Tests
   (plus subset test 1.9.0 -> 1.10.0); stage the 17 agent operationIds in
   `PendingAgentOperations` (BackendContractTests.StemsAndMixes, T005 remainder); point
   `AgentContractTests` at the merged document; Contracts csproj comment.
2. **009/010 tools**: catalogue 1.1.0 (`list_styles`, `restyle_track`, `generate_song`) against
   1.8.0/1.9.0; `IStemStudioActions` groups and Contracts usage for them; `JobPhaseDto`
   `restyle`/`generation` confirmed or adjusted; eval set 1.1.0 (cases 29-30 scored); guardrail
   reason codes confirmed; 32-tool cap rule extended for 45 tools.
3. **UI test ids**: merge `ui-test-ids.delta.md` into `contracts/ui-test-ids.md` as the next minor
   after 010 (expected 1.10.0); place the agent section in a settings page if 009/010 add one.
4. **T006**: full skeleton verification and `docs/handoffs/architect-011-plan.md` after part 2.
5. **quality-gate** (T007/T008): add `StemStudio.Agent` to the dependency map (Agent -> Contracts
   only; only `StemStudio.Agent.Backends` may use OllamaSharp/Anthropic; Web/Persistence must not
   reference Agent). Note: `tools/agent-eval` references StemStudio.Agent.
6. **ai-agent**: `Backends/Fakes/ScriptedChatClient.cs` and `ScriptedTurn.cs` were seeded by the
   architect in an ai-agent path at the CTO's request; ownership passes to the ai-agent
   (`AdversarialChatClient` still to be written, T043). `AgentServiceCollectionExtensions` is empty.
7. **backend**: `ILyricsActions` must drop the lyrics lines before returning (`LyricsSummary`);
   `ILlmGpuRelease` hooks also before 006 align, 008 score and the 009/010 GPU jobs (T078).
8. **devops**: CI picks up `StemStudio.Agent.Tests` through the solution; `CONTRACTS_DIR` for 011
   after the merge; secret scan (T010).
9. **CTO**: THIRD-PARTY-LICENSES entries for the three packages and "Optional external services:
   Anthropic Claude API (paid, opt-in)".

## Next owner

CTO: review; then the 009/010 architect hand-offs unblock Phase 0 part 2 (architect). The wave
(ai-agent, backend, frontend, qa, devops, quality-gate) can already start its foundational tasks
against the ports, DTOs and the delta document.
