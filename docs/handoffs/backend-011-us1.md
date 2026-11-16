# Hand-off: backend / 011 US1 (T032-T035)

Branch `011-lane-backend`. Previous checkpoint: `docs/handoffs/backend-011-foundational.md`.

## What was built

- **Agent endpoints (T032 -> T033)**, `src/StemStudio.Api/Agent/`: `listAgentTools`, `listConversations`,
  `createConversation`, `getConversation`, `sendAgentMessage`, `getAgentRun`, `streamAgentRunEvents`,
  `submitAgentUiResult`, `cancelAgentRun` (operation ids un-staged from `PendingAgentOperations`; still staged:
  status, settings x3, rename, delete, undo, approvals).
  - `AgentMessageService`: 404 / 422 `validation` (text 1-4,000 non-blank, page 1-40) / 409 `run-active` (one active run
    globally, also on the store's race) -> stores the user message (Seq = count + 1, `RunId` set), creates the queued run from
    the settings (`AgentRunFactory`: backend + model of that backend), sets the conversation's `ActiveRunId`, names an
    untitled conversation after the first 60 characters, queues the run and answers 202.
  - `AgentRunExecutor` (hosted, one run at a time): resolves `IAgentRunService` per run in a scope; an exception fails the run
    with `internal` and a fixed readable message (never the exception text) plus `run.failed`; `AgentRunLifecycle` clears
    `ActiveRunId` once the run is terminal (also after cancel).
  - `SseWriter`: `text/event-stream`, replay after `after` (query wins) or `Last-Event-ID` (400 when not a whole number),
    live events through `RunEventHub` subscriptions (every subscriber gets every event), `: keep-alive` every 15 s on the
    injected `TimeProvider`, end after the terminal event (or immediately when the run is terminal and no terminal event
    exists).
  - `GET runs/{id}`: snapshot with steps and the pending approval card (payload of the newest `approval.requested` event of a
    step whose approval is still pending).
  - ui-results: 400 (error > 500 chars, result > 4 KB), 404 unknown run, `IUiToolGateway.TryComplete` -> 204 / 409
    `ui-result-not-expected`. cancel: 404, `IAgentRunService.CancelAsync`, `AgentConflictException` -> 409 with its code.
  - Agent-library interfaces are bound with `[FromServices]`, so the API starts even while `AddStemStudioAgent()` registers
    nothing yet.
- **LoopbackActions (T034 -> T035)**, `src/StemStudio.Api/Agent/Loopback/`: one class per port group (tracks, mixes,
  artists, lyrics, assets, styles, generations, songs, voices) behind `LoopbackActions : IStemStudioActions`, all on one typed
  `LoopbackClient` (`AddHttpClient`, base URL `StemStudio__Agent__LoopbackBaseUrl`, else `http://127.0.0.1:{HTTP_PORTS}`,
  else `:8080`). Songs return `LyricsSheetSummary` (no text), voices `VoiceProfileSummary` (no person name, no sample URL;
  the port has no create/delete). Problems -> `AgentActionException`: status, code (`https://stemstudio.local/problems/<code>`
  suffix, bare codes such as `imitation`, else not-found/conflict/validation/invalid-request), message = detail/title + field
  errors, URLs replaced, at most 500 chars; 5xx and unreachable API -> fixed generic messages. No DbContext anywhere in the
  loopback code (it only has an `HttpClient`).

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T032 -> T033 | `98ddf71` | `4f54491` |
| T034 -> T035 | `434cf20` | `28056c9` |
| SIG fix (`RunEventLog.AppendAsync` 5 -> 2 parameters) | - | see the commit of this hand-off |

## Tests and coverage

- `dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: **1352 passed**; StemStudio.Api **98.84 % line,
  92.03 % branch**; Contracts 100 / 100. Agent tests: 83 (stores 13, endpoints 29, loopback 41 incl. 31 endpoint rows run
  against the real in-memory API through a recording handler).
- `dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true`: **99 passed**; 98.64 % line, 95 % branch.

## Quality checks

Build 0 warnings (TreatWarningsAsErrors); `dotnet format --verify-no-changes` clean. `sig_check.py` on
`src/StemStudio.Api/Agent`, `src/StemStudio.Persistence/Agent`, `AgentConfiguration.cs`: 0 errors after the fix,
**24 warnings** (unit size 16-26 lines in DTO/row mappers, `AddAgentHost`, `SseWriter.LiveAsync`, `LoopbackClient.SendRawAsync`;
complexity 6-7 in `AgentMessageService.Validate`, `CancelAsync`, `LoopbackClient.Readable`, `SseWriter.LiveAsync`). These are
scheduled for T079c (quality-gate asked for no new warnings; I will bring them to 0 there).

## Dependencies added

None.

## Docs impact

None user-visible yet (API only). Setup: `StemStudio__Agent__LoopbackBaseUrl` (optional; default from `HTTP_PORTS`).

## Open issues / requests

- ai-agent: `IUiToolGateway`, `IAgentRunService` and `IAgentStatusService` must be registered by `AddStemStudioAgent()`;
  the backend registers `IStemStudioActions`, the four stores, `IRunEventSink` (+ live broadcast) and will register
  `IGpuActivity` (T053). The runner should publish `approval.requested` with the full `ApprovalRequestDto` (the snapshot reads
  the card from it) and `run.*` terminal events (streams end on them).
- ai-agent: the assistant message is expected to be appended by the runner through `IConversationStore.AppendMessageAsync`
  (Seq = last + 1).

## Next owner

backend (US2: T045/T046).
