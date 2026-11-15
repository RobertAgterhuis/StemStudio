# Hand-off: devops / 004 polish (T065)

## What was built

T065 is a validation task: all CI jobs added in T008/T009 (see `devops-004-foundational.md`),
the Docker web build (size and time) and `scripts/test-all.*` including web-interop were checked.
No functional change after commit `e5e916e`; this note and the T065 tick only.

## Files changed

- `docs/handoffs/devops-004-polish.md` (new), `specs/004-design-system-ux/tasks.md` (T065 ticked)

## Tests and coverage

- `scripts/test-all.ps1 -ContinueOnError`: **all 34 steps passed** (29 before + 5 web-interop
  steps), exit 0:
  - StemStudio.Web.Tests 332 passed, Web 99.88 % line / 95.12 % branch
  - StemStudio.Api.Tests 414 passed, 99.65 % / 98.13 %
  - StemStudio.Domain.Tests 261 passed, 98.54 % / 97.67 %
  - StemStudio.Persistence.Tests 48 passed, 98.56 % / 100 %
  - worker pytest 765 passed (3 deselected), 99.97 % (gate 90 %)
  - audio engine 160 passed; **web-interop 11 passed, 100 % statements 12/12, branches 0/0,
    functions 11/11, lines 12/12** (vitest gate 90 %), build writes `web-interop.js` 3.20 kB
    and `first-paint.js`
  - tools/quality 183 passed, 100 %; fixtures 86 passed, 100 %; contracts lint (003 OpenAPI) passed
  - quality gate: 574 files, 4396 units, 26 clones, 0 errors, 141 warnings (pre-existing) -> exit 0
  - e2e npm ci, lint, typecheck, list ("Total: 78 tests in 14 files")
- Docker web image: `docker build -f docker/web.Dockerfile` with test tags
  `stemstudio/web:devops004-test` (cached, 13 s) and `stemstudio/web:devops004-nocache`
  (`--no-cache`, warm npm/NuGet cache mounts, **15 s**; the interop stage runs in parallel with the
  engine stage: npm ci 4.7 s, build 0.7 s). Size **395 MB** (394 MB before). Smoke on port 18080:
  `/health` 200, `/js/web-interop.js` 200, `/js/first-paint.js` 200, `/js/audio-engine.js` 200.
  Both test images removed afterwards; `stemstudio/web:local` and the running `stemstudio`
  compose project were not touched.
- `docker compose config -q` -> exit 0.

## Quality checks

- actionlint 1.7.12 (Docker image, with shellcheck on `run:` blocks): ci.yml and e2e-real.yml
  "Found 0 errors in 2 files", exit 0 (also validates the local composite action reference).
- shellcheck v0.11.0 on all six `scripts/*.sh`: exit 0.
- PSScriptAnalyzer 1.25.0 on `scripts/`: 0 errors, 0 warnings (6 pre-existing Information
  findings, positional parameters on private helpers).

## Dependencies added

None in this step (the Playwright CI image is listed in devops-004-foundational.md).

## Open issues / requests

- The new `visual`, `a11y` and `lighthouse` jobs have been validated statically (actionlint) and
  locally in parts (helper scripts, Playwright container with `e2e list`). Their first real run is
  on GitHub after the merge; until qa adds the npm scripts and specs they pass with a notice.
- Requests from devops-004-foundational.md still apply (required checks: CTO; Sonar paths:
  quality-gate; npm script names and layout: qa).
- Local `mcr.microsoft.com/playwright:v1.65.1-noble` (3.67 GB) was pulled for the container test
  and left in place (needed by anyone running the visual suite locally); remove with
  `docker rmi mcr.microsoft.com/playwright:v1.65.1-noble` if not wanted.

## Next owner

CTO (merge, required checks, T067-T070).
