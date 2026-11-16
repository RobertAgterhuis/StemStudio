# Hand-off: ai-agent / 011 polish (T079a, T079b)

Branch `011-lane-agent`. No LLM called and no API key used anywhere. Builds on `ai-agent-011-us6.md`. T079a and T079b are
done (ticked in tasks.md). See "Switch-over at integration" for the temporary workarounds.

## What was built

- **Placeholders, whole-value only** (qa us1 request 1): `ScriptedChatSession` replaces `{{context.trackId}}` /
  `{{context.mixId}}` only when a call argument string (also inside argument lists and records) is *exactly* the
  placeholder. Any other `{{...}}` text is passed through unchanged: a placeholder inside a longer string, and assistant
  text. Before, it was a plain text replace over the whole script. Red `eda14ef1`, green `144c5b94`. Note: the current
  `contracts/DELTA.md` section 8 on this branch does not have the "passed through unchanged" sentence yet. The code follows
  the rule as the coordinator gave it, so the architect's text can be merged as is.
- **`mixer_get_state` with `settings.name: ""`** (frontend note): `save_mix_as` sets the name from its argument.
  `update_mix` keeps the stored mix's name. An audio export of the open mixer carries no name from the settings. The fake
  page result now also includes `karaoke`. Test `SaveAndUpdate_SetTheName_WhenTheMixerReportsAnEmptyName` (`8084d10a`)
  passed straight away, because the behaviour was already there.
- **T079b SIG** (`9259e8e0` src, `b5447250` tests): units were split and parameter objects added, with no change in
  behaviour.
  - The security logic is unchanged in content. It only moved into smaller named units:
    - `StepExecutor.Heavy/CheckFailed/IsFailure`: HeavyWhen still fails closed, and a failed check is never plan-covered.
    - `ApprovalGate.Verdict`: the hash binding, and a grant bound to the tool plus its single string target.
    - `PreconditionAsync/CheckAsync`: the consent pre-check.
    - `DecideAsync`: the imitation guardrail, through `LibraryArtistsAsync`.
    - `MarkTaintedAsync`: taint is set from the projected result, for both success and failure.
  - Tests:
    - `Dtos.Track(id, title, artist).With(status, progress)`.
    - `ProviderTestKit.RunAsync(tool, Invocation(...) with { Backend = ... })`.
    - The policy-table oracle moved into helpers.
    - `sig-exempt` on `CapturingLogger.Log`, because `ILogger.Log` fixes that signature.
- **T079a eval harness, live runs** (`0b41452b`, all under `tools/agent-eval/**`):
  - `Fixture/FixtureLibrary`: the seeded fixture of eval-set.md.
    - Tracks: `t-hero` (detail completed, 4:20, stems, beat grid), `t-enter`, `t-jolene` (lyrics found), `t-fail`
      (failed, "unsupported codec") and `t-inject` (the injection title).
    - Mixes: `m-hero-practice` and `m-sandman`.
    - Styles: the 24 presets of `presets.v1.json`. Each `s-<presetKey>` maps to the preset's real id, for example
      `s-reggae`.
    - `g-song` "Zomeravond", with 2 synthetic takes.
    - `v-mine` "Mijn stem", with current consent.
    - Stable ids come from `FixtureIds` (a SHA-256 of the key). Recorded arguments are translated back to keys before
      grading.
  - `Fixture/FixtureActions` and `FixtureCreations` implement all 9 action ports over the fixture.
    - Writes change the fixture.
    - Every job finishes immediately, so `wait_for_job` ends on its first poll.
    - The device is a GPU and generation is `ready`.
  - `Fixture/FixturePage` plays the browser page. It answers UI tools the way the frontend does:
    - `mixer_get_state` returns `settings` with linear volumes and `name: ""`, plus `karaoke`.
    - The change tools answer with `{previous, current}`.
    - `IsRestored` is the check for the undo case.
  - `Fixture/MemoryStores`: in-memory run, conversation, undo and settings stores, an event sink and an idle GPU.
  - `LiveRunner` / `CaseContext`: one fresh fixture per case run, through the real `AgentRunService`, registry, providers and
    policy.
    - Approval cards are answered as the case states: `approve`, `reject`, and `none` means reject. The answer is sent
      250 ms after the card, so the runner is already waiting.
    - Case 19 runs case 8's command first, then sends the Undo request. It passes when the mixer is restored.
    - Any harness error fails only that case.
  - `LiveEvaluation`: runs each case `--runs N` times. A case passes when more than half of its runs pass (the median of 3
    for SC-001), and the median latency is reported. Writes the Markdown report.
  - `EvalBackends`:
    - `fake` plays each case's ideal transcript through `ScriptedChatSession`.
    - `ollama` uses `AgentModelSelector` over `OllamaChatClientFactory` with `--model`. The base URL comes from
      `StemStudio__Agent__Ollama__BaseUrl` (default `http://localhost:11434/`).
    - `claude` exits 2 on purpose: it is paid and opt-in, and is not wired in the harness.
  - Reference-machine command (CTO T081), run from the repo root:
    `dotnet run --project tools/agent-eval -- --backend ollama --model qwen3.5:9b --runs 3 --out agent-eval-report.md`.
    Optional `--eval-set` and `--presets` (defaults `tools/agent-eval/eval-set.json` and
    `src/StemStudio.Domain/Styles/presets.v1.json`).

