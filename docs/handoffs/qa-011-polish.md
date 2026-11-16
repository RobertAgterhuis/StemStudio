# Hand-off: qa / 011 polish (T079e)

Branch `011-lane-qa`, worktree `.claude/worktrees/011-qa`. Closes the qa lane of feature 011
(T011, T040, T049, T058, T067, T074, T078b, T079e). The suite is authored; the CTO executes it in
T080.

## What was built

- **T079e** traceability: `docs/testing/TEST-STRATEGY.md` 17.1 (self-test row), 17.4 (literal tags,
  no `@` in titles), 17.7 (US1-US6 -> unit and E2E tests, checked against the final test names) and
  the new 17.9 (status and the consolidated cross-owner items below).
- Lane result: `e2e/tests/agent.spec.ts` with 12 tests over 9 scripts in `e2e/agent-scripts/`,
  `e2e/tests/agent-support.spec.ts` with 9 stack-free self-tests, agent page objects and support
  modules, `npm test` / `test:legacy` / `test:generative` excluding `@agent`.

| Commit | Task |
|---|---|
| 558b555 | T011 test strategy section 17 |
| e56ba98, fa7a37a | T040 + HO:us1 |
| 28be78d, 55bdc89 | T049 + HO:us2 |
| 0551154, 66e4f0f | T058 + HO:us3 |
| bb8aab5, bb2577f | T067 + HO:us4 |
| f2de25c, e9086f4 | T074 + HO:us5 |
| 8ebbed4, 761861c | T078b + HO:us6 |

E2E tasks have no red -> green pair inside the lane: they are red until the UI and API branches
are merged and turn green in T080.

## Files changed

Modified: `docs/testing/TEST-STRATEGY.md`, `specs/011-ai-composer-agent/tasks.md` (T079e ticked).
New: this note.

## Tests and coverage

No Playwright execution and no stack (machine constraint; T080). Final validation in `e2e`:

- `npx tsc --noEmit`: 0 errors.
- `npx eslint . --max-warnings 0`: 0 problems.
- `npx playwright test --list`: 783 tests in 45 files (all projects);
  `tests/agent.spec.ts tests/agent-support.spec.ts` on chromium: 21 tests.
- Ajv 8.17.1 (draft 2020-12, scratchpad only) against `agent-script.schema.json` 1.0.0: all 9
  scripts and the schema example valid.
- Coverage: not applicable (no component code in the qa lane).

## Quality checks

ESLint config of `e2e` (SIG-aligned: complexity 5, max-params 4, function length 30/60, JSDoc on
public support members): clean.

## Dependencies added

None.

## Docs impact

Technical manual (testing chapter, CTO T084a): agent E2E scripts, placeholders, tags and the
`agent-e2e` passes (TEST-STRATEGY 17.4/17.5/17.8).

## Open issues / requests (consolidated)

1. **ai-agent (blocking)**: the fake backend replaces argument strings that are exactly
   `{{context.trackId}}` / `{{context.mixId}}` with the run's `SendMessageRequest.context`.
2. **ai-agent / backend (blocking)**: every run plays the script from turn 1 (fresh
   `ScriptedChatClient` per run); undo runs need no model turn.
3. **backend (blocking)**: with `StemStudio__Agent__Backend=fake` the effective backend is `fake`
   regardless of a stored settings row; `GET/PUT settings` still read/write the stored choice.
4. **backend (T060/T061)**: `PUT /api/agent/settings` with `backend: ollama` and unchanged model
   names is accepted without a reachable Ollama; models are validated (422) only when they change.
5. **devops / CTO**: a pass for the `@generative` agent tests on the fake generator stack
   (`agent-e2e.sh fake-generative` proposal and the manual commands in `qa-011-us6.md`).
6. **frontend**: page context carries `mixId` when a saved mix is loaded; agent mixer changes set
   `unsaved-indicator`; the panel survives the `open_mixer` navigation; `agent-input` disabled while
   unavailable; history view restores plan and step rows; Claude status names `ANTHROPIC_API_KEY`.
7. **architect**: record the placeholders in `agent-script.schema.json` (description) and DELTA.md 8.
8. **CTO (T080)**: re-render the 004 visual baselines on the 011 layout; run
   `npm test` (backend `none`), `scripts/agent-e2e.sh none`, `scripts/agent-e2e.sh fake` and the
   generative agent pass, all with retries 0; `e2e/agent-scripts/` exists, so the compose bind
   mount is satisfied (devops request).

## Next owner

CTO (T080).
