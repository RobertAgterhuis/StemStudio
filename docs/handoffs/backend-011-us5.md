# Hand-off: backend / 011 US5 (T070-T071)

Branch `011-lane-backend`. Previous: `backend-011-us4.md`.

## What was built

- `POST /api/agent/conversations/{id}/undo` (`undoLastAgentAction`), `AgentUndoService`: 404 for an unknown conversation;
  409 `run-active` when any run is active (checked by the backend, the agent library is not called); otherwise
  `IAgentRunService.UndoAsync` plans the undo run (`AgentConflictException.ProblemCode` -> 409 with that problem type, e.g.
  `nothing-to-undo`; `KeyNotFoundException` -> 404). The conversation is re-read, its `activeRunId` set to the undo run and
  the run queued on `AgentRunExecutor`; 202 `AgentRunAcceptedDto`. The backend publishes no events for it (the runner does).
- `PATCH /api/agent/conversations/{id}` (`renameConversation`): title trimmed, 1-120 characters else 422; `updatedAt` = now;
  200 `ConversationSummaryDto` with the last run status.
- `DELETE /api/agent/conversations/{id}` (`deleteConversation`): 409 `run-active` while a run of the conversation is not
  terminal; otherwise the conversation with its messages, runs, steps, approvals, events and undo entries is removed (204).
- History after a reload (new `ApiFactory` on the same data directory) restores messages, runs, plan and steps.
- `PendingAgentOperations` is **empty**: every 011 operation of backend-api 1.10.0 is served.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T070 -> T071 | `3934a57` | `272be07` |

Red verified: 11 failures (routes missing -> 404/405, served-operation list). `Undo_UnknownConversation_Is404`,
`Undo_ConversationUnknownToTheAgent_Is404` and `Reload_RestoresMessagesPlansAndSteps` pass in red as well (unmapped route
is 404; reload is existing behaviour) - they guard the green behaviour.

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: **1419 passed**; StemStudio.Api **98.81 % line,
91.55 % branch**.

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; new units <= 4 parameters.

## Dependencies added

None.

## Docs impact

User manual: conversations can be renamed and deleted (not while a command runs); "Undo" reverts the newest agent action.

## Open issues / requests

- ai-agent: `IAgentRunService.UndoAsync` must create the undo run in `IRunStore` (status `queued`) before it returns the
  id; the backend only marks it active and queues it.

## Next owner

backend (US6: T078).
