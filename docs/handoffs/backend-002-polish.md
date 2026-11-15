# Hand-off: backend / 002-polish (T083) and feature 002 summary

## What was built (whole feature, backend lane)

| Checkpoint | Tasks | Note |
|---|---|---|
| foundational | T024-T033 | `docs/handoffs/backend-002-foundational.md` |
| us1 | T051-T054 | `docs/handoffs/backend-002-us1.md` |
| us3 | T067-T068 | `docs/handoffs/backend-002-us3.md` |
| us4 | T072-T073 | `docs/handoffs/backend-002-us4.md` |
| us5 | T077-T078 | `docs/handoffs/backend-002-us5.md` |
| polish | T083 | this note |

Polish:
- Production units: none of the backend `src` files has a SIG warning left. The units were split
  during US3 (`DetailSynchronizer`, `DetailJobApplier`, `DetailService`, `TrackJobApplier.Finish`,
  `ApiComposition.AddDetailServices`).
- Duplication: `FaderSettings` (shared by stem/detail channel settings), `WorkerClient.DownloadAsync`,
  `AtomicFile.SaveAsync`, `DataDirectory.TrackDirectory`. In the tests: `CorsAssert`,
  `SqliteTestDatabase.SeedAsync` and the shared worker client factory.
- XML docs: every public type and member in Domain/Persistence/Api is documented. CS1591 is an error via
  `TreatWarningsAsErrors`, and the build has 0 warnings.
- All backend contract 1.2.0 operations are served. `PendingDetailOperations` is empty, and the
  served-operations test matches the contract exactly.

## Red -> green evidence (all pairs)

| Task pair | Red | Green |
|---|---|---|
| T024/T025 | 19383b8 | 97f1d51 |
| T026/T027 | 10c7c56 | f6db42f |
| T028/T029 | 6fe873d | 0a960de |
| T030/T031 | 7610956 | b2a4b44 |
| T032/T033 | 77a84a4 | eda83ae |
| SIG refactor | - | f9df59b |
| T051/T052 | a2d1c8b | b050cef |
| T053/T054 | 709259e | 37e7a41 |
| T067/T068 | 1c20f5f | ae9c0dd |
| T072/T073 | 8feb2e8 (green at once, no production change needed) | - |
| T077/T078 | 9920a95 | fe6598d |
| T083 | - | 6b116d4 |

## Tests and coverage (final, `dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true`)

```text
Domain.Tests       239 passed   StemStudio.Domain       line 98.46 %  branch 97.54 %
Persistence.Tests   40 passed   StemStudio.Persistence  line 98.59 %  branch 100 %
Api.Tests          369 passed   StemStudio.Api          line 99.6 %   branch 97.92 %
                                StemStudio.Contracts    line 100 %    branch 100 %
Web.Tests          198 passed   (frontend component, untouched)
coverlet gates (90 % line+branch per component): all pass
```

## Quality checks

```text
dotnet build StemStudio.slnx -c Release                   0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes          exit 0
uv run --project tools/quality python tools/quality/run_all.py
  398 files, 2910 units, 22 clones, 1 exemptions, 0 errors, 121 warnings -> exit 0
  backend src: 0 warnings; backend tests: 16 remaining warnings in detail test files (unit size 16-19 LOC
  in a few Arrange/Assert-heavy tests, small 7-line clones), the same kind as the 001 baseline
```

## Dependencies added

None (no new NuGet packages; `dotnet-ef` 10.0.15 was already a local tool).

## Deviations

- `DetailStem.Order` is not stored; the order comes from the list, normalised with `DetailCatalog` on
  completion.
- `Tracks.DetailPhase` uses a JSON value converter instead of an EF complex type. The column is the same,
  and `ListDetailActiveAsync` filters the status in memory.
- `IWorkerClient.DownloadDetailStemAsync(WorkerDetailStemRef, Stream, CancellationToken)` takes a parameter
  record to stay within SIG's 4-parameter limit.
- `startDetail` never calls the worker. Submission always goes through the background
  `DetailSynchronizer`, as the contract allows ("stays queued and the background sync submits it later").
  It shows up within one poll interval (~1 s).
- The generated `AddMixDetailSettings` default for `DetailChannels` was `{}`; it was corrected to `[]`.

## Open issues / requests

- qa: a freshly imported track has `detail.status = queued`. `DELETE` answers 409 until the phase is
  cancelled or finished (FR-016), so E2E flows that delete right after import must cancel the detail phase
  first.
- The cancel endpoint and the sync loop can both write `Tracks.DetailPhase`. The last writer wins, and the
  next poll converges on the worker's state. No concurrency token was added (single local user).

## Next owner

CTO (integration wave T089-T093).
