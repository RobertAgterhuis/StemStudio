# beat-analysis 020: resume note

Worktree `.claude/worktrees/020-lane-beat`, branch `020-lane-beat`.
Python: `/e/PatricksSoundStudio/.claude/worktrees/020-insights-handouts/worker/.venv/Scripts/python.exe`
with `PYTHONPATH=src` (run from `worker/`), e.g. `-m pytest tests/insights -p no:cacheprovider`.

State: every task is done and committed in green: T014, T015, T030-T039 (T039 "pending dataset") and
T103. Hand-offs: `beat-analysis-020-{foundational,us1,polish}.md`.

Open: Q3 in `beat-analysis-020-questions.md`.
- Dense control: `test_validation_v1.py::test_dense_song_has_no_false_bleed_above_medium` is a strict
  xfail. Decide: keep the limit, add a pair, or retune.
- Gain cap: `thresholds.MAX_GAIN`. Confirm it, or set it to infinity and recalibrate `BLEED_LEVELS`.

After the answer: adjust, rerun `test_validation_v1.py`, and if a constant changes, bump
`ANALYZER_VERSION`.
