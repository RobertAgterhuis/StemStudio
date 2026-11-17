---

description: "Task list for feature 022 Library by artist and album (Phase 0 by the architect in two parts, one parallel wave of at most 4 owner agents, then a CTO integration wave)"
---

# Tasks: Library by Artist and Album

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.4.0, ADR-036.

**Status**: **Phase 0 part 1 done** (2026-11-16, design round): spec, checklist, research with spike
S-01, ADR-036 (Proposed), data-model, standalone contract deltas (backend `1.22.0-delta.1`,
Redocly valid with 0 warnings; UI ids 1.22.0; tool catalogue 1.1.0), quickstart, this task list.
**Phase 0 part 2 done** (2026-11-17, combined round {022, 023}, branch `r22-p0-022`): full backend-api
1.22.0 (= 1.11.0 + delta, `contracts/merge_full.py`), ui-test-ids 1.22.0, tool catalogue 1.1.0 (= 1.3.0 +
delta), Contracts DTOs/routes/limits/`InstrumentLabels`, `ILibraryActions` port with skeletons, contract
tests, ADR-036 number confirmed; 013 overlaps re-checked against 013 as built (research R-11, two
backfill refinements in T032/T033). Hand-off: `docs/handoffs/architect-022-phase0.md`. The wave starts
from the design-phase head; when 013 lands on `main` the CTO merges `main` in and re-runs
`merge_full.py --base specs/013-library-care/contracts/backend-api.openapi.yaml` (DELTA.md section 4).

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No ML model and no worker change are involved.

