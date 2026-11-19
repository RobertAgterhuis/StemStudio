# 008 Protect main and require a green CI before code lands
- Status: proposed · Date: 2026-11-19 · Area: delivery / repository policy · Priority: P1

## Problem statement
- **`main` has no protection.** `gh api repos/<owner>/StemStudio/branches/main/protection` returns `404 Branch not protected`, and
  `gh api .../rulesets` returns `[]`. The repository is public (`visibility: public`), and `.github/CODEOWNERS` names one owner.
- **`main` is red most of the time.** Over the last 30 `ci` runs on `main` (summary in `.claude/scratchpad/main/ci-history.tsv`),
  14 failed, 8 were cancelled and 8 passed. The latest run (44241813956, 2026-11-18) failed in "E2E main flow" and "Lighthouse
  CI budgets". Several runs failed in `.NET build, format, test`, `Quality gate` or `Worker lint`, which means code landed on
  `main` before its unit gate was green.
- **Pushes to `main` cancel each other's verdict.** `ci.yml:21-23` sets `cancel-in-progress: true` for every ref, including
  `main`. 8 of the 30 runs were cancelled, so the commits behind them never got a full verdict.
- **There is no single required check.** The workflow has 18 jobs. Without an aggregate job a ruleset has to list them all, and
  the list goes stale every time a job is added (11 jobs were added for features 004–023).
- The PR template (`.github/pull_request_template.md`) and `CONTRIBUTING.md:18` describe a PR flow ("Pull requests run the full
  CI ..."). The history shows direct pushes to `main` (all 30 runs are `event: push` on `main`).

## Impact
- Broken builds land unnoticed. Every later round starts from a red `main` and spends time triaging failures that are not its
  own. That cost repeats every round.
- The coverage, SIG and E2E gates are advisory as long as nothing refuses a red commit.
- On a public repository, an unprotected default branch also allows force-pushes that rewrite history.

## Risk
- Leaving it: red stays the normal state, and real regressions hide among known failures.
- The change: low to medium. Autonomous rounds that push straight to `main` would have to go through a PR or a merge queue; plan
  for that before you switch it on. Rollback: disable the ruleset (one click, no data effect).
- Rulesets are a GitHub repository setting. Propose the exact ruleset and wait for the user's explicit confirmation before
  creating it.

## Effort
S (0.5–1 day): one aggregate job, a concurrency change, and one ruleset. The precondition is a green `main`: fix or quarantine
the current E2E and Lighthouse failures first.

## Step-by-step plan
1. Safety net: get `main` green once. Fix the current failures, or quarantine them behind an explicit tag with an issue. Record
   that run as the baseline.
2. Concurrency: in `ci.yml:21-23`, use `cancel-in-progress: ${{ github.event_name == 'pull_request' }}`, so every `main` commit
   gets a full verdict while PR pushes still cancel the run they replace. Check: two quick pushes to a test branch behave as
   expected.
3. Add a job `ci-ok` with `needs: [every gating job]`, `if: always()`, and a step that fails when any needed job did not
   succeed (`contains(needs.*.result, 'failure') || contains(needs.*.result, 'cancelled')`). Make this the one required check.
4. Propose this ruleset for `main` (and wait for confirmation): require a PR, require status check `ci-ok`, block force-pushes
   and deletion, require linear history. Bypass only for the owner, and only for emergencies.
5. Autonomous rounds: let them open a PR and use auto-merge, or a merge queue (`merge_group:` trigger in `ci.yml`), instead of
   pushing to `main`. Update the round instructions accordingly.
6. Verify: a PR with a deliberately failing unit test cannot be merged, and a green PR merges. Roll back by disabling the ruleset.

## Dependencies and related proposals
- `007-quality-gates-real.md`: Sonar becomes meaningful as a required input of `ci-ok`.
- `023-e2e-split.md`: a 2-hour E2E job is a painful required check. Split it first, or require it only for the merge
  queue.
