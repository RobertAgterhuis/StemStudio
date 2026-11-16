# Hand-off: backend / 011 foundational (T018-T021)

Branch `011-lane-backend` (worktree `.claude/worktrees/011-backend`), base `15210c6` (Phase 0 part 2).

## What was built

- **Persistence (T018 -> T019)**: plain row entities in `StemStudio.Persistence.Agent` (no reference to `StemStudio.Agent`:
  Persistence -> Domain only; enum values stored by C# name, lists/limits as JSON text), configurations with cascade deletes
  (conversation -> messages, runs -> steps, approvals, undo entries, events), unique `(ConversationId, Seq)` and `(RunId, Seq)`,
  `RunEvents` keyed `(RunId, Seq)`, `RunEventLog` (append with the next Seq, replay `after`), migration
  **`20261116064402_AddAgent`** (dotnet-ef 10.0.15 from the NuGet cache, LF line endings). Tables: `AgentSettings`,
  `Conversations`, `ConversationMessages`, `AgentRuns`, `RunSteps`, `Approvals`, `UndoEntries`, `RunEvents`.
- **Api stores (T020 -> T021)**, `src/StemStudio.Api/Agent/`:
  - `Stores/EfConversationStore`, `EfRunStore`, `EfUndoStore`, `EfAgentSettingsStore` implement the ports
    `IConversationStore`, `IRunStore`, `IUndoStore`, `IAgentSettingsStore` (singletons; `AgentDb` opens one scope/context per
    operation so long runs can hold them). Mapping in `ConversationMapping`, `RunMapping`, `StepMapping`, `AgentStoreValues`.
  - One active run globally: `CreateRunAsync` throws `InvalidOperationException` while a non-terminal run exists (serialised
    with a lock); the endpoint maps it to 409 `run-active` (T033).
  - Settings defaults: no row -> `AgentSettings.Defaults(backend)` where backend comes from `StemStudio:Agent:Backend`
    (`AgentHostOptions`, case-insensitive; empty/unknown -> `ollama`, per the devops note on compose).
  - `Stores/RunEventHub`: `IRunEventSink` (persist, next Seq) + `IRunEventLogReader` (replay). Live broadcast follows in T033.
  - `AgentStartupRecovery` (called in `UseStemStudioApiAsync` after migrations): non-terminal runs -> `interrupted` + `EndedAt`,
    pending approvals -> `expired`, `awaiting_approval` steps -> `expired`, other open steps -> `failed`
    ("StemStudio restarted while this step was running."), conversation `ActiveRunId` cleared, a `run.interrupted` event
    appended.
  - `AgentRegistration.AddAgentHost()` calls `AddStemStudioAgent()` (ai-agent) and registers the ports; Api now references
    `StemStudio.Agent`.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T018 -> T019 | `fe2fb8b` | `de3e47d` |
| T020 -> T021 | `ac02305` | `b8939e6` |

## Files changed

- New: `src/StemStudio.Persistence/Agent/{AgentEntities,RunEventLog}.cs`, `Configurations/AgentConfiguration.cs`,
  `Migrations/20261116064402_AddAgent{,.Designer}.cs`; `src/StemStudio.Api/Agent/{AgentHostOptions,AgentRegistration,AgentStartupRecovery}.cs`,
  `src/StemStudio.Api/Agent/Stores/*`; `tests/StemStudio.Persistence.Tests/Agent/{AgentPersistenceTests,AgentRows}.cs`,
  `tests/StemStudio.Api.Tests/Agent/{StoresTests,AgentRecords}.cs`.
- Modified: `StemStudioDbContext.cs` (8 DbSets), model snapshot, `ApiComposition.cs`, `StemStudio.Api.csproj` (Agent reference),
  `tests/StemStudio.Api.Tests/TestSupport/ApiFactory.cs` (`Settings` for configuration values), tasks.md (T018-T021 ticked).

## Tests and coverage

- `dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true`: **99 passed**; StemStudio.Persistence
  **98.64 % line, 95 % branch**.
- `dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: **1282 passed**; StemStudio.Api **98.99 % line, 92.81 % branch**;
  Contracts 100 / 100.
- Domain untouched (no agent rules in Domain; they live in `StemStudio.Agent.Model`).

## Quality checks

Build with `TreatWarningsAsErrors`: 0 warnings; `dotnet format --verify-no-changes` clean for Persistence, Persistence.Tests,
Api.Tests (which includes Api). `RunStep` mapping initialisers are ~24 lines (data mapping, below the hard limit of 30).

## Dependencies added

None (project reference Api -> StemStudio.Agent only).

## Docs impact

None yet (new tables only).

## Open issues / requests

- ai-agent: the stores are registered after `AddStemStudioAgent()` with plain `AddSingleton`; if the agent library registers
  defaults for the same ports, use `TryAdd` so the backend's implementations win.

## Next owner

backend (T032 US1 endpoints).
