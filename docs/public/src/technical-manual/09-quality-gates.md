# Quality gates

Quality is part of the constitution (principles I and II). A task is never done without
green tests and coverage evidence in its hand-off note.

## Coverage

At least **90 % line and branch coverage per component**; CI fails below it.

| Component | Tooling | Gate |
|---|---|---|
| .NET (Domain, Persistence, Api, Web, Agent) | xUnit v3, bUnit, coverlet (Cobertura) | `Directory.Build.props`, per project |
| Worker, generator | pytest, pytest-cov | `--cov-fail-under=90` |
| Audio engine, web interop | Vitest | thresholds in `vitest.config.ts` |
| Quality tools, fixture generator | pytest | `fail_under` in `pyproject.toml` |

Heavy dependencies (models, yt-dlp, spotDL, ffmpeg, LLMs) sit behind ports and are replaced by
fakes in unit tests. Exactly one marked integration test runs the real separation model;
later features add one GPU-marked integration test each, never run in CI.

## Maintainability (SIG, 4 stars or higher)

`tools/quality/run_all.py` checks the whole code base:

- Unit size ≤ 15 lines of code (hard maximum 30), cyclomatic complexity ≤ 5 (hard 10),
  at most 4 parameters, no duplicated blocks of 6 or more lines.
- One-way dependencies between components (chapter 2).
- The **raw-colour guard** (`css_tokens_check.py`): only
  `src/StemStudio.Web/wwwroot/css/tokens.css` may contain literal colours.

Lint: .NET with warnings as errors and `dotnet format --verify-no-changes`; Python with ruff
and mypy `--strict`; TypeScript with eslint and `tsc --noEmit`. SonarQube analysis runs in CI
when the `SONAR_TOKEN` secret is configured.

## End-to-end and design gates

The Playwright suite (`e2e/`) runs against an isolated compose stack with the fake separator
on Chromium and Firefox:

| Suite | Command | Purpose |
|---|---|---|
| Default | `npm test` | Main flow import → separate → mix → save → reload, and every feature flow |
| `@ux` | `npm run test:ux` | Design system, levels, help, tours, palette |
| `@detail`, `@gpu`, `@restart` | `npm run test:detail` etc. | Detail separation, fake GPU and OOM, restart recovery |
| Visual | `npm run test:visual` | Screenshot baselines (0.1 % tolerance), in the Playwright container |
| Accessibility | `npm run test:a11y` | axe, WCAG 2.2 AA, zero violations |
| Lighthouse | `npm run test:lighthouse` | FCP ≤ 1.0 s, LCP ≤ 1.5 s, CLS ≤ 0.05 |
| `@real` | `npm run test:real` | Real models; weekly workflow `e2e-real.yml` |

## Running everything locally

```
./scripts/test-all.ps1 -ContinueOnError     # Windows (PowerShell 7)
scripts/test-all.sh --continue-on-error     # Linux, macOS
```

These scripts run the same steps as `.github/workflows/ci.yml`: .NET build, format and tests;
worker lint, types and tests; audio engine and web interop; contract lint (Redocly); the
quality tools and the SIG gate; the fixture generator; the E2E lint, typecheck and list; and
the **documentation check** (`node tools/docs/check.mjs`, chapter 12). `-Gpu` adds the fake
GPU passes.

## Licence gate

The CI job `licences` reports the licences of NuGet, Python and npm packages. Every new
dependency is recorded with name, version, licence and URL in its hand-off note and
consolidated into `docs/THIRD-PARTY-LICENSES.md`.
