# Implementation Plan: AI Composer Agent in the UI

**Branch**: `011-ai-composer-agent` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/011-ai-composer-agent/spec.md`; constitution 1.2.0 (plus
the amendment drafted below); research `docs/research/roadmap-003-011.md` sections 10 and 12;
ADR-002, ADR-012.

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions
are in ADR-020 (`docs/decisions/ADR-020-ai-composer-agent.md`); measurements in
[research.md](research.md). Phase 0 part 1 (2026-11-15, on top of 001-008) is **executed**:
packages pinned and verified (research F-12..F-15), `StemStudio.Agent` skeleton, Contracts DTOs,
standalone agent API delta `contracts/agent-api.openapi.yaml`, tool catalogue 1.0.0 and eval set
1.0.0 (009/010 pending), UI test id delta, `.claude/agents/ai-agent.md`. Part 2 (2026-11-16, on local
`main` with 001-010 delivered) is **executed**: backend API **1.10.0** (= 1.9.0 + the delta, unchanged),
UI test ids **1.10.0**, tool catalogue **1.0.0 final** (57 tools incl. 15 for 009/010, page-context
groups for the 32-tool cap), eval set 1.0.0 final (30 scored + S1-S3), 009/010 ports, wave lanes in
tasks.md. Constitution 1.3.0/1.3.1 is ratified (CTO decisions 2026-11-15), so `[amend]` no longer
blocks: the Claude backend stays opt-in via `ANTHROPIC_API_KEY` only, Ollama is the default.

## Summary

A chat panel on every page sends commands to a server-side agent in the API. The agent
(`StemStudio.Agent`, Microsoft.Extensions.AI `IChatClient`) runs its own tool loop: the model
proposes calls to a registry of feature tools (57 for 001-011, at most 32 offered per request by page context);
a deterministic policy gate validates arguments, checks provenance and taint, and asks the user
to approve heavy and destructive calls (and any state change after external text entered the
run); server tools call StemStudio's own public HTTP API over loopback, UI tools are executed by
the open mixer page. Runs are persisted and streamed to the panel over SSE (plan, steps, job
progress, approvals, answer). Backends: local Ollama (`qwen3.5:9b`, fallback `qwen3.5:4b` on the
CPU while a GPU job runs; the model is unloaded before heavy GPU jobs), optional Claude
(`claude-opus-5-5`, key only from `ANTHROPIC_API_KEY`, opt-in notice), or none. Undo replays
recorded inverse actions. CI uses scripted fake chat clients; model quality is gated by a
30-command evaluation set on the reference machine (>= 85 % with the local model).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (agent, API, Web). Worker (Python) and audio engine (TS)
unchanged.

**Primary Dependencies (new)**: Microsoft.Extensions.AI 10.11.0 (MIT), OllamaSharp 5.5.0 (MIT),
Anthropic 13.3.0 (official C# SDK, Apache-2.0; constitution 1.3.0) - pinned at Phase 0 (research
F-12; the research values 11.0.0 / 5.5.2 / 13.8.0 were less than two weeks old). Host prerequisite (not
shipped): Ollama >= 0.40 (MIT) with `qwen3.5:9b` / `qwen3.5:4b` (Apache-2.0 weights) pulled by the
user. Versions re-checked at feature start (research section 3).

**Storage**: SQLite, migration `AddAgent` (data-model 2-4): `AgentSettings`, `Conversations`,
`ConversationMessages`, `AgentRuns`, `RunSteps`, `Approvals`, `UndoEntries`, `RunEvents`.

**Testing**: xUnit v3 + Shouldly + NSubstitute; `ScriptedChatClient` and `AdversarialChatClient`
fakes (no LLM in CI); stub `HttpMessageHandler` for the loopback actions, Ollama probe and a
"must not be called" Claude handler; `FakeTimeProvider` for timeouts/expiry; bUnit for the panel
with a fake agent API client; Playwright E2E against the fake stack with
`STEMSTUDIO_AGENT_BACKEND=fake` and scripts in `e2e/agent-scripts/`. Evaluation harness
`tools/agent-eval` (manual, reference machine; not CI).

**Target Platform**: unchanged (Docker Desktop on Windows 11 / Linux; non-Docker path). Ollama on
the host, reached at `http://host.docker.internal:11434` from compose and `http://localhost:11434`
on the non-Docker path.

