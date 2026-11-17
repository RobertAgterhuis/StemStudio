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

1. **Live search loses typed characters (product, frontend-022; blocks a green E2E).** Typing fast into
   `library-search` sometimes drops a character: Firefox `library-search.spec.ts:87` ("qam" became "qm") and `:157`
   ("<salt> bass" became "<salt>bss", "No results"), failing again on a rerun (`:157`); Chromium `library-search-perf.spec.ts`
   on the 5,000-item database ("snare" became "snre"/"snae", reproduced in 2 of 2 runs). The fix `1ab8337a` (own URL
   echoes) is not enough. Lead: after the loss the inline clear button is gone while the box shows text, so the server's
   `LiveSearchController.Text` was reset (a location event not recognised as its own echo, e.g. a stale or duplicated
   `LocationChanged`) and the next render wrote the older value into the box (`value="@Controller.Text"`). Needs a
   bUnit reproduction with out-of-order/duplicated echoes, then a fix that never lets a location event older than the
   newest own navigation replace the text. Not fixed here: the 3-attempt budget went to the library-db harness and the
   fix needs a frontend lane plus a rerun of the library specs in both browsers.
2. **SC-004 (search p95 <= 400 ms on 5,000 items) not measured** because of item 1 (the perf spec stops at the first
   lost key). SC-003 (pre-022 library filed after start) passed.
3. Known flake: `lyric-assist.spec.ts` "strict shows the licences as ideas" (1 in 6, QA); green in this run.
4. One-off Firefox axe `color-contrast` on `level-beginner` in `library-care-backup.spec.ts:201` (013); green on the
   rerun (transition caught mid-way); not quarantined, watch.
5. 023 T068 (reference-machine lyric run) and T080 (LanguageTool decision after T068), 023 T070 manual NVDA pass on the
   new list (quickstart Q9), 022 T090 QA audit, owner checks Q8/Q10: open as before.

## Cleanup

`docker compose -p e2eint down -v`, `e2eint/*` images removed; the owner's project `stemstudio` untouched.
