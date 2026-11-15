# Contract delta: feature 011 (draft; final versions assigned at feature start)

Feature 011 adds one group of backend endpoints (`/api/agent/*`), one internal hook in the backend
(GPU release before heavy jobs), additive UI test ids, and the tool catalogue
([tool-catalogue.md](tool-catalogue.md)). The worker contract and the audio-engine contract are
**unchanged**. At feature start the architect writes the full documents
`contracts/backend-api.openapi.yaml` (backend **1.(N+1).0**, where N is the minor version current
after features 004-010, additive) and `contracts/ui-test-ids.md` (next minor, additive), validates
them with Redocly (OpenAPI 3.1), and replaces this file's "draft" markers with the versions.

## 1. Endpoints (backend API, tag `agent`)

All bodies are JSON (`application/json`) except the event stream. Errors are RFC 9457 problem
details as in 001 (`type` = `https://stemstudio.local/problems/<code>`). No endpoint ever returns
the API key or any part of it.

| Method + path | operationId | Request | Responses |
|---|---|---|---|
| `GET /api/agent/status` | `getAgentStatus` | - | `200 AgentStatus` (always 200; probes time out after 2 s each) |
| `GET /api/agent/settings` | `getAgentSettings` | - | `200 AgentSettings` |
| `PUT /api/agent/settings` | `updateAgentSettings` | `AgentSettingsUpdate` | `200 AgentSettings`; `409 claude-not-configured` (key missing); `422 notice-not-acknowledged`, `422 model-unavailable` (not pulled / no tools capability), `422 validation` |
| `POST /api/agent/settings/test` | `testAgentBackend` | `{backend: ollama\|claude}` | `200 BackendTestResult` (never generates text; Claude only on this explicit call) |
| `GET /api/agent/tools` | `listAgentTools` | query `page?`, `trackId?` | `200 AgentToolInfo[]` (registry view for the panel and the Expert log) |
| `GET /api/agent/conversations` | `listConversations` | query `limit` (1-100, 50) `before?` | `200 ConversationSummary[]` newest first |
| `POST /api/agent/conversations` | `createConversation` | `{title?}` | `201 Conversation` |
| `GET /api/agent/conversations/{conversationId}` | `getConversation` | - | `200 Conversation` (messages + run summaries); `404` |
| `PATCH /api/agent/conversations/{conversationId}` | `renameConversation` | `{title}` | `200 ConversationSummary`; `404`; `422` |
| `DELETE /api/agent/conversations/{conversationId}` | `deleteConversation` | - | `204`; `404`; `409 run-active` |
| `POST /api/agent/conversations/{conversationId}/messages` | `sendAgentMessage` | `SendMessageRequest` | `202 {runId, messageId}`; `404`; `409 run-active`; `503 backend-unavailable` (with `AgentStatus` in the problem's `status` extension); `422` (empty / > 4,000 chars) |
| `POST /api/agent/conversations/{conversationId}/undo` | `undoLastAgentAction` | - | `202 {runId}`; `409 nothing-to-undo` / `run-active` |
| `GET /api/agent/runs/{runId}` | `getAgentRun` | - | `200 AgentRun` (snapshot: status, plan, steps, pending approval, usage) |
| `GET /api/agent/runs/{runId}/events` | `streamAgentRunEvents` | query `after?` (int); header `Last-Event-ID?` | `200 text/event-stream` (section 3); stream ends after the terminal event; `404` |
| `POST /api/agent/runs/{runId}/approvals/{stepId}` | `decideAgentApproval` | `{decision: approve\|reject, scope?: once\|plan_heavy, argumentsHash}` | `200 ApprovalState` (idempotent: returns the stored decision); `409 approval-not-pending` (expired/run ended), `409 arguments-changed` (hash mismatch); `404` |
| `POST /api/agent/runs/{runId}/ui-results/{stepId}` | `submitAgentUiResult` | `{ok: bool, result?: object <= 4 KB, error?: string <= 500}` | `204`; `409 ui-result-not-expected` (already answered or timed out) |
| `POST /api/agent/runs/{runId}/cancel` | `cancelAgentRun` | `{cancelJobs: bool}` | `202 AgentRun`; `409` when terminal |

## 2. Schemas (summary; full JSON Schema in the OpenAPI document)

```yaml
AgentStatus:
  backend: enum [ollama, claude, none, fake]          # selected
  ready: boolean                                        # selected backend usable now
  activeModel: string | null                            # model the next turn would use
  usingFallback: boolean                                # Ollama fallback model (GPU busy)
  reason: string | null                                 # readable, e.g. "Ollama not reachable at http://host.docker.internal:11434"
  fix: string | null                                    # e.g. "ollama pull qwen3.5:9b (6.6 GB)"
  ollama: {reachable: bool, version: string|null, models: [{name, sizeBytes, tools: bool}], defaultPresent: bool, fallbackPresent: bool}
  claude: {keyConfigured: bool, noticeAcknowledged: bool, model: string}   # never the key
AgentSettings:      # data-model 2, minus nothing secret
  backend, ollamaModel, ollamaFallbackModel, ollamaThink, claudeModel, claudeEffort,
  claudeNoticeAcknowledged: bool, limits: {...}, updatedAt
AgentSettingsUpdate: same fields optional + acknowledgeClaudeNotice: bool
BackendTestResult: {backend, ok: bool, latencyMs: int, detail: string}
AgentToolInfo: {name, feature, kind: server|ui|meta, risk: read|change|job|heavy|destructive,
                undo: none|inverse|compensate, available: bool, unavailableReason: string|null,
                title: string, description: string}
ConversationSummary: {conversationId, title, createdAt, updatedAt, lastRunStatus|null}
Conversation: ConversationSummary + messages: [Message] + runs: [AgentRunSummary]
Message: {messageId, seq, role: user|assistant, text, context: {page, trackId|null, mixId|null}, runId|null, createdAt}
SendMessageRequest: {text: string 1..4000, context: {page: string<=40, trackId?: uuid, mixId?: uuid}}
AgentRun: {runId, conversationId, status, backend, model, tainted, plan: [PlanItem], steps: [Step],
           usage: {inputTokens, outputTokens, cachedInputTokens}, failure: {code, message}|null,
           startedAt, endedAt|null}
PlanItem: {index, title, tool|null, status}
Step: {stepId, seq, tool, kind, risk, undo, arguments: object, argumentsHash, planIndex|null,
       decision: auto|approval_required|denied, decisionReasons: [string], status,
       resultSummary: object|null, error|null, job: {trackId|null, jobId|null, phase}|null,
       progress: int|null, estimatedMinutes: number|null, startedAt|null, endedAt|null}
ApprovalState: {stepId, status: pending|approved|rejected|expired, scope, decidedAt|null}
```

## 3. Event stream (`text/event-stream`)

Each event: `id: <seq>`, `event: <type>`, `data: <JSON>`; a `: keep-alive` comment every 15 s.
Replay: `after=<seq>` or `Last-Event-ID` returns every stored event with a larger seq, then live
events. The stream closes after `run.completed` / `run.failed` / `run.cancelled` /
`run.interrupted`.

| `event` | `data` | Panel effect |
|---|---|---|
| `run.started` | `{runId, backend, model, usingFallback}` | badge, spinner |
| `model.changed` | `{model, usingFallback, reason}` | badge (e.g. switched to `qwen3.5:4b` because a GPU job started) |
| `plan.updated` | `{plan: [PlanItem]}` | checklist |
| `text.delta` | `{text}` (coalesced per 100 ms) | streamed answer |
| `step.created` | `Step` | step row (planned/running) |
| `step.updated` | `{stepId, status, progress?, resultSummary?, error?}` | status/progress |
| `approval.requested` | `{stepId, tool, risk, arguments, argumentsHash, reasons, effect: string, undoable: bool, estimatedMinutes?, device?, allowPlanScope: bool}` | confirmation card |
| `approval.resolved` | `ApprovalState` | card closes (all tabs) |
| `ui.requested` | `{stepId, tool, arguments, timeoutSeconds: 15}` | the UI tool host executes and POSTs `ui-results` |
| `usage.updated` | `{inputTokens, outputTokens, cachedInputTokens}` | Claude usage (Advanced+) |
| `run.completed` | `{runId, messageId}` | final answer stored |
| `run.failed` | `{code, message, fix?}` | error with fix / "Switch to local" |
| `run.cancelled` / `run.interrupted` | `{runId, cancelledJobs: [..]}` / `{runId}` | |

## 4. UI tool round trip

1. The runner decides `auto` (or the user approved) for a `ui` tool, stores the step as
   `awaiting_ui` and emits `ui.requested`.
2. The Web app's `AgentUiToolHost` (layout-level service) dispatches to the registered handler:
   the open `Mixer` page registers mixer/transport/loop/metronome handlers for its track; the
   layout registers `open_track`, `open_mixer`, `navigate`, `open_help`.
3. The handler executes against `MixerState` (same code paths as the user's own clicks, so engine
   and unsaved-changes behaviour are unchanged) and POSTs `{ok, result}`; a handler for another
   track or no handler returns `{ok: false, error: "The mixer of <title> is not open"}`.
4. No result within 15 s -> step `failed` ("The page did not respond").

## 5. Backend-internal hook (not HTTP)

`ILlmGpuRelease.ReleaseAsync(CancellationToken)` (in `StemStudio.Agent`, implemented by
`OllamaGpuRelease`) is called by the backend **before** it submits a GPU-heavy job to the worker:
`POST /api/imports`, `/api/imports/upload`, `/api/tracks/{id}/retry`, `/api/tracks/{id}/detail`
(and the 006-010 GPU job endpoints when they land). Best effort, 2 s timeout, never fails the
request; skipped when the backend is not `ollama` or the worker device is CPU (`GET /api/device`
cache). `IGpuActivity.IsHeavyJobActive()` (backend, from track/detail states) drives the
fallback-model selection (FR-021).

## 6. UI test ids (additive, next minor of `ui-test-ids.md`)

| Screen | `data-testid` |
|---|---|
| Layout (every page) | `agent-toggle` (opens/closes the panel), `agent-panel`, `agent-backend-badge` (attributes `data-backend` `ollama`/`claude`/`none`/`fake`, `data-model`, `data-fallback` `true`/`false`), `agent-unavailable` (only while not ready; contains reason and fix) |
| Agent panel | `agent-input`, `agent-send`, `agent-cancel` (only while a run is active), `agent-undo` (enabled when something can be undone), `agent-new-conversation`, `agent-conversation-list`, `agent-conversation-<conversationId>`, `agent-conversation-rename-<conversationId>`, `agent-conversation-delete-<conversationId>`, `agent-message-<seq>` (attribute `data-role` `user`/`assistant`), `agent-streaming` (while text streams), `agent-plan`, `agent-plan-item-<index>` (attribute `data-status`), `agent-step-<seq>` (attributes `data-tool`, `data-status`, `data-risk`), `agent-step-progress-<seq>` (`aria-valuenow` 0-100), `agent-step-error-<seq>`, `agent-approval-card` (attributes `data-tool`, `data-risk`, `data-reasons` comma-separated), `agent-approval-arguments`, `agent-approve`, `agent-approve-plan` (only when `allowPlanScope`), `agent-reject`, `agent-run-error` (with `agent-switch-to-local` when the failed backend is Claude), `agent-usage` (Claude, Advanced+), `agent-tool-log` (Expert) |
| Settings (`/settings`, 004 page or a new page if 004 is absent) | `settings-agent-backend` (radio group: `settings-agent-backend-ollama`, `-claude`, `-none`), `settings-agent-ollama-status`, `settings-agent-ollama-model`, `settings-agent-ollama-fallback-model`, `settings-agent-claude-status` (attribute `data-key-configured`), `settings-agent-claude-model`, `settings-agent-claude-effort`, `settings-agent-claude-notice`, `settings-agent-claude-notice-ack`, `settings-agent-test`, `settings-agent-test-result`, `settings-agent-save`, `settings-agent-error` |

## 7. Compose / runtime (devops, not a contract document)

- `api` service: `extra_hosts: ["host.docker.internal:host-gateway"]`,
  `StemStudio__Agent__Ollama__BaseUrl: ${STEMSTUDIO_OLLAMA_URL:-http://host.docker.internal:11434}`,
  `StemStudio__Agent__Backend: ${STEMSTUDIO_AGENT_BACKEND:-ollama}`, and the bare list entry
  `- ANTHROPIC_API_KEY` (passes the host's variable through when set; no value in any file).
- E2E fake stack: `STEMSTUDIO_AGENT_BACKEND=fake`, `STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/<name>.json`
  (mounted from `e2e/agent-scripts/`).
- CI: no `ANTHROPIC_API_KEY`; a test asserts that no request reaches `api.anthropic.com`.
