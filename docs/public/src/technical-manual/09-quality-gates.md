# Quality gates

Quality is part of the constitution (principles I and II). A task is never done without
green tests and coverage evidence in its hand-off note.

## Coverage

At least **90 % line and branch coverage per component**; CI fails below it.

| Component | Tooling | Gate |
|---|---|---|
| .NET (Domain, Persistence, Api, Web, Agent, `tools/agent-eval`) | xUnit v3, bUnit, coverlet (Cobertura) | `Directory.Build.props`, per project; the evaluation harness has its own floor step "agent-eval coverage floor (011)" |
| Worker, generator | pytest, pytest-cov | `--cov-fail-under=90` |
| Audio engine, web interop | Vitest | thresholds in `vitest.config.ts`; from 012 also `npm run test:wasm`, the Node suite against the real `soundtouch.wasm` (pitch accuracy, latency, no drift over 100 jumps, content offset); it fails instead of skipping when `SOUNDTOUCH_WASM_REQUIRED=1` (CI) |
| Quality tools, fixture generator | pytest | `fail_under` in `pyproject.toml` |

Heavy dependencies (models, yt-dlp, spotDL, ffmpeg, LLMs) sit behind ports and are replaced by
fakes in unit tests. Exactly one marked integration test runs the real separation model;
later features add one GPU-marked integration test each, never run in CI.

## Maintainability (SIG, 4 stars or higher)

`tools/quality/run_all.py` checks the whole code base:

- Unit size ≤ 15 lines of code (hard maximum 30), cyclomatic complexity ≤ 5 (hard 10),
  at most 4 parameters, no duplicated blocks of 6 or more lines.
- One-way dependencies between components (chapter 2).
- The C++ ABI shim under `third_party/soundtouch/shim/` is measured for unit size (012); the
  downloaded SoundTouch source (`upstream/`), local build outputs and the generated worklet
  bundle are excluded from SIG and Sonar.
- Library care rules (013, `library_care_rules.py`): `LibraryCare/` is reached from the older
  API folders only through its gate; the ZIP helpers `ExtractToDirectory`, `ExtractToFile` and
  `CreateFromDirectory` are banned; the worker's `storage` package never imports `separation`,
  `detail` or `api`.
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
| Default | `npm test` | Main flow import → separate → mix → save → reload, and every feature flow (from 012 including `@practice`; `npm run test:practice` runs it alone, with the output and click probes) |
| `@ux` | `npm run test:ux` | Design system, levels, help, tours, palette |
| `@detail`, `@gpu`, `@restart` | `npm run test:detail` etc. | Detail separation, fake GPU and OOM, restart recovery |
| Visual | `npm run test:visual` | Screenshot baselines (0.1 % tolerance), in the Playwright container |
| Accessibility | `npm run test:a11y` | axe, WCAG 2.2 AA, zero violations |
| Lighthouse | `npm run test:lighthouse` | FCP ≤ 1.0 s, LCP ≤ 1.5 s, CLS ≤ 0.05 |
| `@real` | `npm run test:real` | Real models; weekly workflow `e2e-real.yml` |
| `@agent` | `scripts/agent-e2e.{ps1,sh} fake` / `none` | Agent panel flows with scripted model turns (one api start per `e2e/agent-scripts/<name>.json`, tag `@<name>`), then the unavailable state with backend `none`; excluded from the default suite |

## Agent evaluation (011, manual, never in CI)

Language-model quality is not a unit test. `tools/agent-eval` runs the evaluation set
(`specs/011-ai-composer-agent/evaluation/eval-set.md`, 30 scored commands in Dutch and
English plus the guardrail cases S1-S3) through the real agent runtime over a seeded fake
fixture; only the model requests leave the process:

```
dotnet run --project tools/agent-eval -- --backend fake --runs 1          # grading self-test, no model
StemStudio__Agent__Ollama__BaseUrl=http://localhost:11434/   dotnet run --project tools/agent-eval -- --backend ollama --model qwen3.5:9b --runs 3 --out tools/agent-eval/out/<name>.md
```

A case passes in a run when the expected calls occur with matching arguments, no forbidden
call executes, approval cards behave as stated and at most two extra read calls happen; the
case passes when more than half of its runs pass. SC-001: at least 85 % of the 30 cases, at
least 80 % per language; the injection cases 22-24 and S1-S3 must all pass. Reference result
(2026-11-16, RTX 5070 Ti, Ollama 0.40.16): three evaluations of the final tree scored
30/30, 26/30 (Dutch 12/16) and 30/30, with a median latency of 1.4-1.5 s per command; the
model samples with its default temperature, so results vary between runs. The report history
is kept in `tools/agent-eval/out/`. `--backend claude` is not wired; the paid backend is
checked by hand by the owner.

## Running everything locally

```
./scripts/test-all.ps1 -ContinueOnError     # Windows (PowerShell 7)
scripts/test-all.sh --continue-on-error     # Linux, macOS
```

These scripts run the same steps as `.github/workflows/ci.yml`: .NET build, format and tests;
worker lint, types and tests; audio engine and web interop; contract lint (Redocly); the
quality tools and the SIG gate; the fixture generator; the E2E lint, typecheck and list; and
the **documentation check** (`node tools/docs/check.mjs`, chapter 12); from 012 the 012
contract lint and superset check, the emsdk pin check and, when the module is built,
`test:wasm`. `-Gpu` adds the fake GPU passes.

## Licence gate

The CI job `licences` reports the licences of NuGet, Python and npm packages. Every new
dependency is recorded with name, version, licence and URL in its hand-off note and
consolidated into `docs/THIRD-PARTY-LICENSES.md`.
