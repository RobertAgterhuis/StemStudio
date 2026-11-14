# Hand-off: backend / foundational

## What was built

- **Domain** (`src/StemStudio.Domain`, no infrastructure references):
  - `SourceKey` (+ `SourceKeyParseResult`, internal `SourceUrlParser`, `SourceIds`, `QueryString`,
    `SourceUrlMessages`): parses every form in `source-url-vectors.json`, derives `youtube:`/`spotify:`/`upload:`
    keys and the canonical URL; every rejection carries an actionable message (SC-007).
  - `Track` aggregate with the forward-only state machine (skips allowed, progress never decreases and is clamped,
    `Complete` forces 100, `Error` iff failed, retry only from failed, field limits 500/500/200/1000),
    `TrackCompletionRules` (duration `(0, 900]`, model -> stem set `htdemucs_6s`/`fake` = 6, `htdemucs` = 4,
    unique stems, sizes >= 0), `Stem`, `BeatGrid` (bpm 0..400, beats/bar 1..12, confidence 0..1, strictly
    ascending beats, downbeats subset), `StemCatalog`.
  - `Mix` aggregate, `MixSettings` validation with JSON-path field keys (`name`, `masterVolume`,
    `channels[i].stem|volume|pan`, `metronome.*`, `loop.start|end`, `trackId`), `ChannelSettings`,
    `MetronomeSettings` (defaults from the beat grid), `LoopRegion`, `MixNaming` (normalise, "(copy)",
    "(copy N)", base truncated to keep <= 100 characters).
  - `DomainError`, `DomainValidationException`, internal `ValidationErrors`/`Bounds`.
- **Persistence** (`src/StemStudio.Persistence`): `StemStudioDbContext`, `TrackConfiguration`,
  `MixConfiguration`, `UtcDateTimeConverter` (UTC `DateTime` storage, Kind restored on read: the data-model
  SQLite note), `TrackRepository`/`MixRepository` (+ interfaces, `MixSummary`), `DuplicateKeyException`
  (SQLite extended codes 2067/1555 translated in `UnitOfWork`), `AddStemStudioPersistence`,
  `DesignTimeDbContextFactory`, migration `Migrations/20261114215330_Initial` (`dotnet ef migrations add Initial
  -p src/StemStudio.Persistence -s src/StemStudio.Api -o Migrations`). Unique indexes `IX_Tracks_SourceKey`,
  `IX_Mixes_TrackId_NormalizedName`; FK Mix -> Track `Restrict`; enums as strings; indexes on `UpdatedAt`.
- **Api foundations** (`src/StemStudio.Api`): `StemStudioApiOptions` (`DataDir`, `WorkerBaseUrl`, `WebOrigin`,
  `JobPollIntervalMs`); `Worker/` typed `WorkerClient` (+ `IWorkerClient`, `WorkerJob`, `WorkerJobResult`,
  `WorkerUnavailableException`, `WorkerRequestException`, `AddWorkerClient` with the standard resilience
  handler: GET-only retries 3x from 200 ms, 2 min attempt / 5 min total timeout, circuit breaker; uploads are
  streamed from disk as part `file`); `Storage/` `StemFileStore` (path from GUID + enum only, temp file then
  rename), `UploadStore` (`uploads/<sha256><ext>`, streamed while hashing via `HashingCopy`, 200 MiB limit,
  empty file rejected), `DataDirectory`.

## Design notes / deviations

- **Complex types instead of owned types.** JSON columns use EF Core 10 `ComplexProperty(...).ToJson()` /
  `ComplexCollection(...).ToJson()`; Microsoft now recommends complex types over owned entities for value
  objects (value semantics, no hidden identity):
  https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew#complex-types . The resulting schema
  (one JSON text column each for `Stems`, `BeatGrid`, `Channels`, `Metronome`, `Loop`) matches data-model.md.
  Complex collections must be `IList<T>`, so `Track._stems`/`Mix._channels` are mapped as fields and exposed
  read-only.
- **Security fix (CTO finding, MEDIUM)**: all validation regexes are anchored with `\A...\z` (in .NET `$` also
  matches before a trailing `\n`, so `watch?v=dQw4w9WgXcQ%0A` was accepted). Applies to `SourceIds`
  (YouTube/Spotify/SHA-256), `Track` job id, `WorkerClient` job id and `UploadStore` file names. Rejection
  tests for trailing LF, CRLF, tab and embedded NUL (via `%0A`, `%0D%0A`, `%09`, `%00` URLs and direct ids).
  Red evidence: the new tests fail 4/76 against the `$` version (run before commit c122f62), green after.

