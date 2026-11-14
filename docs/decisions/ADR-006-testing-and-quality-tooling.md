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
