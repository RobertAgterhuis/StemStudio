# ADR-020: AI composer agent - host, LLM backends, tool registry and safety (feature 011)

- **Status**: Proposed (architect, 2026-11-15). D3 (Claude backend) is conditional on the
  constitution amendment drafted in `specs/011-ai-composer-agent/plan.md` (constitution 1.3.0);
  everything else is within constitution 1.2.0.
- **Context documents**: `specs/011-ai-composer-agent/{spec.md, research.md, data-model.md,
  contracts/DELTA.md, contracts/tool-catalogue.md, evaluation/eval-set.md}`,
  `docs/research/roadmap-003-011.md` sections 10 and 12, ADR-002 (layers), ADR-012 (GPU policy).

## Context

The owner wants an agent in the UI that executes natural-language commands as StemStudio actions
("een soort van composer"), with a configurable LLM backend: local Ollama by default, the Claude
API optionally with a key from an environment variable. Feasibility on the reference machine
(research F-01..F-11): `qwen3.5:9b` selects the right tool in 6/6 probe cases with 0.4-2.4 s warm
latency on the GPU (5.8 GB VRAM at a 16k context), 10-24 s on the CPU; `qwen3.5:4b` is a usable
CPU fallback but obeyed an injected instruction in a track title (F-07), so model behaviour cannot
be the safety control. Ollama is reachable from the compose network via `host.docker.internal`.
CI has no LLM.

## Decisions

### D1 Host and layering

A new class library `StemStudio.Agent` (MEAI abstractions, no ASP.NET, no EF, no Domain) holds
the tool registry, policy, runner, prompt builder, backend factory and ports
(`IStemStudioActions`, `IConversationStore`, `IAgentSettingsStore`, `IGpuActivity`,
`ILlmGpuRelease`, `IRunEventSink`). `StemStudio.Api` hosts it: `/api/agent/*` endpoints, the
`AgentRunExecutor` hosted service, the EF stores (in `StemStudio.Persistence`) and the loopback
`IStemStudioActions` implementation that calls the API's own public endpoints with the
`StemStudio.Contracts` DTOs. Dependency direction: `Api -> Agent -> Contracts`;
`Persistence -> Agent` (store records) is **not** allowed; instead `Api` maps between Agent
records and Persistence entities. `Web -> API client` only (the panel never references
`StemStudio.Agent`).

### D2 Orchestration loop owned by StemStudio

`AgentRunner` drives `IChatClient` itself (tools passed as declarations, `ToolMode = Auto`), and
persists every step and event. It does not use `FunctionInvokingChatClient` auto-invocation or
`ApprovalRequiredAIFunction` (reasons: dynamic approval rules, persisted multi-minute approvals,
browser-executed UI tools, job waits without model turns, and MEAI's all-or-nothing approval of
a whole response; research R-02). Bounds: 12 model turns, 20 tool calls, 3 invalid calls per run,
per-turn timeout, Claude token budget (data-model 2). Parallel tool calls are allowed; they are
decided and executed one by one in order.

### D3 Backends

`IAgentChatClientFactory` creates: **Ollama** (default) through OllamaSharp's `IChatClient`,
model `qwen3.5:9b`, fallback `qwen3.5:4b` on the CPU while a GPU-heavy job is queued or running,
`num_ctx` 16384, `think` false; **Claude** (optional) through the official Anthropic C# SDK's
`IChatClient` integration, model `claude-opus-5-5`, adaptive thinking (not disableable on Opus
5.5) with explicit effort `low` (configurable), `ToolMode` auto (forced tool choice is rejected by
the model), streaming, per-run token budget, no server-side model fallbacks (a refusal ends the
run); **none**; **fake** (tests). The key comes from `ANTHROPIC_API_KEY` (wins) or, since
constitution 1.4.0 and ADR-035 (claude-key follow-up), from a key entered in Settings and stored
encrypted with Data Protection; it is never stored in plain text, logged or returned. Claude is
selectable only when a usable key exists and
the paid-use notice is acknowledged; there is no automatic switching between Ollama and Claude.

### D4 Tool registry that grows per feature

