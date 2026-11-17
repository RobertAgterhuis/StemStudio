# Hand-off: backend-022 / polish (checkpoints B and C: T040-T045, T060-T063, T086)

Builds on `docs/handoffs/backend-022-foundation.md` (T010-T017, T030-T035; decisions D1-D6 there). T072-T073 and
T079-T080 were split off to lane backend-022b (CTO decision, from f9968494).

## What was built
- Folder view (US2): `LibrarySnapshot` (filed items, preview placements of unfiled items, track pictures, folder
  membership), `LibraryIndex` (singleton, one rebuild per revision, built in its own scope), `LibraryRevision`
  (16 hex over item/track counts and newest changes), `FolderTreeBuilder` (display name FR-011, counts, sizes,
  `kind`/`status` filters, paging, mixer order of song items), `FolderSorting` (FR-018), `FolderArtPicker` (R-05),
  `FolderViewRequest`, `LibraryBrowseService` (ETag `"lib-<revision>"`, 304, backfill trigger), `LibraryProblems`,
  `LibraryEndpoints` (`getLibraryRoot`, `getLibraryFolder`, `searchLibrary`).
- Search (US3): `InstrumentTerms` (R-08; cached per channel), `InstrumentMatcher`, `SearchIndex` (lazy per snapshot),
  `LibrarySearch` (R-07), `SearchQueryRequest`.
- Perf/seed (T044/T045): `tests/StemStudio.Api.Tests/Library/LibraryPerfTests.cs` (non-parallel collection, trait
  `Category=Perf`), `LibrarySeed` (5,000 items, 500 tracks, 150 artists; every family, detail stems, mixes).

## Decisions (for the CTO log)
- D7 Alias rule (CTO decision, resolves SC-006 vs R-08): family aliases match only as a whole phrase of consecutive
  search words; id words and labels match as parts of words. Tested both directions ("guitar" never finds bass,
  "bass guitar" finds only bass, "guitar bass" and "kit" find nothing, "drum kit" finds the drum family).
- D8 `q` longer than 100 characters after trimming answers 422 (contract `maxLength`, T062; CTO confirmed); also 422
  when `q` is blank or has no letter or digit. Duplicate words are dropped before the 8-word cut.
- D9 Folder hits: at least one word in the folder's own name, every word in its own name or a parent's name
  ("metallica battery" lists the song Battery, not the artist). Relevance per word: 0 whole field, 1 word start,
  2 inside, 3 only in a parent; ties by sort name (leading "The" ignored), then id.
- D10 Item fields: artist/album/song folder names, the item name, the track's full artist credit
  (`provenance.trackArtist`, searched without highlight), instrument terms. Highlights: first occurrence per word and
  field, at most 32; instrument highlights carry `label`.
- D11 `sort=year` is accepted only on an artist folder (422 at the root and on album/song folders). A folder emptied
  by the `kind`/`status` filter still answers 200 with zero counts (its name and art from all its items); 404 only
  when no item is filed in it at all. `counts.albums`/`songs` are omitted when zero (schema minimum 1).
- D12 Placeholder tone = last two hex characters of the folder id mod 8; glyph = first letter or digit (upper case),
  `?` for fallback names, `♪` for "My songs" / "Songs from my lyrics".
- D13 The unfiled preview in folder views and search uses the item's own facts (provenance snapshot, item info), as
  `Asset.placement` with `filed = false` does.
- D14 Perf budgets are measured uninstrumented in Release (CTO): every `dotnet test` run is instrumented by the
  coverlet collector of `tests/coverlet.runsettings`, so the timing assertions detect instrumentation and are skipped
  then (the code still runs). Command: `dotnet test tests/StemStudio.Api.Tests -c Release --filter Category=Perf
  -p:RunSettingsFilePath=`. Result at 5,000 items / 500 tracks / 150 artists (SQLite file, this PC, other lanes busy):
  backfill 1.1-1.6 s (SC-003 <= 10 s), folder views p95 10.7 ms (<= 100 ms), index rebuild median 73 ms (<= 200 ms),
  search over HTTP p95 17.2 ms (<= 50 ms). Tuning: item haystack pre-filter, highlights only for returned hits,
  ordinal tie-breaks on folded text, loops instead of LINQ in the matcher, one-query revision stamp
  (`LibraryRepository.StampAsync`, constant SQL without input) and indexes `IX_Assets_UpdatedAt`, `IX_Assets_FiledAt`
  (migration re-generated as `20261117111150_AddLibraryFolders`; IX_Assets_Folder re-applied by hand).
- D15 `within=<folderId>` returns only what is inside the folder (its child folders and items), not the folder itself
  (CTO confirmed).