## Red -> green evidence (commit hashes)

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T023 / T024 SourceKey | 9cdc5ed | 1475479 (+ c122f62 regex anchor fix) |
| T025 / T026 Track, Stem, BeatGrid | 784de2a | 5d40a5a |
| T027 / T028 Mix, settings, naming | 871b516 | 639bb08 |
| T029 / T030 DbContext, repositories, migration | 1a7257f | ef082c3 |
| T031 / T032 WorkerClient | e8b2d8e | e6c7cd2 (+ coverage tests in the following commit) |
| T033 / T034 stem and upload stores | 7fdfc76 | 398ff65 |

## Files changed

- `src/StemStudio.Domain/{DomainError,DomainValidationException,ValidationErrors,Text}.cs`
- `src/StemStudio.Domain/Tracks/{SourceKey,SourceKeyParseResult,SourceIds,SourceUrlParser,SourceUrlMessages,QueryString,Track,TrackInputs,TrackCompletionRules,Stem,BeatGrid,StemCatalog}.cs`
- `src/StemStudio.Domain/Mixes/{Mix,MixSettings,ChannelSettings,MetronomeSettings,LoopRegion,MixNaming}.cs`
- `src/StemStudio.Persistence/{StemStudioDbContext,PersistenceServiceCollectionExtensions,DesignTimeDbContextFactory}.cs`,
  `Configurations/*`, `Repositories/*`, `Migrations/*`
- `src/StemStudio.Api/StemStudioApiOptions.cs`, `src/StemStudio.Api/Worker/*`, `src/StemStudio.Api/Storage/*`
- `tests/StemStudio.Domain.Tests/{StemStudio.Domain.Tests.csproj,Tracks/*,Mixes/*}`
- `tests/StemStudio.Persistence.Tests/{StemStudioDbContextTests,SqliteTestDatabase,TestData}.cs`
- `tests/StemStudio.Api.Tests/{StemStudio.Api.Tests.csproj,TestSupport/*,Worker/*,Storage/*}`
  (worker contract examples copied to `Worker/Examples/*.json`)
- `specs/001-stemstudio-mvp/tasks.md` (T023-T034 ticked)

## Tests and coverage

`dotnet test tests/StemStudio.<X>.Tests -p:CollectCoverage=true` (gate: 90 % line AND branch, built into
`Directory.Build.props`):

| Project | Passed / Failed | Line | Branch | Method |
|---|---|---|---|---|
| StemStudio.Domain.Tests | 173 / 0 | 97.73 % | 97.72 % | 96.96 % |
| StemStudio.Persistence.Tests | 21 / 0 | 99.02 % | 100 % | 97.29 % |
| StemStudio.Api.Tests (Api) | 61 / 0 | 100 % | 92.85 % | 100 % |
| StemStudio.Api.Tests (Contracts) | (same run) | 90 % | 100 % | 83.33 % |

Persistence tests run the real migration on in-memory SQLite, assert `HasPendingModelChanges() == false`, and
include the file-based round trip (track + mix written, connection closed, read back through a new context with
every setting equal, G2). The 200 MiB boundary is tested on `HashingCopy` with a length-reporting zero stream
(209,715,200 accepted, 209,715,201 rejected) so no 200 MB file touches the disk.

## Quality checks

- `dotnet build StemStudio.slnx --no-incremental`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean (exit 0).
- `tools/quality/sig_check.py` is not present in this worktree (quality-gate owns it); units were kept
  <= 15 LOC, CC <= 5, <= 4 parameters by construction (parameter objects: `TrackProgress`, `TrackCompletion`,
  `UploadedFile`, `WorkerUpload`, `MixSettings`).

## Dependencies added

None new. `YamlDotNet` 18.1.0 (MIT, already in `Directory.Packages.props`, T003) is now referenced by
`StemStudio.Api.Tests` for the upcoming contract tests.

## Open issues / requests

1. `ContractLimits` has no title/artist/stage/error limits; the Domain defines them (`Track.Max*Length`) from the
   OpenAPI file. Optional for the architect: add them to Contracts so Web can use them.
2. Contracts branch/line is exactly 90 % line; Api tests will cover more of `ApiRoutes` in US1.

## Next owner

backend (self): US1 (T069-T075). CTO: review of this checkpoint.
