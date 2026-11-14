# ADR-008: Hand-off granularity and `/speckit-analyze` resolutions

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: CTO (orchestrator), autonomous operating mode

## Context

`/speckit-analyze` (38 findings, 0 critical) flagged that the constitution asks for a hand-off note
per task, while `tasks.md` has 128 fine-grained tasks executed by nine parallel agents. It also
raised spec-level ambiguities only the CTO can settle.

## Decisions

1. **Hand-off granularity (K1).** A "task" in the constitution's hand-off rule means an owner's
   *checkpoint*: Foundational, US1, US2, US3, US4, Polish. Each owner writes
   `docs/handoffs/<owner>-<checkpoint>.md` with red-to-green evidence (the commit hash of the failing
   test), test counts and coverage numbers. Individual `Txxx` items are ticked in `tasks.md`.
   Rationale: one note per checkpoint keeps the evidence reviewable without 128 near-empty notes.
2. **Over-long audio (I3).** The 15-minute limit is enforced after download, so the import is
   accepted and the job fails with "Audio is longer than 15 minutes".
3. **Upload limit unit (I2).** 200 MiB (209,715,200 bytes) everywhere.
4. **Beats per bar (A2).** User-editable 1-12; reset restores detected BPM, offset 0 and detected
   beats per bar.
5. **SC-004 verification (G3).** Automated Playwright timing check (user action to engine call).
6. **Browsers (U5).** Chromium and Firefox are tested; Safari is best effort.
7. **Restart reconciliation (I6).** The worker reconciles at start-up; the backend reacts to the
   worker reporting `failed` or 404 (ADR-001 reworded).

All remaining findings are resolved by the architect in contracts, `plan.md` and `tasks.md`
(Phase 0 additions).

## Consequences

- Owners deliver up to six hand-off notes each; the CTO reviews each checkpoint before merging.
