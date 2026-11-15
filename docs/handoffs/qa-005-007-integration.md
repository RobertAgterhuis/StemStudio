# Hand-off: qa / 005+007 integration (005 T059 E2E part + T062 live test, 007 T071 E2E part)

## What was built

Integration run of features 005 (artwork, band info) and 007 (export, library) on branch
`005-007-artwork-export` (worktree from `8efb268`), against a separate compose project
`stemstudio-e2e` (fresh volumes, CPU images, `STEMSTUDIO_SEPARATOR=fake`,
`STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`). The owner's `stemstudio` project and its volumes were not
touched; `stemstudio-e2e` was taken down with `down -v` at the end. Every suite is green with
retries = 0. Failures were triaged as test faults (fixed in `e2e/`) or app faults (fixed in
`src/StemStudio.Web`); one worker bug is reported below (live services only).

Fake online modes were switched by restarting only the worker
(`docker compose -p stemstudio-e2e up -d --no-deps --wait worker` with `STEMSTUDIO_FAKE_ONLINE`),
checked with `docker exec ... printenv STEMSTUDIO_FAKE_ONLINE`.

## Results (final build, retries 0)

| Suite | Command | Result |
|---|---|---|
| Fixtures | `npm run fixtures` | 8 + 13 + 6 fixtures written |
| 001-003 at Advanced (SC-006) | `npm run test:legacy` | **66 / 66 passed** (Chromium 33, Firefox 33), 9.4 min |
| UX (`@ux`) | `npm run test:ux` | **74 / 74 passed** (Chromium 37, Firefox 37), 2.2 min; run again on the final build: 74 / 74 |
| Artwork + band info, mode `found` | `npm run test:artwork` | **32 passed**, 14 skipped (16 + 7 per browser; skips = other modes, `@online`, `@perf`) |
| mode `not-found` | `npm run test:artwork` | **28 passed**, 18 skipped (14 + 9 per browser) |
| mode `failed` | `npm run test:artwork` | **28 passed**, 18 skipped (14 + 9 per browser) |
| mode `off` | `npm run test:artwork` | **28 passed**, 18 skipped (14 + 9 per browser) |
| Export + library | `npm run test:export` | **58 passed**, 8 skipped (Chromium 33; Firefox 25 + 8 = `panner-law` is Chromium-only) |
| Detail (incl. `@detail` export case) | `npm run test:detail` | **26 / 26 passed** (13 per browser), 7.2 min |
| Accessibility | `npm run test:a11y` | **104 / 104 passed** (axe matrix incl. `band-info`, `export-dialog`, `library` + robustness); **0 axe violations** |
| Lighthouse (SC-003) | `npm run test:lighthouse` | setup 1 passed; lhci 5 URLs x 3 runs, all assertions passed |
| Visual (SC-005) | container, `--update-snapshots=all`, then compare | **80 baselines** written on a fresh stack, then **80 / 80 passed** |
| Everything else | `scripts/test-all.ps1 -ContinueOnError` | **all 35 steps passed**, exit 0 |
| Quality gate | `run_all.py --report docs/quality/SIG-REPORT.md` | 1 010 files, 8 033 units, 74 clones, **0 errors**, 259 warnings |
| Live services (005:T062) | `uv run pytest -m integration tests/metadata/test_live_services.py` | **3 passed, 1 failed** after one retry (see below) |

Lighthouse medians (desktop preset, `devtools` throttling, cold cache, 3 runs):

| URL | FCP ms (budget 1000) | LCP ms (budget 1500) | CLS (budget 0.05) |
|---|---|---|---|
| Import `/` | 187 | 321 | 0.000 |
| Tracks | 193 | 304 | 0.000 |
| Track | 171 | 401 | 0.000 |
| Mixes | 201 | 328 | 0.000 |
| Mixer | 196 | 380 | 0.023 |

Visual baselines: 80 (`e2e/visual/__screenshots__/screens.spec.ts/*.png`; 60 of 004 regenerated
because covers changed them, 20 new: `band-info`, `export-dialog`, `library` x 6 cells and
`library` at 360 px x 2). Generated first on a fresh stack, then regenerated once more on a new
fresh stack after the library reflow fix (A2); the second set is committed and confirmed 80 / 80.
Ready for the owner review.

### Live-service test (005:T062), generic User-Agent

`STEMSTUDIO_CONTACT_URL` unset, so the worker sent
`StemStudio/0.1.0 ( local-practice-tool; contact not configured )` (no personal data).

- Run 1: 2 passed (Cover Art Archive, Wikidata + Wikipedia), 2 failed: MusicBrainz search answered
  **503 "currently busy"** (rate limiting on their side), and the Kevin MacLeod lookup failed with
  **HTTP 400**.
- Retry (once, as instructed): 3 passed (the MusicBrainz search now found the Load studio take),
  **1 failed**: `test_full_lookup_of_kevin_macleod_by_isrc`, deterministic, a worker bug (below).

## Fixes

### Test faults (e2e, qa-owned)

1. `tests/band-info.spec.ts` correction: the panel already showed `found` before the correction,
   and an earlier run had already stored `correctedByUser`; the case now submits a title unique to
   the run and browser and polls the band info until it shows that title.
2. `tests/band-info.spec.ts`: lookup results are stored, so the shared track kept its `found`
   result after the worker was switched to `not-found`; the spec now imports a track of its own per
   run and mode (`uniqueWavCopy(..., band-info-<mode>-<run id>)`).
3. `pages/export-dialog.ts` `selectChannels`: `locator.all()` ran before the server had rendered
   the channel boxes (Chromium saved "vocals, mixed"); it now waits for the first box and asserts
   each box state.
