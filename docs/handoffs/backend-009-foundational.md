# Hand-off: backend / 009-foundational (T024-T031)

## What was built

- **Domain/Styles** (data-model 3.1, ADR-018 D7/D8):
  - `Style` (preset/user; presets read-only via `StyleReadOnlyException`), `StyleFields` (value equality incl.
    tags, `Normalized()`), `StyleRules` (name 1-60, sub-genre 40, 8 tags of 1-30, description 300),
    `StyleSnapshot`, `StyleFilter` (kind, family, mood, instrument, `q`; presets first then name),
    `StyleNaming` ("(copy)", "(copy 2)"; shares `CopyNames` with `MixNaming`).
  - `StyleTextGuard`: EN/NL imitation phrases (`imitation-phrases.v1.txt`, `*` = word prefix), curated
    deny-list (`artist-denylist.v1.txt`, 237 names; common-word names such as Queen/Yes/Oasis left out),
    library artists (genre words from `genre-words.v1.txt`, blank and one-letter names skipped);
    case-, diacritics- and punctuation-insensitive whole-word matching.
  - `StylePromptBuilder`: caption "{family} {sub-genre}, {tempo feel}, {instruments}, {moods}{,
    instrumental}{, user description}", trimmed to 512 at a word boundary (vectors = contract examples
    `QueuedGeneration`/`FailedGeneration`); strength 0.85/0.70/0.55/0.40/0.25; 8 steps, shift 3.0.
  - `PresetCatalog` + embedded `presets.v1.json` (24 presets of research R-04, 13 families; jazz-trio id
    and fields = contract example `PresetStyle`).
- **Domain/Generations** (data-model 2.4/3.2): `Generation` (restyle; queued -> generator states ->
  completed/failed/cancelled; progress clamped and monotonic within a status; warnings merged, max 5;
  retry = new generation with new seeds; `DetachSourceTrack` keeps the title snapshot; take library/import
  bookkeeping), `GenerationTake` (always `AiGenerated`), `RestyleDetails`, enums incl. 010's kinds,
  `WaitingForStems` and error codes. `Tracks/TrackOrigin` + `Track.Origin`/`MarkGenerated`.
- **Persistence**: `StyleConfiguration` (tags JSON, unique `NormalizedName`), `GenerationConfiguration`
  (details/seeds/takes/warnings/device/error as JSON, indexes `SourceTrackId`, `Status`, `CreatedAt`),
  `Tracks.Origin` JSON; `StyleRepository` (incl. idempotent `SeedPresetsAsync`: upsert by id, retired
  presets deleted, a user style holding a preset name is renamed "(copy)"), `GenerationRepository`;
  migration **`AddGenerativeStyles`** (dotnet-ef 10.0.15; `has-pending-model-changes`: none).
- **Api/Generator**: `IGeneratorClient`/`GeneratorClient` (health within 2 s; multipart restyle create
  with the `params` part and `stem` parts, never retried; get/cancel with 404 -> null; take download with
  SHA-256 check; idempotent delete; problem `code` -> `GeneratorProblemCode`), typed exceptions
  (`GeneratorUnavailableException`, `GeneratorNotInstalledException`, `GeneratorIntegrityException`,
  `GeneratorRequestException`), `AddGeneratorClient` (registered in the composition root);
  `ServiceResilience` now shared by the worker and generator registrations. Options
  `GeneratorBaseUrl` (unset = not installed), `GeneratorCreateTimeout` (5 min), `GeneratorPollTimeout`
  (15 s), `GenerationPollInterval` (2 s).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T024/T025 | f3983c0 | 2b318d2 |
| T026/T027 | 8eb557b | e691fff |
| T028/T029 | a0a3221 | fa8eb69 |
| T030/T031 | 7ed0613 | 8227b38 (+ refactor 5f68cae) |

## Tests and coverage

```text
dotnet test tests/StemStudio.Domain.Tests -p:CollectCoverage=true       659 passed   Domain       line 98.88 %  branch 95.03 %
dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true   87 passed   Persistence  line 98.55 %  branch 94.11 %
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true --filter "FullyQualifiedName!~NewerContracts_ContainTheOlderOnes"
                                                                       1017 passed   Api          line 98.98 %  branch 94.5 %
                                                                                     Contracts    line 100 %    branch 100 %
```

Full Api run: 1027 passed, **2 failed** - `GenerationContractTests.NewerContracts_ContainTheOlderOnesUnchangedExceptTheNamedEntries`
(`paths`, `components/responses`). Pre-existing on the base 6accc47 and not caused by this work: the test compares
only spec files; `be286b4` (CTO reconcile of the 006/008 contracts: lyrics search/align 409s, `createScore` 503,
`LyricsConflict`, `ScoreConflict`) landed after the 009/010 documents were derived from the 008 document, so the
009/010 backend documents lack those changes. See requests.

## Quality checks

- `dotnet build StemStudio.slnx`: 0 warnings, 0 errors; `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 5 warnings (2 of them pre-existing; new:
  `Generation.ApplyReport` 16 NLOC / CCN 6, `GeneratorClient.ThrowOnFailureAsync` 16 NLOC; to be cut at polish).

## Dependencies added

None.

## Deviations

- Folders/namespaces `Domain/Generations`, `Api/Generator` (not `Generation`): a type `Generation` inside a
  namespace `...Generation` would be shadowed by the namespace in the Api.
- Takes are a JSON column of `Generations` (owned by the row) instead of a `GenerationTakes` table.
- Unique style names use an upper-case `NormalizedName` column with a unique index (same pattern as mixes)
  instead of `NOCASE` collation.

## Open issues / requests

- **architect**: port `be286b4` (006/008 reconcile) into `specs/009-style-transfer/contracts/backend-api.openapi.yaml`
  and `specs/010-lyrics-to-song/contracts/backend-api.openapi.yaml` (paths `/api/tracks/{trackId}/lyrics/search`,
  `/lyrics/align`, `/lyrics/align/cancel`, `/api/assets/scores`; responses `LyricsConflict`, `ScoreConflict`) so
  `NewerContracts_ContainTheOlderOnesUnchangedExceptTheNamedEntries` passes again.

## Next owner

backend (009 US1: T037-T042).