- D16 The backfill repeats a failed run up to 5 times, 1 s apart (real time), then waits for the next library read:
  transient `database is locked` under the parallel test suite left items unfiled.

## Tests and coverage
- Domain: `dotnet test tests/StemStudio.Domain.Tests -p:CollectCoverage=true` -> 1080 passed; line 98.88 %, branch 95.3 %.
- Persistence: 129 passed; line 99.05 %, branch 95.23 %.
- Api: `dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: 2609 passed, 4 skipped, 1 failed (an agent
  executor timing test, `AgentEndpointsTests.Executor_FailsTheRunWhenTheAgentThrows`, not touched; it and the other
  occasional flakes `ClaudeKeyLeakTests.StoredSentinel_NeverLeaks`, `StorageOverviewTests.FiveHundredTracks_AreMeasuredQuickly`,
  `AgentEndpointsTests.Executor_ClearsTheActiveRunWhenTheRunEnded` pass when run alone; this PC ran other lanes at the
  same time). Coverage of that run (collector report): StemStudio.Api line 98.34 %, branch 91.52 %; Contracts 100/100.
  022 files: every `Api/Library/*` file 100 % line except LibraryFiler 59/61 and LibraryFilingBackfill 85/95 (retry
  path); branches 100 % except FolderTreeBuilder 27/28, InstrumentMatcher 15/16, InstrumentTerms 31/34,
  LibraryFiler 29/30, LibraryFilingBackfill 10/12, LibrarySnapshot 12/14.
- Seed: `STEMSTUDIO_LIBRARY_SEED_OUT=<dir> dotnet test tests/StemStudio.Api.Tests --filter "Category=Seed"` writes
  `library-5000-pre022.db` (migrated down to `20261116211814_AddMixPractice`, nothing filed) and `library-5000.db`
  (filed). The fixtures hold database rows only: no stem, item or artwork files on disk.

## Quality checks
- `dotnet format --verify-no-changes` clean, build 0 warnings.
- `sig_check.py` on all 022 paths: 0 errors; warnings only for test methods over 15 lines and the existing
  `TrackConfiguration.Configure` (17 lines; +1 line for library info).

## Dependencies added
None.

## Security self-review
- No folder-art upload and no art download exist in 022 (art references the existing 005 artwork URLs only), so magic
  bytes, decode limits, re-encoding and Content-Type/-Disposition from metadata do not apply; no file paths are built
  from names.
- Search is in memory over folded strings: no SQL, no `FromSqlRaw`, no `LIKE`; `q` limited to 100 characters and 8
  words; regexes in `LibraryKey` have timeouts; folder ids validated against `^(ar|al|so)[0-9a-f]{16}$` before use.
- The new GET endpoints are covered by the 013 maintenance gate (test `Maintenance_Answers503`); the backfill waits on
  the gate before each batch. State-changing library-info endpoints (Origin guard, gate) belong to backend-022b.

## Docs impact
- User manual "Library": folders by artist > album > song, art, sorts, live search (instrument words per family,
  aliases as whole phrases), "Sorting your library (n left)".
- Technical manual: `StemStudio:LibraryBackfillBatchSize` (default 500), start-up filing 2 s after start and after a
  013 restore on the first library read.

## Open issues / requests (nobody-owned files)
- `src/StemStudio.Contracts/Library/ContractLimits.Library.cs`, `LibrarySearchMaxLength` doc: "longer input is cut" ->
  "longer input is rejected with 422 library-query-invalid; the UI cuts in the box" (CTO-confirmed). Same for
  `LibrarySearchResultDto.Query` ("cut to 100 characters" -> "at most 100 characters").
- 015: add `recording` to `LibraryFilingRules.IsFileable` and exclude it in `LibraryRepository.CountPendingAsync` /
  `PendingAsync` SQL (foundation D6).
- Merge with backend-022b: `LibraryRegistration.cs` (append its service and `.MapLibraryInfoEndpoints()`),
  `BackendContractTests.Pending022.cs` (only the six *LibraryInfo ids remain on this branch).
- devops/CI: run the perf budgets in a separate step `dotnet test tests/StemStudio.Api.Tests -c Release --filter
  Category=Perf -p:RunSettingsFilePath=` (they are skipped inside the instrumented coverage run).
- Full-suite flakiness under coverage load: `Eventually` (5 s) tests of other areas (`DetailAutoQueueTests`) timed out
  once; my background-wait tests use a 30 s wait (`LibraryTestData.WaitUntilAsync`).

## Next owner
CTO integration (merge order backend-022 before backend-022b and 023), qa E2E with the seed fixtures.
