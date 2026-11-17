# Hand-off: qa / 012 polish (T066) - lane complete

Branch `012-lane-qa` (worktree `.claude/worktrees/012-qa`), base `010c3e6d` (architect Phase 0).
Lane tasks T012, T047, T054, T061, T066 are done and ticked. Earlier checkpoints:
`qa-012-us1.md`, `qa-012-us2.md`, `qa-012-us3.md`. The owner was unavailable; decisions and
deviations are listed below. Per the lane brief nothing was executed against a stack or a browser
(no Docker, no Playwright run): every spec is authored against the binding contracts
(ui-test-ids 1.11.0, backend-api 1.11.0, audio-engine 2.3.0) and validated statically; the CTO
executes them in T070.

## What was built

| Task | Files | Content |
|---|---|---|
| T012 | `tests/fixtures/generate_fixtures.py` (+ tests, `manifest.json`) | `tone_440hz_20s.flac`: stereo 440 Hz sine, -12 dBFS peak, 10 ms fades, CC0; manifest `frequencyHz`, `peakDbfs` |
| T012 | `e2e/support/output-probe.ts` | init scripts tap every `connect(destination)` into a fine (32768) and a fast (2048) `AnalyserNode`; dominant frequency (parabolic interpolation), RMS, in-page timed frequency step |
| T012 | `e2e/support/click-probe.ts` | planned `start(when)` of every click (< 0.25 s buffer) with accent (buffer peak) and cancellation, music starts (`when`, offset, rate) |
| T012 | `e2e/support/practice.ts` | contract constants and `L(p)`, practice DTO builders, position-rate regression, attribute watcher, rate-aware karaoke reference, anchor spread of the stems |
| T012 | `e2e/tests/practice-probes.spec.ts` | stack-free self-tests (`@practice @self-test`, 7 per browser): 440 Hz within 0.2 %, level, a 100 ms-ahead frequency step timed 90-200 ms, disconnect, click times/accents/cancel/music grouping, the maths |
| T047 | `e2e/pages/practice-panel.ts`, `support/test.ts` (fixture `practice`), `support/api.ts` (practice DTOs, `BeatGridDto`) | page object of the 1.11.0 ids |
| T047 | `e2e/tests/practice-tempo.spec.ts` (25 per browser) | P3 speed 50/120 %, P4 pitch x 12 (speeds 50/70/100/120, transposes -12/-5/+2/+12, combined at 50 and 120 %), P5 live (6 transpose steps <= 250 ms, 100 slider steps, `data-underruns="0"`), P6 neutral (zero requests for the module, defaults via the API), P7 karaoke at 50/75/120 % + click spacing 1.000 s at 50 %, P10 persistence + duplicate, P11 unavailable (404 and no `AudioWorkletNode`), FR-008 hint / effective tempo / reset, US1-3 detail stems in sync |
| T047 | `e2e/tests/practice-families.spec.ts` (6 per browser) | P14 / SC-008: vocals, guitar, bass, keys (`piano`), drums, other |
| T047 | `e2e/tests/export.spec.ts` (+1) | P12: `export-practice-note`, "What I hear" length = original |
| T047 | `e2e/tests/agent.spec.ts` (+2), `e2e/agent-scripts/us5-practice-{save,rename}.json`, `support/agent-scripts.ts` | the agent's `save_mix_as` (from the open mixer at 75 % / -2 st) and `rename_mix` keep `practice` |
| T054 | `e2e/tests/practice-trainer.spec.ts` (4 per browser) | P8 sequence 60, 70, 80, 80 + pass lengths, pause/stop, needs-loop / 0.5 s / error, saved settings |
| T061 | `e2e/tests/practice-count-in.spec.ts` (4 per browser) | P9 1 bar (timing, accents, indicator 4..1), 2 bars every pass at 50 % (Expert), pause/seek cancel, no grid |
| T066 | `e2e/tests/practice-levels.spec.ts` (8 per browser), `support/levels.ts` | P13 Beginner/Advanced/Expert ids, spoken values, slider keys, Tab walk with `:focus-visible`, shortcuts `- = , . r c t` (ignored in a text field), help markers, Expert number entry + diagnostics |
| T066 | `e2e/a11y/practice.a11y.spec.ts` (6), `support/screens.ts` | axe on a running count-in (+ no animation with reduced motion), a running trainer, the unavailable notice, both themes; screen `mixer-practice` added to the a11y (6) and visual (6) matrices |
| T066 | `e2e/tests/help.spec.ts` (+1), `support/help.ts` | the five 012 help topics found first and opened |
| T066 | `e2e/package.json` | `npm run test:practice`; `@practice` excluded from `test:legacy` |
| T012/T066 | `docs/testing/TEST-STRATEGY.md` | intro paragraph, fixture row, section 18 (what is tested where, probes, run matrix, fixtures, traceability FR/SC/US -> tests incl. the six families, visual baselines, commands) |

