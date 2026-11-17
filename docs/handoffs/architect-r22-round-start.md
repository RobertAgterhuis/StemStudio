# Hand-off: architect, 022+023 round start (2026-11-17)

Branch `022-023-round` (worktree `.claude/worktrees/r22`). Not pushed, not merged to `main`.

## Done

1. `main` (e8d6a5d8, 013 library care) merged (`--no-ff`). Conflicts kept both sides (pending lists, csproj
   links, `ContractJson`, `PracticeContractTests`, CTO-DECISIONS).
2. 022 re-based onto 013: `merge_full.py --base` 013 (now the default base), backend-api 1.22.0 = 1.12.0 + 022;
   ui-test-ids 1.22.0 = 1.12.0 + 022. `LibraryContractTests.Base`, `LibraryUiTestIdsTests.Base` -> `Contract/013`;
   `LibraryCareContractTests` checks the linked 1.12.0 (`ContractJson.Backend013`). 022 DELTA.md section 4 notes it.
3. `r22-p0-023` merged. 023 re-based onto 022: new block-level three-way
   `specs/023-lyric-writing-assistant/contracts/merge_full.py` (base 1.11.0, ours 1.22.0, theirs the kept Phase 0
   document `backend-api.023-on-1.11.0.openapi.yaml`; `--check` = freshness + superset of 1.22.0 and of the 023
   document). backend-api 1.23.0, ui-test-ids 1.23.0 (= 1.22.0 + 023), tool catalogue 1.2.0 (contains 1.0.0, 1.3.0,
   1.1.0, 1.2.0; 69 tools, 14 approval-always). Current full document link -> 023's; 022's linked as `Contract/022`
   (`ContractJson.Backend022`); `LyricAssistContractTests.BaseDocument` = `Backend022`, `LyricAssistUiTestIdsTests.Base`
   = `Contract/022`. `Backend1110` folded into `Backend012`. 023 DELTA.md section 5 notes it.
4. 023 decision rows 023-P0-1..10 and two round-start rows added to `docs/decisions/CTO-DECISIONS-2026-11-15.md`.
5. Re-check against 013 on `main`: `LibraryGate` is registered unconditionally, so the 022 backfill takes it as a
   required dependency (waits before each batch, re-counts after a maintenance it waited for); 023's assist runner
   pauses during maintenance (023 T028). Updated 022 tasks T032/T033, research R-11, data-model 4, plan flow 6.
   No write-gate marks on 022/023 operations (edits, like renames); the 503 middleware covers new routes.
6. `docs/process/LESSONS.md`: four lessons (integration, frontend, qa).
7. Lane plan `docs/handoffs/cto-r22-lane-plan.md` (6 lanes + wave 2).
8. Skeleton for lane isolation: composition hooks `Api/Library/LibraryRegistration.cs`,
   `Api/LyricAssist/LyricAssistRegistration.cs`, `Web/Library/LibraryUiRegistration.cs`,
   `Web/Components/Lyrics/Assist/LyricAssistUiRegistration.cs` (empty, already called from `ApiComposition`/`Program.cs`);
   022 and 023 pending operation lists moved to `BackendContractTests.Pending022.cs` / `.Pending023.cs`.

## Verification

- `dotnet build -c Release`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
- Tests (Release, coverage line / branch): Api 2471 passed, 2 skipped (98.63 % / 91.29 %); Agent 763 (99.04 % /
  94.19 %); AgentEval 139 (99.83 % / 92.85 %); Web 2364 (99.65 % / 94.03 %). Agent/AgentEval coverage floors met.
- Contract tests: 812 passed. `merge_full.py --check` (022 and 023): superset yes. `check_superset.py` (013): yes.
  `check_output_schema.py`: 0 errors. Redocly 2.78.0: 022 documents valid (11 warnings = 1.12.0's), 023 full + delta
  valid (13 + 2 warnings). ruff clean on both merge scripts.
- `tools/quality/run_all.py`: 0 errors (1093 warnings, pre-existing kinds) -> exit 0.

## Decisions taken (recorded in CTO-DECISIONS)

- 023 merge onto 022 by a block-level three-way merge script instead of a hand merge (repeatable, CI-checkable).
- 022 T081/T082 and the `AgentPageContexts` part of 022 T021 move to frontend-023 (single owner of `Web/Agent/**`).
- EF model snapshot owned by nobody; CTO regenerates 023's migration on the merged head.

## Open for the CTO

- CI/test-all: Redocly lint of the 022/023 documents and both `merge_full.py --check` runs (wave 2, devops).
- `specs/013-library-care/contracts/check_superset.py` stays 013-specific (1.12.0 vs 1.22.0 reports the `Asset`
  growth by design); use the two `merge_full.py --check` runs for 022/023.
- New dependencies/licences: none in this step.
