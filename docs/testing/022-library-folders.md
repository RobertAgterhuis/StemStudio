# Test strategy addendum: 022 library by artist and album

Owner: qa (022 T022, T023, T038, T039, T054, T070, T071, T076; execution T092). Binding contracts:
backend API **1.22.0** (`specs/022-library-folders/contracts/backend-api.library-folders.openapi.yaml`),
UI test ids **1.22.0** (section "Additions in 1.22.0" of the 1.23.0 document), tool catalogue
**1.1.0**. Principle: every instrument family counts equally (one search case per family and per
detail group, SC-006).

## 1. What is tested where

| Level | What | Owner |
|---|---|---|
| Domain (xUnit) | normalised keys (>= 40 cases, SC-002), name precedence (FR-003), featuring and "Various Artists" rules (FR-005), folder tree, art picker, sorts | backend-022 |
| API (xUnit) | folder views, search tokens/highlights/relevance, ETag/304, library info validation, re-filing on match, backfill (idempotent, 5,000 items <= 10 s), perf (folder p95 <= 100 ms, search p95 <= 50 ms; SC-004) | backend-022 |
| Web (bUnit) | routes, tiles and accessible names, breadcrumb, art fallback, live search state machine (latest text wins), library info dialog, level visibility (R7-R9), save confirmations | frontend-022 |
| Agent (xUnit + eval) | 9 library tools, `library` page context (32 tools), LIB-01..LIB-05 | ai-agent |
| E2E (Playwright, fake stack) | the specs below | qa |

## 2. E2E specs

| Spec | Stories / criteria | Tag | Runs in |
|---|---|---|---|
| `tests/library-filing.spec.ts` | US1-1..5, 7-9; FR-001..FR-012; SC-001, SC-002 (8 groups of `scenarios.json`) | `@library` (+ `@generative` for the restyle/own-lyrics case) | `npm test` |
| `tests/library-migration.spec.ts` | US1-6; FR-010; SC-003 | `@library @library-db` | `npm run test:library-db` (E2E_COMPOSE=1) |
| `tests/library-browse.spec.ts` | US2-1..8; SC-005 | `@library` | `npm test` |
| `a11y/library.a11y.spec.ts` | SC-008 (root, artist, album, song, search, dialog; light/dark x comfortable/compact x Beginner/Advanced) | `@library` | `npm run test:a11y` |
| `visual/seeded-library-art.spec.ts` (sorts after `screens.spec.ts`: its seed would show in those screens) | SC-005 (tile art: cover, mosaic 2/3/4) | `@library` | `npm run test:visual` (container; baselines at integration) |
| `tests/library-search.spec.ts` | US3-1..8, 10; FR-022..FR-029; SC-006 | `@library` | `npm test` |
| `tests/library-search-perf.spec.ts` | US3-1, US3-9; SC-004 (p95 <= 400 ms over 30 typed queries, no late stale results) | `@library @library-db` | `npm run test:library-db` |
| `tests/library-info.spec.ts` | US4-1..7; FR-030..FR-037 | `@library` | `npm test` (US4-4/5 case only in mode `found`) |
| `tests/feature-levels.spec.ts` (changed) | R7-R9 for 1.22.0 (Beginner root: search, no filters/sort/All items) | `@library` | `npm test` |
| 007/008 specs (unchanged code) | R-10 migration: `LibraryPage.goto()` opens `/library?view=all` | `@library`, `@export`, `@sheet-music` | `npm test` |

US2-9 and the stale deep link are in `library-filing.spec.ts` (last item deleted). US5 (agent)
is evaluated by the ai-agent lane (LIB-01..05); no Playwright agent spec is planned for 022.

## 3. Fixtures and seeding

`tests/fixtures/library/` (README, `scenarios.json`); helpers `e2e/support/library-seed.ts`,
`library-api.ts`, `library-db.ts`, page objects `library-folders-page.ts`, `library-search.ts`,
`library-info-dialog.ts`.

- **Isolation by salt**: every seeded name gets ` qa<run><project>`; folder ids come from
  `Asset.placement`. Reruns on one data volume never share folders. Specs never assert global
  counts of the root (other runs' folders are there).
- **Mode independence**: browse/search/info seeds set the track library info (source `track`).
  Only the tag-based filing case needs `STEMSTUDIO_FAKE_ONLINE=not-found` and the "later match"
  case `found`; both skip with the reason in other modes.
- **Tagged uploads**: RIFF `LIST/INFO` (`INAM`, `IART`, `IPRD`) appended to a unique WAV copy;
  read by the 005 upload details (tinytag) as import names.
- **Seeding cost**: browse set 12 imports + 12 items, families set 3 imports (one with a detail
  phase) + 15 items, once per browser project and run (memoised per worker).
- **5,000-item databases**: written by the backend seeder (T044) to `tests/fixtures/library/db/`
  (not committed); `LibraryDatabaseSwap` stops the api, saves its database, copies the fixture in
  (`docker compose cp`), empties `-wal`/`-shm`, starts the api and restores the original in
  `afterAll`. No compose change is needed, so devops T085 is **not triggered**.

## 4. Known limits of the fake stack (covered elsewhere)

| Case | Why not E2E | Covered by |
|---|---|---|
| Album years and the year sort | years come only from a band info match; the fake match has one album (2020) | backend `FolderTreeBuilderTests`, bUnit sort |
| Matched "Various Artists" ignored (FR-005) | the fake provider never matches it | backend normalisation/precedence tests |
| Detail groups of guitar and piano ("acoustic guitar", "keys") | the fake detail models never split guitar or piano | backend search tests (R-08 table) |
| `srcset` on a 2x screen | fixed 1x viewport in the suite | bUnit art component |

## 5. Run order at integration (T092)

1. `npm test` (includes the 022 specs; about 1.2 h with the other feature specs).
2. `E2E_COMPOSE=1 npm run test:library-db` after generating the database fixtures.
3. `npm run test:a11y`, then `npm run test:visual -- --update-snapshots` in the CI visual job
   (new baselines `library-art-*`, `library-root-*`, and the `library` screen now shows the song
   folder), reviewed by the CTO.
4. Quickstart validation checklist (`specs/022-library-folders/quickstart.md`); NVDA smoke test.

## 6. Evidence required per hand-off

Backend and frontend lanes: line and branch coverage per component (>= 90 %), the normalisation
case count (>= 40), the perf numbers of T044 (folder and search p95), the bUnit level matrix.
qa: typecheck and lint green, the list of specs with their tags, and (T092) the Playwright report
with retries 0.
