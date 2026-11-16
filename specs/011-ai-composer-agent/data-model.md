# Data Model: AI Composer Agent (feature 011)

Persistence: the API's SQLite database (EF Core, migration `AddAgent`), owned by the backend
agent; the entity rules below live in `StemStudio.Agent` (domain of the agent, owned by the
ai-agent) as plain records with no EF dependency. The backend maps them in
`StemStudio.Persistence` through the `IConversationStore` / `IAgentSettingsStore` ports.

## 1. Configuration (environment, never stored)

| Variable | Default | Meaning |
|---|---|---|
| `ANTHROPIC_API_KEY` | unset | Claude key; presence only is reported (`keyConfigured`). Read once per backend creation from the process environment; never logged, never persisted, never returned. |
| `StemStudio__Agent__Backend` | `ollama` | initial backend when no settings row exists (`ollama`, `claude`, `none`, `fake`) |
| `StemStudio__Agent__Ollama__BaseUrl` | `http://localhost:11434` (compose: `http://host.docker.internal:11434`) | Ollama endpoint; must be `http`/`https`, no credentials in the URL |
| `StemStudio__Agent__LoopbackBaseUrl` | `http://127.0.0.1:${ASPNETCORE_HTTP_PORTS}` | base URL the server tools use to call the API's own public endpoints |
| `STEMSTUDIO_AGENT_BACKEND=fake` + `STEMSTUDIO_AGENT_FAKE_SCRIPT=<path>` | unset | test/E2E only: scripted chat client (file under `e2e/agent-scripts/`) |

## 2. Agent settings (one row, `AgentSettings`)

| Field | Type | Rules |
|---|---|---|
| `Backend` | enum `ollama` \| `claude` \| `none` | `claude` only accepted when the key is configured **and** `ClaudeNoticeAcknowledgedAt` is set (else 409 `claude-not-configured` / 422 `notice-not-acknowledged`) |
| `OllamaModel` | string <= 100 | default `qwen3.5:9b`; must be in Ollama's model list with capability `tools` when saved (validated by the probe; 422 otherwise) |
| `OllamaFallbackModel` | string <= 100 | default `qwen3.5:4b`; same validation; may equal `OllamaModel` |
| `OllamaThink` | bool | default false |
| `ClaudeModel` | enum `claude-opus-5-5` \| `claude-sonnet-5-5` \| `claude-haiku-5-5` | default `claude-opus-5-5` |
| `ClaudeEffort` | enum `low` \| `medium` \| `high` | default `low` |
| `ClaudeNoticeAcknowledgedAt` | timestamp? | set by `PUT /api/agent/settings` with `acknowledgeClaudeNotice: true` |
| `Limits` | owned value: `MaxModelTurns` (default 12, 1-30), `MaxToolCalls` (20, 1-50), `MaxInvalidCalls` (3), `TurnTimeoutSeconds` (local 120, Claude 180), `ClaudeMaxInputTokensPerRun` (60,000), `ClaudeMaxOutputTokensPerRun` (8,000), `MaxJobWaitMinutes` (45) | ranges enforced (422) |
| `UpdatedAt` | timestamp | |

## 3. Conversation

| Entity | Fields | Rules |
|---|---|---|
| `Conversation` | `Id` (GUID), `Title` (<= 120; default = first 60 chars of the first message), `CreatedAt`, `UpdatedAt`, `ActiveRunId?` | at most one active run per conversation and at most one active run globally (single GPU, single user; 409 `run-active`); max 500 conversations (oldest beyond are not deleted automatically; the list shows a warning) |
| `ConversationMessage` | `Id`, `ConversationId`, `Seq` (int, monotonic), `Role` (`user` \| `assistant`), `Text` (user <= 4,000 chars; assistant <= 16,000), `Context` (owned: `Page` <= 40, `TrackId?`, `MixId?`), `RunId?`, `CreatedAt` | user messages are immutable; the assistant message of a run is built from the streamed text and finalised when the run ends |

