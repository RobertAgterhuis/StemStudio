# Hand-off: devops / 011 US3 (T059)

Branch `011-lane-devops`, worktree `.claude/worktrees/011-devops`. Builds on
`devops-011-foundational.md`.

## What was built

- `scripts/agent-e2e.sh` / `scripts/agent-e2e.ps1` (`fake|none`), run against an already started
  fake stack. Each pass recreates **only the api** (`docker compose up -d --no-deps --wait api`)
  with the agent settings, so the worker (and its fake mode) and web stay untouched:
  - `fake`: for every `e2e/agent-scripts/<name>.json` (sorted): api with
    `STEMSTUDIO_AGENT_BACKEND=fake`, `STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/<name>.json`, then
    `npx playwright test --project=chromium --project=firefox tests/agent.spec.ts --grep "@<name>( |$)"`;
    finally one pass with the fake backend and no script for the `@agent` tests that carry no
    script tag. A caller-set `STEMSTUDIO_AGENT_FAKE_SCRIPT` replaces the loop with one pass.
    `@agent-unavailable` and `@generative` tests are excluded from the fake passes (the generative
    ones need the generator overlay). `--pass-with-no-tests` in the fake passes.
  - `none`: api with `STEMSTUDIO_AGENT_BACKEND=none`, then `--grep @agent-unavailable` (no
    `--pass-with-no-tests`: the unavailable spec must exist once agent.spec.ts exists).
  - Skips with a notice (exit 0) while `e2e/tests/agent.spec.ts` does not exist, so CI stays green
    until qa's spec is merged (same pattern as `ci-e2e-suite.sh`). Compose project from
    `COMPOSE_PROJECT_NAME`.
- `.github/workflows/ci.yml`, job `e2e`:
  - job env `STEMSTUDIO_AGENT_BACKEND: none`: the default suite, `@restart` and the GPU passes run
    with the agent off (quickstart A6, SC-005); no Ollama, no key in CI.
  - new step after the stack is healthy: the api container has no Claude key
    (`test -z "${ANTHROPIC_API_KEY+set}"` inside the container).
  - after the `@restart` step (before the fake-GPU steps, so the worker is still the plain fake):
    "Agent specs with the fake backend" (`scripts/agent-e2e.sh fake`, A5) and "Agent unavailable
    spec with backend none" (`scripts/agent-e2e.sh none`). The api env after `none` equals the
    job env, so the GPU steps do not recreate it.
- `scripts/test-all.sh --agent-e2e` / `scripts/test-all.ps1 -AgentE2e`: fake stack as project
  `stemstudio-e2e` with backend none, then the `fake` and `none` passes, then `down -v`
  (needs ports 8080/8081 free, like `--gpu-e2e`).

## Files changed

`scripts/agent-e2e.sh` (new, mode 100755), `scripts/agent-e2e.ps1` (new),
`scripts/test-all.sh`, `scripts/test-all.ps1`, `.github/workflows/ci.yml`,
`specs/011-ai-composer-agent/tasks.md` (T059 ticked).

## Tests and coverage

No E2E run (instruction: no compose up/down on this PC; the user's stack on :8080 untouched).
Checked: `scripts/agent-e2e.sh none` and `scripts/agent-e2e.ps1 fake` without the spec -> skip
notice, exit 0; no argument -> usage, exit 2; PowerShell `ValidateSet` rejects other values. The
real execution is CTO T080 / CI once qa's `agent.spec.ts` is merged.

## Quality checks

- `actionlint` 1.7.12 (container, incl. shellcheck of `run:` blocks): 0 findings.
- `shellcheck` 0.11.0: `agent-e2e.sh`, `test-all.sh`, `dev.sh` clean.
- PowerShell parser 0 errors; PSScriptAnalyzer (Warning, Error): `agent-e2e.ps1`, `test-all.ps1` clean.
- `docker compose config -q` unchanged (no compose edit in this checkpoint).

## Dependencies added

None.

## Docs impact

Technical manual (testing): how to run the agent E2E passes locally
(`scripts/test-all.sh --agent-e2e` / `-AgentE2e`) and the tag convention below.

## Open issues / requests

1. **qa (T040/T049/T058/T067/T074/T078b) - tag convention, please confirm or tell me yours**:
   - every agent test carries `@agent`; the unavailable test carries `@agent-unavailable`
     (and not a script tag);
   - a test that needs a scripted model carries `@<script basename>` for the file in
     `e2e/agent-scripts/` it needs, e.g. `@us1-command` for `us1-command.json`; one script per api
     start (DELTA.md 8: turn *i* answers the *i*-th request of every run, so scenarios with
     different turns need different api starts);
   - tests needing the generator (US6 create) carry `@generative` (not run by these passes);
   - `npm test` (default suite) must exclude `@agent` (`--grep-invert "...|@agent"`), otherwise
     the scripted tests run against backend `none` in the default pass and fail.
2. **CTO**: one api restart per script file costs about 10-20 s each in CI; if the backend later
   supports choosing the script per conversation, `agent-e2e.sh` collapses to one pass (caller-set
   `STEMSTUDIO_AGENT_FAKE_SCRIPT` path already exists).
3. **CTO / qa**: visual, a11y and lighthouse jobs still start the stack with the default backend
   `ollama` (unreachable in CI -> agent unavailable with the Ollama reason). If baselines show the
   agent panel, decide whether those jobs should also use `none`.

## Next owner

qa (confirm the tags), CTO (T080).
