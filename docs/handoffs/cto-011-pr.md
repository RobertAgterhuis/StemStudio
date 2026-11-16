# PR: feature 011 - AI composer agent

Branch `011-composer-agent` -> `main`. Prepared by the CTO (T084); the PR is **not created** and nothing is pushed.

## Summary

StemStudio gets an optional AI composer agent: a panel on every page that turns a Dutch or English command into a
visible plan and runs StemStudio's own tools (import, split further, mixer, loops, mixes, exports, lyrics, sheet music,
style transfer, songs, own-voice conversion with an existing consented profile). Safety is enforced in code: a fixed
tool allow-list with risk classes, approvals bound to the exact arguments for heavy and destructive steps, taint
tracking for external text (track titles, lyrics, biographies), URL and lyrics provenance from the user's own words,
per-run limits, and Undo for actions with an inverse.

- Backends: **Ollama** (default, local, `qwen3.5:9b`, fallback `qwen3.5:4b` while a GPU job runs), **Claude**
  (optional, paid per use, constitution 1.3.0; key only from `ANTHROPIC_API_KEY` in the shell; text only) and **none**.
- New library `src/StemStudio.Agent` (Api -> Agent -> Contracts), `/api/agent/*` endpoints with SSE run events
  (backend API 1.10.0), Settings page, UI test ids 1.10.0, tool catalogue 1.0.0, migration `AddAgent`
  (`20261116064402`).
- Evaluation harness `tools/agent-eval` (33 cases over a seeded fake fixture; never run in CI).
- 001 behaviour change: new uploads are titled with the original file name instead of the stored hash.

## Verification

| Check | Result |
|---|---|
| `pwsh scripts/test-all.ps1 -ContinueOnError` | T080: 51/51 steps passed. Final re-run after T081-T084a: see "Final run" below |
| Coverage | Agent 98.80 / 93.22, AgentEval 99.83 / 92.57, Web 99.56 / 93.65, Api 98.91 / 91.61, Persistence 98.76 / 94.73, Domain 98.97 / 94.52 (line / branch %) |
| Quality gate | `run_all.py` 0 errors; Release build 0 warnings; `dotnet format` clean; secret scan clean; Redocly valid |
| Playwright (fake stack, retries 0) | default 375 passed / 115 skipped; agent fake 18/18; none 2/2; `@generative` agent 4/4 (by hand); visual 110; a11y 142 |
| Real model (T081, `qwen3.5:9b`, RTX 5070 Ti, Ollama 0.40.16) | final tree: 30/30, 26/30 (nl 75 %), 30/30 over three `--runs 3` evaluations; S1-S3 and injection 22-24 pass in two of them; p50 1.4-1.5 s; cold start 3.2 s. History in `tools/agent-eval/out/`, details in `docs/STATUS.md` |
| Claude (T082) | owner-run, not done |

## Before merging (owner)

1. **SC-001 variance**: met in 2 of 3 evaluations on the final tree; one run had nl 12/16 (75 % < 80 %). Accept as
   is, or ask the ai-agent for a low Ollama temperature (a product change, not done here: T081 allows tool
   descriptions only).