## 4. Run, steps, approvals

### 4.1 `AgentRun`

| Field | Type | Rules |
|---|---|---|
| `Id` | GUID | |
| `ConversationId` | GUID | |
| `UserMessageId` | GUID | the command |
| `Status` | `queued` -> `running` -> (`awaiting_approval` \| `awaiting_ui` \| `waiting_job`) <-> `running` -> `completed` \| `failed` \| `cancelled` \| `interrupted` | terminal states are final; `interrupted` set at API start-up for every non-terminal run (pending approvals -> `expired`) |
| `Backend`, `Model` | strings | the model actually used per turn is in the events; `Model` is the first turn's |
| `Tainted` | bool | false at start; true after a tool result with `untrusted_text` (FR-013); never reset within the run |
| `Plan` | owned list of `PlanItem {Index, Title <= 120, Tool?, Status}` | replaced by `set_plan` (not-yet-started items only) |
| `ModelTurns`, `ToolCalls`, `InvalidCalls` | ints | compared with `Limits` |
| `Usage` | owned `{InputTokens, OutputTokens, CachedInputTokens}` | summed from `UsageContent` |
| `FailureCode?`, `FailureMessage?` (<= 500) | | `backend-unavailable`, `limit-reached`, `invalid-calls`, `timeout`, `refusal`, `budget-exhausted`, `internal` |
| `StartedAt`, `EndedAt?` | | |

### 4.2 `RunStep` (one tool call)

| Field | Type | Rules |
|---|---|---|
| `Id` | GUID | also the id of the approval and UI round trip |
| `RunId`, `Seq` | | |
| `CallId` | string <= 100 | the model's `FunctionCallContent.CallId` |
| `Tool` | string | must be a registered tool name; unknown names produce a step with `Status = rejected_invalid` |
| `Risk`, `UndoKind`, `Kind` | enums from the descriptor | copied at creation |
| `Arguments` | JSON (canonical: sorted keys, no whitespace) <= 8 KB | validated against the schema before any decision |
| `ArgumentsHash` | SHA-256 hex of the canonical arguments | approval binding (FR-015) |
| `PlanIndex?` | int | link to a plan item |
| `Decision` | `auto` \| `approval_required` \| `denied` | `denied` for policy denials (guardrails `artist-imitation`, `lyrics-not-from-user`), never executed: the step ends `failed` with the readable refusal as `Error` and the model gets that refusal as the tool result |
| `DecisionReasons` | list of codes: `risk-heavy`, `risk-destructive`, `tainted`, `url-not-from-user`, `lyrics-not-from-user`, `artist-imitation`, `undo-compensation` | shown on the card |
| `Status` | `planned` \| `awaiting_approval` \| `running` \| `awaiting_ui` \| `waiting_job` \| `succeeded` \| `failed` \| `rejected` \| `rejected_invalid` \| `skipped` \| `expired` \| `undone` | |
| `ResultSummary` | JSON projection <= 4 KB | what the model saw |
| `Error?` | string <= 500 | readable |
| `JobRef?` | `{TrackId?, JobId?, Phase}` | for `job`/`heavy` tools and `wait_for_job` |
| `Progress?` | 0-100 | from job polling |
| `StartedAt?`, `EndedAt?` | | |

### 4.3 `Approval`

| Field | Type | Rules |
|---|---|---|
| `Id` | GUID (= `RunStep.Id`) | |
| `Status` | `pending` \| `approved` \| `rejected` \| `expired` | first decision wins; later POSTs return the stored decision (idempotent) |
| `Scope` | `once` \| `plan_heavy` | `plan_heavy` covers later **heavy** steps of the same run whose `Tool` and target id (`trackId`) match a plan item; never destructive steps or compensations |
| `ArgumentsHash` | copy of the step's hash | the executor re-checks it before executing |
| `RequestedAt`, `DecidedAt?` | | pending approvals expire after 24 h or when the run ends |

