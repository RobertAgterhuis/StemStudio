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
