# Hand-off qa 022 wave 1 (library by artist and album)

Lane `qa (022 + 023)`, branch `r22-lane-qa` from 4486ddb7. Tasks 022 **T022, T023, T038, T039,
T054, T070, T071, T076** authored against backend API 1.22.0 and UI test ids 1.22.0 (no E2E stack
in the wave: typecheck and lint only; execution is T092). **T090** (audit of the owners' tests and
coverage evidence) is open: it needs the owners' polish hand-offs (HO:polish). Strategy:
[docs/testing/022-library-folders.md](../testing/022-library-folders.md).

## What was built

| Task | Files |
|---|---|
| T022 page-object migration (R-10) | `e2e/pages/library-page.ts` (`/library?view=all`), `e2e/support/levels.ts` (`library-search` every level; new 1.22.0 lists), `e2e/tests/feature-levels.spec.ts` (Beginner root: search, no filters/sort/All items; Advanced adds them), `e2e/support/screens.ts` (`library` = the seeded song folder at every level, new `library-root`), new page objects `library-folders-page.ts`, `library-search.ts`, `library-info-dialog.ts` |
| T023 fixtures and seeding | `tests/fixtures/library/{README.md,scenarios.json,.gitignore}`, `e2e/support/library-seed.ts` (RIFF INFO tagged uploads, salted names, browse/families sets memoised per worker), `library-api.ts`, `library-db.ts` (5,000-item database swap and restore), `library-test.ts` |
| T038 | `e2e/tests/library-filing.spec.ts` (every item kind filed in the create response, confirmation path + link, normalisation groups, tagged import names in mode not-found, deleted track, last item deleted, restyle/own-lyrics `@generative`) |
| T039 | `e2e/tests/library-migration.spec.ts` (`@library-db`) |
| T054 | `e2e/tests/library-browse.spec.ts`, `e2e/a11y/library.a11y.spec.ts`, `e2e/visual/library.spec.ts` |
| T070 | `e2e/tests/library-search.spec.ts` |
| T071 | `e2e/tests/library-search-perf.spec.ts` (`@library-db`) |
| T076 | `e2e/tests/library-info.spec.ts` |
| scripts | `e2e/package.json`: `npm test` also excludes `@library-db` (and `@lyric-assist`); new `test:library-db` (Chromium) |

## Tests and checks

| Check | Result |
|---|---|
| `npx tsc --noEmit` (e2e) | 0 errors |
| `npm run lint` (ESLint, `--max-warnings 0`, SIG limits: complexity 5, 30/60 lines, 4 params) | 0 problems |
| `playwright test --list` | 022 specs discovered (chromium, firefox, a11y, visual) |
| Stack-free self-test `tests/agent-support.spec.ts` | 11 passed |
| E2E against a stack | not run (lane rule); T092 |
| Coverage | not applicable to Playwright specs (no product code in this lane) |

## Decisions (interim, for the CTO)

1. `/library?view=all` renders only at Advanced+, so the 004 `library` screen (visual, a11y and
   help matrices at every level) now opens the **song folder** of the seeded items; a new
   `library-root` screen covers `/library`. New visual baselines at integration.
2. Seeded names carry a run salt; specs never assert global root counts.
3. Browse/search/info seeds use track library info (mode independent); only the tag-based
   filing case (`not-found`) and the later-match case (`found`) depend on the fake online mode.
4. The 5,000-item fixtures are swapped in with `docker compose cp` on the stopped api, so
   **devops T085 is not triggered**.
5. Year sort, matched "Various Artists", guitar/piano detail groups and `srcset` stay with the
   owners' unit tests (fake stack cannot produce them; strategy section 4).

## Gaps and requests (no contract edited)

- **CTO, SC-006 vs research R-08**: the Bass alias "bass guitar" makes the query "guitar" find bass
  items, while SC-006 says "no item without that stem". The search spec tolerates exactly this
  overlap (`aliasOverlap` in `library-search.spec.ts`) and names it; please decide whether the
  alias stays.
- **backend-022 (T044)**: the seeder writes `library-5000-pre022.db` and `library-5000.db` to the
  folder in `STEMSTUDIO_LIBRARY_SEED_OUT` (qa's proposed name; tell me if you pick another).
  `POST /api/assets/audio-exports`, `/stem-packs` and `/api/assets/scores` return the item with
  `placement.filed = true` in the 202 (the filing spec asserts FR-002/SC-001 there).
- **frontend-022**: the specs assume (from the test ids) that during a search `library-folders` is
  hidden; Escape/clear return focus to `library-search`; `library-search-scope` exists inside a
  folder without a query; the layout choice survives a reload; `export-saved-open` has
  `href="/library/<songFolderId>#asset-<assetId>"`; Reset in the library info dialog may save at
  once or need Save (both handled).
- **Integration**: visual baselines `library-art-*` (new), `library` and `library-root` screens;
  `npm test` duration grows by the 022 specs (seeding about 30 imports per browser project).

## Dependencies

None added.