### 4.4 `UndoEntry`

| Field | Type | Rules |
|---|---|---|
| `Id`, `ConversationId`, `RunId`, `StepId` | | grouped by run (one user command = one undo group) |
| `Kind` | `inverse` \| `compensate` | |
| `Action` | `{Tool, Arguments}` | the inverse/compensating call, e.g. `mixer_set_channel` with the previous values, `update_mix` with the previous version, `delete_mix` for a fresh mix, `cancel_detail_separation` |
| `Fresh` | bool | compensation of an artefact created by this run and not modified since (checked via `updatedAt`): no approval |
| `Status` | `available` \| `applied` \| `obsolete` | `obsolete` when the target changed outside the agent after the step (e.g. mix updated in the UI) or no longer exists |

Undo applies the newest group with `available` entries, in reverse step order, as a new run
(`AgentRun` with `UserMessageId` of a synthetic "Undo" message) so the same policy, events and
log apply.

### 4.5 `RunEvent` (append-only, SSE replay)

| Field | Type | Rules |
|---|---|---|
| `RunId`, `Seq` | composite key, `Seq` from 1 | SSE `id:` = `Seq` |
| `Type` | see contracts/DELTA.md section 3 | |
| `Payload` | JSON <= 16 KB (text deltas are coalesced per 100 ms) | |
| `CreatedAt` | | events of terminal runs older than 30 days may be compacted to the final snapshot (later; YAGNI for now) |

## 5. Tool descriptor (code, not stored)

`AgentToolDescriptor { Name, Feature ("001".."011"), Kind (server|ui|meta), Risk, UndoKind,
Description, ArgumentSchema (JSON Schema), ResultProjection, UntrustedFields, Availability
(Always | Feature(NNN) | Service(name) | Mixer), EstimatedDuration? (for heavy cards) }`.
Registered by one `IAgentToolProvider` per feature (`CoreToolProvider` (001), `DetailToolProvider`
(002), `DeviceToolProvider` (003), `HelpToolProvider` (004), `ArtistToolProvider` (005), `LyricsToolProvider` (006),
`ExportToolProvider` (007), `ScoreToolProvider` (008), `StyleToolProvider` (009: styles, restyles, generations) and
`SongToolProvider` (010: lyrics sheets, songs, own voice)). Each descriptor also carries its `Group` (`core`, `mixer`,
`tracks`, `library`, `create`; tool catalogue 1.0.0 "Selection per request").
`AgentToolRegistry.For(context)` returns the available descriptors, ordered by name (stable for
prompt caching).

## 6. Policy decision (pure function)

```text
Decide(descriptor, args, run, conversation) ->
  if guardrail violated (artist imitation in free text of create_style / generate_song;
     lyrics text of save_lyrics_sheet / generate_song not in a user message)       -> denied
  reasons = []
  if risk in {heavy, destructive}                                    -> reasons += risk-*
  if risk in {change, job} and run.Tainted                           -> reasons += tainted
  if descriptor has url argument and url not literally in a user message of the conversation
                                                                     -> reasons += url-not-from-user
  if plan_heavy approval in run covers (tool, target) and reasons == [risk-heavy] -> auto
  reasons empty -> auto, else approval_required(reasons)
```

Read and meta tools are always `auto` (they have no url/lyrics arguments).

## 7. State transitions (run)

```text
queued -> running
running -> awaiting_approval (step needs approval) -> running (approved/rejected) | cancelled | interrupted
running -> awaiting_ui (UI tool sent) -> running (result or 15 s timeout -> step failed)
running -> waiting_job (wait_for_job) -> running (job terminal) | cancelled
running -> completed (model ends without calls) | failed (limits, backend, refusal, budget)
any non-terminal -> cancelled (POST cancel) | interrupted (API start-up)
```
