# Hand-off: backend / 009-us1 (T037-T042)

## What was built

- `Domain/Generations/RestylePlanner` (range defaults start 0 / end min(duration, start + 480), length 10-480 s,
  inside the track, in place only for tracks <= 480 s, stems a unique subset of the track's in catalog order,
  instrumental styles drop `vocals` by default, strength default = style default, 1-2 takes).
- `POST /api/tracks/{trackId}/restyles` (`RestyleService`): 404 / 409 `track-not-ready` / 400 (`styleId`, planner
  fields) / 409 `not-installed` or `gpu-required` (from `GenerationAvailability`, 10 s cache; unreachable
  `/health` = not installed) -> style snapshot, `StylePromptBuilder` caption and cover strength, one seed per take
  (`ISeedSource`), stored generation, stems uploaded (`GenerationSubmitter`; on refusal or outage the generation
  is removed: generator `gpuRequired` -> 409 `gpu-required`, `invalidInput`/`sourceTooShort` -> 400
  `rangeEndSeconds`, else 503 `generator-unavailable`). Presets are seeded at start-up.
- `GenerationSynchronizer` + `GenerationSyncService` (2 s, `PeriodicSyncService` base now shared with
  `JobSyncService`): mirrors status/progress/stage/device/warnings; completed jobs -> `TakeCollector` downloads to
  `generations/<id>/take-<n>.flac` with SHA-256 check (damaged/missing take -> failed `engineError`; outage -> retried
  next pass), registers each take through `IGeneratedAudioLibrary` (`GeneratedAudioLibraryAdapter`: ready asset,
  origin `generation`, `Provenance.generation`, 48 kHz / 24-bit facts, title `<source> - <style> (take n)`) and
  deletes the generator copy; failed jobs keep the generator code (unknown -> `engineError`); a job the generator
  lost fails as `interrupted`; an unreachable generator changes nothing.
- `GET /api/generations[?kind&sourceTrackId]`, `GET/DELETE /api/generations/{id}`, `POST .../cancel`,
  `POST .../retry`, `GET .../takes/{index}/audio` (Range/ETag/304/416 via `StemService.ServeFile`). Delete removes
  take files and library items, keeps imported tracks; cancel/delete ask the generator best effort. Deleting the
  source track detaches its generations (title snapshot kept). Domain: `Asset.CreateGenerated`,
  `AssetOrigin.Generation`, `Provenance.Generation` (`GenerationSource`).
- Un-staged: `createRestyle`, `listGenerations`, `getGeneration`, `deleteGeneration`, `cancelGeneration`,
  `retryGeneration`, `getTakeAudio`. Still staged for 009: `getGenerationStatus`, the 6 style ids, `importTake`.
- Test support: `FakeGenerator` (generator examples with own ids behind the real `GeneratorClient`),
  `SequenceSeeds`, `ApiFactory.Generator/GeneratorInstalled/SyncGenerationsAsync`.
- qa request (coordinator message): every `ethics-cases.json` guard case (spec + qa-proposal: Dutch phrases, extra
  spaces, upper case, accents, genre names, library artist) is a test vector in `StyleTextGuardTests`; none is
  deliberately left unrefused.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T037/T038 | 9d5aa33 | d00d1f6 |
| T039/T040 | 2460933 | 1d7bba9 |
| T041/T042 | 7c0cd45 | 3b91664 |

## Tests and coverage

```text
Domain.Tests        701 passed   Domain       line 98.9 %   branch 94.92 %
Persistence.Tests    87 passed   Persistence  line 98.55 %  branch 94.11 %
Api.Tests (filter "FullyQualifiedName!~NewerContracts_ContainTheOlderOnes")
                   1058 passed   Api          line 97.96 %  branch 93.24 %   Contracts 100 % / 100 %
```

Full Api run: 1068 passed, 2 failed = the pre-existing 008->009 subset test (see backend-009-foundational).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; `sig_check`: 0 errors, 20 warnings (units of 16-27
NLOC / CCN 6-8 in the new generation services; cut at polish T076).

## Dependencies added

None.

## Open issues / requests

- architect: see backend-009-foundational (be286b4 port into the 009/010 backend documents).

## Next owner

backend (009 US2: T046-T047).
