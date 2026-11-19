# 012 E2E specs that no pipeline runs, and a hand-maintained tag matrix
- Status: proposed · Date: 2026-11-19 · Area: frontend / e2e Playwright suite · Priority: P2

## Problem statement
The Playwright suite selects specs by tag with long, hand-maintained `--grep` / `--grep-invert` lists in `e2e/package.json`
(40 scripts, lines 10–40). `npm test` (run in `.github/workflows/ci.yml:721-723`) excludes
`@real|@restart|@worker-restart|@gpu|@agent|@claude-key|@compose|@fresh-library|@library-db|@lyric-assist|@recording`; the
excluded tags are then supposed to be picked up by dedicated CI steps (`ci.yml:724-762`, `scripts/agent-e2e.sh`,
`scripts/lyric-e2e.sh`). Cross-checking every tag in `e2e/tests/` against `ci.yml`, `e2e-real.yml` and `scripts/*.sh`
(`grep -rhoE "@[a-z][a-z0-9-]+" e2e/tests`, `grep -rnoE 'test:[a-z-]+' .github/workflows scripts`) shows tags that are
excluded from `npm test` and run by **no** pipeline step:

| Tag | Specs | What goes untested |
|---|---|---|
| `@claude-key` | `e2e/tests/agent-claude-key.spec.ts` (10 tests, lines 54–236) | the Claude key never appears in DOM, network, console, container logs or SQLite; local-only write refusal (403); key replace/delete flows |
| `@compose` | `library-care-storage.spec.ts:19,57`, `library-care-restore.spec.ts:215` | storage sizes against disk, worker-unreachable state, cache rehydrate |
| `@library-db` | `library-migration.spec.ts:18`, `library-search-perf.spec.ts:80` | pre-022 library migration; search p95 <= 400 ms |
| `@fresh-library` | `library-care-restore.spec.ts:229` | restore into an empty library |
| `@worker-restart` | `detail-worker-restart.spec.ts:9` | worker restart mid-phase marks interrupted, Retry completes |

`scripts/agent-e2e.sh:21` pins `SPEC=tests/agent.spec.ts`, so the `@claude-key` spec is not reached through the agent step
either; `npm run test:claude-key`, `test:library-db`, `test:library-care-compose`, `test:library-care-fresh` and
`test:worker-restart` exist only for manual runs.

## Impact
- Security safety net: the `@claude-key` tests are the end-to-end proof that a secret never leaks into the page, logs or
  database; today a regression there is only caught if someone runs the script by hand.
- Data safety: migration and restore-into-empty-library paths are untested in CI.
- Every new tag needs edits in up to three exclusion lists; forgetting one silently drops or doubles a suite (there is no check).

## Risk
Risk rating: **low**.
- Leaving it: silent regressions in key handling, migration and restore.
- The change adds CI minutes (each of these suites needs its own stack setup, like the agent and lyric steps) and could expose
  flaky specs that were never run in CI; run them non-blocking for a week first. Rollback: remove the steps.

## Effort
S (1–2 days): a tag-coverage check script, CI steps for the five tags (or a nightly job), one week of observation. Assumes the
stack setups these specs need (api restart, fresh volume, compose access) can reuse the patterns in `scripts/agent-e2e.sh`.

## Step-by-step plan
1. Safety net: run each of the five scripts locally against the compose stack once and record pass/fail and duration
   (output to `.claude/scratchpad/<branch>/`); fix or quarantine (with an issue) any that fail.
2. Add `e2e/support/tag-matrix` (or `scripts/check-e2e-tags.mjs`): one declarative table "tag -> runner (npm script or CI step)",
   and a check that every tag found in `e2e/tests/**/*.spec.ts` is claimed by exactly one runner or explicitly listed as manual-only
   with a reason. Run it in the `e2e` lint step. Generate the `--grep-invert` list of `npm test` from the same table instead of
   hand-editing `package.json`.
3. Add CI steps (or a nightly workflow if CI time is the constraint) for `@claude-key` (api with the key volume, as the agent
   step), `@library-db`, `@compose`, `@fresh-library` and `@worker-restart`; start as `continue-on-error: true`.
4. After a week without flakes, make them blocking.
5. Verify: the tag check passes and lists no tag without a runner; the CI run shows the five suites. Roll back by removing the
   steps; the tag check can stay.

## Dependencies and related proposals
Overlaps with `013-gate-gaps.md` item 3 (same finding seen from CI). This proposal adds the per-test detail of what
goes untested and the tag-matrix check in the e2e package; merge the two or keep this one as the e2e-side half. It also touches
the e2e job that `023-e2e-split.md` restructures; coordinate so the job is not changed twice.