One `IAgentToolProvider` per feature contributes `AgentToolDescriptor`s (name, kind
server/ui/meta, fixed risk class, undo kind, JSON schema, result projection with marked untrusted
fields, availability). 011 ships the providers for 001-003 (26 tools) plus the meta tools; the
providers for 004-010 are added when those features are delivered ("available when feature NNN is
delivered"), each finalised against that feature's contract. The registry exposes only available
tools, filtered by page context (mixer tools only with an open mixer), in stable name order.

### D5 Safety: deterministic policy gate

- Risk classes `read`, `change`, `job`, `heavy`, `destructive` are code constants per tool.
- `heavy` and `destructive` always need user approval; `change`/`job` need it when the run is
  **tainted** (a tool result carried `untrusted_text`: imported titles/tags, artist info, any
  external text) or when an argument fails **provenance** (URLs and lyrics must appear literally in
  the user's own messages).
- Approvals are bound to `(stepId, tool, SHA-256 of canonical arguments)`, are decided only via
  the approval endpoint (user click), are idempotent, expire with the run or after 24 h, and the
  executor re-checks the hash. `plan_heavy` scope covers only heavy steps of the same run that
  match a plan item's tool and target, never destructive ones.
- Guardrails deny before approval: artist/singer imitation for 009/010, voice cloning (not a
  tool), lyrics not from the user for `generate_song`.
- No dangerous primitives: no shell, file paths, raw HTTP, URL fetch, SQL, settings or backend
  changes through tools; results are compact projections (<= 4 KB) without lyrics text,
  biographies, paths or secrets; the system prompt states that tool results are data.

### D6 Transport and persistence

Runs execute server-side; the panel subscribes to `GET /api/agent/runs/{id}/events` (SSE,
resumable by sequence number) via the Web process; approvals, UI results and cancel are POSTs.
Conversations, runs, steps, approvals, undo entries and the event log are stored in SQLite
(migration `AddAgent`). At API start-up non-terminal runs become `interrupted` and pending
approvals `expired`; worker jobs keep running.

### D7 GPU coordination with the host LLM (extends ADR-012 D3)

Before the API submits a GPU-heavy job, it calls `ILlmGpuRelease` (Ollama `keep_alive: 0`, 2 s,
best effort); while such a job is active the agent uses the fallback model on the CPU. The
worker's NVML free-memory check (ADR-012 D4) stays the final arbiter. When 009 introduces the
cross-service GPU scheduler, the release becomes one of its pre-job hooks.

### D8 Undo

Each tool declares `none`, `inverse` or `compensate`. Executed `change`/`job` steps record an
`UndoEntry` with the inverse/compensating call; Undo replays the newest command group in reverse
as a new run through the same policy (compensating deletes need approval unless the artefact is
"fresh": created by that run and unchanged since).

### D9 Testing and evaluation

CI uses `ScriptedChatClient` / `AdversarialChatClient` fakes (no LLM): policy, runner, events,
undo, endpoints, panel (bUnit) and E2E (`STEMSTUDIO_AGENT_BACKEND=fake`). Model quality is
measured by `tools/agent-eval` on the reference machine with fake StemStudio actions and the
30-command evaluation set (SC-001 >= 85 % for `qwen3.5:9b`).

## Consequences

- One new .NET component (`StemStudio.Agent`) and one new agent role (`ai-agent`).
- Three new NuGet packages (MEAI MIT, OllamaSharp MIT, Anthropic Apache-2.0); no new Python or npm
  dependencies; the worker and audio engine are untouched.
- The Claude backend is optional code behind the amendment; StemStudio is fully functional
  without it and without any LLM.
- More approvals in tainted runs (deliberate: safety over convenience).
- Adding a feature now includes an agent tool provider task (cross-cutting rule for 004-010).

## Alternatives rejected

| Alternative | Why not |
|---|---|
| Agent in the Blazor Web process | runs would die with the circuit; key in the Web container; GPU hook belongs to the API |
| Tools calling application services / DbContext directly | bypasses HTTP validators, breaks layering (constitution II) |
| `FunctionInvokingChatClient` + `ApprovalRequiredAIFunction` | static approval only, all-or-nothing per response, no persisted multi-minute approvals, no UI-executed tools |
| Relying on the model to refuse injections | `qwen3.5:4b` obeyed an injected delete (research F-07) |
| SignalR hub in the API | second real-time stack; SSE is simpler, contract-describable and resumable |
| Ollama inside compose (GPU container) | duplicates the owner's host models and GPU claims; host Ollama already present |
| `phi4-mini` as fallback | 1/6 in the probe (research F-06) |
| Claude server-side fallbacks / automatic Ollama->Claude switching | would route to a paid model without an explicit owner choice (FR-020) |
| LLM-written conversation summaries for long histories | extra model calls and failure modes; a sliding window is enough (YAGNI) |
