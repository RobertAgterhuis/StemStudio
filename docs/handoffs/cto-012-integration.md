# Hand-off: CTO / 012 integration wave (T069-T075)

Branch `012-practice-tempo` (worktree `.claude/worktrees/012`), not pushed, not merged into `main`. The owner was
unavailable: every decision below is recorded in `docs/decisions/CTO-DECISIONS-2026-11-15.md` (rows "012").

## What was built

### Merges (`--no-ff`, in order)

| Lane | Merge commit | Conflicts |
|---|---|---|
| 012-lane-quality | `67627e8d` | none |
| 012-lane-devops | `104d8a7f` | `tasks.md` ticks (all kept) |
| 012-lane-backend | `3f1921ed` | none |
| 012-lane-engine (contains devops + quality) | `195b203e` | `tasks.md` ticks |
| 012-lane-frontend | `b5306241` | `tasks.md` ticks |
| 012-lane-qa | `2debcc8e` | `tasks.md` ticks |

### Cross-owner items

- **architect**: audio-engine.md 2.3.0 "Content offset" now reads `n + c(p)` (also the TSDoc of
  `contentOffsetFrames`, ADR-021 E10 marked applied); "Explicit passes" states ADR-021 E4 (gapless passes in source
  time, heard anchor moves by `L(p_{k+1}) - L(p_k)`) and the count-in gap `(N d - (b - A)) / r` (editorial, no version
  change). NOTICE.md names both build scripts and `/third-party/soundtouch/index.html`; README "Rebuild" documents
  `--verify-reproducible` / `--local`.
- **THIRD-PARTY-LICENSES**: SoundTouch 2.4.1 (LGPL-2.1) and the shim/header (LGPL-2.1-or-later) in a new 012 section;
  emsdk 6.0.13 (build-time), actions/download-artifact 8.0.2 (MIT, CI), PyYAML 6.0.3 (MIT, CI/test-all).
- **ai-agent** (test-first): `MixProjections.Settings` adds a read-only `practice` object (numbers and flags only) to
  `get_mix` and `mixer_get_state`; `PracticeToolProjectionTests` (5) prove `save_mix_as`, `update_mix` and the
  `restore_mix` undo carry `practice` unchanged. Tool catalogue **1.3.0** (next free label after 1.1.0/022 and
  1.2.0/023), changelog in `tool-catalogue.md`, recorded in `docs/ROADMAP.md`.
- **devops** request (folder URL 404): kept the frontend's `index.html` links (decision row).
- **qa** request: TEST-STRATEGY traceability updated for the agent case (see below).

### Findings fixed in the integration (test-first where they were product bugs)

| # | Area | Finding | Fix |
|---|---|---|---|
| 1 | e2e support | stored tour versions 1 while 012 bumped both mixer tours to 2: every seeded page offered the tour again | `preferences.ts` versions 2 + stack-free self-test `preferences-support.spec.ts` |
| 2 | web (+ 011 agent.css) | `font-family: var(--ss-num)` (a `font-variant-numeric` value) rendered practice and agent numbers in the browser's serif | `font-variant-numeric`; `FontFamilyTokenTests` |
| 3 | audio-engine | pause/stop sent no `OnPracticeChanged`: the UI kept "running, pass 2" after stop | `onStateChanged` reports; `passes.test.ts` |
| 4 | audio-engine | a waiting trainer reported pass 0, data-model 3.2 says pass 1 | snapshot reports `max(1, pass)`; engine tests updated |
| 5 | web | practice buttons' accessible names did not contain their visible text (axe label-content-name-mismatch, WCAG 2.5.3) | `-5 % (slower)`, `+5 % (faster)`, `100 % (original speed)`, `-1 semitone`, `+1 semitone`; bUnit tests |
| 6 | e2e set-up | `mixer-practice` screen: fast slider keys / transpose clicks could be dropped before the re-render | confirmed +5 % and +1 steps |
| 7 | qa specs | assumptions that did not match the documented behaviour | silence fixture for "no grid" (the tone gets a reliable 140 BPM grid); count-in gap and gapless trainer passes per ADR-021 E4; countdown without "1" when the 4th click coincides with the music start; help query `count-in bars` ("count-in" ranks the glossary first by design, now unit-tested); strips scrolled into view at 1280 x 800; agent practice case uses `update_mix` (`rename_mix` is not offered in the mixer context) |

## Files changed (integration commits)

`src/StemStudio.Agent/Tools/Providers/{MixProjections,Core001ToolProvider}.cs`,
`src/audio-engine/src/practice/{controller,trainer}.ts`, `src/audio-engine/src/stretch/params.ts`,
`src/StemStudio.Web/wwwroot/css/components/{practice,agent}.css`,
`src/StemStudio.Web/Components/Mixer/Practice/{SpeedControl,TransposeControl}.razor`, tests in
`tests/StemStudio.{Agent,Web}.Tests`, `src/audio-engine/tests/practice`, `e2e/**` (support, specs, agent script,
28 visual baselines), `specs/011-ai-composer-agent/contracts/tool-catalogue.md`,
`specs/012-practice-tempo/{contracts/audio-engine.md,tasks.md}`, `docs/{ROADMAP,STATUS,MoSCoW,THIRD-PARTY-LICENSES}.md`,
`docs/decisions/{ADR-021-practice-tempo-time-stretch,CTO-DECISIONS-2026-11-15}.md`, `docs/quality/SIG-REPORT.md`,
`docs/testing/TEST-STRATEGY.md`, `third_party/soundtouch/{NOTICE,README}.md`, `README.md`, `site/src/**`,
`docs/public/src/**` + PDFs + `build-manifest.json`.

