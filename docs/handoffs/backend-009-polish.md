# Hand-off: backend / 009-polish (T076)

## What was built

Feature 009 backend is complete (T024-T031, T037-T042, T046-T047, T053-T054, T058-T059, T063-T064, T070-T071,
T076); every 009 operation id is served and `PendingGenerationOperations` is empty. Polish:
`ServiceResilience.AddServiceClient` (one registration path for the worker and generator clients),
`QueryParsing.Enum` (asset and style list queries), `StyleMapper.Map` delegates to `ScoreMapper.Map`, smaller units
in `GenerationService`, `TakeCollector`, `TakeImporter`; extra tests (generator exception constructors, outage
during a take download).

Hand-offs: backend-009-{foundational,us1,us2,us6,us3,us4,us5,polish}.md.

## Tests and coverage

```text
dotnet test tests/StemStudio.Domain.Tests -p:CollectCoverage=true        701 passed   Domain       line 98.9 %   branch 94.92 %
dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true    87 passed   Persistence  line 98.55 %  branch 94.11 %
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true --filter "FullyQualifiedName!~NewerContracts_ContainTheOlderOnes"
                                                                        1103 passed   Api          line 99.26 %  branch 93.59 %
                                                                                      Contracts    line 100 %    branch 100 %
```

Unfiltered Api run: 1103 + 2 failed (pre-existing `GenerationContractTests.NewerContracts_...` for `paths` and
`components/responses`, see backend-009-foundational: spec drift from be286b4, architect lane).

## Quality checks

- `dotnet build StemStudio.slnx`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 25 warnings (16-21 NLOC / CCN 6-7 units,
  e.g. `GenerationMapper.ToDto`, `GenerationSynchronizer.ApplyAsync`, `RetryService.RetryAsync`; none above the hard
  limits).
- `duplication.py`: 0 clones in production code.
- `dependency_check.py`: 0 errors, 0 warnings (Api -> Persistence -> Domain; Domain without infrastructure).

## Dependencies added

None.

## Deviations (summary)

- Namespaces `Domain.Generations`/`Api.Generations` and `Api.Generator` instead of `Generation`.
- Takes as a JSON column of `Generations`; style name uniqueness via `NormalizedName`.
- `IGeneratedAudioLibrary.RegisterAsync` returns a `Guid` (contract `libraryAssetId` is a uuid) and takes the
  generation and take instead of the flat entry of data-model 3.6.
- Take import names the uploaded file `take.flac` and sets the title through `ApplyMetadata` (the take title holds
  characters such as `/` that a file name cannot).
- T070 tests were green on arrival (in-place rules implemented with the planner in T038).

## Open issues / requests

- **architect**: port be286b4 into the 009/010 backend documents (blocking `NewerContracts_...`).
- **frontend**: problem `type` values as in the contract; `generation/status.voice` is `notInstalled` until 010.

## Next owner

backend (010 foundational), then CTO integration (009 T081).
