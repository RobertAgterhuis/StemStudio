# Test strategy addendum: 023 lyric writing assistant

Owner: qa (023 T024; specs T037, T044, T053, T060, T065, T078; execution and audit T070). Binding
contracts: backend API **1.23.0** (delta `specs/023-lyric-writing-assistant/contracts/backend-api.delta.openapi.yaml`),
UI test ids **1.23.0** ("Additions in 1.23.0", keyboard table, live-region texts), tool catalogue
**1.2.0** (group `lyric-assist`, 8 tools), lyric-assist-output **1.0.0**, decision 023-P0-10.
**No automated test calls an LLM**: the model layer is `ScriptedLyricModel` (scripts in
`e2e/lyric-scripts/`), the agent is `ScriptedChatClient` (`e2e/agent-scripts/`); real models are
measured only by the evaluation harness (Q7, Q8; ai-agent T068).

## 1. Coverage map (user story / criterion -> tests)

| Story / criterion | Unit / integration (owner) | E2E (qa) |
|---|---|---|
| US1 check, poetic licence (FR on check, sensitivity, marks, my words) | Domain `SuggestionFilter`, `LicenceLexicon`, `AnchorResolver`, `SuggestionApplier` (backend-023); pane state, filters, accept/undo (frontend-023) | `lyric-assist.spec.ts`: check, accept one, accept all, strict + intentional mark, my words |
| US2 devices, syllables, rhythm target | `SyllableCounter` gold lists (SC-007), `RhymeKey`, verification (SC-002) | `lyric-assist.spec.ts`: devices (line 2 deviates 15 -> 12, end rhyme `licht` verified) |
| US3 writing help, rhymes, titles | assistant block planning, supersede groups | `lyric-assist.spec.ts`: alternatives (supersede), continue (nothing before accept), rhyme finder <= 500 ms without a model call |
| US4 agent | `LyricAssistToolProvider`, policy `lyrics-edit`, projections without text (ai-agent) | `agent.spec.ts`: `@us4-lyric-check`, `@us4-lyric-apply` (card diff, reject, approve, undo) |
| US5 local, degraded | layer availability, failing outbound handler (backend) | `lyric-assist-local.spec.ts`: backend none (`@lyric-assist-none`), Claude notice (`@lyric-assist-claude`), browser outbound recorder |
| US6 own-work override | override domain rules, statement version (backend); dialog gating (frontend) | `lyric-assist-guardrails.spec.ts`: confirm -> check runs, verbatim still withheld, imitation still refused, revoke; `agent.spec.ts` `@us6-lyric-override` (eval S5: no override tool) |
| SC-001 quality, SC-004 latency | - | evaluation harness on the reference machine (Q7, Q8; ai-agent T068) |
| SC-002 verified devices | verification unit tests | devices test (`syllablesBefore/After`, `rhymeWith`) |
| SC-003 text changes only by accept/undo | applier tests | every E2E test compares the sheet text before/after; continue and inject tests assert no change |
| SC-005 injection and guardrails | `AdversarialLyricModel`, `VerbatimGuard`, sheet guard | section 4 matrix |
| SC-006 nothing leaves the machine | failing HTTP handler in the api tests | outbound recorder in `lyric-assist-local.spec.ts` (page side) |
| SC-007 syllable counter | gold lists `syllables-{nl,en}.tsv` | - |
| SC-008 keyboard and screen reader | bUnit key handling, announcements | keyboard test in `lyric-assist.spec.ts`; `a11y/lyric-assist.a11y.spec.ts`; one manual NVDA pass (section 6) |
| SC-009 existing tests unchanged | - | `npm test` excludes `@lyric-assist`; 010 lyrics specs run unchanged |

## 2. Fixture inventory (Phase 0, T009; architect)

`tests/fixtures/lyrics/assist/`: `sheets/` (zomeravond, avondlied, radio, p1/p2 for latency,
inject en/nl, verbatim, not-own, not-own-two-tracks, cliche en/nl), `corpus/` (t-fake-song,
t-fake-song-2, cliches en/nl), `expected.json` (blocks, planted fixes, licences, unknown words,
rhythm facts, corpus matches), `syllables-{nl,en}.tsv`, `rhymes-{nl,en}.json`. The specs read
every fact from `expected.json` (`e2e/support/lyric-assist-fixtures.ts`); nothing is duplicated in
the specs.

## 3. Fake stack

| Variable | Value | Who sets it |
|---|---|---|
| `STEMSTUDIO_AGENT_BACKEND` | `fake` (default lyric runs), `none` (`@lyric-assist-none`) | E2E override / caller |
| `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` | `/lyric-scripts` (folder, mounted read-only by `docker-compose.e2e.yml`; per assist `<title-slug>-<kind>.json`, else `<kind>.json`) | devops T069 |
| `STEMSTUDIO_AGENT_FAKE_SCRIPT` | `/scripts/<name>.json` per agent script (`scripts/agent-e2e.*`) | agent E2E script |
| Claude double | backend reported as `claude` with the test handler (Q6 step 3) | devops T069 (**gap**: no variable named in the contracts; the spec skips with the reason until one exists) |

