# 030 ADR-038: No SonarQube or Sigrid for this project
- Status: accepted; the record is placed at `docs/decisions/ADR-038-no-sonarqube-sigrid.md` (2026-11-19)
  · Date: 2026-11-19 · Area: delivery / quality gates · Priority: P1

Audit mode does not allow writes to `docs/decisions/`, so this proposal carries the full record text below. Moving it is a
copy of the block between the two rules, with no other change.

## Problem statement
Proposal 007 found that the SonarQube job in `.github/workflows/ci.yml:113-163` never ran (skipped in 30 of 30 runs, yet green)
and that Sigrid is not connected. Setting both up needs a `SONAR_TOKEN`, a SonarQube server and a Sigrid subscription. The owner
decided on 2026-11-19 that this project does not use either service. The record below writes that down and replaces 007.

## Impact
- No external accounts, tokens or secrets are needed for quality checks, so there is no outward change and nothing to configure.
- The CI stops showing a gate that never runs, so a green run is no longer a false signal.
- Maintainability stays measured by the local SIG check that already blocks CI (`tools/quality/run_all.py`, `ci.yml:476-477`).

## Risk
- No external SonarQube or Sigrid rating: the local SIG check is an approximation, not a certified rating. Security hotspot
  analysis that Sonar would add is not covered. ADR-038 below says how that gap is covered.
- The change itself is low risk: removing a job that never runs changes no verdict.

## Effort
S. One record, plus a follow-up in factory mode: remove the Sonar job and the Sonar files, and update the docs that name them.

## Step-by-step plan
1. Switch to factory mode, then copy the record below to `docs/decisions/ADR-038-no-sonarqube-sigrid.md`.
2. Remove the `sonar` job from `.github/workflows/ci.yml` (lines 113-163) and every `needs:` that names it.
3. Remove `sonar-project.properties` and `tools/quality/sonar_args.py` with its tests. Keep the OpenCover and Cobertura coverage
   output: the 90 % gate still uses it.
4. Update the docs that name SonarQube or Sigrid as a gate: the constitution (`.specify/memory/constitution.md:105`, through a
   constitution amendment), ADR-006 (add a "Superseded in part by ADR-038" note), `CONTRIBUTING.md`, `docs/testing/` and
   `docs/quality/`, and `.claude/agents/quality-gate.md`.
5. Mark proposal 007 as rejected in the index (done), and keep 013 (`gate-gaps`) for the local gates.

---

# ADR-038: No SonarQube or Sigrid for this project

**Status**: Accepted (owner, 2026-11-19). Supersedes in part ADR-006 (the SonarQube scanner and the Sonar formats as a CI gate) and
the constitution's mention of a SonarQube-compatible `sonar-project.properties`.

**Context**: ADR-006 planned SonarQube (through `sonar-project.properties` and the .NET scanner) next to a local SIG check. The
owner's general stack names SonarQube and Sigrid (SIG) as quality gates. In StemStudio neither was ever active: the CI `sonar`
job only runs when a `SONAR_TOKEN` secret exists, and it was skipped in 30 of 30 runs while still reporting success (refactor
proposal 007). Sigrid was never connected. Using either one needs an external account, a server or subscription, and a repository
secret. StemStudio is a local-first project.

## Decisions

**D1 - No SonarQube.** StemStudio does not use SonarQube or SonarCloud: no scanner in CI, no `SONAR_TOKEN` secret, no
`sonar-project.properties`. Rejected: setting up the token and making the gate blocking (proposal 007). It needs an external
account and secret, and runs a second full build and test pass of about 15 minutes.

**D2 - No Sigrid.** StemStudio does not upload to Sigrid (SIG) and holds no Sigrid token. Rejected: a Sigrid subscription and a
CI upload.

**D3 - The local gates are the quality gates.** These stay blocking in CI:
- the local SIG maintainability check, `tools/quality/run_all.py` with `sig_check.py`: unit size, complexity, parameters,
  duplication through jscpd, and dependency direction;
- the 90 % coverage gate for each component;
- the linters and type checkers: the .NET analyzers, ruff and mypy (strict), and ESLint;
- `css_tokens_check.py` and the secret scan.

The SIG thresholds stay as ADR-006 sets them. "SIG" in project docs means this local check, not a Sigrid rating.

**D4 - Security coverage without Sonar.** The security review per lane stays as it is: `tipoca:crosshair-security`, the
security tests and the secret scan. It covers what Sonar's security hotspots would have flagged. Gaps in the local gates are
handled in refactor proposal 013 (`gate-gaps`).

**Consequences**:
- Remove the `sonar` job from `ci.yml`, plus `sonar-project.properties` and `tools/quality/sonar_args.py`.
- Keep the coverage report formats, because the coverage gate uses them.
- Amend the constitution and add a note to ADR-006.
- Agents and reviewers do not ask for SonarQube or Sigrid in this project.
- If the project later wants an external rating, a new ADR supersedes this one.
