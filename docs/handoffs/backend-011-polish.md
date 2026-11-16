# Hand-off: backend / 011 polish (T079c)

Branch `011-lane-backend`. Previous: `backend-011-us6.md`. This closes the backend row of feature 011
(T018-T021, T032-T035, T045-T046, T052-T055, T060-T061, T070-T071, T078, T079c).

## What was done

- **SIG**: `tools/quality/sig_check.py` on `src/StemStudio.Api/Agent`, `src/StemStudio.Persistence/Agent` and
  `src/StemStudio.Persistence/Configurations/AgentConfiguration.cs`: **0 errors, 0 warnings, 0 exemptions** (42 files, 297
  units). Before: 2 errors (`AddAgentHost` 32 lines, `AgentSettingsRules.Merge` complexity 11) and 35 warnings (the 24 of
  `backend-011-us1.md` plus the units added in US4-US6). Refactors without behaviour change:
  - `AgentRegistration`: `AddAgentStores` / `AddAgentRuntime` / `AddAgentEndpointServices`.
  - `AgentEndpoints`: conversation and run endpoints in four groups.
  - `SseWriter`: replay split (`WriteStoredAsync`), live loop over the new `LiveEvents` reader (keeps a pending read across
    keep-alive ticks, as before); subscription still taken before the replay.
  - `EfRunStore.CreateRunAsync`, `RunEventHub.PublishAsync`: `SemaphoreExtensions.RunExclusiveAsync`.
  - Mappers (`AgentDtoMapping`, `RunMapping`, `StepMapping`, `AgentSettingsRules`): grouped initialisers and helper units.
  - Message, settings, status probe, GPU gate, loopback client, startup recovery, undo, run endpoint service: small helpers.
- **Duplication** (`tools/quality/duplication.py`, jscpd) on the agent code: 0 clones (the using-header clone between the store
  mappers, present since US1, is gone: `RunMapping`/`StepMapping` call `AgentStoreValues.*` qualified).
- **`AgentProblems.CallAsync`**: one mapping of agent-library exceptions for approvals, cancel and undo:
  `AgentConflictException.ProblemCode` -> 409 with that problem type and the library's message;
  `KeyNotFoundException` -> 404 (new for approvals and cancel; test `Cancel_ARunUnknownToTheAgent_Is404`).
- **XML docs**: every public and internal type and member of the agent code is documented (CS1591 is an error in the build).

## Red -> green commits

Refactor only: `11f6e68` (all tests green before and after; one new test for the 404 mapping).

## Tests and coverage

| Project | Tests | Line | Branch |
|---|---|---|---|
| `tests/StemStudio.Api.Tests` (StemStudio.Api) | 1431 passed | **98.79 %** | **91.59 %** |
| same run, StemStudio.Contracts | | 100 % | 100 % |
| `tests/StemStudio.Persistence.Tests` | 99 passed | **98.64 %** | **95.00 %** |
| `tests/StemStudio.Domain.Tests` | 752 passed | **98.92 %** | **94.43 %** |

## Quality checks

Build 0 warnings (TreatWarningsAsErrors); `dotnet format --verify-no-changes` clean; SIG 0/0; duplication 0; all units <= 4
parameters; dependency direction Api -> Persistence -> Domain unchanged (no new project references).

## Dependencies added

None in feature 011's backend lane.

## Docs impact

None beyond the US4-US6 hand-offs.

## Open issues / requests

- architect: DELTA.md section 5 wording for generator/singer jobs (see `backend-011-us6.md`).
- ai-agent: `IAgentRunService.UndoAsync` creates the queued undo run before it returns its id (see `backend-011-us5.md`);
  publish `approval.requested.device` as `cpu`/`gpu` (see `backend-011-us3.md`).
- quality-gate: three non-agent warnings remain in `src/StemStudio.Persistence/Configurations`
  (`GenerationConfiguration`, `StyleConfiguration`, `TrackConfiguration` `Configure` 16-20 lines, features 001-010); out of the
  011 scope, not touched.

## Next owner

qa (011 E2E), quality-gate (011 audit).
