# Lint and quality configuration map

Maps every constitution quality rule to the tool and file that enforces it. Run everything with
`scripts/test-all.ps1` (or `.sh`); CI runs the same steps (`.github/workflows/ci.yml`).

| Rule (constitution) | Enforced by | Configuration | Fails CI |
|---|---|---|---|
| Unit size <= 15 LOC (warn), > 30 (error) | `tools/quality/sig_check.py` (lizard) | thresholds in `tools/quality/sig_check.py` | yes (hard limit) |
| Cyclomatic complexity <= 5 (warn), > 10 (error) | `tools/quality/sig_check.py` (lizard) | same | yes (hard limit) |
| <= 4 parameters per unit | `tools/quality/sig_check.py` | same; `sig-exempt: <reason>` marker for framework-mandated signatures (listed in the report) | yes |
| No duplicated blocks of >= 6 lines | `tools/quality/duplication.py` (jscpd) | jscpd settings in `tools/quality/` | yes for production code |
| One-way dependencies, no cycles | `tools/quality/dependency_check.py` | allowed project references and Python package imports in the script | yes |
| .NET analyzers, warnings as errors | MSBuild | `Directory.Build.props` (`TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`), `.editorconfig` | yes |
| .NET formatting | `dotnet format --verify-no-changes` | `.editorconfig` | yes |
| .NET coverage >= 90% line + branch per component | coverlet.msbuild | `Directory.Build.props` (`Include=[StemStudio.<Component>]*`, `Threshold=90`) | yes |
| Python lint/format | ruff | `worker/pyproject.toml`, `tools/quality/pyproject.toml`, `tests/fixtures/pyproject.toml` | yes |
| Python types | mypy `--strict` | same pyproject files | yes |
| Python coverage >= 90% line + branch | pytest-cov (`--cov-branch`, `fail_under = 90`) | same pyproject files | yes |
| TypeScript lint + TSDoc on exports | ESLint (typescript-eslint, eslint-plugin-jsdoc) | `src/audio-engine/eslint.config.js`, `e2e/eslint.config.js` | yes |
| TypeScript types | `tsc --noEmit` | `tsconfig.json` per package | yes |
| TypeScript coverage >= 90% | Vitest (`@vitest/coverage-v8`) | `src/audio-engine/vitest.config.ts` | yes |
| OpenAPI contracts valid | Redocly CLI | `specs/001-stemstudio-mvp/contracts/redocly.yaml` | yes |
| SonarQube analysis | dotnet-sonarscanner | `sonar-project.properties`, CI job (runs only with `SONAR_TOKEN`) | only when configured |
| OSI licences only | licence report (pip-licenses, nuget-license, license-checker) + manual review | CI artifact, `docs/THIRD-PARTY-LICENSES.md` | report only |
