# Resume note: architect / 020 Phase 0

Branch `020-insights-handouts` (from main cc314608), worktree `.claude/worktrees/020-insights-handouts`.
Scratch/logs: `.claude/scratchpad/020-round/`.

## State (update at every milestone)

- [x] Ready check (spec/plan/tasks consistency) and spec fixes (47596492)
- [x] T003 contracts merged (worker 1.11.0, backend 1.19.0, ui-test-ids 1.19.0) via `contracts/merge_full.py` (f9bed136)
- [x] T004 014/016 reconciliation recorded (DELTA.md "Phase 0 outcome" P0-4/P0-5)
- [x] T001 worker contract code (2bc0f737; worker suite 3822 passed, 99.43 %)
- [ ] T002 .NET contract code (fork agent working; files uncommitted in src/StemStudio.Contracts and tests/StemStudio.Api.Tests)
- [x] T005 G2 spike: pending owner (dataset); facts in research.md
- [x] T006 WeasyPrint spike passed (research.md)
- [x] T007 sync_handout_style.py (1c04d856 red, ace546eb green)
- [ ] T008 verification + hand-off `architect-020-plan.md`

## Decisions so far (Phase 0)

- Base documents are main's 016 full documents (backend 1.15.0, worker 1.9.0, ui-test-ids 1.15.0); 017-019 are not on main and 020 needs nothing from them.
- G2 pending (no MUSDB18-HQ): US5 deferred, G2 parts not merged into 1.11.0/1.19.0.
- G1 not run: every reliability label `indicative`; levels are still shown (only a dimension that FAILED G1 masks low/medium as `unknown`).
- 016 domain (ChordSpeller, ChordSheetBuilder, KeyFormatter) is reused for chords, transposition and chords over lyrics.
- 014's setlist PDF (ReportLab, streamed) stays; the 020 setlist handout is the stored lesson PDF.