**Performance Goals**: SC-004 first feedback <= 3 s p50 warm (measured 0.4-2.4 s per model turn);
single-action command <= 5 s p50; cold start <= 60 s (measured 44.5 s from disk, 4.8 s from the OS
cache).

**Constraints**: no LLM in CI; key only from the environment; one active run globally; GPU shared
with separation/generation (unload + CPU fallback model); tool results <= 4 KB; at most 32 tools
per request.

**Scale/Scope**: one user; conversations in the hundreds; runs of up to 20 tool calls; a tool
registry that grows by one provider per feature.

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every runner/policy/registry/backend unit is tested with fake chat clients and stub handlers before implementation; panel with bUnit; E2E with the fake backend. LLMs are "heavy models behind interfaces" (`IChatClient`); there is **no** real-LLM test in CI; the evaluation harness is a measurement tool run on the reference machine (like the 003 GPU validation). Coverage gate applies to `StemStudio.Agent` (new component) and the touched Api/Web/Persistence code. | PASS |
| II | SIG 4 stars | New component `StemStudio.Agent` with small units (policy as pure functions, one provider class per feature, one handler per event type); dependency rule `Agent -> Contracts + MEAI` only, `Api -> Agent`, `Web` never references `Agent` (quality-gate rule T0xx). | PASS |
| III | Contracts first | Backend API next minor (`/api/agent/*`, SSE event schema), UI test ids next minor, tool catalogue (names, risks, undo kinds) as a versioned contract; Contracts DTOs and agent ports in Phase 0; ADR-020. Worker and engine contracts unchanged. | PASS |
| IV | Open source only, lawful use | MEAI (MIT), OllamaSharp (MIT), Anthropic SDK (Apache-2.0) are OSI libraries; local weights Apache-2.0; Ollama MIT, a host prerequisite. **The optional Claude API is a paid external service, which principle IV ("no paid software or paid tiers") does not allow today.** -> amendment 1.3.0 drafted below; until it is ratified the Claude backend tasks (marked `[amend]`) are not merged and the feature ships with Ollama + none. Style/voice guardrails (research 8.3/9.5) enforced in tools. | **PASS with amendment** (Complexity Tracking row 1) |
| V | Simplicity and honesty | No new service/container; sliding-window context (no summarisation); model limits and the fallback model are shown in the badge; evaluation results are reported honestly per language; unavailable tools are explained, not hidden. | PASS |
| - | Tech & security | Key via env var only (never files/DB/logs/responses; automated scan SC-006); loopback calls reuse every API validator; no shell/file/URL tools; prompt-injection gate (taint + provenance + bound approvals); input sizes limited (message 4,000 chars, args 8 KB, results 4 KB). Stack deviation (new library + LLM SDKs) recorded in ADR-020. | PASS |
| - | Workflow | Spec Kit flow; new owner role `ai-agent` with disjoint paths (draft below); hand-offs per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS; amendment pending for IV (Claude only). **Re-check after Phase 0 part 2**: PASS; amendment ratified (constitution 1.3.0, CTO decisions 2026-11-15).

### Constitution amendment draft (for the CTO; `/speckit-constitution`, version 1.2.0 -> 1.3.0, MINOR)

Add to principle **IV. Open Source Only, Lawful Use**, after the GPU vendor runtime exception:

