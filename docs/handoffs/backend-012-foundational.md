# Hand-off: backend / 012 foundational (T025-T028)

Branch `012-lane-backend` (worktree `.claude/worktrees/012-backend`), base `010c3e6d` (architect Phase 0 hand-off).

## What was built

- **Domain (T025 -> T026)**, `src/StemStudio.Domain/Mixes/`:
  - `PracticeSettings` (`SpeedPercent` 50..120 = 100, `TransposeSemitones` -12..12 = 0, `Trainer`, `CountIn`,
    `static Default`, `IsNeutral` = 100 % && 0 st && trainer off, `TargetAboveStartMessage`),
    `SpeedTrainerSettings` (`Enabled` false, `StartPercent` 50..119 = 70, `StepPercent` 1..20 = 5,
    `TargetPercent` 51..120 = 100, `PassesPerStep` 1..8 = 1), `CountInSettings` (`Bars` 0..2 = 0, `EveryPass` false).
    Records with `init` properties and defaults (same style as `MetronomeSettings`).
  - `MixSettings.Practice` (default `PracticeSettings.Default`); `MixSettings.Validate` calls
    `Practice.Validate(errors.Nested("practice"))` after the existing rules, so the error paths are
    `practice.speedPercent`, `practice.transposeSemitones`, `practice.trainer.<field>`, `practice.countIn.bars`.
  - `start < target` is checked always (also with the trainer off) at `practice.trainer.targetPercent` with
    "The target must be above the start speed."; it is reported only when the target is inside its own range, so each
    field carries at most one error (e.g. target 50 -> only the range message). No rule couples practice to the loop.
  - `Mix.Practice` reads a nullable backing field `_practice` and returns `PracticeSettings.Default` when it is null
    (mixes saved before 012); `Settings`, `Update` and `Duplicate` carry it.
  - Bounds in `SpeedTrainerSettings` are literals, not derived from `PracticeSettings`: deriving them would create a
    cross-type static-initialisation cycle in which `PracticeSettings.Default.Trainer` could be built before
    `SpeedTrainerSettings.Default` exists (null).
- **Persistence (T027 -> T028)**: `MixConfiguration.ConfigurePractice` maps `_practice` as one complex JSON column
  `Practice` with the nested complex `Trainer` and `CountIn` (EF 10 `ComplexProperty(...).ToJson("Practice")`);
  migration **`20261116211814_AddMixPractice`** (dotnet-ef 10.0.15 tool manifest): `Mixes.Practice TEXT NULL`. Existing
  rows keep `NULL` and read as the defaults; no data migration.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T025 -> T026 | `320e313f` | `1dbd9a02` |
| T027 -> T028 | `0b0a231c` | `a343d7a6` |

## Files changed

- New: `src/StemStudio.Domain/Mixes/{PracticeSettings,SpeedTrainerSettings,CountInSettings}.cs`,
  `src/StemStudio.Persistence/Migrations/20261116211814_AddMixPractice{,.Designer}.cs`,
  `tests/StemStudio.Domain.Tests/Mixes/PracticeSettingsTests.cs`, `tests/StemStudio.Persistence.Tests/MixPracticePersistenceTests.cs`
- Changed: `src/StemStudio.Domain/Mixes/{Mix,MixSettings}.cs`, `src/StemStudio.Persistence/Configurations/MixConfiguration.cs`,
  `src/StemStudio.Persistence/Migrations/StemStudioDbContextModelSnapshot.cs`, `specs/012-practice-tempo/tasks.md` (ticks)

## Tests and coverage

```text
dotnet test tests/StemStudio.Domain.Tests -c Release -p:CollectCoverage=true
  Passed! Failed 0, Passed 793     StemStudio.Domain       line 98.98 %  branch 94.46 %
dotnet test tests/StemStudio.Persistence.Tests -c Release -p:CollectCoverage=true
  Passed! Failed 0, Passed 120     StemStudio.Persistence  line 99.01 %  branch 95 %
```

The migration test uses the last migration before 012 (`20261116165940_AddAgentSecrets`, 011 claude-key) instead of
"the 010 schema" named in T027: that is the newest schema a real database can have before 012; older ones pass through
it.

## Quality checks

New units <= 15 LOC, CC <= 3, <= 2 parameters. Full build/format/SIG run at HO:us1.

## Dependencies added

None.

## Docs impact

None (no user-visible change yet).

## Open issues / requests

None.

## Next owner

backend (T035/T036, US1).
