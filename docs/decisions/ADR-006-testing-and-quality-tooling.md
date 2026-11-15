# ADR-006: Testing, coverage and maintainability tooling

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: architect agent, on the CTO brief

## Context

The constitution requires >= 90 % line and branch coverage per component, fakes for heavy tools,
one real-model integration test, Playwright E2E for the main flow and automated SIG checks.

## Decision

| Component | Tests | Coverage | Lint |
|---|---|---|---|
| .NET (Domain, Persistence, Api, Web) | xUnit v3, Shouldly, NSubstitute, bUnit, WebApplicationFactory, FakeTimeProvider | coverlet (Cobertura) via `--collect "XPlat Code Coverage"` + `tests/coverlet.runsettings`, or `-p:CollectCoverage=true` | analyzers `latest-recommended`, warnings as errors, `dotnet format` |
| worker | pytest, httpx TestClient, fakes for every port; one `@pytest.mark.integration` Demucs test (deselected by default) | pytest-cov, branch, `fail_under = 90` | ruff (incl. C90/PLR limits), ruff format, mypy --strict |
| audio engine | Vitest + jsdom + AudioContext mock | v8, thresholds 90 % lines/branches/functions/statements | ESLint (typescript-eslint strict type-checked), `tsc --noEmit` |
| E2E | Playwright (TypeScript) against compose with `STEMSTUDIO_SEPARATOR=fake` | n/a | n/a |
| SIG | `tools/quality/sig_check.py`: lizard (unit size, CC, params), jscpd (>= 6-line duplicates), dependency direction | | Sonar-compatible properties |

Generated code (`*.generated.cs`, `*.g.cs`, EF migrations) is excluded from coverage.
FluentAssertions is not allowed (paid licence since v8).

## Consequences

- CI (devops) runs exactly these commands; agents report the same numbers in hand-offs.
- Coverage is measured per test project; Contracts are covered through Api/Web tests.

## Amendment 2026-11-14: per-component coverage gate and Sonar formats (Phase 0, K3/K4)

- **Gate = coverlet.msbuild** (`dotnet test -p:CollectCoverage=true`), configured once in
  `Directory.Build.props`: each test project `StemStudio.X.Tests` includes only `[StemStudio.X]*`
  and fails below 90 % line AND branch (`Threshold=90`, `ThresholdType=line,branch`,
  `ThresholdStat=Minimum`). Without the filter, referenced assemblies (e.g. Contracts at 0 % in
  Web.Tests) would fail the minimum.
- **Contracts** are not pure records (`ApiRoutes`, `StemNames`, `ContractLimits` contain code), so
  they are gated, through `StemStudio.Api.Tests` (`Include=[StemStudio.Api]*,[StemStudio.Contracts]*`).
  Domain and Persistence are gated by their own test projects even though Api tests also execute them.
- **Formats**: `CoverletOutputFormat=cobertura,opencover` -> `tests/<Project>/TestResults/coverage/`
  `coverage.cobertura.xml` (CI summary, humans) and `coverage.opencover.xml` (SonarQube
  `sonar.cs.opencover.reportsPaths`). The collector path (`--collect "XPlat Code Coverage"` with
  `tests/coverlet.runsettings`) is report-only and also emits both formats.
- **Sonar for .NET** needs the scanner around the build:
  `dotnet sonarscanner begin /k:stemstudio /d:sonar.cs.opencover.reportsPaths="tests/**/TestResults/coverage/coverage.opencover.xml"`
  -> `dotnet build` -> `dotnet test -p:CollectCoverage=true` -> `dotnet sonarscanner end`
  (token only via the `SONAR_TOKEN` environment variable, never on the command line). Python (`coverage.xml`) and
  TypeScript (`lcov.info`) are read from `sonar-project.properties`.
- **Engine lint**: `eslint-plugin-jsdoc` (`jsdoc/require-jsdoc` on exported functions, classes,
  interfaces, type aliases and interface methods) enforces Constitution III for TypeScript.