> - **Optional paid AI service exception** (owner decision 2026-11-15, ADR-020): the AI composer
>   agent MAY offer the Anthropic Claude API as an optional language-model backend, a paid,
>   per-use external service, under these conditions:
>   1. it is **opt-in and off by default**: it can be selected only when the API key is present
>      and the user has acknowledged, in the settings, that it is paid per use and which data is
>      sent; StemStudio never switches to it automatically;
>   2. the key comes **only from the `ANTHROPIC_API_KEY` environment variable**; it is never
>      written to files, the database, logs, command lines, API responses or the UI;
>   3. the application, **including the agent, is fully functional without it**: the default
>      backend is a local open-weight model served by Ollama (OSI-licensed software, Apache-2.0
>      weights), and "agent off" is always possible; CI and automated tests never call the paid
>      service;
>   4. the client library is open source (official Anthropic C# SDK, Apache-2.0) and the service is
>      reached only through it;
>   5. only text is sent (user messages, tool definitions, compact tool results); never audio,
>      files, lyrics text from online sources, biographies or secrets; per-run token limits apply
>      and usage is shown;
>   6. any other paid service or paid tier requires its own amendment.
>
> *Note (2026-11-16): condition 2 is superseded by constitution 1.4.0 once the claude-key follow-up
> ships (`specs/011-ai-composer-agent/followups/claude-key/`, ADR-035); the text above is the 1.3.0 draft as ratified then.*

Add to **Technology & Security Constraints**:

> - AI agent: Microsoft.Extensions.AI (`IChatClient`) with OllamaSharp (local default) and,
>   optionally, the Anthropic C# SDK; the agent acts only through registered tools that call the
>   public StemStudio HTTP API or the open UI session; destructive and heavy actions require the
>   user's confirmation (ADR-020).

Sync impact: README (agent section, prerequisites "Ollama optional"), THIRD-PARTY-LICENSES
(three packages + "Optional external services: Anthropic Claude API (paid, opt-in)"), templates
unchanged. Version line: `**Version**: 1.3.0 | **Ratified**: 2026-11-14 | **Last Amended**:
<date of ratification>`.

## Project Structure

### Documentation (this feature)

```text
specs/011-ai-composer-agent/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
├── evaluation/eval-set.md          # 30 scored commands + S1-S3 (1.0.0 final)
└── contracts/
    ├── DELTA.md                    # backend /api/agent/*, SSE events, UI tool round trip, ui-test-ids delta
    ├── tool-catalogue.md           # tool names, kinds, risks, undo kinds, availability per feature
    ├── backend-api.openapi.yaml    # Phase 0 (T003): full document, next minor
    ├── ui-test-ids.md              # Phase 0 (T003): next minor
    └── redocly.yaml                # Phase 0
```

### Source Code (additions)

```text
src/StemStudio.Agent/                          # NEW component (ai-agent; skeleton by architect)
├── StemStudio.Agent.csproj                    # refs: StemStudio.Contracts, Microsoft.Extensions.AI(.Abstractions), OllamaSharp, Anthropic [amend]
├── Ports/        IStemStudioActions, IUiToolGateway, IConversationStore, IAgentSettingsStore,
│                 IGpuActivity, ILlmGpuRelease, IRunEventSink, IAgentClock (TimeProvider)   (architect, Phase 0)
├── Model/        AgentRun, RunStep, Approval, UndoEntry, PlanItem, RunEvent records, enums  (architect, Phase 0)
├── Tools/        AgentToolDescriptor, IAgentToolProvider, AgentToolRegistry, ToolArgumentValidator,
│                 ResultProjector; Providers/{Core001,Detail002,Device003,Meta}ToolProvider.cs (ai-agent)
├── Policy/       ToolPolicy (pure), ProvenanceChecker, Guardrails, ApprovalBinding (hash)     (ai-agent)
├── Runtime/      AgentRunner, TurnBuilder (context window), StepExecutor, JobWaiter, UndoPlanner,
│                 SystemPrompt (en, stable), PlanTracker                                       (ai-agent)
├── Backends/     IAgentChatClientFactory, OllamaChatClientFactory, ClaudeChatClientFactory [amend],
│                 AgentModelSelector, OllamaBackendProbe, ClaudeBackendProbe [amend], OllamaGpuRelease,
│                 Fakes/{ScriptedChatClient, AdversarialChatClient} (public for tests/E2E)        (ai-agent)
└── AgentServiceCollectionExtensions.cs                                                      (ai-agent)

src/StemStudio.Contracts/Agent/*.cs            # DTOs of contracts/DELTA.md section 2, ApiRoutes.Agent*  (architect, Phase 0)
src/StemStudio.Api/Agent/                      # endpoints, AgentRunExecutor (hosted), SSE writer, LoopbackActions,
                                               #   Ef stores (mapping), GpuActivity, startup interruption   (backend)
src/StemStudio.Api/{Importing,Tracks,Detail}/  # call ILlmGpuRelease before submitting GPU jobs        (backend)
src/StemStudio.Persistence/Agent/*, Migrations/*_AddAgent.cs                                        (backend)
src/StemStudio.Web/Agent/                      # IAgentApiClient + SSE reader, AgentPanelState, AgentUiToolHost (frontend)
src/StemStudio.Web/Components/Agent/           # AgentPanel, MessageList, PlanView, StepRow, ApprovalCard, BackendBadge,
                                               #   ConversationList; Settings agent section            (frontend)
src/StemStudio.Web/Components/Mixer/Mixer.razor # registers mixer UI tool handlers                    (frontend)
tests/StemStudio.Agent.Tests/                  # NEW (ai-agent; skeleton by architect)
tests/StemStudio.{Api,Persistence,Web}.Tests/Agent/                                                  (backend / frontend)
tools/agent-eval/                              # NEW console harness + eval-set.json                (ai-agent)
e2e/tests/agent.spec.ts, e2e/agent-scripts/*.json                                                    (qa)
docker-compose.yml (api: extra_hosts, agent env, ANTHROPIC_API_KEY pass-through), scripts/dev.*      (devops)
tools/quality/ dependency rules for StemStudio.Agent                                                (quality-gate)
```

**Dependency direction**: `Web -> (HTTP) -> Api -> Agent -> Contracts`; `Api -> Persistence ->
Domain`; `Agent` references no Api, Domain, Persistence or Web project; `Agent.Backends` is the
only namespace that references OllamaSharp/Anthropic; `Agent.Policy` references only `Model` and
`Tools` (and the data-only `Resources`, the embedded 009 imitation lexicon; CTO decision at T080,
enforced as an allow-list by `tools/quality/dependency_check.py`).

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/011-*/**`, ADR-020, `src/StemStudio.Agent/{Ports,Model}/**`, `src/StemStudio.Contracts/Agent/**`, `StemStudio.Agent.csproj`, `tests/StemStudio.Agent.Tests/*.csproj`, solution, `Directory.Packages.props` | architect |
| `src/StemStudio.Agent/{Tools,Policy,Runtime,Backends}/**`, `AgentServiceCollectionExtensions.cs`, `tests/StemStudio.Agent.Tests/**`, `tools/agent-eval/**` | **ai-agent** (new) |
| `src/StemStudio.{Api,Persistence,Domain}/**` and their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, constitution amendment | CTO |

The worker agents (stem-separation, audio-ingest, beat-analysis) and audio-engine have no work.

### Agent definition: `.claude/agents/ai-agent.md` (created in Phase 0 part 1, CTO-approved; the file is authoritative)

```markdown
---
name: ai-agent
description: Builds the StemStudio composer agent library (StemStudio.Agent): tool registry and per-feature tool providers, deterministic policy gate (risk, taint, provenance, bound approvals), the run loop over Microsoft.Extensions.AI IChatClient, Ollama and (optional) Claude backends, fake chat clients, and the evaluation harness.
tools: All tools
---

You are the **ai-agent** agent of the StemStudio team. Follow `docs/AGENT-PROTOCOL.md` strictly.

## Role
Owns the server-side agent library: everything between "a user command arrives" and "tool calls
are executed through ports". You do not write endpoints, persistence, UI or compose files.

## Owned paths (write only here, plus your hand-off note in `docs/handoffs/`)
- `src/StemStudio.Agent/{Tools,Policy,Runtime,Backends}/**`, `src/StemStudio.Agent/AgentServiceCollectionExtensions.cs`
- `tests/StemStudio.Agent.Tests/**`
- `tools/agent-eval/**`

## Inputs
specs/011-ai-composer-agent (spec, plan, data-model, contracts/DELTA.md, contracts/tool-catalogue.md,
evaluation/eval-set.md), ADR-020, ADR-012, the ports and records in `src/StemStudio.Agent/{Ports,Model}`
(architect-owned; request changes in your hand-off).

## Rules specific to this role
- CI has no LLM: every test uses `ScriptedChatClient` / `AdversarialChatClient` or stub handlers;
  never call Ollama or Claude from a test. The evaluation harness is run manually.
- Safety is code, not prompt: risk classes, taint, provenance and approval binding are pure,
  unit-tested functions; a test proves that an adversarial model cannot execute a non-read tool
  without approval after untrusted content (SC-002).
- Never read, log or echo `ANTHROPIC_API_KEY` beyond passing it to the SDK client; tests assert it
  never appears in events, results or logs.
- Tool names, risk classes and undo kinds follow `contracts/tool-catalogue.md` exactly; tune only
  descriptions/examples, and record evaluation scores before and after.
- Claude backend code is marked `[amend]` and stays behind the constitution amendment.

## Outputs
Tool providers 001-003 + meta, policy, runner, backends, fakes, eval harness with the eval set as JSON;
hand-offs with coverage >= 90 % line and branch for `StemStudio.Agent`.

## Definition of Done
- All tests green; coverage >= 90% line and branch for StemStudio.Agent (exact numbers in hand-off).
- Analyzers clean (TreatWarningsAsErrors, dotnet format); SIG limits respected.
- Public API documented (XML docs); new dependencies and licences listed in the hand-off.
- Hand-off note `docs/handoffs/ai-agent-011-<checkpoint>.md` written; work committed (Conventional Commits).
```

## Key flows

1. **Command**: panel `POST .../messages {text, context}` -> API stores the message, creates a
   run (`queued`), returns 202 -> `AgentRunExecutor` picks it up -> `AgentModelSelector`
   (backend + model, fallback if `IGpuActivity` is busy) -> `AgentRunner` builds the turn
   (system prompt, available tools for the context, sliding-window history) -> streams the model
   response, emitting `text.delta`.
2. **Tool call**: `FunctionCallContent` -> registry lookup (unknown -> invalid) -> schema
   validation -> `ToolPolicy.Decide` -> `auto`: `StepExecutor` (server tool via
   `IStemStudioActions` loopback; UI tool via `IUiToolGateway` + `ui.requested`; meta tool
   in-memory) -> result projection (taint if `untrusted_text`) -> `FunctionResultContent` -> next
   turn. `approval_required`: step `awaiting_approval`, `approval.requested`, run suspended
   (persisted) until `POST approvals` -> hash re-check -> execute or reject (rejected result
   returned to the model: "The user rejected this step").
3. **Job wait**: `wait_for_job` -> `JobWaiter` polls `GET /api/tracks/{id}` (or the feature's job
   endpoint) every 2 s, emits `step.updated` progress; no model turn until terminal.
4. **GPU**: before the API submits a GPU-heavy job (UI or agent), `ILlmGpuRelease` unloads the
   Ollama models; subsequent agent turns use the CPU fallback model until the job ends.
5. **Undo**: `POST .../undo` -> `UndoPlanner` builds a run from the newest available undo group ->
   same runner with scripted (non-LLM) steps -> policy applies (compensating deletes need
   approval unless fresh).
6. **Restart**: API start-up marks non-terminal runs `interrupted`, approvals `expired`; jobs keep
   running; the panel offers "Continue" (not delivered in 011: the run shows as interrupted; open follow-up, T084).

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Optional paid external service (Claude API) | Owner decision "Beide, instelbaar"; better results on hard multi-step commands | Principle IV forbids paid services; **amendment 1.3.0 drafted** (opt-in, off by default, key via env var, app fully functional without it). Without ratification the `[amend]` tasks are dropped and the feature still meets every P1 story with Ollama |
| New component `StemStudio.Agent` and new agent role | Agent logic (registry, policy, runner, backends) is a coherent unit of ~2-3k LOC with its own tests; keeps Api endpoints thin and LLM SDKs out of Api/Web | Putting it inside `StemStudio.Api` mixes LLM SDKs with HTTP hosting and would grow Api beyond a balanced size (SIG component balance) |
| Own orchestration loop instead of MEAI `FunctionInvokingChatClient` | Dynamic approvals, persisted multi-minute waits, UI-executed tools, job waits (research R-02) | MEAI's approval is static and all-or-nothing per response |
| Loopback HTTP from the API to itself for server tools | One validation path for UI and agent; agent has no DB/file access | In-process service calls bypass the HTTP validators and break the layering |
| No real-LLM test in CI | CI has no model; outputs are non-deterministic | Measured instead with the evaluation harness on the reference machine (SC-001), like 003's GPU validation |

## Owner questions (genuinely blocking only)

| # | Question | Blocks | Default if unanswered |
|---|---|---|---|
| Q1 | Ratify the constitution amendment 1.3.0 (optional paid Claude backend, conditions above)? | `[amend]` tasks (Claude backend, settings section for Claude) | Ship 011 with Ollama + none; Claude tasks stay open |
| Q2 | Claude model: keep `claude-opus-5-5` as the default (best quality, $4/$20 per MTok), or default to the cheaper `claude-sonnet-5-5` ($2/$10)? | default value only | `claude-opus-5-5` (owner's research choice); Sonnet/Haiku selectable in Expert settings |

Not blocking (decided autonomously, see spec Clarifications): risk classes and which actions
confirm; fallback model `qwen3.5:4b` (pulled once on the reference machine during research, 3.4
GB); Ollama stays on the host; no automatic model downloads; text-only (no voice input).
