# Hand-off qa 023 wave 1 (lyric writing assistant)

Lane `qa (022 + 023)`, branch `r22-lane-qa` from 4486ddb7. Tasks 023 **T024, T037, T044, T053,
T060, T065, T078** authored against backend API 1.23.0, UI test ids 1.23.0 and tool catalogue
1.2.0 (typecheck and lint only; execution, axe run, NVDA pass and the coverage audit are wave 2
**T070**). Strategy addendum (T024): [docs/testing/023-lyric-assist.md](../testing/023-lyric-assist.md).

## What was built

| Task | Files |
|---|---|
| T024 | `docs/testing/023-lyric-assist.md`; pointer paragraph in `docs/testing/TEST-STRATEGY.md` |
| support | `e2e/support/lyric-assist-api.ts`, `lyric-assist-fixtures.ts` (facts from `expected.json`, sheets with exact titles, corpus track via 006 setLyrics, backend guards), `lyric-assist-flow.ts` (`runAssist` waits on the id of the assist it started), `lyric-assist-test.ts`; page object `e2e/pages/lyric-assist-pane.ts` |
| T037, T044, T053 (writing help) | `e2e/tests/lyric-assist.spec.ts` (Q1, Q2, Q3 steps 1-5, keyboard of FR-028) |
| T053 (guardrails), T078 | `e2e/tests/lyric-assist-guardrails.spec.ts` (Q3 step 6, Q5 steps 1-7) |
| T065 | `e2e/tests/lyric-assist-local.spec.ts` (Q6; `@lyric-assist-none`, `@lyric-assist-claude`, browser outbound recorder) |
| T060, T078 (S5) | `e2e/tests/agent.spec.ts` (3 tests), scripts `e2e/agent-scripts/{us4-lyric-check,us4-lyric-apply,us6-lyric-override}.json`; `support/agent-scripts.ts` + `agent-script-check.ts` know `{{context.lyricsId}}` and `{{result.list_lyric_suggestions.ids:<category>}}`; self-test cases in `tests/agent-support.spec.ts` |
| extra (SC-008) | `e2e/a11y/lyric-assist.a11y.spec.ts` (axe, both themes, Beginner and Expert) |
| scripts | `e2e/package.json`: `test:lyric-assist`, `test:lyric-assist-none`, `test:lyric-assist-claude`; `npm test` excludes `@lyric-assist` (SC-009: default suite unchanged) |

## Tests and checks

| Check | Result |
|---|---|
| `npx tsc --noEmit` (e2e) | 0 errors |
| `npm run lint` (`--max-warnings 0`) | 0 problems |
| `tests/agent-support.spec.ts` (stack-free; script validity, placeholders, tags, every script used) | 11 passed |
| E2E against a stack | not run (lane rule); wave 2 T070 |
| Coverage | not applicable to Playwright specs |

## Decisions

1. **T060 gap, decided by the CTO** (main session, this wave): `{{context.lyricsId}}` (whole value)
   and `{{result.list_lyric_suggestions.ids:<category>}}` (ids of that category in the run's most
   recent `list_lyric_suggestions` result; whole value, JSON array; empty when none; script error
   without such a call). The apply script lists first, then applies. ai-agent implements them in
   `ScriptedChatSession`; the integration updates the 011 agent-script schema description.
2. Spec files: `e2e/tests/lyric-assist*.spec.ts` (the playwright `testDir` is `tests/`; the task
   rows name `e2e/lyric-assist.spec.ts`).
3. The agent lyric tests live in `agent.spec.ts`, the file `scripts/agent-e2e.*` runs; they need
   the lyric scripts folder mounted in the agent passes as well.
4. The check script does not answer the unknown name `Zelmara` with a verdict, so it may be
   reported as a spelling improvement; the specs assert the planted fixes and the licence spans,
   not exact totals. Accept-all asserts both planted spelling fixes changed in one revision.
5. Not-own/verbatim/own-work tests seed the corpus track once per worker (title "Paper Lanterns
   qa<run>") and assert the matched track by that title prefix (older runs' copies may match).

## Gaps and requests (no contract edited)

- **devops T069**: mount `e2e/lyric-scripts/` at `/scripts/lyric` and set
  `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/scripts/lyric` in the E2E override **for the agent passes
  too**; a way to start the **Claude double** (quickstart Q6 step 3 "fake-claude"): no variable is
  named in the contracts. `lyric-assist-local.spec.ts` runs `@lyric-assist-claude` only when
  `GET /api/lyric-assist/status` reports backend `claude` and skips with the reason otherwise.
- **ai-agent**: an unknown tool name from the scripted model (`confirm_own_work` in
  `us6-lyric-override.json`) must end as a failed step / tool error and let the run continue to
  the answer turn (S5); a `check_lyrics` refused with `not-own-lyrics` must be a failed step.
- **frontend-023**: an imitation refusal has no dedicated test id; the spec expects the refusal
  text (matching `/imitat/i`) inside `lyric-assist-pane` and no new assist. A focused suggestion
  after F8 is found by `:focus` or `aria-selected="true"` inside `lyric-assist-list`. Escape from
  the list returns focus to `lyrics-text-input`.
- **backend-023**: `GET /api/lyrics/{id}/assists` is an array (newest first); the specs wait on the
  new `assistId`. The personal word list is global: the "my words" test removes `Zelmara` (nl)
  before and after.

## Open items

- Wave 2 T070: run the 023 specs, axe, one NVDA pass, coverage audit.
- 022 T090 / T092: see `qa-022-wave1.md`.

## Dependencies

None added.
