# Hand-off: devops / 004 foundational (T008, T009)

## What was built

- **T008** `src/web-interop` is built and tested everywhere the audio engine is:
  - `docker/web.Dockerfile`: new `interop` stage (`node:22-slim`, `npm ci` with the npm cache mount,
    `npm run build`) next to the `engine` stage; the build stage copies both stages'
    `wwwroot/js/` outputs (`web-interop.js`, `first-paint.js` + maps) before `dotnet publish`.
    Both Node stages run in parallel.
  - `.dockerignore`: local `web-interop.js`, `first-paint.js` and their maps are never copied into
    the context (always built in the image, like `audio-engine.js`).
  - `.github/workflows/ci.yml`: job `web-interop` (npm ci, lint, typecheck, `test:coverage` with
    the 90 % thresholds of `vitest.config.ts`, build, coverage artifact); `e2e` now also needs it;
    licence job adds `license-checker@25.0.1 --start src/web-interop` -> `npm-web-interop.md`;
    the Sonar job also produces `src/web-interop/coverage/lcov.info`.
  - `scripts/test-all.{ps1,sh}`: five steps `web-interop npm ci|lint|typecheck|test:coverage|build`
    after the engine steps.
  - `scripts/dev.{ps1,sh}`: the bundle build builds `src/audio-engine` and `src/web-interop`;
    `-SkipEngineBuild` / `--skip-engine-build` now skips both (name kept for compatibility).
  - `UI_IDS_DOC` is not used by any workflow or script, so nothing to repoint; `CONTRACTS_DIR` stays
    `specs/003-gpu-acceleration/contracts` (DELTA.md: no OpenAPI change in 004).
- **T009** CI jobs `visual`, `a11y`, `lighthouse` against the fake stack:
  - `.github/actions/fake-stack/action.yml` (composite): setup-uv + setup-node, ffmpeg, generated
    audio fixtures, `STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`, health curls.
    The jobs stop the stack with `docker compose down -v` (always) and dump compose logs on failure.
  - `scripts/playwright-container.sh <cmd...>`: runs a command in `e2e/` inside
    `mcr.microsoft.com/playwright:v<pin>-noble`, where `<pin>` is read from
    `e2e/package.json` `@playwright/test` (today 1.65.1; the image can no longer drift from the
    package). Host network (the stack on localhost:8080/8081), `--ipc host`, runs as the calling
    user (`HOME=/tmp`), passes `E2E_BASE_URL`, `E2E_API_URL`, `E2E_RUN_ID`, `CI`.
  - `scripts/ci-e2e-suite.sh <npm-script> <marker> [args...]`: runs `npm run <script> -- args` in
    `e2e/`, but passes with a `::notice::` when the npm script is not defined yet or the marker has
    no specs (`visual/` and `a11y/` need a `*.spec.ts`; `lighthouserc.json` must exist). This is
    the "empty suites pass during the wave" rule; once qa adds the specs the suites really run.
  - `visual`: `npm run test:visual` in the Playwright container; uploads `playwright-report/` and
    `test-results/` (diffs). New `workflow_dispatch` input **`update-baselines`** (boolean): runs
    `test:visual -- --update-snapshots` and uploads `e2e/visual/__screenshots__/` as artifact
    `visual-baselines` for review; nothing is committed.
  - `a11y`: `npm run test:a11y` in the same container (browsers come with the image).
  - `lighthouse`: `npm ci` + `npm run test:lighthouse` on the runner (Chrome of the ubuntu-24.04
    image), uploads `e2e/.lighthouseci/`.
  - All three run on push and every PR (`needs: [web-interop, e2e-lint]`).

## Files changed

- `docker/web.Dockerfile`, `.dockerignore`
- `.github/workflows/ci.yml`, `.github/actions/fake-stack/action.yml` (new)
- `scripts/test-all.ps1`, `scripts/test-all.sh`, `scripts/dev.ps1`, `scripts/dev.sh`,
  `scripts/ci-e2e-suite.sh` (new, +x), `scripts/playwright-container.sh` (new, +x)
- `specs/004-design-system-ux/tasks.md` (T008, T009 ticked)

## Tests and coverage

devops owns no unit-tested code; validation instead:

