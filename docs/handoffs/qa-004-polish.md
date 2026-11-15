# Hand-off: qa / 004 polish (T064) and wave summary

## What was built

- `e2e/lighthouserc.json`: desktop preset, 3 runs, performance only, headless Chromium flags for
  the container; assertions (median) FCP <= 1000 ms, LCP <= 1500 ms, CLS <= 0.05; reports to
  `e2e/lighthouse-report/` (filesystem target, git-ignored).
- `e2e/lighthouse/urls.setup.ts` (project `lighthouse-setup`): seeds the shared data and writes
  `.state/lighthouse-urls.json` with the URLs of Import, Tracks, Track, Mixes and Mixer and
  Playwright's Chromium path.
- `e2e/lighthouse/run-lighthouse.mts`: `lhci autorun --config=lighthouserc.json` with
  `--collect.url=...` x 5 and `--collect.chromePath=...`, started through `process.execPath`
  (argument list, no shell, works on Windows and Linux); exit code 1 on any failure.
- `docs/testing/TEST-STRATEGY.md`: suite inventory and SC -> evidence map appended to the
  traceability matrix (10.7).

## Files changed

`e2e/lighthouserc.json`, `e2e/lighthouse/urls.setup.ts`, `e2e/lighthouse/run-lighthouse.mts`,
`docs/testing/TEST-STRATEGY.md`, `specs/004-design-system-ux/tasks.md` (T064).

## Tests and coverage

Nothing executed against a stack in the wave (T067 is the CTO's). Final wave evidence:

```text
npm run typecheck                    -> 0 errors
npm run lint                         -> 0 problems (--max-warnings 0)
npx playwright test --list           -> Total: 275 tests in 23 files
  chromium + firefox: 78 x 2 (1.0.0-1.2.0, unchanged) + 37 x 2 (@ux)
  visual 60, a11y 62, lighthouse-setup 1
node lighthouse/run-lighthouse.mts   -> without the setup file: clear ENOENT, exit 1 (as intended)
node node_modules/@lhci/cli/src/cli.js --version -> 0.15.1; `collect --help` lists --url, --chromePath
```

## Quality checks

`uv run python sig_check.py ../../e2e` (in `tools/quality`): 61 files, 555 units, **0 errors**,
7 warnings (unit size 16-20 lines; 4 new, in test bodies and the `everyLevelMixerIds` id list;
the hard limit 30 is respected). ESLint enforces complexity <= 5, <= 4 parameters, max depth 3.
`duplication.py` (jscpd via `npx`) was not run in this worktree: the `npx` download was refused by
the sandbox; the quality-gate's `run_all.py` covers it in T066.

## Dependencies added

| Name | Version | Licence | URL |
|---|---|---|---|
| `@lhci/cli` (dev) | 0.15.1 (2025-06-25) | Apache-2.0 (`LICENSE` in the package) | https://github.com/GoogleChrome/lighthouse-ci |

(Added in HO:foundational, verified again here; `lighthouse` 12.6.1 Apache-2.0 transitively.)

## Commands for the CTO (T067)

```bash
# fake CPU stack, from the repository root
export STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5
docker compose up -d --build --wait
cd e2e && npm ci && npx playwright install --with-deps chromium firefox
npm run fixtures
npm run test:legacy        # SC-006: the 1.2.0 suite at Advanced, must be 100 % green
npm run test:ux            # journeys, levels, help, tours, keyboard, theme flash
npm test                   # both together (the default CI run)
npm run test:a11y          # 62 axe scans (container via scripts/playwright-container.sh in CI)
npm run test:lighthouse    # setup project + lhci autorun

# visual: fresh stack, alone, inside mcr.microsoft.com/playwright:v1.65.1-noble
docker compose down -v && docker compose up -d --build --wait
#   CI: scripts/playwright-container.sh with "npm run test:visual"
#   generate baselines (T067):  ... "npm run test:visual -- --update-snapshots"
#   -> e2e/visual/__screenshots__/screens.spec.ts/*.png (60 files), owner review in T068
#   by hand on Linux:
docker run --rm --network host --ipc=host -v "$PWD:/work" -w /work/e2e \
  mcr.microsoft.com/playwright:v1.65.1-noble sh -c "npm ci && npm run test:visual -- --update-snapshots"
```

## Deviations

1. Visual: the spec skips itself outside the Playwright Linux image (detected by `/ms-playwright`,
   or `E2E_VISUAL_CONTAINER=1`) instead of failing, so `npm run test:visual` on Windows reports
   skipped, never a Windows baseline.
2. Visual masks include `page.locator('time')`, the suite's only non-test-id selector (cosmetic).
3. SC-008 counts a text entry as one action and starts after an API import (no URL import on the
   fake stack); measured flow = 20 actions.
4. `tours.spec.ts` reads the front end's tour JSON at run time (step counts).
5. a11y runs on Chromium only (axe results do not depend on the engine for these rules).
6. Theme flash runs in the default suite (Chromium + Firefox), tagged `@ux`, not in `visual`.

## Open issues / requests

Consolidated from HO:us1..us5 for the frontend: `<time>` for dates; raw `detail-status` text;
"Panned" in `pan-marker-*`; stable `transport-state`; help button opens the screen topic (its own
marker ignored); `mute-*` -> `mixer-mute-solo`, `pan-*` -> `mixer-pan` with
`help-tooltip-mixer-pan` + `help-learn-more`; `help-tour-start-<id>` listed for the mixer tours;
`mixer-detail-groups` marker only when a group exists; tour keys ArrowRight/Enter/Escape;
`Mod+K` works while a text field has focus; palette action for "open mixer"; `Mod+S`
preventDefault and focus `mix-name-input`; tour versions mirrored in `support/preferences.ts`.

## Next owner

CTO: T067 (run everything above, generate baselines), T068 (baseline review), T069 (licences:
`@axe-core/playwright` 4.14.0 MPL-2.0, `@lhci/cli` 0.15.1 Apache-2.0, dev only).
