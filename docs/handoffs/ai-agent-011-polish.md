# Hand-off: ai-agent / 011 polish (T079a part 1, T079b part 1) - interim

Branch `011-lane-agent`. No LLM called; no key anywhere. Builds on `ai-agent-011-us6.md`. **Both tasks stay open** (see
"Open").

## What was built

- **T079a part 1** (`tools/agent-eval`): `eval-set.json` = evaluation/eval-set.md 1.0.0 as data (30 scored cases, 16 nl /
  14 en, plus S1-S3 unscored), fixture keys (`t-hero`, `m-sandman`, `s-reggae`, `g-song`, `v-mine`, ...) as placeholders
  for the seeded ids; `EvalSet` model; `Grader` with the grading rules of the md (matchers `=`, `~`, `in`, `>`, `<`,
  `absent`; ordered `->` expectations; alternatives; forbidden calls; forbid any non-read / any call; at most 2 extra
  reads; `set_plan`/`wait_for_job` not counted); `EvalReport` (Markdown: per case, success per language, supplementary
  cases, latency p50/p90); `--backend fake` runs a grading self-test (ideal transcript passes, forbidden/missing/too many
  reads fail): `dotnet run --project tools/agent-eval -- --backend fake --eval-set tools/agent-eval/eval-set.json` ->
  "33 cases, 0 grading failures". `--backend ollama|claude` exits 2 with a message (live runs not implemented yet).
- **T079b part 1**: artist, song and style units split; agent-eval within the parameter limit.

## Open (next session)

1. T079a part 2: seeded fixture actions (`IStemStudioActions` over the eval fixture incl. styles, `g-song`, `v-mine`),
   the live loop over `AgentRunService` (approval answers per case, Undo for case 19, `--runs N`, model latency), report
   history. Unit tests of the grader as an xUnit project need an architect-owned project file (see requests); until then
   the self-test is the check.
2. T079b: `sig_check src/StemStudio.Agent` **0 errors, 49 warnings** (was 33 before US3; the 004-010 providers and undo
   added units). Biggest: `StepExecutor.DecideAndRunAsync/RunUiToolAsync/SucceedAsync`, `AgentRunner.RunGuardedAsync/
   RecordTurnAsync`, `ToolArgumentValidator`, `ExportRequests.AudioAsync`, provider `GetTools` lists.
3. qa us1 request 1: placeholder replacement as whole string values only (currently a JSON text replace; the committed
   scripts use whole values, so behaviour matches).

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **586 passed**, 0 failed.
`StemStudio.Agent`: **line 98.55 %, branch 93.5 %, method 98.13 %** (gate 90/90 passed). agent-eval self-test: 33 cases,
0 failures.

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean for `src/StemStudio.Agent`, the tests and `tools/agent-eval`.
`sig_check`: `src/StemStudio.Agent` 127 files, 487 units, 0 errors, 49 warnings; `tools/agent-eval` 5 files, 25 units,
0 errors, 4 warnings.

## Dependencies added

None.

## Docs impact

Technical manual: `dotnet run --project tools/agent-eval -- --backend fake --eval-set tools/agent-eval/eval-set.json`
checks the grading; live runs follow.

## Open issues / requests

1. **architect**: a test project for `tools/agent-eval` (or `InternalsVisibleTo` + a reference from an existing test
   project) so the grader gets xUnit tests and coverage; `Microsoft.Extensions.DependencyInjection` in the agent test
   project (US4 request); `risk-check-failed` reason (US6 request); `UndoEntry.ExpectedUpdatedAt` (US5 request).
2. **frontend (blocking)**: `mixer_get_state` must include `settings` (US5/US6 request).

## Next owner

ai-agent (T079a part 2, T079b).
