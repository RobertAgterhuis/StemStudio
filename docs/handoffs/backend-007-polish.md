# Hand-off: backend / 007-polish (T066) and feature summary

## What was built

- Refactor 7dac03d: `Domain/JobIds` is the shared worker job id rule. It removes the duplication error that
  `run_all.py` reported between `Asset.AssignJob` and `Track.AssignJob`.
- Every public 007 type and member has XML docs (CS1591 is an error).
- Feature summary (see `backend-007-{foundational,us1..us5}.md`):
  - Domain: library items and naming, and the render planner, which matches all 12 gate cases of
    `render-vectors.json`.
  - Persistence: migration `AddAssets` and `AssetRepository`; 005 databases migrate.
  - Worker client: render jobs.
  - Endpoints: raw/mixed/range exports, stem packs, list/get/rename/delete/retry, and file serving with Range, ETag
    and RFC 6266 headers.
  - Asset sync runs in the `JobSyncService` tick.
  - `PendingAssetOperations` and `PendingArtworkOperations` are both empty.

## Red -> green evidence (feature 007)

| Task pair | Red | Green |
|---|---|---|
| T025/T026 | 380fb00 | a58271e |
| T027/T028 | 779b031 | 7d7f97e |
| T029/T030 | 6efb196 | df0641d |
| T031/T032 | ab22c2e | fa0683d |
| T035/T036 | 7f1f4c9 | d43c344 |
| T042/T043 | 43da4cf (green on its first run, see us2) | d43c344 |
| T049/T050 | de4bfcb | 1deb50e |
| T054/T055 | eb057f3 | be2d448 |
| T059/T060 | 22fb9f0 | 52de322 |
| refactors | - | 14cb051, 7dac03d |

## Tests and coverage

`dotnet build StemStudio.slnx -c Release` gives 0 warnings and 0 errors. `dotnet format --verify-no-changes` is clean.
`dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true` exits 0 with every gate on:

```text
StemStudio.Domain.Tests       389 passed   Domain        line 98.63 %  branch 96.39 %
StemStudio.Persistence.Tests   66 passed   Persistence   line 98.35 %  branch 92.85 %
StemStudio.Web.Tests          332 passed   Web (untouched) line 99.88 % branch 95.12 %
StemStudio.Api.Tests          672 passed   Api           line 99.46 %  branch 95.75 %
                                           Contracts     line 100 %    branch 100 %
```

`dotnet ef migrations has-pending-model-changes` reports no changes.

## Quality checks

- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 163 files, 646 units, **0 errors, 0 warnings**.
- `run_all.py`: 722 files, 5434 units, 50 clones, 1 error, 208 warnings. The only error is
  `e2e/support/preferences.ts:12` duplicating `src/web-interop/src/types.ts:8`. Neither file is backend code, and both
  are unchanged since d2e06a7.

## Dependencies added

None (feature 007 adds no NuGet package).

## Deviations / requests

- **architect**: list `source-unavailable` under `ExportConflict` of `createAudioExport` and `createStemPack`. A worker
  400/404 at creation (a missing stem file) is answered with it.
- **devops**: `StemStudio:MinFreeDiskBytes` (default 1 GiB) is an API setting. There is no `AssetSyncIntervalMs`:
  assets are polled in the existing `JobSyncService` tick (`JobPollIntervalMs`).
- **qa / quality-gate**: the e2e/web-interop duplication error above.

## Next owner

CTO (integration wave); frontend 007 tasks can use every asset route.
