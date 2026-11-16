# Hand-off: ai-agent / 011 US5 (T068, T069)

Branch `011-lane-agent`. No LLM called; no key anywhere. Builds on `ai-agent-011-us4.md`.

## What was built

- **Recording** (`StepExecutor` + `UndoRecorder`): every succeeded step of a tool with undo kind `inverse`/`compensate`
  records its undo calls as one `UndoEntry` each (group = run; `Fresh` from the tool result). Server tools give their call
  in `ToolResult.Undo`; mixer UI tools get theirs from the page's answer (`Tools/UiUndo`: `mixer_set_channel`,
  `mixer_reset` (one entry per channel), `mixer_set_master`, `set_loop` (no previous loop -> `enabled: false`),
  `set_metronome`; values clamped to the argument ranges). Read, failed and rejected steps record nothing; undo runs record
  nothing (a second Undo takes the next older group).
- **Mix guards** (`Tools/UndoGuard`, reserved argument `$expect` stripped before execution): `save_mix_as`/`duplicate_mix`
  (fresh `delete_mix`), `rename_mix` and `update_mix` store the mix's `updatedAt` after the step; at undo time a different
  `updatedAt` (tolerance 1 s for storage rounding) -> entry `obsolete` ("it was changed outside the agent since"), 404 ->
  `obsolete` ("it no longer exists"). `update_mix` is undone by the undo-only tool `restore_mix` (writes the previous version;
  never offered to the model, not in the registry).
- **Undo run** (`UndoPlanner`, `UndoRun`): `IAgentRunService.UndoAsync(conversationId)` -> 404 unknown conversation, 409
  `run-active` (conversation has an active run), 409 `nothing-to-undo`; otherwise it appends the synthetic user message
  `Undo` (page context of the last user command) and **stores the queued run (`Model = "undo"`) before returning its id**.
  Executing it publishes `run.started` (model `undo`), applies the newest group in reverse step order through the normal
  step executor (step events, policy, approval cards) with **no model turn**, trusted arguments from the store; fresh
  compensations run without approval, other compensations get `undo-compensation` (+ risk) and wait for approval. Applied
  entries -> `applied` and the original step `undone`; obsolete targets, failed or rejected undo calls -> `obsolete`. The
  answer is appended as the assistant message: `Undone: N step(s).` plus `Not undone: tool (reason); ...`; terminal
  `run.completed` as usual.

## Files changed

`src/StemStudio.Agent/Runtime/{UndoPlanner,UndoRun,StepExecutor,RunState,AgentRunner,AgentRunService}.cs`,
`src/StemStudio.Agent/Tools/{UiUndo,UndoGuard,ToolInvocation}.cs`,
`src/StemStudio.Agent/Tools/Providers/{Core001Handlers,ToolDefinitions,UndoTools}.cs`;
tests `Runtime/UndoPlannerTests.cs` (new), `Tools/Providers/Core001ToolProviderTests.cs` (guarded undo arguments).

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T068 / T069 | 9c0aee3 | ec7f57e |

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **493 passed**, 0 failed.
`StemStudio.Agent`: **line 97.89 %, branch 92.64 %, method 97.62 %** (gate 90/90 passed).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean. `sig_check src/StemStudio.Agent`: 116 files, 401 units,
**0 errors, 36 warnings** (3 new complexity/size warnings in `StepExecutor.RunUiToolAsync`, `UndoRun.ApplyAsync`,
`UndoRun.TargetChangedAsync`; T079b).

## Dependencies added

None.

## Docs impact

User manual: Undo reverses the last command's changes (mixer values, a new mix, renamed/updated mixes, started jobs);
deleting imported tracks and other non-fresh compensations ask for confirmation; changes made by hand since are not undone
and the answer says so.

## Open issues / requests

1. **frontend (blocking for save/update via the agent, us5-save-undo)**: `mixer_get_state` must include
   `settings` = `MixerState.ToSettings("")` (`MixSettingsDto`, web JSON). The agent saves/updates mixes from it; the flat
   catalogue projection (channels in dB, visible channels only) cannot rebuild a mix losslessly (detail channels,
   `expanded`). Without it `save_mix_as`/`update_mix` fail with "The mixer did not report its settings."
2. **qa (us5 request 1)**: confirmed, undo runs never request a model turn; scripts need no undo turn.
   **qa (us5 request 3)**: three parallel `mixer_set_channel` calls of one turn are one group (one run).
3. **architect**: `UndoEntry` has no field for the target version; the guard travels as the reserved argument `$expect`
   inside `Action.Arguments`. A dedicated `UndoEntry.ExpectedUpdatedAt` would be cleaner (agent change is local).
4. Known limitation: undoing an older group whose mix the agent itself changed in a newer, already undone group sees a new
   `updatedAt` and reports "changed outside the agent".

## Next owner

ai-agent continues with US6 (T075, T076, T077, T077b).
