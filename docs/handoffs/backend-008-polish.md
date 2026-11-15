# Hand-off: backend / 008-polish (T074) and feature summary

## What was built

- Refactor 80943de:
  - `Assets/AssetJobState` applies render and transcription job states and stores completed results for
    `AssetJobApplier` and `ScoreJobApplier`. This removes the three `run_all.py` duplication errors between the two
    appliers and between `ExportService`/`ScoreService`.
  - Every score unit is within 15 LOC and CC 5.
  - New tests cover an unreachable worker in the options, an invalid name and a score retry after the track is gone.
- Every public 008 type and member has XML docs.
- Feature summary (see `backend-008-{foundational,us1..us5}.md`):
  - Domain: `Domain/Scores` (profiles, key names, recipe rules, facts, tempo grid) and score items on `Asset`.
  - Persistence: migration `AddScoreFacts`.
  - Worker client: transcription jobs and `Health.transcription`.
  - Options and creation endpoints; score sync, files, retry and delete through the 007 asset flow.
  - `PendingScoreOperations` is **empty**, and so is `PendingLyricsOperations` (006).

## Red -> green evidence (feature 008)

| Task pair | Red | Green |
|---|---|---|
| T030/T031 | 9ff716b | 3a3401e |
| T032/T033 | b4e34ab | 5719f9a |
| T034/T035 | 426ea31 | b3fcc37 |
| T036/T037 | bece563 | 7edddc6 |
| T041/T042 | c6b0877 | b5da236 |
| T046/T047 | 2a3a8a5 (green first run) | b5da236 |
| T052/T053 | c625b9a | fe420e5 |
| T061/T062 | 487fc2c (green first run) | b5da236 |
| T066/T067 | aa78d9f (green first run) | b5da236 |
| T074 refactor | - | 80943de |

## Tests and coverage

`dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
`dotnet test tests/StemStudio.<X>.Tests -c Release -p:CollectCoverage=true`:

```text
StemStudio.Domain.Tests       555 passed   Domain        line 99.05 %  branch 96.09 %
StemStudio.Persistence.Tests   77 passed   Persistence   line 98.59 %  branch 92.85 %
StemStudio.Api.Tests          923 passed   Api           line 99.49 %  branch 95.26 %
                                           Contracts     line 100 %    branch 100 %
StemStudio.Web.Tests          332 passed   Web (untouched) line 99.88 % branch 95.12 %
```

`dotnet ef migrations has-pending-model-changes` reports no changes. The migrations run in order: `AddTrackLyrics`
(006) and then `AddScoreFacts` (008). Existing 005/007 databases migrate; tests cover both steps.

## Quality checks

- `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 199 files, 871 units, **0 errors, 0 warnings**.
- `tools/quality/run_all.py`: 922 files, 7415 units, 78 clones, **0 errors**, 264 warnings, exit 0.

## Dependencies added

None (features 006 and 008 add no NuGet package).

## Open issues / requests (consolidated)

- **architect**: `createScore` should declare 503 `WorkerUnavailable` and list the 409 type `source-unavailable`
  (worker 400/404 at creation), as the 007 exports do.
- **CTO**: refine the `StemStudio:ScoreEstimates` defaults on the reference machine (T080). The 515 s in the contract
  example does not follow from the data-model formula.
- The rest of the 006 requests are in `backend-006-polish.md`.

## Next owner

CTO (integration wave 006:T064+, 008:T079+).
