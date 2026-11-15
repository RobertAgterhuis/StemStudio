# Hand-off: devops / polish (T117, CI finalisation)

## What was built

- **CI guards removed** (`.github/workflows/ci.yml`): no `hashFiles(...)` guards are left. A missing
  component now fails the job instead of skipping it. The real owner commands are wired in:
  - `quality`: setup-uv on the `tools/quality` project. Runs `uv lock --check`, `ruff check .`,
    `ruff format --check .`, `mypy` and `pytest --cov --cov-branch` (`fail_under = 90` in pyproject).
    Then `uv run --project tools/quality python tools/quality/run_all.py --report $RUNNER_TEMP/SIG-REPORT.md`
    and uploads the report as an artifact. These are the exact quality-gate commands.
  - `worker` fixture step: runs the commands from TEST-STRATEGY section 9, with `tests/fixtures/pyproject.toml` as
    `-c`, `--cov-config` and mypy `--config-file`. This closes F3 part 1.
  - `e2e-lint`: `npm ci`, `npm run lint`, **`npm run typecheck`** (closes F3 part 2) and `npx playwright test --list`.
  - `e2e`: installs ffmpeg (the MP3 fixture encoder) and generates the fixtures. Then compose `--build --wait` with
    `fake`, the health smoke, Playwright Chromium + Firefox `npm test`, and **`docker compose restart api web`
    followed by `npm run test:restart`** (FR-022 persistence).
  - `licences`: always reports the e2e npm tree.
  - `sonar` (only when the `SONAR_TOKEN` secret is set): the begin arguments come from
    `tools/quality/sonar_args.py`, read with `mapfile`. The job also runs the worker pytest (`coverage.xml`) and
    the engine `test:coverage` (`lcov.info`), so the C#, Python and TS coverage all reach the scanner.
- **`dotnet-tools.json`**: adds `dotnet-sonarscanner` 12.0.0 with `rollForward: true`. The tool targets
  netcoreapp3.1 and runs on the .NET 10 SDK only with roll-forward. Verified with `dotnet tool restore` and
  `dotnet sonarscanner` ("SonarScanner for .NET 12.0").
  - 12.1.0 is the newest version, but it was published on 2026-11-05, which is under the two-week rule.
- **`scripts/test-all.{ps1,sh}`**: the skip logic is removed, so a missing working directory fails the step.
  The steps match CI: quality-gate self-checks + run_all, the fixture checks with config, `fixtures generate`,
  and e2e `lint`, `typecheck`, `playwright test --list`.
- **Image fix** (`docker/api.Dockerfile`, `docker/web.Dockerfile`): `--no-restore` is dropped from `dotnet publish`.
  - The rebuild failed with `NETSDK1064 Package Microsoft.AspNetCore.OpenApi 10.0.15 was not found`.
  - Cause: the restore layer stayed cached while the BuildKit NuGet cache mount had been pruned.
  - The implicit restore is a no-op when the packages are present.

## Files changed

`.github/workflows/ci.yml`, `scripts/test-all.ps1`, `scripts/test-all.sh`, `docker/api.Dockerfile`,
`docker/web.Dockerfile`, `dotnet-tools.json` (CTO-authorised for this task), `specs/001-stemstudio-mvp/tasks.md`
(T117 ticked), this note.

## Tests and coverage

### `pwsh scripts/test-all.ps1 -ContinueOnError` (Windows, merged tree at 4502808)

| Step | Result | Detail |
|---|---|---|
| dotnet tool restore / build / format | passed | |
| dotnet test + coverage gate | **failed** | 598 tests passed, 0 failed. The gate fails on Persistence branch coverage (see below). |
| worker uv sync / ruff / format / mypy | passed | |
| worker pytest + coverage | passed | 386 passed, 2 deselected; 1529 stmts, 178 branches, 100 % |
| engine npm ci / lint / typecheck / test:coverage / build | passed | 121 tests; 100 % lines, 100 % branches (147/147) |
| contracts lint (Redocly 2.78.0) | passed | |
| quality ruff / format / mypy / pytest | passed | 119 passed, 100 % |
| quality gate (run_all) | **failed** | Only the 2 known errors (see below). 336 files, 2447 units, 15 clones, 120 warnings. |
| fixtures ruff / format / mypy / pytest | passed | 31 passed, 100 % |
| fixtures generate | passed | |
| e2e npm ci / lint / typecheck / list | passed | `Total: 40 tests in 5 files` |

.NET per component (line / branch):

| Component | Tests | Line | Branch |
|---|---|---|---|
| Domain | 173 | 97.94 % | 97.77 % |
| Persistence | 21 | 99.16 % | **83.33 %** |
| Web | 184 | 99.84 % | 93.37 % |
| Api + Contracts | 220 | 100 % | 97.72 % (Contracts 100 %) |

The Persistence gap is the only one: `src/StemStudio.Persistence/Repositories/UnitOfWork.cs:31`
(`IsUniqueViolation`) covers 5/6 conditions. The untested case is a `DbUpdateException` whose inner exception
is not a unique/PK violation (or is not a `SqliteException`).

