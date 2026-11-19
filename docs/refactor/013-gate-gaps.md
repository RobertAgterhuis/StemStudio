# 013 Close the silent holes in the 90 % coverage and E2E gates
- Status: proposed · Date: 2026-11-19 · Area: delivery / CI gate correctness · Priority: P2

## Problem statement
Several gates can be skipped or bypassed without CI turning red.

1. **Most devops Python is outside the gate.** `scripts/pyproject.toml:20` measures only `key_volume_checks`,
   `backup_folder_checks` and `api_image_size_checks`, and the CI step (`ci.yml:468-475`) lints, type-checks and tests only those
   three. Not linted, not type-checked and not coverage-gated in CI: `cpu_image_guard.py` (120 lines), `generative_smoke_checks.py`
   (276), `gpu_smoke_checks.py` (166), `library_mount_checks.py` (80), `image_licences.py` (63), `secret_scan.py` (195; only its
   `--self-test` runs, `ci.yml:442`) and `coverage_floor.py` (51). That is about 950 lines, more than the gated three, and
   `secret_scan.py` and `coverage_floor.py` are themselves gates.
2. **Only 2 of 6 .NET test projects have a missing-report guard.** `scripts/coverage_floor.py` exists so that "a component cannot
   drop out of the gate unnoticed" (its docstring, lines 3-5). It runs only for `StemStudio.Agent.Tests` and
   `StemStudio.AgentEval.Tests` (`ci.yml:81-85`). Domain, Persistence, Api(+Contracts) and Web rely on coverlet's threshold alone.
   That threshold does not fire when a project collects no coverage at all, for example after a csproj change that disables
   `CollectCoverage`.
3. **Some E2E tags never run in CI.** `e2e/package.json:11` (`npm test`) excludes `@compose`, `@fresh-library`, `@library-db`,
   `@worker-restart` and `@claude-key`, among others. No step in `ci.yml` or `e2e-real.yml` and no `scripts/*.sh` runs them (grep:
   the only hits are contract paths). Spec files tagged that way: `@compose` 2, `@library-db` 2, `@fresh-library` 1,
   `@worker-restart` 1, `@claude-key` 1. Those tests exist but gate nothing.
4. **Skip-when-absent logic outlived its purpose.** `scripts/ci-e2e-suite.sh:15-18, 39-40` passes (exit 0 with a notice) when an
   npm script or the spec folder is missing. It was meant for the time "while qa adds the specs" (`ci.yml:778-779`), but the suites
   now exist (`e2e/a11y/*.spec.ts`, `e2e/lighthouserc.json`, visual baselines). Renaming `test:a11y` today would turn the a11y gate
   green without running anything. In the same way, `ci.yml:204` skips the backup-fixture gate when
   `tests/fixtures/backup/pyproject.toml` is missing, and that file exists. Agent runs use `--pass-with-no-tests`
   (`scripts/agent-e2e.sh:58,62,68`).
5. **Python tests have no time limit outside e2e-real.** This is a minor point: pytest steps in `ci.yml` rely on the job
   timeout only.

## Impact
- The project's rule ("CI must fail below 90 %") does not hold for the scripts that implement the gates themselves.
- Specs that are written and reviewed give no protection, and regressions in compose wiring and library-database behaviour
  (the `@compose` and `@library-db` tags) go unnoticed.
- Green can mean "skipped", which undermines trust in the required checks.

## Risk
- Leaving it: gate erosion is silent by definition. The first sign is a production defect.
- The change: low. New coverage may initially fail for the scripts that are not tested yet; add tests first, then add each
  module to `source`. Running the extra E2E tags adds runtime, so place them in their own matrix shard
  (`023-e2e-split.md`). Rollback per step.

## Effort
S–M (1.5–3 days, mostly writing tests for the roughly 950 ungated script lines). Assumes the excluded E2E tags work against
the fake stack. If some need special setup, give them a nightly job instead of the PR run.

## Step-by-step plan
1. Safety net: list today's gate inputs (the measured modules, the test projects with a guard, the tags run) in
   `docs/testing/`, as the baseline to compare against.
2. Scripts: add tests in `scripts/tests` for the seven modules, one per PR. Add each module to `[tool.coverage.run] source` and to
   `known-first-party`, and to the `checks=(...)` list in `ci.yml:471` (or better, let CI lint `scripts/*.py` as a glob).
3. .NET guard: replace the two `coverage_floor.py` steps with one loop over `tests/*.Tests/`, which fails when a project has no
   `coverage.cobertura.xml`. Check: temporarily delete one report and see the step fail.
4. E2E tags: decide per tag whether it runs on every PR, nightly, or only by hand. Add a test that every tag used in
   `e2e/**/*.spec.ts` is either run by some CI path or listed in an explicit `manual-only` list with a reason.
5. Remove the skip logic: make `ci-e2e-suite.sh` exit non-zero when the script or the marker is missing (or delete the wrapper
   and call `npm run` directly). Remove the `hashFiles` condition at `ci.yml:204`, and drop `--pass-with-no-tests` where a script is
   known to have specs.
6. Add `--timeout` (pytest-timeout is already used in `e2e-real.yml:58`) to the worker and generator pytest steps.
7. Verify: each removed guard has a red-proof (delete the input on a branch and see CI fail). Roll back per step.

## Dependencies and related proposals
- `022-single-source-checks.md` (new Python cores land directly under this gate).
- `023-e2e-split.md` (a shard for the extra tags).
- `012-e2e-tags-never-run.md` covers item 3 in depth (the tag matrix in `e2e/package.json`). If both are accepted,
  keep item 3 there and drop it here.
