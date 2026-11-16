# Hand-off: backend / 012 polish (T063) - lane complete

Branch `012-lane-backend` (worktree `.claude/worktrees/012-backend`), base `010c3e6d`. Lane tasks T025, T026, T027, T028,
T035, T036, T063 are done and ticked. Earlier checkpoints: `backend-012-foundational.md`, `backend-012-us1.md`.

## What was built

- **Agent round trip (011 is merged)**: `tests/StemStudio.Api.Tests/Agent/LoopbackMixPracticeTests.cs` drives the
  composer agent's mix port (`LoopbackActions.Mixes`, the Api-side adapter of `IMixActions`) against the in-memory API:
  create with `practice`, get, update with `previous.Settings with { Name = ... }` (what `rename_mix` and the `restore_mix`
  undo send) and duplicate all keep `practice`. It passed on first run (characterisation test, no production change):
  the loopback client serialises the Contracts DTOs, which carry `Practice`.
- Refactor / docs: all new public members carry XML docs; new units <= 15 LOC, CC <= 3, <= 2 parameters; SIG warnings back
  at the Phase 0 baseline (817).

## Commits (lane)

| Commit | Task |
|---|---|
| `320e313f` red / `1dbd9a02` green | T025 / T026 Domain |
| `0b0a231c` red / `a343d7a6` green | T027 / T028 Persistence + migration `20261116211814_AddMixPractice` |
| `69861119` | HO:foundational |
| `c5285dfe` red / `577de7e2` green | T035 / T036 Api |
| `0823b820` | SIG refactor |
| `1a465213` | T063 agent loopback round trip |
| this commit | HO:us1, HO:polish, decisions row, ticks |

## Files changed (lane total)

- Domain: `src/StemStudio.Domain/Mixes/{PracticeSettings,SpeedTrainerSettings,CountInSettings}.cs` (new), `Mix.cs`, `MixSettings.cs`
- Persistence: `Configurations/MixConfiguration.cs`, `Migrations/20261116211814_AddMixPractice{,.Designer}.cs` (new),
  `Migrations/StemStudioDbContextModelSnapshot.cs`
- Api: `src/StemStudio.Api/Mixes/{MixMapper,MixValidator}.cs`
- Tests (new): `tests/StemStudio.Domain.Tests/Mixes/PracticeSettingsTests.cs`,
  `tests/StemStudio.Persistence.Tests/MixPracticePersistenceTests.cs`, `tests/StemStudio.Api.Tests/Mixes/MixPracticeEndpointTests.cs`,
  `tests/StemStudio.Api.Tests/Agent/LoopbackMixPracticeTests.cs`
- Docs: `docs/handoffs/backend-012-{foundational,us1,polish}.md`, `docs/decisions/CTO-DECISIONS-2026-11-15.md` (one row),
  `specs/012-practice-tempo/tasks.md` (ticks)

## Tests and coverage

```text
dotnet test tests/StemStudio.Domain.Tests -c Release -p:CollectCoverage=true
  Passed! Failed 0, Passed 793, Skipped 0      StemStudio.Domain       line 98.98 %  branch 94.46 %
dotnet test tests/StemStudio.Persistence.Tests -c Release -p:CollectCoverage=true
  Passed! Failed 0, Passed 120, Skipped 0      StemStudio.Persistence  line 99.01 %  branch 95 %
dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true
  Passed! Failed 0, Passed 1706, Skipped 2     StemStudio.Api          line 98.97 %  branch 92.06 %
                                               StemStudio.Contracts    line 100 %    branch 100 %
New files: PracticeSettings / SpeedTrainerSettings / CountInSettings / MixMapper / MixValidator 100 % line and branch.
```

Only the Domain, Persistence and Api test projects were run (other agents share the machine); Web, Agent and AgentEval
were not touched by this lane.

## Quality checks

```text
dotnet format (6 owned projects) --verify-no-changes      exit 0
tools/quality/run_all.py                                   0 errors, 817 warnings (Phase 0 baseline), exit 0
dotnet build tests/StemStudio.{Api,Persistence,Domain}.Tests -c Release   0 Warning(s), 0 Error(s)
```

Dependency direction unchanged: Api -> Persistence -> Domain; Domain has no new reference.

## Dependencies added

None.

## Docs impact

Technical manual: `Mixes.Practice` JSON column (migration `AddMixPractice`, NULL for older mixes = defaults); API
reference for `practice` (see HO:us1). No setup change.

## Open issues / requests

- **frontend** (repeat from HO:us1): `MixerState.ToSettings` must carry `Practice`; both the Save button and the agent's
  `mixer_get_state` -> `update_mix` / `save_mix_as` send that body, and `PUT` without `practice` resets it to the defaults.
- **ai-agent**: (1) add an Agent.Tests case that `update_mix` with a `mixer_get_state` result containing `practice` sends it
  unchanged, and that the `restore_mix` undo entry (serialised `previous.Settings`) restores it; (2) `MixProjections.Settings`
  does not project `practice`, so `get_mix` / `mixer_get_state` results do not show the model the practice tempo; exposing it
  (and practice tools) is the backlog item "agent tool catalogue minor for practice tools" in T074 - not done here because
  the tool catalogue is ai-agent / architect owned.
- **CTO**: the decisions row "012 Backend lane" in `CTO-DECISIONS-2026-11-15.md` (appended at the end of the table; trivial
  merge if other lanes appended too).

## Next owner

qa (in backend's slot, per the architect's lane start list), then the CTO integration wave (T069-T075).
