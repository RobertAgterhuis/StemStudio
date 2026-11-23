# Hand-off: backend / 020 foundational (T020-T025)

Branch `020-lane-backend` (from design head ca82a055). Contracts: backend-api 1.19.0, worker-api 1.11.0 (unchanged).

## Tasks and commits (red -> green)

| Task pair | Red | Green | What |
|---|---|---|---|
| T020/T021 | 0a4e3fdb | e876d4f5, fix d142825d | `Track.QualityReport` (JSON), `PracticeNote` + table `PracticeNotes` (cascade), `AssetKind/AssetOrigin.Handout`, `Provenance.HandoutRecipe`, `Asset.Handout` (`HandoutFacts`), `StemsVersion`; migrations **AddQualityReport**, **AddPracticeNotes** |
| T022/T023 | 8f04a33f | 884c7558 | `IWorkerInsightsClient` (`WorkerClient.Insights.cs`): quality jobs, handout jobs, PDF download (64 MiB cap), `/health.insights`; kebab-case wire enums (`WorkerInsightsJson`) |
| T024/T025 | 8989252f | 0efeabb6 | Ports + adapters `Api/Handouts/Sources/`: `ISectionSource`, `ISetlistSource` (`note` "" -> null, positions 1-based), `IPracticeLogSource` (`bestTrainerPercent` rounded), `IChordSource` (ready/stale 016 chart), `INotationCatalog` (`NotationClassifier`: drums / tab / transposed part by `WrittenInstrument` suffix Bb/Eb/F) |
| refactor | 7913eed5 | | SIG parameter objects (`HandoutStart`, `HandoutRendering`), `QualityGate.RunAsync`, `WorkerInsightsJson.AddTo` |

## Migrations: safe on the owner's data (copy-first test at deploy)

Both are additive: `Tracks.QualityReport TEXT NULL`, `Assets.Handout TEXT NULL`, new table `PracticeNotes`
(FK cascade to `Tracks`). No existing column changes; existing rows read as "never made" / "not a handout".
Tests: `InsightsPersistenceTests.Migrations_ApplyOnAPre020Database_AndKeepItsRows` and
`Migrations_GoDownToPre020AndUpAgain_...`. **Down** drops `Assets.Handout` with a native `ALTER TABLE DROP COLUMN`
because EF's SQLite table rebuild lost the 022 index `IX_Assets_Folder` (found by the 022 Down test, fixed d142825d).
**Integration: run the copy-first migration test on a copy of the owner's database at deploy.**

## Consumer-driven fakes

Worker examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/` (`quality-request`, `quality-completed-job`,
`handout-bass-request`, `handout-completed-job`); variants built from the 1.11.0 schemas (`quality-{queued,running,failed}-job`,
`handout-{queued,running,failed,warnings}-job`, `problem-409-handout`, `problem-422-notice`, `health-insights`). The client
tests send **exactly** the contract request examples (JSON deep-equal). `FakeWorkerInsightsClient` (ApiFactory
`InsightsWorker`) reads the same examples.

## Tests and coverage

See `backend-020-us4.md` "Final verification" (one full run for all checkpoints of this lane).

## Dependencies

None added.
