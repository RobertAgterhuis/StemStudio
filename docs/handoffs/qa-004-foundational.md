# Hand-off: qa / 004 foundational (T010, T011)

## What was built

- **T010** `docs/testing/TEST-STRATEGY.md`: feature 004 header note, two rows in the gate table
  (web-interop, design-quality suites), section 5 now binds ui-test-ids **1.3.0**, new commands in
  section 9 and a new section 10: storage state per level (10.1), visual regression rules (container
  only, fresh stack, masks, 0.1 % threshold, baseline review; 10.2), the a11y scan matrix (10.3),
  Lighthouse budgets (10.4), help-coverage split between bUnit and E2E (10.5), the SC-008 counting
  rule (10.6) and the traceability US1..US5 -> test files (10.7).
- **T011** `e2e/support/preferences.ts`: typed schema-1 record (`preferencesRecord`), storage states
  `advancedState` (level advanced, welcome dismissed, every tour of `tourVersions` recorded as
  completed), `freshState`, `expertState`, `levelState(level, options)`, plus
  `readStoredPreferences` / `writeStoredPreferences`.
- `e2e/playwright.config.ts`: default `use.storageState = advancedState` (ui-test-ids R7) for every
  context; new projects `visual` (testDir `visual/`), `a11y` (testDir `a11y/`), `lighthouse-setup`
  (`lighthouse/*.setup.ts`); `toHaveScreenshot` defaults (maxDiffPixelRatio 0.001, animations
  disabled, caret hidden, CSS scale) and a platform-free `snapshotPathTemplate`
  (`visual/__screenshots__/<spec>/<name>.png`).
- `e2e/package.json` scripts: every behavioural script now selects `--project=chromium
  --project=firefox` (same tests as before, the new projects never leak into `npm test`); new
  `test:legacy` (1.2.0 set only, excludes `@ux`), `test:ux`, `test:visual`, `test:a11y`,
  `test:lighthouse`. `test:visual` passes extra args through (`npm run test:visual --
  --update-snapshots`), as the devops CI job expects.
- `tsconfig.json` / `eslint.config.mjs` include `visual/`, `a11y/`, `lighthouse/`; `.gitignore`
  adds `.lighthouseci/` and `lighthouse-report/`.

The 1.2.0 specs are not edited. Behavioural equivalence (SC-006) can only be shown against the
running 004 front end: the CTO runs `npm run test:legacy` in T067.

## Files changed

`docs/testing/TEST-STRATEGY.md`, `e2e/support/preferences.ts` (new), `e2e/playwright.config.ts`,
`e2e/package.json`, `e2e/package-lock.json`, `e2e/tsconfig.json`, `e2e/eslint.config.mjs`,
`e2e/.gitignore`, `specs/004-design-system-ux/tasks.md` (T010, T011 ticked).

## Tests and coverage

No stack is run in the wave (tasks.md: suites executed in T067). Evidence:

```text
npm run typecheck                          -> 0 errors
npm run lint                               -> 0 problems (--max-warnings 0)
npx playwright test --project=chromium --project=firefox --list
                                           -> Total: 78 tests in 14 files (unchanged vs 6258eae)
```

Coverage: n/a (behavioural suite, TEST-STRATEGY section 2).

## Quality checks

`uv run python tools/quality/sig_check.py ../../e2e` (from `tools/quality`): 0 errors, 3 warnings,
all pre-existing (`engine-probe.ts` x2, `main-flow.real.spec.ts`).

## Dependencies added (dev, e2e only)

| Name | Version | Licence | URL |
|---|---|---|---|
| `@axe-core/playwright` | 4.14.0 (2026-10-15; still `latest`) | MPL-2.0 | https://github.com/dequelabs/axe-core-npm |
| `axe-core` (transitive) | 4.14.0 | MPL-2.0 | https://github.com/dequelabs/axe-core |
| `@lhci/cli` | 0.15.1 (2025-06-25; 0.16.0/0.16.1 are < 2 weeks old) | Apache-2.0 | https://github.com/GoogleChrome/lighthouse-ci |
| `lighthouse` (transitive) | 12.6.1 | Apache-2.0 | https://github.com/GoogleChrome/lighthouse |

Pinned exactly (`--save-exact`). The lockfile tree after the install (from the lockfile `license`
fields): MIT 339, Apache-2.0 42, ISC 36, BSD-2-Clause 13, BSD-3-Clause 11, BlueOak-1.0.0 4,
MPL-2.0 2, 0BSD 2, CC0-1.0 1, CC-BY-3.0 1 (`spdx-exceptions`, data, pre-existing via
eslint-plugin-jsdoc), and `parse-cache-control` 1.0.1 without an SPDX field whose `LICENSE` is the
BSD-3-Clause text. All OSI-approved or permissive data licences. `@playwright/test` stays 1.65.1.

## Open issues / requests

- frontend: tour versions are mirrored in `e2e/support/preferences.ts` `tourVersions` (all 1); a
  version bump in `Help/tours/*.json` must be reported so qa updates it (else the 1.2.0 screens get
  an offer chip at Advanced).

## Next owner

qa continues with T037 (visual + theme flash).
