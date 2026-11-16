# Hand-off: frontend / 011 US3 (T056, T057)

Branch `011-lane-frontend`. Contracts: backend API 1.10.0 (`getAgentStatus`, `503 backend-unavailable` with
`agentStatus`), ui-test-ids 1.10.0.

## What was built

- `agent-unavailable` (`role="status"`) in the panel while `AgentStatus.ready` is false: the status' `reason`
  (or "The agent is not ready.") and `fix`; input and Send are disabled. Every other page keeps working - the panel
  is the only place that calls the agent API.
- The open panel refreshes `GET /api/agent/status` every 10 s (`AgentPanel.StatusInterval`, `TimeProvider`
  delay, stopped when the panel closes); a failed refresh keeps the last status.
- A `503 backend-unavailable` on send replaces the status with the problem's `agentStatus` extension, so the
  unavailable state appears at once (FR-023).
- Badge: `data-backend` `none` and empty `data-model` for the agent-off state; `data-fallback` from the status before
  a run starts and from `run.started`/`model.changed` during a run (US1).

## Files changed

Modified: `src/StemStudio.Web/Agent/AgentPanelState.cs` (`RefreshStatusAsync`, status from the 503 problem),
`src/StemStudio.Web/Components/Agent/AgentPanel.razor(.cs)`, `wwwroot/css/components/agent.css`.
New: `tests/StemStudio.Web.Tests/Components/Agent/AgentUnavailableTests.cs`. `ApprovalCardTests` now wait for the
decision call (was timing-sensitive under a parallel run). tasks.md (T056, T057).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: Passed 1642, Failed 0 (3 consecutive runs green).
`StemStudio.Web` line **99.42 %**, branch **94.27 %**. Agent code: line 1704/1710 (99.6 %), branch 824/892 (92.4 %).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean.

## Dependencies added

None.

## Docs impact

User-visible: when the agent backend is not ready the panel shows why and how to fix it (e.g. "Start Ollama",
`ollama pull qwen3.5:9b (6.6 GB)`) and disables the input; it checks again every 10 s while open.

## Open issues / requests

None.

## Next owner

frontend (US4: T062, T063, T066).
