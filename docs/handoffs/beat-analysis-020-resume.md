# beat-analysis 020: resume note

Worktree `.claude/worktrees/020-lane-beat`, branch `020-lane-beat`.
Python: `/e/PatricksSoundStudio/.claude/worktrees/020-insights-handouts/worker/.venv/Scripts/python.exe`
with `PYTHONPATH=src` (run from `worker/`), e.g. `-m pytest tests/insights -p no:cacheprovider`.

State (green, committed):
- T014 `f7b6a2cc` (red), T015 `a231addb` (green): `insights/representation.py`, 17 tests, 98 % line.
- `worker/tests/insights/synthetic.py`: six family generators plus the bleed, TF-mask and gating injections.

Waiting: `beat-analysis-020-questions.md` Q1 (B2 guard) and Q2 (A1 flicker, A2 drums).
Next after the answers: T030/T031 bleed, T032/T033 artefacts and detail, T034/T035 rating, thresholds and
analyzer, T036/T037 fake, T038 reliability table, T039 G1 script ("pending dataset"), T103 timing.
Experiments: `.claude/scratchpad/020-round/beat/exp/` (`bleed_exp2.py`, `bleed_exp3.py`, `art_exp*.py`;
`SPARSE=1` selects the arrangement with rests).