The two quality-gate errors are exactly the ones the backend is fixing in parallel:

```
tests/StemStudio.Api.Tests/TestSupport/ListLoggerProvider.cs:29: error [parameters] ListLogger::Log<TState> has 5 parameters (max 4)
tests/StemStudio.Domain.Tests/Mixes/MixTests.cs:143: error [parameters] MixTests::Validate_RejectsMetronomeOutOfRange has 5 parameters (max 4)
quality gate: 336 files, 2447 units, 15 clones, 2 errors, 120 warnings -> exit 1
```

### Compose (T117)

```
docker compose config -q                                              -> exit 0
docker compose -f docker-compose.yml -f docker-compose.gpu.yml config -q  -> exit 0
docker compose config --services -> worker, api, web
docker compose config --volumes  -> worker-data, model-cache, api-data, web-keys
docker compose build             -> stemstudio/{api,web,worker}:local Built (after the --no-restore fix)
STEMSTUDIO_SEPARATOR=fake docker compose up -d --wait --wait-timeout 300 -> worker/api/web Healthy
GET :8081/health -> {"status":"ok"} HTTP 200
GET :8080/health -> {"status":"ok"} HTTP 200
worker /health (in container) -> {"status":"ok","separator":"fake","device":"cpu","contractVersion":"1.0.2"}
GET :8080/js/audio-engine.js -> HTTP 200, 32699 bytes, text/javascript
GET :8080/ -> HTTP 200 Blazor shell (5.8 KB)
docker compose down -> containers and network removed; the 4 volumes are kept
```

`GET /` does not contain `data-testid="legal-notice"`. The web app renders Interactive Server **without
prerendering**: the shell holds only Blazor component markers, and the import page (with the legal notice)
appears after the circuit connects. The browser run confirms that the notice renders: the import specs use
`legal-notice` via `e2e/pages/import-page.ts` and pass. If a no-JS check of the legal notice is wanted,
frontend would have to enable prerendering on the import page.

Image sizes (`docker image inspect .Size`): worker 3096 MB, api 395 MB, web 376 MB.

### Smoke E2E against the stack (once)

`npx playwright test --project=chromium --grep-invert "@real|@restart"` with
`E2E_BASE_URL=http://localhost:8080` and `E2E_API_URL=http://localhost:8081`:

```
18 passed (29.9s)     # health 2, import 9, main-flow 1, mixer 6; 0 failed
```

Firefox and `@restart` were not run here. CI runs both; T122 (qa) owns the full run.

## Quality checks

```
actionlint 1.7.12 (docker rhysd/actionlint:1.7.12, -verbose): ci.yml and e2e-real.yml -> "Found total 0 errors", exit 0
shellcheck v0.11.0 scripts/test-all.sh scripts/dev.sh -> exit 0
Invoke-ScriptAnalyzer scripts/test-all.ps1 (Warning,Error) -> 0 findings
```

## CI job list (ci.yml)

`dotnet`, `sonar` (secret-gated), `worker` (+ fixture generator), `audio-engine`, `contracts`, `quality`,
`e2e-lint`, `licences`, `e2e` (needs dotnet, worker, audio-engine, contracts, quality, e2e-lint).
`e2e-real.yml` is unchanged: dispatch + weekly, real Demucs.

## Dependencies added

| Name | Version | Licence | URL |
|---|---|---|---|
| dotnet-sonarscanner (local tool, CI only) | 12.0.0 | **LGPL-3.0** (not MIT; read from the package's `licenses/LICENSE.txt`) | https://github.com/SonarSource/sonar-scanner-msbuild |
| CI only: rhysd/actionlint image (local validation) | 1.7.12 | MIT | https://github.com/rhysd/actionlint |
| local only: koalaman/shellcheck image | v0.11.0 | GPL-3.0 (tool, not distributed) | https://github.com/koalaman/shellcheck |

The scanner is a build tool. It is not shipped in any image and not linked into the product. Even so, the
CTO should record LGPL-3.0 in THIRD-PARTY-LICENSES (the task brief said MIT).

## Open issues / requests

1. **backend**: Persistence branch coverage is 83.33 % (< 90). Add a test for `UnitOfWork.SaveChangesAsync` with a
   `DbUpdateException` that is not a unique/PK violation (for example a NOT NULL or FK violation, or a non-Sqlite
   inner exception) at `UnitOfWork.cs:31`. CI's `dotnet` job fails until then.
2. **backend**: the two SIG parameter errors (ListLoggerProvider.cs:29, MixTests.cs:143) are already being fixed.
3. **CTO**: the `dotnet-sonarscanner` licence is LGPL-3.0, not MIT (see the table above).
4. **CTO**: CI now runs `npm run test:restart` after `docker compose restart api web`. If qa's @restart spec
   is not green yet (T122), the `e2e` job fails at that step by design.
5. **frontend (optional)**: there is no prerender, so `/` holds no server HTML for the legal notice (see Compose).
6. `.gitignore` still lacks `coverage.xml` (repeat of foundational item 1).

## Next owner

CTO: merge. Then backend (items 1 and 2), and qa T122 (Chromium + Firefox, @restart).
