# Hand-off: frontend / 011 US2 (T047, T048)

Branch `011-lane-frontend`. Contracts: backend API 1.10.0 (`decideAgentApproval`), ui-test-ids 1.10.0.

## What was built

- `Components/Agent/ApprovalCard.razor`, shown in the panel while `AgentRunView.Approval` is set (from
  `approval.requested` or the run snapshot's `pendingApproval`): `agent-approval-card` (`data-tool`, `data-risk`,
  `data-reasons` comma-separated, `data-help-id="agent-approvals"`), `agent-approval-effect`, readable reasons
  (tainted: "Proposed after reading external content ..."), "Can be undone"/"Cannot be undone",
  `agent-approval-estimate` for `heavy` only (`data-minutes` invariant, `data-device` `gpu`/`cpu`/empty, text
  "About 7 minutes on the graphics card" / "Duration unknown"), `agent-approval-arguments` (exact arguments,
  indented JSON), `agent-approve` (scope `once`), `agent-approve-plan` (only when `allowPlanScope`, scope
  `plan_heavy`), `agent-reject` (no scope). Every decision carries the `argumentsHash`. Buttons are disabled while
  the decision is posted; Approve never auto-focuses.
- `AgentPanelState.DecideAsync`: the card stays until `approval.resolved` (so all tabs close it together); a `409`
  (approval-not-pending / arguments-changed) closes the card and shows the problem in `agent-error`.
- `Agent/ApprovalText.cs`: reason texts, estimate text, device and minutes attributes.
- Help topic `agent-approvals` now anchors on the card.

## Files changed

New: `src/StemStudio.Web/Agent/ApprovalText.cs`, `src/StemStudio.Web/Components/Agent/ApprovalCard.razor`,
`tests/StemStudio.Web.Tests/Components/Agent/ApprovalCardTests.cs`.
Modified: `Agent/AgentPanelState.cs`, `Components/Agent/AgentPanel.razor`, `wwwroot/css/components/agent.css`,
`Help/topics/start/agent-approvals.md`, tasks.md (T047, T048).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: Passed 1634, Failed 0.
`StemStudio.Web` line **99.42 %**, branch **94.31 %**. Agent code: line 1630/1636 (99.6 %), branch 804/868 (92.6 %).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean.

## Dependencies added

None.

## Docs impact

User-visible: confirmation card for heavy/destructive (and tainted change/job) steps with effect, arguments, reasons,
undo, estimate; Approve / Approve all heavy steps of this plan / Reject.

## Open issues / requests

- backend: `approval.requested.device` - the card maps `cpu` to `cpu` and any other value (`cuda`, `gpu`) to `gpu`.

## Next owner

frontend (US3: T056, T057).