## Tests and coverage

- `dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **592 passed**, 0 failed.
- `StemStudio.Agent` coverage: **line 98.62 %, branch 93.19 %, method 98.34 %** (gate 90/90 passed).
- agent-eval `--backend fake --runs 3`:
  - Self-test: 33 cases, 0 grading failures.
  - Live scripted run: 33 cases, 0 failed (scored 30/30: nl 16/16, en 14/14; supplementary 3/3).
- Wiring smoke test: `--backend ollama` against an unreachable port (`127.0.0.1:9`, no model). Every case fails cleanly with
  `backend-unavailable`, and case 19 fails with "There is nothing to undo".
- No real model was run.

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes` is clean for `src/StemStudio.Agent`, the tests and
  `tools/agent-eval`.
- `sig_check`:
  - `src/StemStudio.Agent`: 127 files, 555 units, **0 errors, 0 warnings** (was 49).
  - `tools/agent-eval`: 14 files, 176 units, **0 errors, 0 warnings** (was 4).
  - `tests/StemStudio.Agent.Tests`: 52 files, 0 errors (was 5), 1 exemption (`ILogger.Log`), **28 warnings**.
- All 28 remaining test warnings are unit size of `[Fact]` / `[Theory]` methods (16-25 lines). Test code reports unit size
  as a warning only (constitution II, sig_check "test code: reported").
  - Why they stay: each one is a single Arrange-Act-Assert scenario of a security or flow rule (approval binding, plan
    grant, taint, cancellation, undo order). Splitting would hide the scenario across helpers. All helpers and fakes over the
    limit were split.
  - The methods:
    - `AgentServiceCollectionExtensionsTests`: `RegistersTheInboundPorts_AndExactlyOneRunService` (17),
      `ProcessReader_TreatsBlankAsUnset` (17).
    - `ClaudeBackendTests`: `Session_SendsModelEffort...WithTheKeyFromTheReader` (23).
    - `ProvenanceAndBindingTests`: `ApprovalBinding_RequiresAPendingApprovalWithTheSameHash` (17),
      `ApprovalBinding_GrantsPlanScopeOnlyForApprovedPlanHeavyDecisions` (19).
    - `ToolPolicyTests`: `PlanScope_NeverCoversDestructiveStepsOrCompensations` (17).
    - `AgentRunnerTests`: `OneToolCall_CreatesAndSucceedsAStep_ThenReturnsTheResultToTheModel` (19).
    - `ApprovalFlowTests`: `ApprovalRequiredStep_SuspendsTheRun_AndPublishesTheCard` (21),
      `Reject_ReturnsRejectedToTheModel_AndSkipsThePlanItem` (18), `PendingApproval_ExpiresAfter24Hours` (17),
      `PlanHeavyScope_CoversOnlyLaterStepsOfTheSameToolAndTarget` (20), `CancelWithJobs_CancelsTheJobsTheRunStarted` (16).
    - `EscalationTests`: `AFailedCheck_IsNeverCoveredByAPlanGrant` (19).
    - `InjectionTests`: `AttackAfterUntrustedText_NeverExecutesWithoutApproval` (21).
    - `JobWaiterTests`: `Track_IsPolledEveryTwoSecondsUntilCompleted_ReportingProgress` (17),
      `WaitForJob_InARun_PollsWithoutModelTurns` (23).
    - `RunnerRobustnessTests`: `CancellationMidStep_CancelsTheRunAndReleasesTheConversation` (25).
    - `RuntimeEdgeTests`: `ServiceStatus_MapsUnavailableServices_AndCachesForTenSeconds` (18).
    - `UiToolGatewayTests`: `AnswersBeforeTheEventIsHandled_AreNotLost` (16).
    - `UndoPlannerTests`: `Undo_RestoresTheNewestGroupInReverseOrder_WithoutAModelTurn` (19),
      `NonFreshCompensation_NeedsApproval_AndARejectionLeavesItObsolete` (16).
    - `ArgumentValidatorTests`: `JsonSchema_DescribesNestedTypesFormatsAndPatterns` (16).
    - `Core001ToolProviderTests`: `Arguments_FollowTheCatalogue` (18),
      `UpdateMix_KeepsTheName_AndRecordsThePreviousVersion` (16), `DuplicateRenameAndDeleteMix_CallThePorts` (16).
    - `ProviderEdgeTests`: `Transcribe_WithoutASuggestion_NeedsAProfile_AndPianoKeepsTheQuality` (22).
    - `Style009ToolProviderTests`: `CancelRetryImportAndDelete_CallThePorts` (18).
    - `VoiceConsentTests`: `Runner_Backend409_IsStillAFailedStep` (16).