## Tests and coverage

```text
scripts/build-soundtouch.sh        soundtouch.wasm 30,271 B, no imports, ABI 1, 13 exports, sha256 4b490ba9...585f
dotnet build -c Release            0 warnings, 0 errors;  dotnet format --verify-no-changes: clean
SOUNDTOUCH_WASM_REQUIRED=1 pwsh scripts/test-all.ps1 -ContinueOnError
  Domain 793 (98.98 / 94.46)  Persistence 120 (99.01 / 95)  Web 2061 (99.6 / 93.66)
  Api 1706 + 2 skipped (99 / 92.24)  Agent 726 (99.03 / 94.18)  AgentEval 134 (99.83 / 92.85)
  engine 488 (100 % statements 1814/1814, branches 753/753)  test:wasm 26/26  web-interop 293 (99.71 / 98.19)
  worker 2381 (100 %)  generator 503 (99.05 %)  quality 716 (100 %)  key checks 73 (100 %)  fixtures 254 (100 %)
  contracts lint (incl. 012) + superset, secret scan, key-volume checks, site check, e2e lint/typecheck/list: passed
  failed: docs manuals (before the PDF rebuild, now passing); ClaudeKeyLeakTests.StoredSentinel_NeverLeaks
          (pre-existing 011 timing flake: WAL already checkpointed; 3/3 green on rerun)
tools/quality/run_all.py           2321 files, 18912 units, 0 errors, 866 warnings; SIG-REPORT.md regenerated
node tools/docs/check.mjs          passed after the rebuild
```

## E2E (project `e2e012`, 18080/18081, fake separator, retries 0, Chromium + Firefox)

| Suite | Result |
|---|---|
| `scripts/check-soundtouch-served.sh --expect-wasm ... http://localhost:18080` | all checks passed; served module = script build |
| probe self-tests (`practice-probes.spec.ts`, part of `npm test`) | passed |
| `npm test` (includes `@practice`, `@export`/`@library`, `@karaoke`/`@lyrics`) | run 1: 481 passed / 14 failed (7 cases x 2 browsers, fixed above); run 2: **495 passed / 115 skipped / 0 failed**; `@practice` 112/112, `@export|@library` 62, `@karaoke|@lyrics` 43 |
| `npm run test:a11y` | 151 passed / 6 skipped / 3 failed (2 x target-size, 1 set-up race fixed afterwards) |
| `scripts/agent-e2e.sh fake` / `none` | 22/22 / 2/2 |
| `npm run test:claude-key` | 19/20; the failing local-only case passed 2/2 on rerun (pre-existing save race) |
| visual (container, fresh stack) | update: 116 passed; compare: 116 passed / 6 skipped; a later compare hit the pre-existing first-screenshot `tracks--beginner--dark` row-order flake |

Visual baselines: new `mixer-practice--{beginner,advanced,expert}--{dark,light}`; changed `mixer--*` (6),
`mixer-expanded--*` (6), `mixer--768--*` (2), `export-dialog--*` (6), `help-panel--mixer--*` (2). Reviewed: only the
practice panel (and the help text/related link on the help panel) differ; the count-in slot is empty when idle. The
settings baselines stay unchanged: they depend on the Ollama status block, so the run used an unreachable
`STEMSTUDIO_OLLAMA_URL` like CI.

Teardown: `down -v` of `e2e012`, images `e2e012/*` removed. Owner stack `stemstudio`: same container ids, images and
volumes before and after.

## Quality checks

Release build 0 warnings; `dotnet format` clean; engine/web-interop/e2e eslint + tsc clean; `run_all.py` 0 errors;
new units within the SIG limits.

## Dependencies added

None in the integration (licence rows consolidated from the lanes).

## Docs impact

Done: user manual (chapter 5 "Practice tempo", 3 levels, 9 export note, 14 shortcuts, 16 FAQ) and technical manual
(2 architecture/contracts, 3 SoundTouch module build/replace/CSP, 5 licences, 6 API + agent tools, 7 data model and
migration, 9 quality gates, 11 operations); README, website (out of "Coming soon"), STATUS, MoSCoW, ROADMAP.

## Open issues / requests

- **frontend (owner decision)**: the practice panel pushes the channel strips below the fold on ~900 px screens; at
  Beginner with help docked the `-5 %` button sits partly under the sticky karaoke + transport dock (axe target-size,
  2 a11y cases). Proposal: a collapsible practice panel (collapsed at neutral settings) or a two-column panel row.
- **audio-engine**: the count-in indicator skips "1" when the 4th click coincides with the music start (grid without
  the song's first beat); consider showing each number for its beat interval.
- **frontend**: rapid repeated +1 / +5 % clicks within one render round trip can be dropped (seen only with
  automated clicks).
- **qa / devops**: pre-existing flakes: first `tracks--beginner--dark` visual (row order right after seeding),
  claude-key local-only save race, `ClaudeKeyLeakTests` WAL timing.
- **owner (T071)**: listening notes at 85/70/50 % and +-7 st (vocals, guitar, drums soloed) and replacing the module
  with an own rebuild by hand.
- Backlog: agent tools that change the practice tempo (later catalogue minor); "export at practice tempo" with 015.

## Next owner

Coordinator: review, push and merge `012-practice-tempo` into `main`; then 013.