## Commits (lane)

| Commit | Task |
|---|---|
| `bbbc0b88` | T012 fixture, probes, self-tests, strategy |
| `c28417b3` | T047 + HO:us1 |
| `5bb5d3ea` | T054 + HO:us2 |
| `a58d154e` | T061 + HO:us3 |
| this commit | T066 + HO:polish |

There is no red -> green pair inside this lane: the E2E specs are the red half for the
audio-engine/frontend implementation and turn green only on the integrated tree (T070).

## Tests and coverage

```text
uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
    --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
  254 passed; fail_under 90 reached; total 100.00 % (1068 stmts, 114 branches, 0 missed)
  generate_fixtures.py 178 stmts / 22 branches: 100 % line, 100 % branch
uv run --project worker ruff check tests/fixtures           All checks passed!
uv run --project worker ruff format --check tests/fixtures  21 files already formatted
uv run --project worker mypy --config-file tests/fixtures/pyproject.toml tests/fixtures
                                                            Success: no issues found in 18 source files
cd e2e (node_modules = junction to the main checkout's e2e/node_modules, git-ignored)
npx tsc --noEmit                                            exit 0
npx eslint . --max-warnings 0                               exit 0
npx playwright test --list                                  Total: 965 tests in 54 files
  --grep @practice --project=chromium --project=firefox     Total: 112 tests in 8 files (56 per browser)
  default npm test selection                                Total: 608 tests in 44 files
  --project=a11y                                            Total: 160 tests in 3 files (+12: 6 mixer-practice, 6 practice states)
  --project=visual                                          Total: 122 tests in 1 file (+6 mixer-practice)
  tests/agent.spec.ts (chromium + firefox)                  Total: 28 tests (+4)
```

The E2E specs were not executed (lane brief). The probe self-tests need no stack and can run
first: `npx playwright test tests/practice-probes.spec.ts`.

## Quality checks

```text
uv run --project tools/quality python tools/quality/run_all.py
  quality gate: 2232 files, 17749 units, 252 clones, 3 exemptions, 0 errors, 831 warnings -> exit 0
```

New warnings: 14 `unit-size` warnings on test bodies of 16-22 lines (limit 15, hard max 30; the
existing specs carry the same kind, e.g. `agent.spec.ts`, `export.spec.ts`); no duplication,
complexity or parameter finding in a new file (two clones found on the first run were removed).
ESLint enforces complexity <= 5, <= 4 parameters, <= 30 lines per support function, <= 60 per
test callback. Public API of `pages/` and `support/` documented (TSDoc, `jsdoc/require-jsdoc`).

## Dependencies added

None (no npm, Python or other package). The tone fixture is self-generated (CC0).

## Docs impact

None user-visible. Technical manual (testing chapter, optional): `npm run test:practice`, the
output and click probes, the new screen `mixer-practice` in the visual/a11y matrices.

## Decisions taken (owner unavailable)

1. **`@practice` is part of the default `npm test`**: the module is built inside the web image
   (devops T010), so no new service, variable or profile is needed; the karaoke-at-speed cases
   skip on non-`word` lyrics stacks like the 006 cases. `npm run test:practice` runs them alone.
2. **Latency (P5)** is measured on transpose jumps of 7 semitones through the Expert
   `practice-transpose-input` (466 <-> 698 Hz, unambiguous for a 46 ms analyser) from the input
   event to the changed output, including the Blazor round trip; slider steps are used for the 100
   no-dropout changes. The detection window makes the number up to ~23 ms pessimistic.