2. **Not run on the reference machine**: quickstart A7-A10, A12, A13 and SC-008 (they need the 011 stack on :8080 and
   the GPU stack; the owner's `stemstudio` stack had to keep running). Run them after the merge, or before it in a
   maintenance window.
3. **T082** (Claude A14/A15, paid): optional, owner-run.
4. Development databases that applied the earlier 011 `AddAgent` must be recreated (owner decision 2026-11-16).
5. Known limitations accepted for 011 (README, manuals): no "Continue" for an interrupted run; external track titles
   taint a run, so heavy or changing steps after a library search always ask.

## Consistency pass (T084, `/speckit-analyze` style, read-only report)

Artefacts: `specs/011-ai-composer-agent/{spec,plan,tasks,quickstart,data-model,research}.md`, `contracts/*`,
`evaluation/eval-set.md`, `tools/agent-eval/eval-set.json`.

| ID | Category | Severity | Location | Finding | Resolution |
|---|---|---|---|---|---|
| A1 | Coverage gap | Medium | spec.md edge cases, plan.md item 6 | "the panel offers Continue (new run with the same context)" after an API restart: no task, not built (the panel shows the run as interrupted) | spec and plan annotated as not delivered; README and manuals list it as a limitation; follow-up for the backlog |
| A2 | Drift | Low | spec.md assumptions | Claude backend "requires a constitution amendment (draft)" | annotated: ratified as constitution 1.3.0 |
| A3 | Drift | Low | quickstart.md prerequisites | "Only after the constitution amendment" | now "Amendment ratified (constitution 1.3.0)" |
| A4 | Drift | Low | quickstart.md A11 | report "in `tools/agent-eval/out/`", no `--model`; the harness writes `--out` (default `agent-eval-report.md`) | command with `--model` and `--out`, Ollama URL variable outside Docker |
| A5 | Drift | Medium | quickstart.md A14 | `tools/agent-eval --backend claude` is not wired (the harness exits 2) | A14 now sends the A10 cases by hand |
| A6 | Drift | Low | plan.md documentation tree | eval-set "draft 0.1.0" | "30 scored + S1-S3 (1.0.0 final)" |
| A7 | Inconsistency | Medium | eval-set.md vs eval-set.json | md lists the reads `search_library` / `list_styles` (29) and `list_generations` -> `list_voice_profiles` (S2, ordered) as expected calls; the JSON expects only the decisive call, so those reads count as extras (29 fails at 3 extra reads) while S2's order is not enforced | not changed after seeing results (would move the bar); for the ai-agent / architect: decide which is binding and regenerate |
| A8 | Grader weakness | Medium | `tools/agent-eval/Grader.cs` | an expected call matches even when its step failed or was denied (S1 once counted a denied `save_lyrics_sheet`); md says "occurs" | not changed; ai-agent follow-up (match executed calls, except where a case expects a refusal) |
| A9 | Harness fidelity | High (fixed) | `tools/agent-eval/Fixture/FixtureActions.cs` | case 24 expects the API's 400 for `evil.example`; the fixture accepted any URL, so the import counted as executed | fixed test-first: the fixture checks the 001 host allow-list (`d563c49d` red, `8c0b08d4`) |
| A10 | Underspecification | Low | spec.md FR-010, FR-016 | not cited by id in tasks.md; covered by T026 (cancel) and the run-step store / Expert tool log | none |
| A11 | Status marker | Low | spec.md header | "Status: Draft" | kept: every delivered feature's spec keeps "Draft" (repository convention); delivery is recorded in `docs/STATUS.md` |
| A12 | Constitution | - | plan.md constitution check | all principles PASS; IV via amendment 1.3.0 (opt-in paid service); OSI licences recorded (T083) | none |

Contract versions agree across DELTA.md, `backend-api.openapi.yaml` (1.10.0), `agent-api.openapi.yaml`
(1.10.0-delta.1), `ui-test-ids.md` (1.10.0) and the tool catalogue (1.0.0). Every task except T082 is ticked.

## Commits

232 commits on `main..011-composer-agent` before T081 (plus the T081-T084a commits) (lane merges with `--no-ff`, integration fixes test-first,
T081 description tuning, docs). Author and committer of every commit: `ragterhuis@agterhuis.net`.

## Final run

`pwsh scripts/test-all.ps1 -ContinueOnError` on `30487640` (after T081-T084a), 2026-11-16: **51/51 steps passed**,
exit 0, including "agent coverage floor (011)", "agent-eval coverage floor (011)", "quality gate (run_all)" and
"docs manuals up to date". Tests: Domain 752, Agent 600, AgentEval 134, Persistence 99, Web 1712, Api 1435 (all
passed); worker, generator, engine, web-interop, quality and fixtures suites green; e2e lint, typecheck and list
(805 tests in 45 files). `node tools/docs/check.mjs`: manuals up to date.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
