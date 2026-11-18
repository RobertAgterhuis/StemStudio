# CTO hand-off: round 022 + 023 integration (2026-11-17)

Branch `022-023-round`, from `d399fa5e` (all lane and QA branches merged, incl. `r22-qa023` and `r22-qadef`). Not pushed,
not merged to `main` (the orchestrator does that).

## What was integrated

022 library by artist and album and 023 lyric writing assistant, with the QA follow-ups: the hash-title guard
(`backend-022-hash-title-guard.md`) and the suggestion list as a plain list (`frontend-023-a11y-list.md`). Contracts:
backend-api 1.23.0, ui-test-ids 1.23.0, agent tool catalogue 1.2.0, lyric-assist-output 1.0.0. Migrations of the round
(`AddLibraryFolders`, `AddLyricAssist`, `AddAgentMessageLyricsId`) are consistent with the model snapshot (the pending
model change test in `StemStudioDbContextTests` is green).

## Fixed in this integration (spec and harness only, no product change)

| Commit | What |
|---|---|
| `e23e28c9` | `library-browse:51` "all three artists on the root page": `library-folders[data-level]` is already `root` on the loading skeleton, so the tiles were read before the first load. `gotoRoot`/`gotoFolder` now wait for `library-folder-counts` (rendered only once the view has loaded). Spec race, no product bug. |
| `7eac67f4` | `test:library-db` ran for the first time: (1) `docker compose cp` copies the fixture database in as root, the api then crash-looped with "attempt to write a readonly database"; the harness now chowns the database files to the api user (1654) with a one-off root container of the api service; (2) the migration spec counted the pending note as gone before the view had loaded (and before the backfill started); it now waits for the view and polls `pendingFiling` through the API; (3) the search-speed spec built only 18 of 30 queries (the seeder's artist names share their prefixes); it now fills up with whole artist names. |

## Verification

See the per-suite table in `docs/STATUS.md` (section "Feature 022 + 023"). Stack: own compose project `e2eint`
(`docker-compose.yml` + `docker-compose.e2e.yml` + an untracked integration override with images `e2eint/*:local` and ports
18580/18581), fake separator, agent backend `none` for the default suite, retries 0; heavy suites one at a time.

## Open items

1. **Live search loses typed characters: fixed** (frontend-022, `docs/handoffs/frontend-022-live-search-keys.md`,
   commits `cb6f2e02`, `9db52012`, `d33a3815`, `d75c2be4`). Three causes: own `?q=` echoes taken for navigations
   (now told apart by a per-controller history entry state, `OwnNavigations`); the page's parameters set again before
   the echo (the page now takes over a URL/state pair once); and the box's `value` was not bound (`value=` plus
   `@oninput` is not kept in step with the browser, so every render wrote the server's text back over keys in flight;
   now `@bind:get/@bind:set`, `@bind:event="oninput"`). Verified on an own stack (`e2efix`, 18680/18681, retries 0):
   `library-search.spec.ts --repeat-each 5` 70/70 (Chromium + Firefox); browse, filing, info, feature-levels 70 passed /
   4 skipped; `test:library-db`: migration SC-003 passed.
2. **SC-004 measured outside the spec: p95 292 ms** (median 252, max 347; 30 queries, 5,000-item fixture, last key to
   the last change in the results, debounce included; no lost key). The spec `library-search-perf.spec.ts` itself still
   fails, now on a spec bug (qa): it waits for a browser request to `/api/library/search`, but in Blazor Server the
   search runs on the server, so no such request is ever made (timeout after 15 s with the right results on screen).
3. Known flake: `lyric-assist.spec.ts` "strict shows the licences as ideas" (1 in 6, QA); green in this run.
4. One-off Firefox axe `color-contrast` on `level-beginner` in `library-care-backup.spec.ts:201` (013); green on the
   rerun (transition caught mid-way); not quarantined, watch.
5. 023 T068 (reference-machine lyric run) and T080 (LanguageTool decision after T068), 023 T070 manual NVDA pass on the
   new list (quickstart Q9), 022 T090 QA audit, owner checks Q8/Q10: open as before.

## Cleanup

`docker compose -p e2eint down -v`, `e2eint/*` images removed; the owner's project `stemstudio` untouched.