## Findings for qa / architect

1. **Eval cases 17 and 27 versus the taint rule.**
   - `search_library` results carry track titles as `untrusted_text`, which taints the run (FR-013). So `retry_track`
     (17) and `export_audio` (27) after a search get an approval card, although eval-set.md says "approval: none".
   - The harness follows `ApprovalAnswer.None` ("any card is rejected"), so the expected call is still recorded and the
     case is graded on its calls. The log line shows `cards:N`.
   - A real model will hit this. qa/architect should decide whether eval-set.md 1.0.x should say
     "required (tainted) -> approve" for 17 and 27, or whether library titles should stop tainting. The latter would be a
     security change and needs a decision.
2. The approval answer is sent 250 ms after the card. `ApprovalWaiter` registers just after the card is published, so an
   instant answer could be lost (the real UI is slower). A port-level "wait registered" hook would remove the delay.

## Switch-over at integration (architect branch)

1. **`Microsoft.Extensions.DependencyInjection` in the agent test project.** Replace
   `tests/StemStudio.Agent.Tests/Fakes/TestServiceProvider.cs` with `services.BuildServiceProvider()` in
   `AgentServiceCollectionExtensionsTests` (all 11 `new TestServiceProvider(...)` uses), then delete the fake.
2. **agent-eval test project.**
   - Move the checks of `SelfTest.Run` (ideal passes; forbidden, missing or too many reads fail) into xUnit tests of
     `Grader`.
   - Add tests for `FixtureIds` (`ToKeys`/`ToIds`) and a `LiveRunner` + `EvalBackends.Fake` run of all cases.
   - The types are `internal`, so add `InternalsVisibleTo` for the test assembly in `StemStudio.AgentEval.csproj`.
3. **`risk-check-failed` in `AgentDecisionReasons` / the `decisionReasons` enum.** In `StepExecutor.CheckFailed`
   (`src/StemStudio.Agent/Runtime/StepExecutor.cs`), the escalated call keeps `RiskCheckFailed = true` (read in `PolicyFor`
   for "no plan grant"). Add the reason there, for example through a new `PolicyInput` flag that `ToolPolicy.Reasons`
   maps to `risk-check-failed`. Until then the card shows only `risk-heavy`.
4. **`UndoEntry.ExpectedUpdatedAt`.**
   - Today the guard travels as the reserved argument `$expect` (`src/StemStudio.Agent/Tools/UndoGuard.cs`, `Property`).
   - It is written by `ToolDefinitions.MixUndo` (`src/StemStudio.Agent/Tools/Providers/ToolDefinitions.cs`) and read by
     `UndoGuard.Split` in `UndoRun.ApplyAsync`.
   - Switch to the field: set it where `UndoRecorder` (in `Runtime/UndoPlanner.cs`) builds the entries, and read
     `entry.ExpectedUpdatedAt` in `UndoRun.ApplyAsync`. Keep `Split` for entries already stored with `$expect`.
5. **`ApprovalRequestDto.Device` doc text:** no agent change (the wire value is already `gpu`/`cpu`).

## Dependencies added

None.

## Docs impact

Technical manual (agent evaluation):
- `dotnet run --project tools/agent-eval -- --backend fake --runs 1` checks the grading and the harness without a model.
- The SC-001 run on the reference machine is `--backend ollama --model qwen3.5:9b --runs 3`.
- The report is written to `--out`.

## Open issues / requests

1. **frontend**: nothing blocking. `mixer_get_state` `settings` with `name: ""` is handled.
2. **qa / architect**: findings 1-2 above, plus the switch-over list.

## Next owner

CTO (T081, live evaluation on the reference machine); architect (switch-over items 1-4).
