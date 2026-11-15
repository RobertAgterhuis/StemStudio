# Hand-off: quality-gate / integration

## What was built
- **`sig-exempt: <reason>` exemption** in `tools/quality/sig_check.py`:
  - The marker goes in a comment (`//`, `#`, `/* */`, `@* *@`) on the unit's signature line or on the line directly above it.
  - It waives **only** the `parameters` check of that unit. Unit size and complexity still apply.
  - The reason is mandatory. `sig-exempt:` without a reason is an error (new check `exemption`) and does not exempt the unit.
  - A marker on a unit with 4 or fewer parameters is a warning, so stale markers get removed.
  - A marker two or more lines above the signature is ignored.
  - Every exemption (count, file:line, unit, reason) is printed by `sig_check.py` and `run_all.py`, written to `--json`, and listed in the report under `## Exemptions (n)`.
- **lizard `*global*` pseudo-unit is no longer measured.** lizard puts leftover file-level tokens into it, for example after a C# switch expression. It is not a SIG unit and produced 6 false unit-size warnings (WorkerClient, StemStudioApiClient, SchemaValidator, MixTests, TrackTests).
- **Integration report** `docs/quality/SIG-REPORT.md` (generated, not hand-edited):
  - overall risk profiles and an estimated star rating, labelled as a house estimate and not Sigrid
  - a per-component table: % of LOC in size bands <=15/16-30/>30, CC bands <=5/6-10/>10 and parameter bands <=2/3-4/>4, with stars and clone count
  - the dependency-direction result
  - exemptions and the full clone list with owner and a production flag
  - the top-10 soft warnings by impact: production code first, then excess x weight (complexity 3, size 1, duplication 1, Razor 0.5)
  - all findings, and refactor requests per owner
- New modules:
  - `risk.py`: risk profiles and stars, moved out of `report.py`
  - `components.py`: component map and three-band rows
  - `priorities.py`: impact ranking and remedies
- `Finding` gained `excess: int = 0`, the amount over the limit, which is used for ranking.

Red to green: `a20a7cc` holds the tests and sample only, and collection failed with "1 error during collection" on missing `Exemption`/`find_marker`. The implementation commit follows it.

Simulation of the backend change: I copied `ListLoggerProvider.cs` to the scratchpad, added `// sig-exempt: interface-mandated signature (ILogger.Log)` above `Log<TState>`, and ran:
```
exemption: tests/StemStudio.Api.Tests/TestSupport/ListLoggerProvider.cs:30 ListLogger::Log<TState> - interface-mandated signature (ILogger.Log)
sig_check: 1 files, 5 units, 0 errors, 0 warnings, 1 exemptions
exit 0
```
The test sample `tools/quality/tests/samples/exempt_logger.cs` reproduces the same signature.

## Files changed
- Modified in `tools/quality/`: `sig_check.py`, `common.py`, `duplication.py`, `report.py`, `run_all.py`, `README.md`.
- New in `tools/quality/`: `risk.py`, `components.py`, `priorities.py`.
- Tests:
  - new: `tools/quality/tests/test_exemptions.py`, `test_components.py`, `test_priorities.py` and the sample `samples/exempt_logger.cs`
  - updated: `test_report.py` and `test_run_all.py`
- `docs/quality/SIG-REPORT.md` and this note.

## Tests and coverage
`uv run --directory tools/quality pytest --cov --cov-branch`
```
common.py 100% | components.py 100% | dependency_check.py 100% | duplication.py 100%
priorities.py 100% | razor.py 100% | report.py 100% | risk.py 100% | run_all.py 100%
sig_check.py 100% | sonar_args.py 100%
TOTAL 760 stmts, 138 branches, 0 missed -> 100.00% line and 100% branch (fail_under = 90)
148 passed in 0.84s
```

## Quality checks
- `ruff check .`: All checks passed.
- `ruff format --check .`: 23 files already formatted.
- `mypy` (strict): Success: no issues found in 22 source files.
- `uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md`:
  `quality gate: 342 files, 2461 units, 15 clones, 0 exemptions, 2 errors, 114 warnings -> exit 1`
  - The 2 errors are exactly the expected ones:
    - `tests/StemStudio.Api.Tests/TestSupport/ListLoggerProvider.cs:29` needs the backend marker.
    - `tests/StemStudio.Domain.Tests/Mixes/MixTests.cs:143` is being fixed by backend in parallel.
  - After the backend merge, expect `1 exemptions, 0 errors -> exit 0`.
  - `tools/quality` itself has 0 findings.
- Production code (884 units):
  - size 95.7/4.3/0/0, CC 98.4/1.6/0/0, params 80.1/19.9/0/0, duplication 0.42%
  - estimated 4.8 stars (house heuristic, **not Sigrid**)
  - dependency direction PASS
- Weakest component is unit interfacing:
  - worker/separation: 52.6% of its LOC in 3-4 parameter units
  - worker/jobs: 44.4%
  - Api: 33.5%
- **Clones:** all 15 are in test code, so **0 are in production code**. backend owns 13:
  - BackendContractTests set-up repeated in LoggingTests, MixEndpointsTests, StemEndpointTests and TrackEndpointsTests (5 clones)
  - MixEndpointsTests/StemEndpointTests (1)
  - MixServiceTests (1)
  - JobSyncServiceTests (3)
  - TrackTests/Persistence TestData (1)
  - StemStudioDbContextTests (2)

  frontend owns 2: MixerStateTests/SaveMixTests and SaveMixTests/MixLibraryPageTests.

## Dependencies added
None.

## Open issues / requests
- **backend**:
  - Add `// sig-exempt: interface-mandated signature (ILogger.Log)` above `ListLogger.Log<TState>`, and fix `MixTests.cs:143` with a parameter object or `[MemberData]` record.
  - Top-10 soft items:
    - `WorkerClient::ReadDetailAsync` CC 7
    - `MixValidator::ToDomain` CC 6
  - Test-only refactors:
    - Extract a shared `ApiTestHost`/factory helper inside Api.Tests for the 5 BackendContractTests clones.
    - Extract a builder for the 3 JobSyncServiceTests clones.
- **frontend**, top-10 soft items:
  - `ChannelState::Apply` CC 9 (rank 1)
  - `LoopControls.razor` and `TransportBar.razor` `@code` blocks of 40 and 38 lines: move them to code-behind.
  - long units: `MetronomePanel::Format`, `Mixes::RenameAsync`, `Import::RunImportAsync`
- **beat-analysis**: `detect_meter` CC 7.
- **stem-separation**:
  - `jobs/runner.py process` CC 7
  - `reconcile` CC 6
  - Consider parameter objects in separation/jobs; these have the weakest interfacing profile.
- **devops**: the `quality` CI job is unchanged (one command). The report now has more sections, and the artifact upload path stays the same.
- Stars are a house heuristic (`tools/quality/risk.py`). Sigrid is authoritative.

## Next owner
backend: the marker and the MixTests fix. After that merge, the CTO re-runs `run_all.py --report` and expects exit 0.