3. **Speed (P3, P14)** is the least-squares slope of all `transport-position` changes; tolerance
   `max(0.2 s, 2 %)` per quickstart P3, 2 % for the 4 s family cases.
4. **Count-in timing** uses the detected grid (`beatGrid.beats`, fixture beat times as fallback)
   and the contract formula; the every-pass case starts the loop between two beats (A = 1.75 s) so
   the "first beat >= A" rule is exercised without float ties.
5. **Extra check (SC-006/FR-011)**: trainer pass lengths from the stem start times equal
   `(B - A)/r_k` corrected by `L(p_{k+1}) - L(p_k)` within 5 ms.
6. **Visual screen `mixer-practice`** is set through every-level controls and saves nothing, so
   the seeded mix library (and its baselines) stays unchanged.
7. **Agent coverage**: two scripted cases (`save_mix_as` from the open mixer, `rename_mix`) run by
   `scripts/agent-e2e.*` like the 011 scripts.

## Open issues / requests

- **frontend** (assumptions the specs make where ui-test-ids leaves the element type open):
  `countin-bars` is a native `<select>` with values `1`/`2`; `trainer-*`, `practice-speed-input`,
  `practice-transpose-input` are number inputs that commit on `change`; `practice-speed` reacts to
  Arrow/PageUp/PageDown/Home/End and updates `aria-valuenow`; `practice-panel` `data-speed`
  reports the configured speed while stopped (the trainer's start speed while `waiting`);
  `trainer-status` is rendered at Advanced/Expert whenever the trainer panel is (state `off`
  without a loop); help search finds `slow down` -> `practice-speed`, `transpose`,
  `count-in`, `speed trainer`, `sound quality` -> `practice-sound-quality` first (adjust the
  queries in `e2e/support/help.ts` `practiceReferenceQueries` if the topics use other words).
  `MixerState.ToSettings` must carry `practice` (repeat of the backend request) - the agent
  save case depends on it.
- **frontend lane notes received (coordinator, end of lane) and applied**: `practice-transpose`
  is a `div[role=spinbutton]` - the specs drive it only through `practice-transpose-up/-down`
  and read `data-transpose` / `aria-valuetext`; Page Up/Down on the speed slider needs the built
  `web-interop.js` (present in the web image; the Advanced "slider keys" case uses PageDown);
  the count-in announcer is `[data-countin-announcer]` without a test id, so the a11y case no
  longer asserts `role="status"` on `countin-indicator` (ids only, R1); `+` is an alias of `=`;
  the new count-in slot in the mixer dock changes the visual baselines (listed below).
- **audio-engine**: the specs rely on the contract details: explicit pass sources start with
  `start(T_k - L(p_k), A, B - A)`; clicks and count-in clicks are `AudioBufferSourceNode`s with
  buffers shorter than 0.25 s, the accent buffer having the larger peak; cancelled clicks are
  stopped at or before their start time; `playbackRate.value` of a running stem reflects its rate.
- **CTO (T070)**: regenerate the visual baselines (new `mixer-practice--{beginner,advanced,expert}--{dark,light}.png`;
  every mixer baseline changes with the panel and the count-in slot of the mixer dock:
  `mixer--{beginner,advanced,expert}--{dark,light}`, `mixer-expanded--*`, `mixer--768--*`,
  `export-dialog--*`, `help-panel--mixer--*`) through the documented container flow and review
  them; T070 runs: `STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`,
  `scripts/check-soundtouch-served.sh`, `cd e2e && npx playwright test tests/practice-probes.spec.ts`
  (stack-free first), `npm test` (includes `@practice`) or `npm run test:practice`,
  `npm run test:a11y`, `npm run test:export`, `npm run test:lyrics`, then
  `scripts/agent-e2e.sh fake` (picks up the two new scripts by itself); retries 0, Chromium and
  Firefox. If a case fails on an assumption above rather than on behaviour, adjust the page
  object (`e2e/pages/practice-panel.ts`) - every interaction goes through it.
- **CTO**: TEST-STRATEGY 17.9's counts (12 agent tests, 9 scripts) are 011 history; 012 adds 2
  tests and 2 scripts (section 18.2).

## Next owner

CTO (T069-T075).
