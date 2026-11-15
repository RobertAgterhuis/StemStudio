# Hand-off: backend / 008-us1 (T041-T042)

## What was built

- **`GET /api/scores/options?trackId&channel | ?assetId`** (`Scores/ScoreOptionsService.cs`):
  - It returns the suggested and allowed profiles, `drumsRequireSplit`, `gridStatus`, and the default quality:
    `accurate` on a GPU worker, else `fast`. The device and the piano model state come from the worker `/health`;
    `WorkerHealth.Transcription` is now parsed. An unreachable worker counts as a CPU without the piano model.
  - It gives one estimate per profile, two for piano.
  - It returns the limit texts and the piano attribution.
  - Problems: 404 for an unknown track or item; 409 `track-not-ready`; 422 for neither or both source forms, a
    channel the track does not have, or a non-ready/non-audio item.
- **Estimates** (`Scores/ScoreEstimates.cs`): `duration * factor(engine, device) + overhead`, plus the download time
  while the piano model is not cached. The factors live in `StemStudio:ScoreEstimates`
  (`StemStudioApiOptions.ScoreEstimates`; defaults from data-model 4.5).
- **`POST /api/assets/scores`** (`ScoreService`, `ScorePlanner`, `ScoreSourceResolver`, `ScoreSubmitter`):
  - It resolves the source, applies the domain recipe rules and resolves the grid (tempo, then the reliable track
    grid shifted, else 422 `tempo-required`), then runs the disk guard.
  - The default name is "<track title> - <channel> (sheet music)". The provenance has origin `transcription`,
    contents "<channel>, sheet music", `scoreRecipe`, and `recipe: null`.
  - The worker request carries the input path (the channel path or `assets/<id>/audio.<ext>`), the drum inputs,
    the range, the grid, the resolution, the key and titles (item name, "<title> - <artist>", instrument name).
  - It answers 202 with `Location`. A worker 4xx gives 409 `source-unavailable`; an unreachable worker gives 503.
- **Sync and files**:
  - `AssetSynchronizer` switches on the item kind. Score items poll their transcription job.
  - `ScoreJobApplier` copies `pdf`, `musicxml` and `midi` to `assets/<id>/score.{pdf,musicxml,mid}`, compares
    every SHA-256, validates the summary with `ScoreFactsRules` and fills `Asset.Score` (summary + device).
  - The worker job is deleted after the item finishes. Retry re-plans the stored score recipe, and delete removes
    an active transcription job.
  - `getAssetFile` serves the three roles: attachment by default, `inline=true` for the PDF preview.
  - `AssetFileStore` accepts `.pdf`, `.musicxml` and `.mid`.
- `PendingScoreOperations` is **empty**.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T041/T042 | c6b0877 | b5da236 |

## Tests and coverage

```text
StemStudio.Api.Tests   894 passed at b5da236   Api line 98.15 %  branch 90.37 %
```

The uncovered branches at b5da236 were the US2-US5 paths, which their own tests cover (see HO:polish for the final
numbers).

## Quality checks

Build: 0 warnings. `dotnet format`: clean. `sig_check`: 0 errors. The 7 unit-size/CC warnings at b5da236 were fixed
in 80943de.

## Dependencies added

None.

## Open issues / requests

- **architect**: `createScore` does not declare 503, but the backend answers 503 when the worker cannot be reached
  at creation, like the 007 exports, and stores nothing. Please add the `WorkerUnavailable` response.
- **architect**: `createScore` answers 409 `source-unavailable` when the worker rejects the input (400/404), as
  007 does. `ScoreConflict` does not list that type.

## Next owner

backend (US2..US5); frontend T043/T044.
