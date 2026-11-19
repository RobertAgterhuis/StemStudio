# 007 Make the SonarQube and Sigrid gates real and blocking
- Status: rejected (owner, 2026-11-19: no SonarQube or Sigrid, see 030 / ADR-038) · Date: 2026-11-19 · Area: delivery / CI quality gates · Priority: P1

## Problem statement
SonarQube and Sigrid (SIG) are the project's named quality gates, but in CI neither one is a real gate today.

1. **The SonarQube job never analyses anything, yet reports success.** `.github/workflows/ci.yml:113-163` puts every step behind
   `if: env.SONAR_TOKEN != ''`. Evidence: `gh run list --workflow ci.yml --limit 30` plus `gh run view` per run (summary in
   `.claude/scratchpad/main/ci-history.tsv`): the job is `success` in 30 of 30 runs. In run 44241813956 it took 4 s: the only step that
   ran was "Sonar not configured" (`ci.yml:122-124`), and checkout, build and scan were all `skipped`. A missing secret turns the
   gate green without a sound.
2. **When it does run, the Sonar verdict cannot fail the build.** `sonar.qualitygate.wait` is not set, neither in
   `sonar-project.properties` nor in `tools/quality/sonar_args.py` (grep for `qualitygate`: no hits). So `dotnet sonarscanner end`
   uploads the analysis and exits 0 whatever the quality-gate result.
3. **The Sonar inputs are incomplete.** `sonar-project.properties:163,204` expects `generator/coverage.xml`, but the scan step
   (`ci.yml:160-162`) runs the worker, audio-engine and web-interop tests and never the generator's. Generator code would show up as
   0 % covered.
4. **Token handling breaks the project's own rule.** `sonar-project.properties:168-170` says the token comes from `SONAR_TOKEN` and
   "Never put it ... on a command line", but `ci.yml:156` and `ci.yml:163` pass `/d:sonar.token="$SONAR_TOKEN"` as a process
   argument.
5. **The Sonar job repeats the whole .NET build and test run** (`ci.yml:158-159`, about 15 min going by the `dotnet` job in run
   44241813956) instead of reusing the coverage the `dotnet`, `worker`, `generator`, `audio-engine` and `web-interop` jobs already
   upload as artifacts (`ci.yml:105-111, 212-217, 245-250, 272-277, 300-305`).
6. **Sigrid is not connected at all.** `grep -ril sigrid` finds only docs and `tools/quality/*.py`. CI runs the local SIG
   approximation `tools/quality/run_all.py` (`ci.yml:476-477`), which does fail on errors (`run_all.py:72-76`). Nothing is uploaded to
   Sigrid, so the SIG rating the project is judged on is never measured in CI.

## Impact
- The stated gates ("SonarQube and Sigrid are the quality gates") are not enforced. Regressions in duplication, code smells,
  security hotspots or coverage on new code can reach `main` without being noticed.
- A green CI run gives a false signal to reviewers and to the overnight autonomous rounds that rely on it.
- With a token configured, a needless second full build and test run would add about 15 min of runner time to every run.

## Risk
- Leaving it: the quality claim cannot be audited, and a Sigrid or Sonar rating measured later can turn out much worse than
  expected.
- The change: low. A gate that fails on an existing backlog can block merges, so start with a "new code" quality gate in
  SonarQube and a Sigrid baseline. Rollback: set `sonar.qualitygate.wait=false`, or make the Sonar job non-required again.
- Adding the `SONAR_TOKEN` secret and a Sigrid token is a change to the GitHub repository. Propose it and wait for the user's
  explicit confirmation. No token value ever goes into a file.

## Effort
S–M (1–2 days). Assumes a SonarCloud or SonarQube project `stemstudio` exists or can be created, and that the user decides whether a
Sigrid subscription is available. Without Sigrid, step 6 reduces to documenting that `tools/quality` is the stand-in.

## Step-by-step plan
1. Safety net: run the scan once by hand (`workflow_dispatch`) with the token set, and record the current quality-gate result and
   the per-language coverage as the baseline.
2. Make a missing token fail on `main` and stay a notice on forks and PRs. In "Sonar not configured", call `exit 1` when
   `github.ref == 'refs/heads/main'`, so the job can no longer be green without analysing.
3. Add `sonar.qualitygate.wait=true` (and `sonar.qualitygate.timeout=300`) to `sonar-project.properties`. Check with
   `sonar_args.py` that the setting is passed through, and add a test in `tools/quality/tests`.
4. Remove `/d:sonar.token=...` from `ci.yml:156,163`. Check against the SonarScanner for .NET 12 documentation that it reads
   `SONAR_TOKEN` from the environment. If it does not, keep the argument and correct the claim in the properties file, so the
   rule and the code agree.
5. Have the Sonar job `needs:` the five test jobs and download their coverage artifacts, then run `begin`, `build` and `end` only,
   with no second test run. This also brings in `generator/coverage.xml`. Check: the Sonar UI shows coverage for all five
   components.
6. Sigrid, if the user wants it: add a `sigridci` job (pinned by SHA, token from a secret, `contents: read`) that uploads the source
   and fails below the agreed target rating. Without it, state in `docs/quality/` that `tools/quality` is an approximation, not
   the Sigrid rating.
7. Verify: plant a deliberate code smell on a branch, and check that the Sonar job turns red and blocks the merge (see the
   branch-protection proposal). Roll back by reverting the workflow commit.

## Dependencies and related proposals
- Branch protection (`008-protect-main.md`): the gates only block once they are required checks.
- `001-dotnet-affected.md`: the Sonar job must keep a full build, so put it on the always-build list.
