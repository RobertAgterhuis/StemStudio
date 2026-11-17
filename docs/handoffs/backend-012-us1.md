# Hand-off: backend / 012 US1 (T035-T036)

Branch `012-lane-backend`, after HO:foundational (`69861119`).

## What was built

- `MixMapper`: `ToDto(PracticeSettings)` (public) and `ToDomain(PracticeSettingsDto)` (internal); `ToDto(MixSettings)` always
  sets `Practice`, so every `Mix` response (`createMix`, `getMix`, `updateMix`, `duplicateMix`) carries `practice`;
  pre-012 mixes (`Mixes.Practice IS NULL`) return the defaults. `ToDomain(MixSettingsDto)`: absent or `null` `practice` ->
  `PracticeSettings.Default`.
- `MixValidator.RequirePractice`: a present `practice` needs `trainer` and `countIn` (400 "This value is required." at
  `practice.trainer` / `practice.countIn`, prefixed with `settings.` on create). Ranges and `start < target` come from the
  Domain (`MixSettings.Validate`), so the paths are `practice.speedPercent`, `practice.transposeSemitones`,
  `practice.trainer.<field>`, `practice.countIn.bars` (`settings.practice...` on `POST /api/mixes`).
- No operation added or staged: the `Pending*Operations` lists are unchanged (architect Phase 0: 1.11.0 adds no
  operation); the mix responses are checked against the 1.11.0 document by `ContractAssert`.
- Exports: `RenderPlanner` and the export recipe never read `practice`; a test exports one saved mix before and after
  setting a practice tempo and compares the worker render request and the recipe byte for byte (ADR-016 vectors unchanged).
- `MixSummary` unchanged (test: exactly `id, trackId, trackTitle, name, updatedAt`).

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T035 -> T036 | `c5285dfe` (16 of 23 failing) | `577de7e2` |
| SIG refactor | | `0823b820` (`MixMapper.ToDto` 16 -> 13 LOC, persistence test helpers) |

## Decisions taken (owner unavailable; also in `docs/decisions/CTO-DECISIONS-2026-11-15.md`, row "012 Backend lane")

1. `"practice": null` = absent = defaults (precedent: `expanded` / `detailChannels`).
2. A fractional percent (`75.5`) fails JSON binding -> 400 problem, nothing stored. data-model.md 2.2 says "422"; all mix
   body errors are 400 in backend-api and the contract lists no 422 for the mix operations, so 400 is kept.
3. `start < target` is reported only when the target is within 51..120 (one message per field).
4. `PUT /api/mixes/{id}` without `practice` resets it to the defaults (contract text "absent = defaults"). Clients that
   replace a mix must send the current `practice` (see requests).
5. A missing integer inside a present `practice` (e.g. no `speedPercent`) binds as 0 and fails the range rule with
   "Must be between 50 and 120." at its path; the positional DTO records (architect-owned) give no presence signal for
   value types, and the message still points at the right field.

## Files changed

- `src/StemStudio.Api/Mixes/{MixMapper,MixValidator}.cs`
- `tests/StemStudio.Api.Tests/Mixes/MixPracticeEndpointTests.cs` (new, 23 tests)
- `tests/StemStudio.Persistence.Tests/MixPracticePersistenceTests.cs` (refactor only)
- `docs/decisions/CTO-DECISIONS-2026-11-15.md` (one row), `specs/012-practice-tempo/tasks.md` (ticks)

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true
  Passed! Failed 0, Passed 1706, Skipped 2     StemStudio.Api line 98.97 %  branch 92.06 %; Contracts 100 % / 100 %
  MixMapper.cs 100 % / 100 %, MixValidator.cs 100 % / 100 %
dotnet test tests/StemStudio.Domain.Tests -c Release -p:CollectCoverage=true       793 passed, Domain 98.98 % / 94.46 %
dotnet test tests/StemStudio.Persistence.Tests -c Release -p:CollectCoverage=true  120 passed, Persistence 99.01 % / 95 %
```

The 1.21.0-era tests that round-trip `MixSettings` / `Mix` examples (`PracticeContractTests`, `BackendContractTests.StemsAndMixes`,
`ClaudeKeyContractTests`) pass unchanged.

## Quality checks

```text
dotnet format <Domain|Persistence|Api and their test projects> --verify-no-changes   exit 0 (all six)
uv run --project tools/quality python tools/quality/run_all.py
  quality gate: 2227 files, 17527 units, 252 clones, 3 exemptions, 0 errors, 817 warnings -> exit 0
  (817 = the Phase 0 baseline; no warning in a file this lane touched)
```

## Dependencies added

None.

## Docs impact

Technical manual / API reference: `practice` on mixes (ranges, defaults, never applied to exports, `PUT` without
`practice` resets it). No setup change.

## Open issues / requests

- **frontend**: `MixerState.ToSettings` must include the mixer's `Practice` for Save and for the agent's
  `mixer_get_state` (`src/StemStudio.Web/Agent/MixerUiTools.cs` returns `State.ToSettings(...)`); otherwise a save or the
  agent's `update_mix` / `save_mix_as` resets the practice tempo to the defaults.
- **ai-agent**: see `backend-012-polish.md`.

## Next owner

backend (T063 polish), then qa / CTO.
