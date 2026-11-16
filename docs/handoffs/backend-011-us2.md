# Hand-off: backend / 011 US2 (T045-T046)

## What was built

`POST /api/agent/runs/{runId}/approvals/{stepId}` (`decideAgentApproval`, un-staged): 400 when the body is missing,
malformed, has an unknown decision or an `argumentsHash` that is not 64 lower-case hex characters; 404 when the run has no
approval for the step; otherwise `IAgentRunService.DecideAsync` decides (hash re-check, first decision wins, idempotent
answer) -> 200 `ApprovalState`; `AgentConflictException` -> 409 with its code (`approval-not-pending`,
`arguments-changed`); the conversation is released when the decision ended the run. `approval.resolved` reaches every
open stream (proven with two SSE clients on the event hub).

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T045 -> T046 | `4450cdb` | `543ab52` |

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: **1364 passed**; StemStudio.Api **98.85 % line,
92.03 % branch**; Contracts 100 / 100. New: `Agent/ApprovalEndpointTests.cs` (12 cases).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; the new service has no SIG error (4 parameters max).

## Dependencies added

None.

## Docs impact

None.

## Open issues / requests

- ai-agent: `DecideAsync` must publish `approval.resolved` through `IRunEventSink` (the backend does not publish it, so
  there is exactly one event per decision).

## Next owner

backend (US3: T052-T055).
