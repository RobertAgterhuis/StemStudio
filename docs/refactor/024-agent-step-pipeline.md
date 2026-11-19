# 024 Split the agent StepExecutor into explicit pipeline stages
- Status: proposed · Date: 2026-11-19 · Area: Agent / Runtime (StemStudio.Agent/Runtime/StepExecutor.cs) · Priority: P3

## Problem statement
`src/StemStudio.Agent/Runtime/StepExecutor.cs` is the largest backend source file (425 lines; `wc -l` over the backend projects,
migrations excluded). It holds the whole security-critical path of an agent tool call: argument validation, preconditions, heavy-job
escalation, policy decision, approval, execution of server and UI tools, taint marking, undo recording and error projection to the
model. That is about 30 private members (lines 33-342), plus four helper types in the same file: `UiCall`, `PageTransitions`,
`UiToolFailedException` and `StepFailure` (lines 348-425).

Evidence:
- It has the most churn after the composition root: 17 commits. 5 of them are security fixes (`git log --grep=^fix`):
  `a006b00a` (every run ends exactly once), `49d8b8bc` (job and page error text only as untrusted text), `8373074a` (heavy
  escalation fails closed), `ad29d204` (API error messages as untrusted text), and `4f2fea90` (precondition hook for voice
  conversion).
- The stages are implicit in the call chain (`ExecuteAsync`, `DecideAndRunAsync`, `AskThenRunAsync`, `RunAsync`, `SucceedAsync` /
  `FailStepAsync`), so the order of the security checks is only visible by reading the whole file.
- Unit metrics are within limits (lizard 1.26.1: 42 units, the largest 15 NLOC, highest CCN 5). This is a cohesion and readability problem, not a
  SIG hard-limit breach.

## Impact
- Each agent security fix so far has landed in this file. Changes are hard to review, and the "fails closed" behaviour depends on
  the order of try/catch blocks spread across several methods.
- New tool kinds (lyric assist, practice, recording tools) add more branches here.

## Risk
- Leaving it: moderate review risk on the most security-sensitive component. Today it is well covered by `tests/StemStudio.Agent.Tests/Runtime`
  (ApprovalFlow, Escalation, ExternalText, Injection, RunnerRobustness and RuntimeEdge tests).
- The change: medium. The stage order is the security contract. Any reorder must be caught by tests, and there must be no change in
  the events sent to the UI (`RunEvents`). Rollback is a revert per stage.

## Effort
M (2-3 days).

## Step-by-step plan
1. Safety net: add an ordering test that records the stage sequence for one call of each kind (rejected-invalid, precondition-refused,
   escalated-heavy, approved, rejected, expired, UI-tool timeout, tool error). Assert the persisted `RunStep` states and the
   `RunEvents` sequence, so that both are pinned.
2. Move the helper types (`StepFailure`, `PageTransitions`, `UiCall`, `UiToolFailedException`) into their own files. No behaviour
   change.
3. Extract `StepAdmission` (count, validate, precondition, escalate, policy) that returns a `PolicyDecision`, and `StepRunner`
   (invoke server or UI tool, taint, undo, project result). `StepExecutor` then becomes a short orchestrator that reads like the
   security order. Check: step-1 tests unchanged.
4. Document the stage order in the type comment, linking ADR-020 and the security fixes above.
5. Verify: each new type under 150 lines, the Agent.Tests and AgentEval.Tests green, and the agent-eval scripts with unchanged
   transcripts. Roll back with a revert.

## Dependencies and related proposals
None blocking. Do it before the next round that adds agent tools.
