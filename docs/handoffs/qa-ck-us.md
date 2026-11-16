# Hand-off: qa / 011 follow-up claude-key, E2E specs (T047 part 1, T046 part 2)

Branch `ck-lane-qa` (worktree `.claude/worktrees/ck-qa`, from `011-claude-key-spec`). Nothing pushed or merged. No docker
command and no Playwright test execution (CTO runs the suite at integration); Anthropic never called; no key-shaped literal.

## What was built

- `e2e/tests/agent-claude-key.spec.ts` (10 tests x Chromium/Firefox, tag `@claude-key`): not configured; malformed (admin
  key refused, value never repeated); store (status `stored` "...STO1", backend unchanged, reload, network/console/DOM
  clean); replace (+ Cancel); delete with confirmation (Keep key, Escape, Delete key); local-only API (PUT/DELETE with a
  foreign `Origin` -> `403 .../local-only`, key unchanged); local-only page (non-loopback host -> notice, field and Save
  disabled); environment wins (api recreated with `ANTHROPIC_API_KEY` = ENV sentinel, override note, test-source choice
  offered, api restored in `finally`); unreadable after key ring loss (`rm -f /keys/*.xml` + `restart api`, first status
  call `stored-unreadable`, other endpoints 200, recovery by Replace); logs and database (api/web container logs and
  `stemstudio.db`/`-wal`/`-shm` bytes without any sentinel form). Scoped axe scans (WCAG 2.2 AA tags) of
  `settings-agent-claude-key` in not-configured, stored, environment+stored, unreadable and delete-confirmation-open,
  page opened as `localhost` (CK-SC-006).
- Helpers: `support/claude-key.ts` (run-time sentinels unique per `E2E_RUN_ID`, four search forms: plain, base64, UTF-16LE
  base64, raw UTF-16LE; reports name the role, never the value), `support/leak-watch.ts` (response bodies, received
  WebSocket frames, console), `support/claude-key-stack.ts` (compose control via `execFileSync` argument lists; the
  sentinel only in the child environment; `MSYS_NO_PATHCONV=1`), `support/claude-key-flow.ts` (skips, API status scan,
  clean-up), `support/lan-origin.ts`, `pages/claude-key-section.ts`; `AgentApi` gained `ClaudeKeyStatusDto` and the key
  calls; `axeViolationsIn(page, selector)`; `storageStateFor(record, origin?)`; fixture `claudeKey`.
- `e2e/tests/claude-key-support.spec.ts` (`@self-test`, stack-free): sentinels well formed / admin-shaped, leak search finds
  every form without naming the value, loopback host list.
- Tag proposal implemented: **`@claude-key`** (no `@agent`, so `scripts/agent-e2e.*` never select it). `npm test` and
  `test:legacy` exclude it; new script `npm run test:claude-key`.
- `docs/testing/TEST-STRATEGY.md` 17.10 (test table, what is and is not covered in E2E, commands).

Design decisions worth knowing:
- **Every key write goes through the UI.** Behind Docker's forwarding the api sees host requests from the gateway (F-01),
  so the test process cannot write the key; the spec asserts that refusal with a foreign `Origin`, which is refused in
  every topology (Docker, non-Docker, Playwright container).
- **Test key is never pressed** (it would send the sentinel to the real Anthropic). There is no fake Anthropic handler in
  the compose stack, so the "must-not-call via the API's request counter" part of T047 is **not provable in E2E**; the
  evidence stays at unit/API level (see the audit, `qa-ck-polish.md`).
- T046 names `scripts/agent-e2e.*` (devops-owned) for the docker steps; they are done inside the spec instead
  (`support/claude-key-stack.ts`), so no script change is needed.
- `beforeAll` fails when the api already has an environment key: the suite must never display, replace or test a real key.

## Files changed

New: `e2e/tests/agent-claude-key.spec.ts`, `e2e/tests/claude-key-support.spec.ts`, `e2e/pages/claude-key-section.ts`,
`e2e/support/{claude-key,claude-key-flow,claude-key-stack,leak-watch,lan-origin}.ts`, this note.
Modified: `e2e/package.json`, `e2e/support/{agent-api,agent-test,axe,preferences}.ts`, `docs/testing/TEST-STRATEGY.md`,
`specs/011-ai-composer-agent/followups/claude-key/tasks.md` (T046, T047 ticked).

## Tests and coverage

Not executed (lane rule: the CTO runs Playwright). Stack-free validation in `e2e/` (node_modules via a temporary junction
to `main-run/e2e/node_modules`, removed afterwards):

| Command | Result |
|---|---|
| `npx tsc --noEmit` | exit 0 |
| `npx eslint . --max-warnings 0` | exit 0 |
| `npx playwright test --list tests/agent-claude-key.spec.ts tests/claude-key-support.spec.ts` | 26 tests in 2 files |
| `npx playwright test --list --project=chromium --project=firefox --grep @claude-key` | 20 tests in 1 file |
| `npx playwright test --list` with the `npm test` grep-invert | 496 tests, none from `agent-claude-key.spec.ts` |
| helpers compiled to a scratch dir and run with node | 5 sentinels 79-91 chars, prefix ok, charset ok, last four STO1/STO2/ENV1/REC1/ADM1; all four forms found |
| `python scripts/secret_scan.py` | 3496 files clean |

Coverage: no new production code (E2E only); the coverage gate does not apply to `e2e/`.

## Quality checks

ESLint (type-checked, SIG limits: complexity 5, 4 params, 30 lines per support function, 60 per test) 0 findings; TSDoc on
every exported support/page member.

## Dependencies added

None (`@axe-core/playwright` 4.14.0 was already a dev dependency).

## Docs impact

None user-visible (test suite only).

## Open issues / requests

1. **backend / devops (optional)**: a test-only Anthropic base-URL override (e.g. `StemStudio__Agent__Claude__BaseUrl`) and a
   stub container with a request counter would let E2E prove CK-SC-002 and press **Test key**; not needed for the follow-up.
2. **CTO**: run the spec from the shell that started the stack (compose re-reads `STEMSTUDIO_*` from it); run it from the
   host with `E2E_BASE_URL` on `localhost` (in the Playwright container the base URL is `web`, so the key tests skip:
   key entry is local-only). `E2E_LAN_BASE_URL` chooses the non-loopback address of the local-only page test.

## Next owner

qa T048 (audit, `qa-ck-polish.md`), then CTO.
