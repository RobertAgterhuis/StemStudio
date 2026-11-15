# Hand-off: backend / 008-foundational (T030-T037)

## What was built

- **Domain** (`Domain/Scores/*`):
  - `ProfileCatalog`, data-model 2.1:
    - suggestion per channel: `vocals*`, `bass`, `guitar*`, `piano`, `piano.*`/`other*` -> keys, `drums`;
      `drums.<piece>` -> none;
    - allowed profiles: pitched for pitched channels and library items, drums only for `drums`;
    - default resolution (vocals 1/8, else 1/16), instrument names, the limit texts of the contract examples,
      the engine per profile and quality, and the piano attribution.
  - `KeyNames`: the 30 music21 key names, generated from two tonic lists so there is no clone of
    `ContractLimits.KeyNames`.
  - `ScoreRecipeRules`, data-model 4.2:
    - defaults: suggested profile (the library-item suggestion comes from the caller), piano quality `accurate`
      on a GPU worker else `fast`, null quality for the other profiles, resolution per profile;
    - 422 fields: `profile`, `keyOverride`, `tempo.*`, `range` (>= 2 s, inside the source);
    - 409 `ScoreConflict.DrumsNotSplit`;
    - text helpers "<label> (sheet music)" and "<label>, sheet music".
  - `ScoreFacts` with `ScoreFactsRules`: bpm, n/4 time signature, counts >= 1, tonic and confidence, at most 8
    warnings, text lengths.
  - `TempoGridResolver`, data-model 4.3: a manual tempo wins, else a reliable grid shifted by
    `Offset(assetRangeStart, scoreRangeStart)`, keeping negative beats; else null, which means `tempo-required`.
    It also gives the grid status.
  - Assets: `Asset.CreateScore`, `CompleteScore` and `Score`; `Provenance.ScoreRecipe`; `AssetOrigin.Transcription`.
    `Provenance.Recipe` is now nullable (the architect's `ProvenanceDto` change): `Asset.Create` requires it and
    `CreateScore` requires `ScoreRecipe`. The 007 readers use `Recipe!` only on audio paths.
- **Persistence**: migration `AddScoreFacts` adds the nullable JSON column `Assets.Score`. The score recipe rides
  in the `Provenance` JSON, so 006/007 databases load with `Score = null` and `ScoreRecipe = null`.
- **Worker client** (`WorkerClient.Transcription.cs`, `WorkerTranscriptionJob.cs`):
  - create: never retried; the grid is sent as `beat-grid` or `manual` by its runtime type;
  - get: 404 -> null; the files and the summary are read into the domain `KeyInfo`, `ScoreWarning` and enums;
    engines and warning codes are kebab-case on the wire;
  - file download per role: `pdf`, `musicxml`, `midi`; any other role is rejected without a request;
  - idempotent delete;
  - fake worker client support.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T030/T031 | 9ff716b | 3a3401e |
| T032/T033 | b4e34ab | 5719f9a |
| T034/T035 | 426ea31 | b3fcc37 |
| T036/T037 | bece563 | 7edddc6 |

## Tests and coverage

```text
StemStudio.Domain.Tests       555 passed   Domain       line 99.05 %  branch 96.09 %
StemStudio.Persistence.Tests   77 passed   Persistence  line 98.59 %  branch 92.85 %
StemStudio.Api.Tests          872 passed   (Api coverage reported at HO:us1)
```

`dotnet ef migrations has-pending-model-changes` reports no changes. Build: 0 warnings. `dotnet format`: clean.
`sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

None yet.

## Next owner

backend (008 US1: T041/T042).