**Organization**: grouped by user story (US1..US5); the [lane plan](#lane-execution-plan-max-4-parallel-agents)
regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on an
  incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-022-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-022-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us5`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| backend | DTOs, routes, limits, `InstrumentLabels` | Contracts (T003), backend-api 1.22.0 (T002) | - |
| frontend | backend API | backend-api 1.22.0 + Contracts DTOs (T003) | fake `IStemStudioApiClient` |
| ai-agent | loopback actions | `IStemStudioActions` additions (T003 stubs), catalogue 1.1.0 (T004) | fake actions |
| qa | UI | `ui-test-ids.md` 1.22.0 (T004), fake stack | suite authored in the wave, executed in T092 |
| quality-gate | layering | plan.md structure | - |

---

## Phase 0: Contracts and skeleton (architect, BEFORE the wave)

### Part 1 (done, 2026-11-16)

- [x] T001 [architect] spec.md, checklists/requirements.md, research.md (spike S-01), ADR-036, data-model.md, plan.md, quickstart.md, tasks.md; `contracts/backend-api.library-folders.openapi.yaml` 1.22.0-delta.1 (Redocly 2.78.0: valid, 0 warnings), `contracts/redocly.yaml`, `contracts/ui-test-ids.delta.md`, `contracts/tool-catalogue.delta.md`, `contracts/DELTA.md`; MoSCoW S13 and ROADMAP rows

### Part 2 (when 022 is scheduled)

- [x] T002 [architect] Full `contracts/backend-api.openapi.yaml` **1.22.0** = newest full base + delta (DELTA.md section 1); Redocly lint; contract test `LibraryContractTests` (superset proof; `PendingLibraryOperations` lists the 9 new operation ids until the endpoints exist)
- [x] T003 [P] [architect] `src/StemStudio.Contracts/Library/*` DTOs and enums (data-model 5), `AssetDto.Placement`, `ApiRoutes.Library*`, `ContractLimits` additions (data-model 6), `InstrumentLabels` catalogue (research R-08, one entry per family, XML docs); `IStemStudioActions` additions `BrowseLibraryFolderAsync`, `SearchLibraryAsync`, `GetLibraryInfoAsync`, `SetLibraryInfoAsync` (stubs); contract unit tests (`InstrumentLabelsTests`: every `StemNames`/`DetailNames` id has a label; every family has aliases)
- [x] T004 [P] [architect] Full `contracts/ui-test-ids.md` **1.22.0** = base + delta (changelog line, changed-behaviour table, help topics); `tool-catalogue.md` **1.1.0** = 1.0.0 + delta (groups, page contexts, counts, evaluation rows)
- [x] T005 [architect] Skeleton green: `dotnet build` (0 warnings), `dotnet test`, `dotnet format --verify-no-changes`; hand-off `docs/handoffs/architect-022-phase0.md` (HO:phase0)

---

## Phase 1: Foundational (blocking for the stories of the same owner)

- [x] T010 [P] [backend] Tests (red) `LibraryKeyTests`: >= 40 normalisation cases (research R-02: case, spaces/NBSP, accents, ß/æ/ø/ł/đ/þ/œ, quotes, dashes, full-width, "The" for artist/album only, "Thee", "The The", featuring bracket/tail for artist only, "Various Artists", emoji/punctuation-only -> fallback, Cyrillic/Greek, CJK); `FoldedText` index maps (ß -> ss, ligatures, removed marks) (FR-004, FR-005, SC-002)
- [x] T011 [backend] `src/StemStudio.Domain/Library/LibraryKey.cs`, `FoldedText.cs` (green)
- [x] T012 [P] [backend] Tests (red) `FolderIdTests` (deterministic, prefixes, pattern, separators, `TryParse`) and `LibraryInfoRulesTests` (trim, 1..200, control characters, null = automatic) (FR-031)
- [x] T013 [backend] `FolderId.cs`, `FolderLevel.cs`, `PlacementSource.cs`, `LibraryInfo.cs`, `LibraryInfoRules.cs`, `Placement.cs`, `PlacementLevel.cs` (green)
- [x] T014 [P] [backend] Tests (red) `PlacementResolverTests`: precedence per level (item > track > match > import > fallback), levels independent, not-found/failed lookups use import names, Various Artists ignored, deleted track -> provenance snapshot, score from an item -> source placement, no track and no source -> "My songs", recording never filed, cut to 200 characters (FR-001, FR-003, FR-006)
- [x] T015 [backend] `PlacementInput.cs`, `PlacementResolver.cs`, `LibraryFilingRules.cs`; `Asset.File`, `Asset.SetLibraryInfo`, `Track.SetLibraryInfo` (`Track.Library.cs`) (green)
- [x] T016 [P] [backend] Tests (red) persistence: owned `Placement` mapping, library info columns on `Assets`/`Tracks`, `FiledVersion` default 0, `IX_Assets_Folder`, migration up/down on a copy of a pre-022 database (SQLite)
- [x] T017 [backend] EF configurations and migration `AddLibraryFolders` (green) (research R-13)
- [x] T018 [P] [frontend] Tests (red) API client methods for the 9 new operations against a stub handler (ETag/304 handling, 404 folder-not-found, 422 mapping) and pure helpers `LibraryLocationTests` (routes, `?q=`, `?view=all`, URL building), `HighlightedTextTests` (ranges, overlaps, out-of-range safety), `FolderArtModelTests` (cover/mosaic 2-4/placeholder, `srcset`/`sizes`, tone class)
- [x] T019 [frontend] `src/StemStudio.Web/Api/*` additions, `src/StemStudio.Web/Library/{LibraryLocation,HighlightedText,FolderArtModel}.cs` (green)
- [x] T020 [P] [ai-agent] Tests (red) descriptors and JSON schemas of the 4 tools (names, kinds, risk, undo, `additionalProperties: false`, folder id pattern, xor of `trackId`/`assetId`), registry groups and the 32-tool cap per page context (library = 32, others unchanged) (catalogue 1.1.0)
- [x] T021 [ai-agent] Tool descriptors, group `library` (9), `AgentPageContexts` maps `/library/{folderId}` to `library` (green)
- [ ] T022 [P] [qa] Page-object migration first (red against 1.22.0): `e2e/pages/library-page.ts` opens `/library?view=all` for the 007/008 specs; `e2e/support/levels.ts` drops `library-search` from `beginnerHiddenLibraryIds`; new page objects `library-folders-page.ts`, `library-search.ts`, `library-info-dialog.ts`
- [ ] T023 [P] [qa] Fixtures `tests/fixtures/library/`: tagged upload files and fake-online scenarios for artists with diacritics, "The", featuring, unknown artist/album, a compilation, the same song on two albums, and items of every instrument family and detail group; seeding helpers over the public API; a seeding plan for 5,000 items (database fixture produced by T044's seeder, copied into the API volume by the spec's global setup)
- [ ] T024 [P] [quality-gate] Dependency rules in `tools/quality/` (Domain.Library depends on nothing outside Domain; Api.Library -> Domain, Persistence, Contracts; Web.Library -> Contracts only; Agent tools -> Ports only) - test first (rule fixture fails), then the rule (FR: constitution II)

**Checkpoint**: HO:foundational for backend, frontend, ai-agent, qa, quality-gate.

---

## Phase 2: User Story 1 - Saved items are filed automatically (P1)

- [x] T030 [P] [US1] [backend] Tests (red) `LibraryFilerTests`: filing in the same save for exports (stem, detail, what I hear, selected channels, range), stem packs, scores (track and asset sources), generated takes (009 with source track, 010 without); `RefileTrackAsync` after `CompleteLookup` found/not-found and after track metadata applied; item-source levels untouched; deleted track never re-filed; `UpdatedAt` unchanged when the placement is unchanged (FR-002, FR-008, FR-009)
- [x] T031 [US1] [backend] `src/StemStudio.Api/Library/LibraryFiler.cs` + hooks in `ExportService`, `StemPackService`, `ScoreService`, `GeneratedAudioLibrary`, `MetadataJobApplier`, `TrackSynchronizer` (green)
- [x] T032 [P] [US1] [backend] Tests (red) `LibraryFilingBackfillTests`: files `FiledVersion < Version` in batches, idempotent across restarts, skips recordings, `Pending` count, 5,000 items <= 10 s on SQLite file; **also started by a library read with `pendingFiling > 0` (single-flight; a 013 restore switches the library without a restart) and paused while the 013 gate is in `Maintenance` (a real `LibraryGate`: `EnterMaintenance` holds the next batch, `ExitMaintenance` releases it and pending items are re-counted)** (Phase 0 part 2, research R-11 re-check; round start: 013 on `main`) (SC-003, FR-010)
- [x] T033 [US1] [backend] `LibraryFilingBackfill.cs` (hosted service + `ILibraryFilingTrigger`), `StemStudio:LibraryBackfillBatchSize` option; required `LibraryGate` constructor dependency (013 is on `main`; no `GetService` fallback) (green)
- [x] T034 [P] [US1] [backend] Tests (red) `AssetMapperTests`: `placement` on every asset response (get, list, create, rename, retry, stem pack, score, generation import), `filed = false` preview before backfill, null for recordings
- [x] T035 [US1] [backend] `AssetMapper`/`LibraryMapper` placement mapping (green)
- [x] T036 [P] [US1] [frontend] Tests (red) export dialog and sheet music dialog confirmations show `export-saved-folder`/`score-saved-folder` (three links) and `export-saved-open`/`score-saved-open` link to `/library/<songFolderId>#asset-<id>` (FR-012)
- [x] T037 [US1] [frontend] Confirmation changes in the export and score dialogs (green)
- [ ] T038 [P] [US1] [qa] E2E `e2e/tests/library-filing.spec.ts`: save each item kind, check folder path and confirmation; two spellings of one artist -> one folder; unknown artist/album; deleted track keeps items; last item deleted -> folders gone (US1 scenarios 1-5, 7-9; SC-001)
- [ ] T039 [P] [US1] [qa] E2E `e2e/tests/library-migration.spec.ts`: start on a pre-022 database fixture, `library-filing-pending` visible then gone, every item filed (US1 scenario 6, SC-003)

**Checkpoint**: HO:us1 (backend, frontend, qa).

---

## Phase 3: User Story 2 - Browse folders with art (P1)

- [x] T040 [P] [US2] [backend] Tests (red) `FolderTreeBuilderTests` and `FolderArtPickerTests`: display name rule (match spelling, most used, earliest), counts and sizes per level, `kind`/`status` filters drop empty folders, default and Advanced sorts (The ignored, unknown/My songs last, album year with unknown last), paging, song items in instrument order; art: song = track picture, album = best 005 rank then latest track, artist mosaic of 1-4 distinct album covers, placeholder glyph/tone, deleted-track pictures absent (FR-007, FR-011, FR-013..FR-018)
- [x] T041 [US2] [backend] `LibrarySnapshot.cs`, `LibraryIndex.cs` (rebuild per revision, single-flight), `LibraryRevision.cs`, `FolderTreeBuilder.cs`, `FolderArtPicker.cs` (green)
- [x] T042 [P] [US2] [backend] Tests (red) endpoints `GET /api/library/folders[/{id}]`: 200 per level, ETag and 304, 404 `folder-not-found` (well-formed unknown / emptied), 422 (bad id, sort `year` outside an artist, paging), `pendingFiling`; contract test against the delta examples
- [x] T043 [US2] [backend] `LibraryEndpoints.cs` (folders), `LibraryProblems.cs` (green)
- [x] T044 [P] [US2] [backend] Performance test (red until tuned) with a seeded SQLite file of 5,000 items / 150 artists: folder view p95 <= 100 ms, index rebuild <= 200 ms; the seeder (`LibrarySeed`, test category `Seed`) also writes the E2E database fixtures (pre-022 and current) to the path qa passes in (SC-004)
- [x] T045 [US2] [backend] Tuning if needed (projection query, no tracking, cached sort keys) (green)
- [x] T046 [P] [US2] [frontend] Tests (red) `LibraryPage` folder mode: routes `/library` and `/library/{folderId}`, breadcrumb (`nav` landmark, `aria-current`, collapse under 480 px), title/art/counts header, `library-folder-missing`, `library-filing-pending`, polling every 2 s while items are active (stops when none), "All items" keeps every 007/008 id on `?view=all` (FR-013, FR-020, FR-021)
- [x] T047 [US2] [frontend] `LibraryPage` (folders/all modes), `LibraryBreadcrumb`, header (green)
- [x] T048 [P] [US2] [frontend] Tests (red) `FolderTile`/`FolderArt`/`FolderGrid`: one link per tile with accessible name "<name>, <level>, <counts>", `alt=""` images, width/height, `loading="lazy"`, `srcset`, mosaic cells 2-4, placeholder glyph `aria-hidden`, `onerror` fallback, grid/list toggle persisted in preferences, year on album tiles (FR-014..FR-016, FR-019)
- [x] T049 [US2] [frontend] `FolderGrid`, `FolderTile`, `FolderArt` components; 8 art tone tokens in the design system (light/dark, letter contrast >= 4.5:1), tile and mosaic styles, compact density (green)
- [x] T050 [P] [US2] [frontend] Tests (red) `SongFolder`: 007 `AssetRow`s with play/download/rename/delete/retry unchanged, `asset-path-<id>`, `song-open-track`/`song-open-mixer` only while the track exists, items in instrument order (FR-020)
- [x] T051 [US2] [frontend] `SongFolder`, `AssetPath` (also in "All items" rows) (green)
- [x] T052 [P] [US2] [frontend] Tests (red) level visibility R7-R9 for every 022 id (Beginner hides `library-sort`, `library-view-all`, scope; library info ids render at every level, owner decision 2026-11-16), Advanced sort select per level (FR-018, FR-035)
- [x] T053 [US2] [frontend] Level rules and sort select (green)
- [ ] T054 [P] [US2] [qa] E2E `e2e/tests/library-browse.spec.ts` + `e2e/a11y/library.spec.ts` + visual snapshots: art kinds (cover, mosaic 2/3/4, placeholder, broken image), breadcrumb and keyboard path, grid/list, sorts at Advanced, Beginner path, axe in both themes and densities (US2 scenarios 1-9; SC-005, SC-008)

**Checkpoint**: HO:us2 (backend, frontend, qa).

---

## Phase 4: User Story 3 - Find anything by typing (P1)

- [x] T060 [P] [US3] [backend] Tests (red) `InstrumentTermsTests` (stem/detail ids as words, labels, family aliases equal per family, mixes index all channels incl. `channels = null` plans, score profiles, generations "mix") and `LibrarySearchTests`: folding of query and fields, <= 8 tokens of 100 characters, AND across tokens / OR across fields, folder-hit rule (>= 1 token in its own name), relevance classes, groups and totals, `limit`, `within`, highlight ranges mapped to original UTF-16 offsets (Motörhead, Straße, ligatures), recordings excluded, **one case per instrument family and per detail group** (FR-022..FR-025, FR-029, FR-036, SC-006)
- [x] T061 [US3] [backend] `InstrumentTerms.cs`, `LibrarySearch.cs` (green)
- [x] T062 [P] [US3] [backend] Tests (red) `GET /api/library/search`: 200 groups, 304 same revision, 404 unknown `within`, 422 blank/too long; perf at 5,000 items p95 <= 50 ms (extends T044) (FR-030, SC-004)
- [x] T063 [US3] [backend] Search endpoint (green)
- [x] T064 [P] [US3] [frontend] Tests (red) `LiveSearchControllerTests` with fake `TimeProvider`: first character, 200 ms debounce, Enter immediate, sequence numbers drop stale answers, cancellation, Escape/clear restores the previous folder URL and focus, `?q=` replace while typing and push on Enter (FR-022, FR-026)
- [x] T065 [US3] [frontend] `LiveSearchController.cs` (green)
- [x] T066 [P] [US3] [frontend] Tests (red) `LibrarySearchBox`/`SearchResults`/`SearchGroup`: grouped results with headings and counts, `search-show-all-<group>`, `<mark>` segments (`search-mark`), paths as links, item actions in results, `library-search-empty` with tips and clear, `library-search-error` keeps results, `library-search-status` announced once 500 ms after the last change, Arrow Down to the first result, `library-search-scope` at Advanced inside a folder (FR-025, FR-027..FR-029)
- [x] T067 [US3] [frontend] Search components and `LibraryAnnouncer` (green)
- [x] T068 [P] [US3] [frontend] Tests (red) `/` shortcut focuses the search on library pages (014 catalogue row when present, else 004 key map registration; no conflict with Ctrl+K and Ctrl+/)
- [x] T069 [US3] [frontend] Shortcut registration (green)
- [ ] T070 [P] [US3] [qa] E2E `e2e/tests/library-search.spec.ts`: one character at a time, accents ("motorhead"), two words, **one query per instrument family and per detail group**, highlights, paths, item actions in results, Escape restores the folder, empty state, keyboard and live-region text (US3 scenarios 1-10; SC-006)
- [ ] T071 [P] [US3] [qa] E2E `e2e/tests/library-search-perf.spec.ts` on the 5,000-item fixture: p95 last-key-to-results <= 400 ms over 30 typed queries; no stale result flicker (SC-004)

**Checkpoint**: HO:us3 (backend, frontend, qa).

---

## Phase 5: User Story 4 - Put items in the right folder (P2)

- [x] T072 [P] [US4] [backend] Tests (red) `LibraryInfoServiceTests` + endpoint tests: GET/PUT/DELETE for tracks and items, per-level sources and automatic names, `movedItems`, item-source levels kept on track edits, a later lookup never overrides user names, 400 field messages, 404, 409 `not-fileable` for recordings (FR-009, FR-031..FR-033)
- [x] T073 [US4] [backend] `LibraryInfoService.cs`, library-info endpoints (green)
- [x] T074 [P] [US4] [frontend] Tests (red) `LibraryInfoDialog` (track and item scope): fields with "Automatic" boxes showing the automatic name and its source, validation messages, reset, save follows to the new song folder, `library-info-lookup` link to the 005 correction, rendered at every level including Beginner (owner decision 2026-11-16) (FR-031..FR-033)
- [x] T075 [US4] [frontend] `LibraryInfoDialog`, `library-info-edit`, `asset-library-info-<id>` (green)
- [ ] T076 [P] [US4] [qa] E2E `e2e/tests/library-info.spec.ts`: edit track album -> all items move and old folders vanish; edit one item; Automatic restores; fake lookup found later moves automatic items only (US4 scenarios 1-7)

**Checkpoint**: HO:us4 (backend, frontend, qa).

---

## Phase 6: User Story 5 - The composer agent knows the folders (P3)

- [x] T077 [P] [US5] [ai-agent] Tests (red) projections of `browse_library_folder` and `search_library_items` (whitelisted fields, <= 4 KB, names only in `untrusted_text`, run tainted), `open_library_folder` (exactly one of `folderId`/`query`), `set_library_info` (GET before PUT, merged arguments, inverse restores, approval in a tainted run, 409 reason) (FR-034)
- [x] T078 [US5] [ai-agent] Tool implementations in `src/StemStudio.Agent/Tools/Library/` (green)
- [x] T079 [P] [US5] [backend] Tests (red) loopback `IStemStudioActions` library methods against the endpoints (in-process host)
- [x] T080 [US5] [backend] `IStemStudioActions` library implementation (green)
- [ ] T081 [P] [US5] [frontend] Tests (red) UI tool host: `open_library_folder` navigates (unsaved-changes guard), page context `library` for `/library/{folderId}`
- [ ] T082 [US5] [frontend] UI tool handler (green)
- [x] T083 [P] [US5] [ai-agent] Evaluation cases LIB-01..LIB-05 in `specs/011-ai-composer-agent/evaluation/` and the eval test project (fake chat client scripts)

**Checkpoint**: HO:us5 (ai-agent, backend, frontend).

---

## Phase 7: Polish and cross-cutting

- [x] T084 [P] [frontend] Help topics `library-folders`, `library-search`, `library-info`, `library-art` (help-content 1.0.0 format; test first: `HelpCatalogueTests` lists them and every `data-help-id` resolves) (FR-035)
- [ ] T085 [P] [devops] **Conditional**: only if qa (T023/T039/T071) cannot copy the database fixtures into the API volume from the spec's global setup without a compose change - add an opt-in fixture mount to the fake stack (check-first test in the task). Otherwise not executed.
- [x] T086 [P] [backend] Polish: XML docs, `dotnet format`, SIG check on `Domain/Library` and `Api/Library`, coverage >= 90 % line and branch (HO:polish)
- [x] T087 [P] [frontend] Polish: 320 px layout, compact density, reduced motion (no tile animations), SIG, coverage >= 90 % (HO:polish)
- [x] T088 [P] [ai-agent] Polish: coverage >= 90 %, catalogue counts test (HO:polish)
- [ ] T089 [quality-gate] SIG and Sonar pass on all 022 code; refactor requests to owners; dependency rules green (T024) (HO:polish)
- [ ] T090 [qa] Audit the owners' tests and coverage evidence (HO:polish)

## Phase 8: Integration (CTO)

- [ ] T091 [CTO] Merge the lanes; full backend-api/ui-ids/catalogue consistency; `/speckit-analyze`
- [ ] T092 [qa] Run the whole E2E suite on the fake stack (007/008 specs on `?view=all`, 022 specs, a11y, visual, perf) and the quickstart validation checklist
- [ ] T093 [CTO] Manuals: user manual "Library" chapter (folders, art, search, library info), technical manual (API, backfill); rebuild PDFs (`cd tools/docs && npm ci && npm run browser && npm run build`); `docs/STATUS.md`; MoSCoW S13 delivered; PR

---

## Dependencies and execution order

- Phase 0 part 2 (T002-T005) blocks the wave.
- Inside each owner: foundational before stories; each story's test task before its implementation.
- US1 filing (T030-T035) is needed by the backend's US2-US4 tests only through the domain types
  (T010-T017); the frontend works against the fake API client throughout.
- US5 needs the endpoints of US2-US4 for the loopback tests (T079); agent tools themselves use fakes.
- qa specs run against the integrated stack in T092; they are authored during the wave.

## Lane execution plan (max 4 parallel agents)

This PC runs at most **4 agents at once**. Lanes per wave:

| Wave | Lane 1 | Lane 2 | Lane 3 | Lane 4 |
|---|---|---|---|---|
| 0 (part 2) | architect: T002 -> T003, T004 (parallel) -> T005 | - | - | - |
| A (foundational + US1) | backend: T010 -> T011, T012 -> T013, T014 -> T015, T016 -> T017, T030 -> T031, T032 -> T033, T034 -> T035 | frontend: T018 -> T019, T036 -> T037 | ai-agent: T020 -> T021 | qa: T022, T023, T038, T039 |
| B (US2 + US3) | backend: T040 -> T041, T042 -> T043, T044 -> T045, T060 -> T061, T062 -> T063 | frontend: T046 -> T047, T048 -> T049, T050 -> T051, T052 -> T053, T064 -> T065, T066 -> T067, T068 -> T069 | quality-gate: T024 | qa: T054, T070, T071 |
| C (US4 + US5 + polish) | backend: T072 -> T073, T079 -> T080, T086 | frontend: T074 -> T075, T081 -> T082, T084, T087 | ai-agent: T077 -> T078, T083, T088 | qa: T076, T090 (devops T085 only if triggered, replacing qa for that slot) |
| D (integration) | quality-gate: T089 | qa: T092 | CTO: T091, T093 | - |

ai-agent is idle in wave B (nothing to do before the endpoints exist); quality-gate takes its slot.

### Lane split for the combined round {022, 023} (Phase 0 part 2 suggestion, up to 6 agents)

The round runs up to 6 agents (orchestrator brief for this round; at most 1-2 heavy processes per
agent). 022's backend (29 tasks) and frontend (24 tasks) lanes are the heaviest of the round, so they
stay feature-pure; the small 022 ai-agent (6) and qa (10) lanes are shared with 023. The CTO decides.

| Agent | 022 tasks | 023 tasks | Why |
|---|---|---|---|
| backend-022 | T010-T017, T030-T035, T040-T045, T060-T063, T072-T073, T079-T080, T086 | - | heaviest lane; domain + API + migration |
| frontend-022 | T018-T019, T036-T037, T046-T053, T064-T069, T074-T075, T081-T082, T084, T087 | - | second heaviest; `/library` page |
| backend-023 | - | 023 backend lane (023 tasks.md) | disjoint paths (`Api/Lyrics*`) |
| frontend-023 | - | 023 frontend lane | disjoint pages |
| ai-agent (shared) | T020-T021 (wave A), T077-T078, T083, T088 (wave C) | 023 ai-agent lane (fills 022's idle wave B) | both change the catalogue: 1.1.0 + 1.2.0 merged by containment; one owner avoids conflicts in `AgentToolRegistry`/`AgentPageContexts`/`Catalogue.cs` |
| qa (shared) | T022-T023, T038-T039, T054, T070-T071, T076, T090, T092 | 023 qa lane | one E2E stack at a time; shared page objects |

quality-gate (T024, T089) and the conditional devops T085 (and 023's devops/quality tasks) run in a
second short wave or are bundled into the qa agent's slot when it is idle. CI additions for 022 (Redocly
lint of the 022 documents, `merge_full.py --check`) belong to the devops/CTO bundle (hand-off note).

## Task counts

| Lane | Tasks | Test-first pairs |
|---|---|---|
| architect | 5 (T001-T005) | - (contract tests inside T002/T003) |
| backend | 29 (T010-T017, T030-T035, T040-T045, T060-T063, T072-T073, T079-T080, T086) | 14 (+ T044 perf, T086 polish) |
| frontend | 24 (T018-T019, T036-T037, T046-T053, T064-T069, T074-T075, T081-T082, T084, T087) | 11 (+ T084 test-first inside, T087 polish) |
| ai-agent | 6 (T020-T021, T077-T078, T083, T088) | 2 (+ eval cases, polish) |
| qa | 10 (T022-T023, T038-T039, T054, T070-T071, T076, T090, T092) | E2E authored before integration |
| quality-gate | 2 (T024, T089) | 1 (T024 test-first inside) |
| devops | 1 conditional (T085) | check-first inside |
| CTO | 2 (T091, T093) | - |
| **Total** | **79** (78 + 1 conditional) | 28 pairs |