4. `tests/export.spec.ts` raw stem: default item names are made unique (data-model 4.4,
   " (2)", " (3)"); the name check accepts the suffix.
5. `tests/library.spec.ts` deleted track: a fresh import starts its detail phase, so DELETE was
   409; new `api.stopDetail` cancels an active phase and waits for it to end.
6. `pages/library-page.ts` `playerDuration`: the players use `preload="none"`, so `readyState` stayed
   0; the helper now requests the metadata (`preload = "metadata"`, `load()`).
7. `support/help.ts` 007 reference queries: "save to library" is the label of every export button,
   so it rightly finds `export-dialog`; the `library` topic is reached with "download" (same as
   frontend's bUnit test).
8. `docs/testing/TEST-STRATEGY.md` 12.5: these rules written down.

### App faults (minimal, test-first)

| # | Fault (found by) | Fix | Test first |
|---|---|---|---|
| A1 | Help query "export range" returned `export-dialog` first (help.spec 007 reference queries, both browsers) | `Help/topics/tracks/export-range.md`: summary starts "Export a range: ...", keyword "export range" | `ExportAccessibilityTests.HelpSearch_FindsTheFeature007Topics("export range", "export-range")` red, then green |
| A2 | Library overflowed to 714 px at 320 px (WCAG 1.4.10; long hash names/track titles and the track filter's longest option); the first 360 px library baseline even showed a 631 px page | `wwwroot/css/components/library.css`: cells wrap anywhere, filters shrink (`min-width: 0`, `max-width: 100%`, percentage width under 48rem), table/tbody block under 48rem, player max-width 100 % | E2E `robustness.a11y` "library has no horizontal overflow at 320 px" red (394 px), then green (CSS) |

## Bugs for other owners

- **audio-ingest (worker), 005**: the ISRC lookup sends
  `GET https://musicbrainz.org/ws/2/isrc/USUAN1400011?inc=artist-credits%2Breleases%2Brelease-groups&fmt=json`,
  which MusicBrainz refuses with **400** `"release-groups is not a valid inc parameter for the isrc
  resource."` (`inc=artist-credits+releases` answers 200). The lookup then fails with
  "Band information could not be retrieved" for every song that has an ISRC (Spotify imports,
  tagged files), instead of falling back. Repro:
  `cd worker && uv run pytest -m integration tests/metadata/test_live_services.py -k kevin`.
  The recorded fixtures in `tests/fixtures/metadata/` therefore do not reflect the real service for
  this request; fix the request (e.g. ISRC -> recording ids, then a recording lookup with
  `inc=releases+release-groups+artist-credits`) and the replayed fixture.
- **architect** (ui-test-ids next minor): additive ids the frontend introduced and the suite does not
  depend on yet: 005 `band-info-close`, `band-info-album`, `band-info-error`, `metadata-correct-cancel`,
  `metadata-correct-error`, `artwork-progress`; 007 `export-close`, `export-error-hint`,
  `library-error`, `library-action-error`, `asset-rename-cancel`, `asset-rename-error`, command id
  `go-library`.
- **frontend (design, owner review)**: on the desktop library the narrow "Status" column wraps
  "Statu s" / "Rea dy" (visible in `library--*` baselines); not an a11y failure, a column width
  choice.

## Files changed

- e2e: `pages/{export-dialog,library-page}.ts`, `support/{api,help}.ts`,
  `tests/{band-info,export,library}.spec.ts`, `visual/__screenshots__/screens.spec.ts/*.png` (80)
- web: `Help/topics/tracks/export-range.md`, `wwwroot/css/components/library.css`
- web tests: `Export/ExportAccessibilityTests.cs`
- docs: `docs/testing/TEST-STRATEGY.md` (12.5), `docs/quality/SIG-REPORT.md`, this note
- tasks ticked: 005 T030, T043, T050, T055; 007 T041, T047, T053, T058, T063, T068

## Tests and coverage

```text
scripts/test-all.ps1 -ContinueOnError: 35/35 steps passed, exit 0
  dotnet: Domain 389, Persistence 66, Web 1004, Api 672 passed (0 failed)
    StemStudio.Web 99.44 % line | 92.47 % branch; Api 99.51 | 95.88; Domain 98.63 | 96.39;
    Persistence 98.35 | 92.85; Contracts 100 | 100
  worker pytest 1596 passed, 7 deselected, 100 % (line + branch)
  engine vitest 204 passed, 100 % statements/branches/lines
  web-interop vitest 232 passed, 99.62 % lines, 97.7 % branches
  tools/quality 314 passed, 100 %; fixtures 159 passed, 100 % line and branch (qa component)
  e2e lint, typecheck clean; playwright --list 447 tests in 31 files
```

## Quality checks

- `npm run lint` (e2e, `--max-warnings 0`), `npm run typecheck`: 0 problems.
- `sig_check.py e2e`: 85 files, 812 units, 0 errors, 15 warnings (unit size 16-20 of spec bodies).
- `css_tokens_check.py`: 0 errors, 0 warnings.
- `run_all.py`: 0 errors, 259 warnings; report committed in `docs/quality/SIG-REPORT.md`.

## Dependencies added

None.

## Open issues / requests

- Worker ISRC request (above) is the only red item: 005:T062 cannot be recorded as fully green
  until audio-ingest fixes it; the MusicBrainz 503 of the first run was rate limiting and passed on
  the retry.
- The 80 baselines need the owner review (covers on the 004 screens, three new screens, the library
  at 360 px after A2).
- `stemstudio-e2e` is down with its volumes removed; images `stemstudio/*:local` were rebuilt from
  this branch.

## Next owner

CTO: baseline review with the owner, 005 T058-T063 / 007 T071-T074 (merge, licences, README,
STATUS); audio-ingest: the ISRC `inc` fix, then a new live-service run.
