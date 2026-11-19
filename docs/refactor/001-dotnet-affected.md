# 001 Affected builds and tests for the .NET monorepo
- Status: proposed · Date: 2026-11-19 · Area: build / CI · Priority: P2

## Problem statement
Every build and test run covers the whole solution `StemStudio.slnx` (13 projects, 15 `ProjectReference` edges, per `.claude/tipoca/state/stack.json`), also when a change touches one project.
Existing pipelines: `.github/workflows/ci.yml`, `.github/workflows/e2e-real.yml`, `.github/workflows/pages.yml`.

## Impact
Estimated savings: roughly 83 % fewer projects built and tested per iteration and per CI run. Assumption: a typical
change touches one project plus the projects that depend on it; with 13 projects and 15 references a
project has about 1.2 direct dependants on average. Measure the real gain on the last 20 merged pull requests before
and after (build + test minutes). Changes to shared build inputs still build everything (always-build list), so the gain is
largest for leaf projects and smallest for core libraries.

## Risk
- Leaving it: CI minutes and feedback time grow with every project added.
- The change: a missed dependency would skip a build that should have run. Mitigations: the tool follows `ProjectReference`s;
  any analysis failure falls back to a full build; files outside every project (scripts, root config) only count when they
  are on the always-build list; the final verification before a hand-off and every push to `main` can stay full.
- Central Package Management: with `CentralPackageTransitivePinningEnabled`, a `Directory.Packages.props` change can mark every
  project affected, so expect a full build for package updates (that file is on the always-build list anyway). Detected: `Directory.Packages.props` (Central Package Management) without transitive pinning.
- A clone without the base commit (shallow fetch) builds everything; CI needs `fetch-depth: 0` (or enough depth).
- Rollback: delete the workflow (or revert the pipeline change); the tool manifest entry is harmless on its own.

## Effort
S (0.5–1 day): pin the tool, add the always-build list, add or adapt one pipeline, compare one week of CI runs. Assumes the
solution builds and tests green today and that the pipelines can be changed by the team.

## Step-by-step plan
1. Safety net: record the current full build and test time and the test count on `main` (baseline for the comparison).
2. Pin the tool (files that change: `.config/dotnet-tools.json`): `dotnet new tool-manifest -o .config` (only if there is no
   manifest), `dotnet tool install dotnet-affected --version 7.0.1`, `dotnet tool restore`. Commit the manifest.
3. Add `.claude/tipoca/affected/always-build.txt` (copy of the Tipoca template) and extend it with repository-wide inputs
   (shared `.editorconfig`, analyzer rulesets, `.runsettings`, generated code sources).
4. CI (files that change: `.github/workflows/tipoca-affected.yml`, or the existing pipeline): start from the Tipoca templates
   `templates/ci/github-affected.yml` or `templates/ci/azure-pipelines-affected.yml` and set `SOLUTION` to `StemStudio.slnx`.
   Pull requests compare with the base commit, pushes with the previous commit, manual runs build everything; exit 166 skips
   build and test cleanly.
5. Run it next to the existing full pipeline for a week; compare the affected sets with the full runs (no test that the full run
   failed may be skipped by the affected run).
6. Switch the pull request check to the affected pipeline; keep a full build on `main` (or nightly).
7. Verify: CI minutes and feedback time against the baseline. Roll back by removing the workflow or reverting the pipeline.

## Dependencies and related proposals
Tipoca skill `tipoca:dotnet-affected` (runner, formats, exit codes, known limitations). Locally, Tipoca iterations already use
`scripts/affected.mjs` once the project leaves audit mode; this proposal is about the team's own builds and CI.
