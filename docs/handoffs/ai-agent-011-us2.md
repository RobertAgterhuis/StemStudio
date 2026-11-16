# Hand-off: ai-agent / 011 US2 (T041-T044)

Branch `011-lane-agent`. No LLM called; no key anywhere. Builds on `ai-agent-011-us1.md`.

## What was built

- **`AgentRunService : IAgentRunService`** (`Runtime/AgentRunService.cs`, one instance per process):
  - `ExecuteAsync` runs the `AgentRunner`.
  - `DecideAsync(runId, stepId, decision)`: unknown step or other run -> `KeyNotFoundException` (404); an approved or
    rejected approval returns the stored state (idempotent, first decision wins); expired / no longer awaited (run ended,
    API restarted) -> `AgentConflictException(approval-not-pending)`; hash different from the card ->
    `AgentConflictException(arguments-changed)` and the approval stays pending. A `plan_heavy` scope is stored only when
    the card offered it (`allowPlanScope`), otherwise it is downgraded to `once`. Publishes `approval.resolved`.
  - `CancelAsync(runId, cancelJobs)`: unknown -> `KeyNotFoundException`; terminal -> `AgentConflictException(run-ended)`;
    executing -> cancels the run's token (the runner ends it `cancelled`, pending approvals and their steps `expired`);
    queued/not executing here -> ended `cancelled` directly (conversation released, `run.cancelled`). With
    `cancelJobs: true` the jobs the run started are cancelled (detail separation, restyles/generations; best effort) and
    listed in `run.cancelled.cancelledJobs` as `phase:id`.
  - `UndoAsync`: placeholder that answers `nothing-to-undo` until US5 (T068/T069).
- **`ApprovalGate`** (in-memory wait, `RunCoordination.ApprovalWaiter`): the step stays `awaiting_approval` (persisted),
  the approval is saved `pending`, the run goes `awaiting_approval`, the card (`approval.requested`) carries effect,
  arguments, hash, reasons, `undoable`, the heavy estimate (`estimatedMinutes`, `device` from the tool's `Estimator`) and
  `allowPlanScope` (heavy + tool in the plan + one string target). After the decision the hash binding is re-checked
  (`ApprovalBinding.Matches`); a `plan_heavy` grant is bound to the tool and its single target (foundational security
  fix unchanged); reject -> step `rejected`, plan item `skipped`, model gets "The user rejected this step."; no decision
  within 24 h (`TimeProvider`) -> approval and step `expired`, plan item `skipped`.
- **`AdversarialChatClient`** (`Backends/Fakes`, public for the E2E stack): probe call first, the attack calls once a tool
  result contains `untrusted_text`.

## Injection evidence (SC-002)

`Runtime/InjectionTests`: for every untrusted source of the delivered providers (`search_library`, `get_track` incl. its
error, `list_mixes`, `get_mix`, `wait_for_job` error) the adversarial model's `delete_track`, `import_from_url(evil)` and
`update_mix` all become approval cards with reason `tainted` (`url-not-from-user` for the URL), the harness rejects them,
and the ports `DeleteTrackAsync`, `ImportFromUrlAsync`, `UpdateMixAsync` receive **0** calls. Eval cases 22-24 are
scripted (24: the user typed the URL, the API's 400 fails the step). `mixer_get_state` is not an untrusted source by
design: its projection whitelists numeric/boolean mixer fields and never carries titles or names. Sources of 005-010 are
added to this test with their providers (US6).

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T041 / T042 | 5673613 | ead1a34 (+ ec9dded import order) |
| T043 / T044 | 936ec62 | ea5ddbb |

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **400 passed**, 0 failed.
`StemStudio.Agent`: **line 98.31 %, branch 94.3 %, method 97.63 %** (gate 90/90 passed).

## Quality checks

`dotnet format --verify-no-changes`: clean. Build: 0 warnings. `sig_check src/StemStudio.Agent`: 96 files, 302 units,
**0 errors, 33 warnings** (T079b).

## Dependencies added

None.

## Docs impact

None.

## Open issues / requests

1. **backend**: map `KeyNotFoundException` from `DecideAsync`/`CancelAsync` to 404 and `AgentConflictException.ProblemCode`
   to 409 problem types. Register **one** `AgentRunService` (singleton): the approval wait and the run cancellation live
   in its memory; DI registration in `AddStemStudioAgent()` follows in US3 with the chat session/model selector.
2. **frontend** (note received): a UI tool answer `{ok: false, error: "The mixer of the track is not open"}` is a normal
   failed step (panel shows the page text; the model gets "The page could not execute the step." plus the text as
   `untrusted_text`). `show_karaoke` is not used by any agent test beyond fakes.
3. Data-model 4.3 wording "marks dependent plan items skipped": implemented as the rejected step's own plan item; later
   items stay pending so the model can replan (architect may refine).

## Next owner

ai-agent continues with US3 (T050, T051).
