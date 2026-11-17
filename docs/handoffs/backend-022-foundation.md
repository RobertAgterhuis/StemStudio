# Hand-off: backend-022 / foundation (checkpoint A: T010-T017, T030-T035)

## What was built
- Domain `src/StemStudio.Domain/Library/`: `LibraryKey` + `FoldedText` (R-02 folding with an index map for
  highlights), `FolderId` (R-03, matches the contract examples `arb385da54bfe8c6a9` / `alb7d8a7e8ed84d5af` /
  `soc02cef10277ea444`), `LibraryInfo` + `LibraryInfoRules`, `Placement`/`PlacementLevel`, `PlacementInput`/`TrackFacts`,
  `PlacementResolver` (R-01 per level), `LibraryFilingRules`; `Asset.Library.cs` (`Placement`, `LibraryInfo`,
  `FiledVersion`, `FiledAt`, `IsFiled`, `File`, `SetLibraryInfo`), `Track.Library.cs`.
- Persistence: placement and library info as EF 10 **complex types** on plain columns (data-model 3.1/3.2 names),
  migration `AddLibraryFolders` (columns + `IX_Assets_Folder` + `IX_Assets_FiledVersion`; regenerated snapshot
  committed; re-generated in the polish checkpoint as `20261117111150_AddLibraryFolders` with two more indexes), `Repositories/LibraryRepository.cs` (registered in `LibraryRegistration`).
- API `src/StemStudio.Api/Library/`: `LibraryFiler` (+ `FileAllAsync` for the backfill), `LibraryFilingBackfill`
  (hosted service + `ILibraryFilingTrigger`, `StemStudio:LibraryBackfillBatchSize`, required `LibraryGate`),
  `LibraryMapper`; hooks in `ExportService`, `StemPackService`, `ScoreService`, `GeneratedAudioLibrary`,
  `MetadataJobApplier` (refile after a finished lookup), `TrackSynchronizer` (refile after a status change);
  `AssetMapper` writes `placement` on every asset response.

## Decisions (for the CTO log)
- D1 `Placement.Version` is not stored: `Asset.FiledVersion` carries the rules version. EF 10 optional complex types
  need a required member, so the placement complex type has a shadow discriminator column `Placement_Discriminator`
  (one extra nullable TEXT column versus data-model 3.1). `IX_Assets_Folder` is created in the migration (outside
  the model, its columns belong to the complex property).
- D2 `PlacementInput.Previous` (not in data-model 2.6): an item whose track and source item are gone keeps its
  last place (FR-008, edge case "keeps its last place"); its `item`-sourced levels follow the current item info.
- D3 Display names are cut to 200 characters with "…" (one character) and control characters removed; the key is
  computed from the cut name.
- D4 The start-up backfill runs `LibraryFilingBackfill.StartDelay` (2 s, on `TimeProvider`) after the host started,
  not during host start: running it during start-up produced intermittent `database is locked` / lost writes in
  other start-up work in the API tests. Reads (folder view, search) trigger it at once when `pendingFiling > 0`.
- D5 `placement.filed = true` already in the 202 of export, stem pack and score (filed in the same save, FR-002);
  matches the contract (`filed` false only before the backfill reached an older item). Confirms qa's expectation.
- D6 Recordings (015) do not exist yet: `LibraryFilingRules.IsFileable` is true for every current origin. When 015
  adds `recording`, `LibraryRepository.CountPendingAsync/PendingAsync` must also exclude it in SQL (otherwise a batch
  of only recordings stops the backfill early).

## Tests and coverage
- `dotnet test tests/StemStudio.Domain.Tests -p:CollectCoverage=true`: 1080 passed; StemStudio.Domain line 98.88 %,
  branch 95.3 %.
- `dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true`: 129 passed; line 99.05 %, branch 95.23 %.
- `dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: 2495 passed, 2 skipped (pre-existing); StemStudio.Api
  line 98.35 %, branch 91.29 %; Contracts 100/100. New files: LibraryFiler 59/61 lines 29/30 branches,
  LibraryFilingBackfill 82/86 and 9/10, LibraryMapper 27/27 and 4/4.
- SC-003: 5,000 items filed in about 3 s (no coverage) on SQLite file; the perf test runs in its own non-parallel
  collection (`LibraryPerfTests`, trait `Category=Perf`); under the full parallel suite with coverage it exceeded 10 s.

## Quality checks
- `dotnet format --verify-no-changes`: clean; build 0 warnings.
- `sig_check.py` on Domain/Library, Api/Library, LibraryRepository, Sync, Metadata: 0 errors, warnings only in test
  methods (unit size 16-18).

## Dependencies added
None.

## Docs impact
None for this checkpoint (filing is invisible until the folder view; the technical manual can mention
`StemStudio:LibraryBackfillBatchSize`).

## Security self-review
Names from metadata/imports go only into keys/display names: control characters removed, lengths cut (200/400),
regexes with timeouts, no file system or SQL use of names. No upload, no file paths, no new downloads in 022 (art
reuses 005 URLs). The backfill waits on the 013 maintenance gate.

## Open issues / requests
- Pre-existing flake seen once under the full coverage run: `ClaudeKeyLeakTests.StoredSentinel_NeverLeaks` (not touched).

## Next owner
backend-022 continues with US2/US3 (T040-T045, T060-T063), then T072-T073, T079-T080, T086.