- `docker compose config -q` -> exit 0.
- `docker build -f docker/web.Dockerfile -t stemstudio/web:devops004-test .` -> exit 0;
  `--no-cache` build (npm/NuGet cache mounts warm) **15 s**; image **395 MB** (was 394 MB:
  +3.2 kB web-interop.js, +0.25 kB first-paint.js, maps, precompressed .gz/.br).
  `/app/wwwroot/js` holds `web-interop.js`, `first-paint.js`, `audio-engine.js`, `mixer-keys.js`.
- Container smoke on a spare port (`-p 18080:8080`, name `devops004-smoke`; the running
  `stemstudio` compose project was not touched): `/health` -> 200 `{"status":"ok"}`,
  `/js/web-interop.js` 200 (3203 B), `/js/first-paint.js` 200 (251 B), `/js/audio-engine.js` 200.
- `scripts/ci-e2e-suite.sh test:visual visual` -> notice "npm script 'test:visual' is not defined",
  exit 0; missing marker -> notice, exit 0; no arguments -> usage, exit 2; defined script with
  specs -> runs and propagates the exit code.
- `scripts/playwright-container.sh scripts/ci-e2e-suite.sh list tests` (Git Bash,
  `MSYS_NO_PATHCONV=1`): image `mcr.microsoft.com/playwright:v1.65.1-noble`, npm ci in the
  container, "Total: 78 tests in 14 files", exit 0.
- `scripts/test-all.ps1 -ContinueOnError`: see devops-004-polish.md (T065).

## Quality checks

- actionlint 1.7.12 (`rhysd/actionlint:1.7.12`, includes shellcheck of the `run:` blocks):
  "Found 0 errors in 2 files", exit 0.
- shellcheck v0.11.0 on `scripts/{test-all,dev,ci-e2e-suite,playwright-container,check-cpu-image,gpu-smoke}.sh`: exit 0.
- PSScriptAnalyzer 1.25.0 on `scripts/`: 0 errors, 0 warnings (6 pre-existing `Information`
  PSAvoidUsingPositionalParameters on the private helpers of dev.ps1, same style as before).

## Dependencies added

| Name | Version | Licence | URL | Note |
|---|---|---|---|---|
| mcr.microsoft.com/playwright (CI image) | v1.65.1-noble (follows the `@playwright/test` pin) | Apache-2.0 | https://github.com/microsoft/playwright | CI runtime only, not shipped |

No new npm/NuGet/Python packages (driver.js etc. were added by the architect in Phase 0).

## Open issues / requests

- **CTO / repo admin**: make `visual`, `a11y` and `lighthouse` (and `web-interop`) required status
  checks in the branch protection of `main` once qa's specs exist; YAML cannot do that.
- **qa (T011)**: the jobs call `npm run test:visual`, `test:a11y` and `test:lighthouse` in `e2e/`.
  Expected layout: `e2e/visual/*.spec.ts`, `e2e/a11y/*.spec.ts`, `e2e/lighthouserc.json`, snapshots
  under `e2e/visual/__screenshots__/`, reports in `playwright-report/`, `test-results/` and
  `.lighthouseci/`. `test:visual` must accept extra Playwright arguments (`--update-snapshots`).
  The suites see the stack at `E2E_BASE_URL=http://localhost:8080` / `E2E_API_URL=http://localhost:8081`.
  Keep `@playwright/test` pinned exactly; the container image version is derived from it.
- **quality-gate**: add `src/web-interop/coverage/lcov.info` to `sonar.javascript.lcov.reportPaths`
  and `src/web-interop/{vite,vitest}.config.ts`, `eslint.config.js` and
  `src/StemStudio.Web/wwwroot/js/{web-interop,first-paint}.js(.map)` to the Sonar exclusions
  (`sonar-project.properties` is quality-gate owned); the CI Sonar job already produces the lcov.
- Not run: `scripts/dev.ps1 -Fake -SmokeTest` (ports 8080/8081 are held by the running
  `stemstudio` compose project, which must stay up); the bundle build it calls is the same
  `npm ci` + `npm run build` that test-all runs.

## Next owner

qa (T011 npm scripts and specs feed these jobs), quality-gate (Sonar paths), CTO (required checks).
devops continues with T065 (HO:polish).