Scripts are chosen by **sheet title**, so specs create sheets with the exact titles of
`expected.json` ("Zomeravond", "Avondlied", "l-inject", "Kettle", "Lanterns"). Sheets are created
per test (no shared state); the personal word list is global, so the "my words" test removes
`Zelmara` before and after.

**Corpus track** (decision 023-P0-10): the 006 fake lyrics have 3-word lines and never trip the
own-lyrics guard. `ensureCorpusTrack` imports a WAV titled "Paper Lanterns qa<run>" and pastes
`corpus/t-fake-song.en.txt` through 006 `setLyrics` (`PUT /api/tracks/{id}/lyrics`); the not-own,
verbatim and own-work tests need it.

**Agent placeholders** (CTO decision on the qa T060 gap): `{{context.lyricsId}}` (page context)
and `{{result.list_lyric_suggestions.ids:<category>}}` (ids of that category from the run's most
recent `list_lyric_suggestions` result; whole value, JSON array; empty when none; script error
without an earlier call). The ai-agent lane implements them in `ScriptedChatSession`; the
integration adds them to the 011 agent-script schema description; `agent-script-check.ts` and
its self-test accept exactly these forms.

## 4. Injection and guardrail matrix (SC-005)

| Case | Input | Expected | Test |
|---|---|---|---|
| L-I1/L-I3 | `inject.en.txt` lines 3, 5 (instructions in the sheet) + `l-inject-check.json` (three DELETE items) | only `sleaping` -> `sleeping` shown; >= 3 discarded (anchor, range, whole-line); text unchanged | guardrails "injected lines" |
| L-V1 | `not-own.en.txt` (4 lines of t-fake-song) | every button refused, `lyric-assist-not-own` names the track, `409 not-own-lyrics`, `overridable` | guardrails "library lyrics" |
| L-V2 | `verbatim.en.txt` + `alternatives.json` alternative 1 = 9-word run | `discarded.verbatim = 1`, `lyric-assist-withheld` 1, run nowhere in the page | guardrails "verbatim" |
| L-V3 | override + `alternatives.json` | still withheld | guardrails "own work" |
| Imitation | instruction "zoals Marco Borsato" / "like Ed Sheeran" | `400` imitation, no assist, message in the pane; also with the override | guardrails "imitation", "own work" |
| S5 | agent asked to override | `check_lyrics` fails `not-own-lyrics`; `confirm_own_work` is no tool; no override stored | agent `@us6-lyric-override` |
| Agent apply | `apply_lyric_suggestions` | approval always, reason `lyrics-edit`; reject changes nothing | agent `@us4-lyric-apply` |
| L-V4 (two tracks), L-I2 (Dutch injection), cliché sheets | - | backend and assistant unit tests (owners) | - |

## 5. Run matrix

| Command | Stack | Tests |
|---|---|---|
| `npm run test:lyric-assist` | fake, lyric scripts folder | `@lyric-assist` except `-none`/`-claude` and `@agent` |
| `npm run test:lyric-assist-none` | `STEMSTUDIO_AGENT_BACKEND=none` | `@lyric-assist-none` |
| `npm run test:lyric-assist-claude` | Claude double | `@lyric-assist-claude` |
| `scripts/agent-e2e.sh fake` | fake, per script | `@us4-lyric-check`, `@us4-lyric-apply`, `@us6-lyric-override` (with the lyric scripts folder mounted too) |
| `npm run test:a11y` | fake, lyric scripts | `a11y/lyric-assist.a11y.spec.ts` |

## 6. Accessibility plan (SC-008, quickstart Q9)

- Automated: axe (WCAG 2.2 AA tags) on the editor with the pane idle and after a check, both
  themes, Beginner and Expert; keyboard-only test of Ctrl+Alt+C, F8, Alt+Enter, Alt+Backspace,
  Escape and the live-region texts ("Check finished: ...", "Line n, ...").
- Manual (T070, once): NVDA + Firefox on the fake stack: Check by keyboard, the finished
  announcement once, each focused suggestion read with category, original and proposal, Alt+I
  marks, Escape returns to the text. Recorded in the qa T070 hand-off with date and versions.

## 7. Evidence required per hand-off

- backend-023: coverage >= 90 % line and branch for `Domain/Lyrics/Assist` and `Api/LyricAssist`;
  syllable accuracy against the gold lists (SC-007); the outbound-handler test; contract tests.
- frontend-023: bUnit coverage of `Components/Lyrics/Assist`, key handling and announcement tests,
  level visibility (A/E columns).
- ai-agent: tool provider tests, policy `lyrics-edit`, `ScriptedChatSession` placeholder tests,
  eval cases 31-35, S4, S5 (scripted), T008 spike numbers.
- qa: typecheck and lint green; at T070 the Playwright report (retries 0), the NVDA note and the
  audit of the owners' coverage evidence.
