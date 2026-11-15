# Hand-off: backend / 002-foundational (T024-T033)

## What was built

- **Domain detail phase** (`src/StemStudio.Domain/Tracks/Detail/`, `Track.Detail.cs`): immutable
  `DetailPhase` snapshot (status, stage, current parent, progress, error, worker detail job id,
  parents, requested/updated at), `ParentOutcome`, `DetailStem`, `DetailProgress`,
  `DetailPhaseStatus`, `ParentOutcomeKind`, `DetailCatalog` (parents, allowed names per parent in
  display order, `rest` last) and `DetailCompletionRules`. `Track` gains `DetailPhase` (null = none),
  `IsDetailActive`, `CanStartDetail`, `CanDelete`, `RequestDetail`, `AssignDetailJob`,
  `ApplyDetailProgress`, `CompleteDetail`, `FailDetail`, `CancelDetail` (data-model 4.1: forward only,
  progress never decreases, error iff failed, restart only from none/failed/cancelled on a completed
  track; detail changes never touch `Status`, `Stems` or `Progress`; they do bump `Track.UpdatedAt`,
  as in the contract examples).
- **Domain mix detail** (`Mixes/DetailChannelSettings.cs`, `MixDetailRules.cs`, `FaderSettings.cs`):
  `MixSettings.Expanded` / `DetailChannels` (default empty) validated against the split parents of a
  *completed* detail phase (paths `expanded[i]`, `detailChannels[i].parent|stem|volume|pan`); `Mix`
  stores, returns and duplicates both. `FaderSettings` is the shared base record of
  `ChannelSettings` and `DetailChannelSettings` (SIG duplication).
- **Persistence**: `Tracks.DetailPhase` nullable JSON text via `DetailPhaseJson` (value converter +
  content comparer); `Mixes.Expanded` (primitive collection) and `Mixes.DetailChannels` (complex
  collection, JSON). Migrations `20261115091546_AddDetailPhase` and
  `20261115091608_AddMixDetailSettings` (generated with the `dotnet-ef` 10.0.15 local tool; the
  generated `defaultValue: "{}"` of `DetailChannels` corrected to `"[]"`, data-model 3.2).
  `ITrackRepository.ListDetailActiveAsync`.
- **Worker client** (`Worker/WorkerClient.Detail.cs`, `WorkerDetailJob.cs`, `WorkerDetailResult.cs`):
  create (200/202), get (404 -> null), cancel, result, detail stem download; names checked against
  the enums before any request; kebab-case outcome enum.
- **Storage**: `IDetailStemFileStore` / `DetailStemFileStore` (`tracks/<id>/detail/<parent>/<name>.flac`,
  temp + rename via the shared `AtomicFile.SaveAsync`, `DeleteAll`), registered as singleton.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T024 / T025 detail phase | 19383b8 | 97f1d51 |
| T026 / T027 mix detail | 10c7c56 | f6db42f |
| T028 / T029 persistence + migrations | 6fe873d | 0a960de |
| T030 / T031 worker detail client | 7610956 | b2a4b44 |
| T032 / T033 detail stem store | 77a84a4 | eda83ae |
| SIG refactor (duplication) | - | f9df59b |

Red commits fail at compile time (the tested members did not exist yet).

## Files changed

- `src/StemStudio.Domain/Tracks/Detail/*.cs` (new), `Tracks/Track.Detail.cs` (new),
  `Mixes/{DetailChannelSettings,MixDetailRules,FaderSettings}.cs` (new), `Mixes/{ChannelSettings,Mix,MixSettings}.cs`,
  `ValidationErrors.cs` (`Bounds.Pan`)
- `src/StemStudio.Persistence/Configurations/{DetailPhaseJson.cs (new),TrackConfiguration.cs,MixConfiguration.cs}`,
  `Repositories/{ITrackRepository,TrackRepository}.cs`, `Migrations/*AddDetailPhase*`, `Migrations/*AddMixDetailSettings*`,
  `Migrations/StemStudioDbContextModelSnapshot.cs`
- `src/StemStudio.Api/Worker/{IWorkerClient,WorkerClient,WorkerClient.Detail,WorkerDetailJob,WorkerDetailResult}.cs`,
  `Storage/{DetailStemFileStore (new),AtomicFile,StemFileStore,DataDirectory}.cs`, `ApiComposition.cs`
- Tests: `tests/StemStudio.Domain.Tests/{Tracks/DetailPhaseTests,Tracks/DetailTestData,Mixes/MixDetailTests}.cs`,
  `tests/StemStudio.Persistence.Tests/{DetailPersistenceTests,DetailPhaseJsonTests}.cs`,
  `tests/StemStudio.Api.Tests/Worker/{WorkerDetailClientTests.cs, Examples/detail-*.json, Examples/problem-409-detail.json}`,
  `tests/StemStudio.Api.Tests/Storage/DetailStemFileStoreTests.cs`, `TestSupport/FakeWorkerClient.cs`,
  `Worker/WorkerClientTests.cs`, `StemStudio.Api.Tests.csproj` (link to the 002 `worker-api.openapi.yaml`, so
  the copied examples are asserted equal to the contract examples)

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release                   0 Warning(s), 0 Error(s)
dotnet test tests/StemStudio.<X>.Tests -c Release -p:CollectCoverage=true   (gate 90 % line+branch)
  Domain.Tests       239 passed   StemStudio.Domain       line 98.46 %  branch 97.54 %
  Persistence.Tests   40 passed   StemStudio.Persistence  line 98.59 %  branch 100 %
  Api.Tests          298 passed   StemStudio.Api          line 99.87 %  branch 97.61 %
                                  StemStudio.Contracts    line 100 %    branch 100 %
  Web.Tests          198 passed   (unchanged, not my component)
Existing migration test Migration_MatchesTheModel green (no pending model changes).
```

## Quality checks

```text
dotnet format StemStudio.slnx --verify-no-changes          exit 0
uv run --project tools/quality python tools/quality/run_all.py
  387 files, 2790 units, 19 clones, 1 exemptions, 0 errors, 115 warnings -> exit 0
  (no warnings in src/StemStudio.{Domain,Persistence,Api}; "detail is not in the dependency map" is
   the worker package, fixed by quality-gate T007)
```

## Dependencies added

None.

## Deviations / decisions

- `DetailStem` has no stored `Order` (data-model 3.1 lists it): the order is the list order, derived
  from `DetailCatalog` on completion.
- `DetailPhase` is stored as one JSON text column through a value converter instead of an EF complex
  type: it is an immutable record with `IReadOnlyList` members; the JSON column is the same as the
  data model asks for. `ListDetailActiveAsync` therefore filters the status in memory over completed
  tracks with a non-null phase (a local single-user library).
- `IWorkerClient.DownloadDetailStemAsync` takes a `WorkerDetailStemRef` record (SIG <= 4 parameters);
  the T030 tests were adjusted in the T031 commit.

## Open issues / requests

None for other owners.

## Next owner

backend (US1: T051-T054).
