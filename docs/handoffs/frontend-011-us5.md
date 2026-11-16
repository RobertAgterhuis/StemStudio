# Hand-off: frontend / 011 US5 (T072, T073)

Branch `011-lane-frontend`. Contracts: backend API 1.10.0 (`undoLastAgentAction`, `renameConversation`,
`deleteConversation`, `getConversation`, `getAgentRun`, `streamAgentRunEvents`), ui-test-ids 1.10.0.

## What was built

- `agent-undo`: enabled when the open conversation has a finished run (from its run summaries or the run just
  shown), no run is active and the API has not answered `409` (nothing-to-undo / run-active) since; Undo follows the
  undo run's stream like any run. A `409` shows the problem and disables Undo until the next command.
- Rename / delete per entry (Advanced+, feature `agent.conversation-actions`): `agent-conversation-rename-<id>` turns
  the entry into an input (`aria-label="Conversation title"`, max 120; Enter or "Save title" saves, Escape cancels);
  `agent-conversation-delete-<id>` asks for confirmation (`IConfirmService`), is disabled while its run is active
  (open conversation's run, or `lastRunStatus` not terminal), and clears the view when the open one is deleted.
- History restore: opening the panel without an open conversation opens the newest one; opening a conversation shows
  its messages and restores its last run - a finished run from the snapshot (plan, steps, failure, pending approval),
  a running run by replaying its stream from the first event (the snapshot has no streamed text). Replayed
  `ui.requested` events of steps the snapshot no longer has in `awaiting_ui` are not executed again; new ones are.

## Files changed

New: `src/StemStudio.Web/Agent/AgentPanelState.History.cs`, `tests/StemStudio.Web.Tests/Components/Agent/AgentHistoryTests.cs`.
Modified: `Agent/AgentPanelState{,.Conversations}.cs`, `Components/Agent/{AgentPanel.razor(.cs),ConversationList.razor}`,
`tests/.../Components/Agent/AgentPanelTests.cs` (entry selector excludes the new action buttons), tasks.md (T072, T073).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: Passed 1682, Failed 0.
`StemStudio.Web` line **99.39 %**, branch **94.02 %**. Agent code: line 2074/2086 (99.4 %), branch 1012/1108 (91.3 %).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean.

## Dependencies added

None.

## Docs impact

User-visible: Undo in the agent panel; conversations can be renamed and deleted (Advanced+); reopening the panel or
reloading the page shows the last conversation and a running run continues live.

## Open issues / requests

- backend: resume relies on `GET .../events` without `after` replaying every stored event (DELTA.md section 3).
- Interrupted runs show their state; the "Continue" button of the spec edge case (new run with the same context) is
  not built (no contract operation; the user can resend the command).

## Next owner

frontend (polish: T079d).
