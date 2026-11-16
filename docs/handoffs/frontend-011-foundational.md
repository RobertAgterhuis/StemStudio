# Hand-off: frontend / 011 foundational (T022, T023)

Branch `011-lane-frontend` (from `011-composer-agent`). Contract: backend API 1.10.0
(`specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml`), `StemStudio.Contracts/Agent` DTOs.

## What was built

- `IAgentApiClient` / `AgentApiClient`: all 17 `/api/agent/*` operations of DELTA.md section 1 as a typed
  client (own `AddHttpClient` registration in `Program.cs`, same `ApiBaseUrl`). JSON = Web defaults with
  nulls omitted (all optional request members of 1.10.0 are nullable/optional).
- `StreamRunEventsAsync(runId, after?)`: `Accept: text/event-stream`, `ResponseHeadersRead`, parsed by
  `SseReader` (WHATWG rules: comments/keep-alive ignored, multi-line data, CRLF, id carried over, unterminated
  last event dropped). Ends after the terminal event. A dropped connection (IOException), a stream that ends
  without a terminal event or an unreachable API reconnects with `Last-Event-ID: <last id>` (or the `after`
  value when nothing arrived yet), 1 s apart; after `MaxReconnects` (3) consecutive failures it throws
  `ApiException(ApiError.Unreachable)`. 4xx/5xx with a problem body are not retried.
- `AgentEvent(Id, Type, Data)` with `Read<T>()` and `IsTerminal`.
- `ApiTransport` (internal): shared send/problem/unreachable plumbing, now also used by `StemStudioApiClient`
  (behaviour unchanged, all 1,471 existing tests green).
- `ApiError` gained `Type`, `Code` (last segment of the problem type, e.g. `run-active`) and `Extensions`
  (problem extension members); `AgentProblems.StatusOf(error)` reads `agentStatus` of `503 backend-unavailable`.

## Files changed

- New: `src/StemStudio.Web/Agent/{IAgentApiClient,AgentApiClient,AgentEvent,AgentProblems,SseReader}.cs`,
  `src/StemStudio.Web/Api/ApiTransport.cs`, `tests/StemStudio.Web.Tests/Agent/{AgentApiClientTests,SseReaderTests}.cs`,
  `tests/StemStudio.Web.Tests/Agent/Fixtures/*.sse` (5 recorded streams), `tests/StemStudio.Web.Tests/Fakes/{AgentData,SseHttpHandler}.cs`.
- Modified: `src/StemStudio.Web/Api/{ApiError,ProblemReader,StemStudioApiClient}.cs`, `src/StemStudio.Web/Program.cs`,
  `tests/StemStudio.Web.Tests/StemStudio.Web.Tests.csproj` (fixtures copied to output), `specs/011-ai-composer-agent/tasks.md` (T022, T023 ticked).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`:
Passed 1497, Failed 0. `StemStudio.Web` line **99.47 %**, branch **94.64 %** (gate 90/90 passed).
New code: `Agent/` line 316/316, branch 128/132; `Api/` line 500/500, branch 118/124.

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean for Web and Web.Tests.
`tools/quality/sig_check.py src/StemStudio.Web/Agent src/StemStudio.Web/Api`: 0 errors, 3 unit-size warnings
(16 LOC, limit 15, hard max 30): `ConnectAsync`, `SseReader.ReadAsync`, `PendingEvent.Set`.

## Dependencies added

None.

## Docs impact

None (no user-visible change yet).

## Open issues / requests

- backend: the event stream should flush headers immediately (the client reads with `ResponseHeadersRead`)
  and honour `Last-Event-ID` exactly as `after` (DELTA.md section 3) - the client relies on it for resume.

## Next owner

frontend (US1: T036-T039).
