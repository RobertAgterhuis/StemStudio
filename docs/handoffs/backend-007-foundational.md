# Hand-off: backend / 007-foundational (T025-T032)

## What was built

- **Domain/Assets**
  - `Asset`, the library item:
    - state machine queued -> rendering -> ready | failed; retry only from failed
    - progress taken from the worker's 0..1 value, clamped and never lowered
    - files and audio set only when ready; size is the sum of the files
    - error at most 500 characters
  - `AssetValues`: `AssetKind`, `AssetStatus`, `AssetOrigin`, `AssetFile`, `AudioFacts`, `Provenance`.
  - `AssetNaming`:
    - default name "<title> - <contents>", cut to 120 characters, with " (2)", " (3)" suffixes compared
      case-insensitively
    - rename rules: trimmed, 1..120 characters, no control characters
    - download file name: `<>:"/\|?*` and control characters become `_`, at most 150 characters plus the extension
- **Domain/Rendering**
  - `ExportRecipe`, `ExportMode`, `ExportFormat`, `AudioFormat`, `ExportRange`, `ExportMix`.
  - `RenderSource`/`RenderChannel`: the channels of a track in mixer order and the library paths (data-model 4.3
    rule 7).
  - `RenderPlanner`:
    - mode `mix` with `channels = null` uses the audible level of gate rule v2
    - explicit channels keep their fader and pan; `raw` uses gain 1, pan 0, master 1
    - inputs with gain 0 are dropped; an empty plan raises `NothingToExportException` ("Nothing to export: no channel
      is audible")
    - validation errors name `channels[i]`, `channels` or `mix`
    - `ValidateMix` applies the 002 mix rules to an export mix
  - `ExportContents` adds ", m:ss-m:ss" for a range.
  - **All 12 gate cases of `render-vectors.json` 1.0.0 match**: master gain, inputs (id, path, gain, pan, mixer order)
    and contents.
- **Persistence**
  - Migration `20261115152313_AddAssets` adds the `Assets` table:
    - kind and status stored by name
    - `Files`, `Audio` and `Provenance` (with the recipe) as JSON columns that compare by content (`JsonColumn<T>`)
    - indexes on `CreatedAt`, `TrackId` and `Status`. There is no FK, so items outlive their track.
  - `IAssetRepository`/`AssetRepository`: find, filtered and paged list (kind, track, status, case-insensitive
    name `LIKE` with escaped wildcards) with `total` and `totalSizeBytes`, active items, names, add, remove, save.
- **Worker client**
  - `CreateRenderJobAsync`: unset `bitDepth`/`mp3BitrateKbps` are left out; `range` is null for the whole length.
  - `GetRenderJobAsync` (404 -> null), `DownloadRenderFileAsync`, `DeleteRenderJobAsync` (204, and 404 is tolerated).
  - Worker 400/404/422/409 problems become `WorkerRequestException` with the worker detail.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T025 / T026 assets | 380fb00 (build fails: types missing) | a58271e |
| T027 / T028 render planner | 779b031 (build fails: planner missing) | 7d7f97e |
| T029 / T030 persistence + migration | 6efb196 (build fails: repository missing) | df0641d |
| T031 / T032 worker render client | ab22c2e (build fails: members missing) | fa0683d |
| SIG refactor | - | 14cb051 |

## Tests and coverage

```text
StemStudio.Domain.Tests       389 passed   Domain        line 98.46 %  branch 96.57 %
StemStudio.Persistence.Tests   65 passed   Persistence   line 98.16 %  branch 92.85 %
StemStudio.Api.Tests          592 passed   Api           line 99.56 %  branch 97.09 %   Contracts 100 / 100
```

`dotnet ef migrations has-pending-model-changes` reports no changes. A 005 database migrates with an empty `Assets`
table (tested).

## Quality checks

- Build has 0 warnings. `dotnet format --verify-no-changes` is clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 0 warnings after 14cb051.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend (US1: T035/T036).
