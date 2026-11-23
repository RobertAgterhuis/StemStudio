# Resume note: backend lane, feature 020

Branch `020-lane-backend` (worktree `.claude/worktrees/020-lane-backend`). Lane rules:
`.claude/scratchpad/020-round/lane-rules.md`. Scratch/logs: `.claude/scratchpad/020-round/backend/`
(red-commit helper `tools/stub.py stub|restore <stash> <files>`).

## Done (committed, green)

| Tasks | State |
|---|---|
| T020/T021 | Track.QualityReport, PracticeNote, Asset handout kind/recipe/facts, migrations AddQualityReport + AddPracticeNotes (additive; Down keeps IX_Assets_Folder) |
| T022/T023 | IWorkerInsightsClient (quality + handout jobs, health) against worker 1.11.0 examples in `tests/StemStudio.Api.Tests/Worker/Examples/` |
| T024/T025 | Source ports/adapters `src/StemStudio.Api/Handouts/Sources/` |
| T042-T045 | Quality service/applier/trigger/endpoints/badge (`src/StemStudio.Api/Insights/`); ops moved to InsightsOperations |
| T049-T052 | TipRules (Domain/Insights), QualityTips |
| T056-T067 | Song handouts, options, practice notes (`Api/Handouts/`, `Domain/Handouts/`) |
| T081/T082 | Setlist handouts |

## State

All backend tasks of 020 (T020-T025, T042-T045, T049-T052, T056-T067, T081, T082) are committed green; hand-offs
`backend-020-{foundational,us1,us2,us3,us4}.md` written. G2 T093-T096 deferred (not built). Next: CTO integration
answers to the "please confirm" decisions in the hand-offs.
