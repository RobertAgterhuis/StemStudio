# Hand-off: architect / 011 follow-ups (contracts and skeleton for the ai-agent, backend and frontend lanes)

Branch `011-composer-agent` (base of all 011 lanes), on top of de1751b8. Requests from
`ai-agent-011-{foundational,us2,us4,us5,us6,polish}.md` and the coordinator (`mixer_get_state` shape).
Contracts 1.10.0 are **not released** (docs/ROADMAP.md row 011: backend 1.10.0, agent tool catalogue
1.0.0, both still open), so every change stays within 1.10.0 / catalogue 1.0.0 and is recorded in the
changelog text. No lane worktree was touched; no push or merge.

## What changed

| # | Item | Where |
|---|---|---|
| 1 | Decision reason **`risk-check-failed`** (dynamic heavy-risk check threw -> step escalated to heavy, approval required, never plan-covered; replaces `risk-heavy` on that card) | `DecisionReason` enum + description in `backend-api.openapi.yaml` and `agent-api.openapi.yaml` (identical, the merge test holds); `AgentDecisionReasons.RiskCheckFailed`; data-model 4.2 and 6 |
| 2 | **`UndoEntry.ExpectedUpdatedAt`** (`DateTimeOffset?`, the mix's `updatedAt` after the step; tolerance 1 s; null for unversioned targets) | `src/StemStudio.Agent/Model/UndoEntry.cs`; data-model 4.4 |
| 3 | **`ApprovalRequest.device`** = `cpu` \| `gpu` \| null (enum added, values = `ProcessingDeviceKind`; description) | both OpenAPI documents; XML doc of `ApprovalRequestDto.Device`; contract sample now `"gpu"` |
| 4 | Data-model 4.3 / 6: a `plan_heavy` grant is bound to its **tool** and **exactly one string target**; no grant without such a target (acts as `once`); target-less / array / non-string / null targets always ask; only reason `risk-heavy` can be covered. "Dependent steps skipped" **confirmed** as: only the rejected/expired step's own plan item becomes `skipped`, later items stay `pending`, the model re-plans with `set_plan` and says what was not done | data-model 4.3, 6; tasks.md T041 wording |
| 5 | `Microsoft.Extensions.DependencyInjection` **10.0.15** in `tests/StemStudio.Agent.Tests` | `Directory.Packages.props` (Testing), test csproj |
| 6 | New test project **`tests/StemStudio.AgentEval.Tests`** (xUnit v3, coverlet, Shouldly, NSubstitute via Directory.Build.props), in `StemStudio.slnx`; `InternalsVisibleTo` in `tools/agent-eval/StemStudio.AgentEval.csproj`; skeleton `EvalOptionsTests` (9 cases) | |
| 7 | 009 imitation lexicon embedded: the three `src/StemStudio.Domain/Styles/*.v1.txt` linked (not copied) into `StemStudio.Agent` with logical names `StemStudio.Agent.Lexicon.<file>`; loader `StemStudio.Agent.Resources.EmbeddedLexicon` (`Phrases`, `DenyList`, `GenreWords`, `Lines(file)`), same parsing as Domain's `EmbeddedText.Lines` | `src/StemStudio.Agent/StemStudio.Agent.csproj` (`Resources/` is architect-owned), `Resources/EmbeddedLexicon.cs`, tests |
| + | Coordinator: **`mixer_get_state`** result `{trackId, mixId?, unsaved, channels[...] (dB), master (dB), position, playing, loop, metronome, karaoke, settings}`; `karaoke` required (bool, banner visible); `settings` exactly a `MixSettingsDto` (camelCase, linear volumes 0..1, `name` = "", the agent sets the name before `save_mix_as` / `update_mix` / `export_audio`) | `contracts/tool-catalogue.md` row + changelog (stays 1.0.0) |

## Integration switch-over list (lanes adopt the new members)

**ai-agent** (`011-lane-agent`)
1. `StepExecutor`/`ToolPolicy` (`HeavyWhen` fail-open, US6 item 2): emit `AgentDecisionReasons.RiskCheckFailed`
   instead of `RiskHeavy` on the escalated card; keep the step out of plan coverage (rule "reasons == [risk-heavy]"
   already excludes it). Update the US6 test that asserts `risk-heavy`.
2. `Tools/UndoGuard`: write `UndoEntry.ExpectedUpdatedAt` instead of the reserved `$expect` argument; drop the
   stripping of `$expect` before execution; read it in the undo planner. Tests on `$expect` move to the property.
3. `Policy/ImitationGuard`: use `StemStudio.Agent.Resources.EmbeddedLexicon.{Phrases,DenyList,GenreWords}` and delete
   `Policy/ImitationLexicon.cs`. Suggested one-off check before deleting: assert the copy equals `EmbeddedLexicon`
   (order may differ). quality-gate request 3 of the foundational hand-off (duplication of the copy) disappears.
4. DI tests: replace `Fakes/TestServiceProvider` by `new ServiceCollection().AddStemStudioAgent().BuildServiceProvider()`
   where useful (package now referenced).
5. Grader tests go to `tests/StemStudio.AgentEval.Tests`. The skeleton `EvalOptionsTests` assumes the defaults
   `ollama`/3/`eval-set.json`/`agent-eval-report.md`, exit 64 for bad arguments and exit 2 for `--backend ollama`;
   adjust when T079a part 2 implements live runs. The 90 % line+branch gate now covers all of `StemStudio.AgentEval`
   (incl. `Program`, `SelfTest`, `Grader`, `EvalReport`).
6. `mixer_get_state` consumers (`save_mix_as`, `update_mix`, `export_audio` mode `mix`): read `settings` as
   `MixSettingsDto` and set `name`; `karaoke` is always present.

**backend** (`011-lane-backend`)
1. Persistence of `UndoEntry`: map the new nullable `ExpectedUpdatedAt` (EF column; migration in the lane's 011
   migration, not yet released, so fold it into the existing 011 migration if it is not merged yet).
2. `ApprovalRequestDto.Device`: send only `cpu`/`gpu`/null (the agent's `ToolEstimate.DeviceCode` already does);
   reason `risk-check-failed` passes through unchanged (string list).

**frontend** (`011-lane-frontend`)
1. Approval card: a label/help text for reason `risk-check-failed` (e.g. "The size could not be checked, so this is
   treated as a heavy step"); device shown from the code `cpu`/`gpu`.
2. `mixer_get_state` already returns the shape above (coordinator) - no change.

**qa**: E2E/script expectations that check `reasons: [risk-heavy]` on a failed export size check change to
`risk-check-failed`; `device` assertions use `gpu`/`cpu`.

**devops**: `tests/StemStudio.AgentEval.Tests` is in `StemStudio.slnx`, so `scripts/test-all.{ps1,sh}` and CI
(`dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true`) pick it up and gate it automatically
(Directory.Build.props: component `[StemStudio.AgentEval]*`, 90 % line AND branch, report
`tests/StemStudio.AgentEval.Tests/TestResults/coverage/coverage.{cobertura,opencover}.xml`). Please add that report
to the Sonar `sonar.cs.opencover.reportsPaths` and the CI coverage artefact list, and keep `tools/agent-eval` itself
out of CI runs (it is only executed through its unit tests; live runs need a model).

## Tests and coverage

Only the affected projects (two other agents build on this PC):

| Project | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Agent.Tests -> StemStudio.Agent | 58 passed (55 + 3 new) | 100 % | 100 % |
| StemStudio.AgentEval.Tests -> StemStudio.AgentEval | 9 passed (new) | 100 % | 100 % |
| StemStudio.Api.Tests, filter `Contract` | 349 passed (incl. new `ApprovalDevice_IsAProcessingDeviceCodeOrNull`, updated `Constants_MatchTheContractEnums`, merge-rule tests) | not measured (filtered run; Contracts change is one constant and an XML doc) | |

`dotnet build StemStudio.slnx -c Release`: **0 warnings, 0 errors**. `dotnet format --verify-no-changes`: clean.
Contract lint:
`npx @redocly/cli@2.78.0 lint --config specs/011-ai-composer-agent/contracts/redocly.yaml specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml specs/011-ai-composer-agent/contracts/agent-api.openapi.yaml`
-> both valid (OpenAPI 3.1), 10 warnings (the known 8 + 2 `operation-4xx-response`).

## Quality checks

New units are small (`EmbeddedLexicon.Lines` 6 LOC, complexity 2); no parameter or duplication issue. The lexicon
data is now single-sourced (Domain files), which removes the duplication once the agent copy is deleted.

## Dependencies added

- `Microsoft.Extensions.DependencyInjection` 10.0.15 (MIT, Microsoft) - test project only; same servicing band as
  `Microsoft.EntityFrameworkCore.*` / `Microsoft.AspNetCore.*` 10.0.15 already pinned.

## Docs impact

None user-visible. Technical: data-model 4.2-4.4 and 6, tool catalogue changelog, backend API 1.10.0 changelog.

## Open issues

1. The full `StemStudio.Api.Tests` coverage run was not repeated (parallel builds); run it at integration.
2. ROADMAP unchanged (versions unchanged).

## Next owner

ai-agent, backend, frontend, qa, devops per the switch-over list.
