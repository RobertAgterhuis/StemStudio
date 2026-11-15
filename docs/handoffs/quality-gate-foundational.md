# Hand-off: quality-gate / foundational (T004-T010)

## What was built
- `tools/quality/` is a small uv project (flat scripts, `package = false`, Python 3.12):
  - `sig_check.py` runs lizard 1.24.3 over C#, Python, TypeScript and Razor `@code` blocks. Razor bodies are cut out and measured as C#, with line numbers kept.
    - Hard (exit 1): NLOC > 30, CCN > 10, params > 4.
    - Soft (warning): NLOC > 15, CCN > 5, and a Razor `@code` block > 30 lines.
    - Test code (`tests/`, `e2e/`, `*.Tests`, `test_*.py`, `*.test.ts`): unit size is reported as a warning only. CCN and params still fail.
    - Writes JSON with `--json`.
  - `duplication.py` runs `npx --yes jscpd@5.4.2` with `.jscpd.json`: minLines 6, minTokens 40, formats csharp/python/typescript/tsx/razor.
    - A clone that touches `src/` or `worker/src/` production code is an error. A clone between test files only is a warning.
    - If Node/npx is missing, the tool-error exit code is 2.
  - `dependency_check.py` checks two graphs and fails on forbidden edges and cycles. Modules missing from the map are warnings.
    - .NET `ProjectReference` graph: Domain and Contracts reference nothing; Persistence -> Domain; Api -> Domain/Persistence/Contracts; Web -> Contracts.
    - Worker import graph (AST, absolute and relative imports):
      - ingest, separation, analysis and jobs may import only `ports`, `schemas` and `config`.
      - api may import those plus `jobs`.
      - `main.py` is unrestricted (composition root).
  - `run_all.py` is the one-command gate: `--report` writes the Markdown file. Exit codes: 0 pass, 1 violation, 2 tool error.
  - `report.py` holds the risk profiles, indicative stars and per-owner refactor requests.
  - `sonar_args.py` turns `sonar-project.properties` into `/d:` arguments for `dotnet sonarscanner begin`.
  - `requirements.txt` is exported from `uv.lock` for the pip path in Appendix B.
- `sonar-project.properties` covers three languages:
  - Sources and tests per component, exclusions, CPD exclusions for the samples.
  - Coverage reports: `sonar.cs.opencover.reportsPaths`, `sonar.python.coverage.reportPaths`, `sonar.javascript.lcov.reportPaths`.
  - The header documents the begin -> build -> test -> end flow, with the token only in `SONAR_TOKEN`.
- `docs/quality/SIG-REPORT.md` is the baseline run on the skeleton.

Red to green: commit `f518964` (tests + samples only; `pytest` -> "8 errors during collection") -> `1bd7528` (implementation, all green). I wrote the tests and the code in the same session and committed the tests first.

## Files changed
`tools/quality/{pyproject.toml,uv.lock,requirements.txt,README.md,.jscpd.json,common.py,razor.py,sig_check.py,duplication.py,dependency_check.py,report.py,run_all.py,sonar_args.py}`, `tools/quality/tests/**` (8 test modules + 12 samples), `sonar-project.properties`, `docs/quality/SIG-REPORT.md`, this note, and the T004-T010 ticks in `specs/001-stemstudio-mvp/tasks.md`.

## Tests and coverage
`uv run --directory tools/quality pytest --cov --cov-branch --cov-report=term-missing`
```
common.py 100% | dependency_check.py 100% | duplication.py 100% | razor.py 100%
report.py 100% | run_all.py 100% | sig_check.py 100% | sonar_args.py 100%
TOTAL 575 stmts, 116 branches, 0 missed -> 100.00% line and 100% branch (fail_under = 90)
119 passed in 0.63s
```

## Quality checks
- `ruff check .`: All checks passed. `ruff format --check .`: 17 files already formatted. `mypy` (strict): no issues in 16 source files.
- `uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md`:
  `quality gate: 69 files, 225 units, 1 clones, 0 errors, 4 warnings -> exit 0`.
  - The gate also checks `tools/quality` itself, with no findings.
  - Warnings: 3 test units > 15 NLOC, and one 8-line test clone (Api.Tests HealthEndpointTests / Web.Tests WebHostTests).

## Dependencies added
| name | version | licence | url |
|---|---|---|---|
| lizard | 1.24.3 | MIT | https://github.com/terryyin/lizard |
| pathspec (via lizard) | 1.1.1 | MPL-2.0 | https://github.com/cpburnz/python-pathspec |
| pygments (via lizard) | 2.21.0 | BSD-2-Clause | https://pygments.org |
| jscpd (npx, not installed) | 5.4.2 | MIT | https://github.com/kucherenko/jscpd |
| dev: pytest 9.2.0, pytest-cov 7.1.0, ruff 0.17.5, mypy 2.4.1 | | MIT / MIT / MIT / MIT | pypi.org |

pathspec is MPL-2.0, which is weak copyleft at file level. It is flagged for the CTO but is acceptable as an unmodified dependency.

## Open issues / requests
- **devops (T016 `quality` job):**
  - Python 3.12 + uv, Node 22.
  - Run `uv run --directory tools/quality ruff check .`, then `ruff format --check .`, `mypy`, `pytest --cov --cov-branch --cov-report=xml`.
  - Then run `uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md` and upload the report as an artifact.
- **devops (Sonar):** begin with `dotnet sonarscanner begin /k:stemstudio $(uv run --project tools/quality python tools/quality/sonar_args.py)`. This needs `dotnet-sonarscanner` in `dotnet-tools.json`, which is architect-owned, so this is a request.
  - Multi-language analysis is on by default in SonarScanner for .NET. It analyses Python from scanner v10.0+.
  - I could not confirm in the SonarSource docs that the .NET scanner reads the `SONAR_TOKEN` environment variable. The docs show `/d:sonar.token`. Please verify this against the scanner version you use, and keep the token out of logs and command lines.
- **architect:** `worker/tests/test_schemas.py` has two tests at 16 NLOC (warning only).
- **backend/frontend:** the 8-line setup clone between `HealthEndpointTests.cs` and `WebHostTests.cs` is a warning only. A shared helper is not possible across separate test projects without a shared test-utilities project, so it is fine to ignore for now.
- **Star estimates** in the report come from a house heuristic in `report.py`, not from SIG's benchmark. Sigrid is authoritative.

## Next owner
devops (T016 CI jobs), CTO (merge order: quality-gate first). quality-gate continues with T109 (polish) and T121 (re-run after merge).
