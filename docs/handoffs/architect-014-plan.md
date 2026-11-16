# Hand-off: architect / 014-plan (Phase 0 part 1)

## What was built

Complete Spec Kit design for feature 014 Practice flow (MoSCoW S2, S5, S6, S7, S8), branch
`014-practice-flow` from `5ae61cf`: spec with clarifications, research with two local spikes,
ADR-023 (Proposed), data model, plan with constitution check, standalone contract deltas,
quickstart, TDD task list per lane. No production code: Phase 0 part 2 (schemas, ports, DTOs,
`types.ts`, full contract documents) waits for 012 and 013 to merge, because their contract
minors (backend 1.11.0/1.12.0, worker 1.7.0, engine 2.3.0) are the base of 014's.

Key decisions (ADR-023): own DSP structure analyser on the stems (Foote novelty + vocals-stem
switches + rule-based kinds, solo names its instrument; no new dependency); sections per track
with whole-list PUT and revision, `LoopRegion.sectionId`; score view made by the worker (Verovio
SVG pages with embedded Leipzig + bar map from bounding boxes and the score's own grid) shown as
`<img>`, highlight drawn by the engine on the karaoke clock; setlist PDFs with ReportLab in the
worker (PDFsharp 6.5 is AGPL); data-driven key map (web-interop 1.1.0) in browser preferences,
single-key switch, pedal layer with presets and Learn; practice log in SQLite, on by default,
local only, off/delete; new worker lane `practice`; engine `OnLoopPass`.

Cross-feature (CTO messages during the task): 014 owns the complete shortcut catalogue with an
owner column (004, 012, 014, 015); 015's `R`/`C` are reserved; 012 keys `-` `=` `,` `.` `0` `T`.
Count-in vs. the shipped 004 metronome `K`: decided by the CTO (2026-11-16) - metronome keeps
`K`, count-in is `Shift+K` (DELTA R-2). web-interop 1.1.0 is allocated to 014 in the roadmap.

## Files changed

- `specs/014-practice-flow/{spec.md, research.md, plan.md, data-model.md, quickstart.md, tasks.md}`
- `specs/014-practice-flow/checklists/requirements.md`
- `specs/014-practice-flow/contracts/{DELTA.md, practice-api.openapi.yaml, worker-practice.openapi.yaml, audio-engine.delta.md, web-interop.delta.md, ui-test-ids.delta.md, redocly.yaml}`
- `docs/decisions/ADR-023-practice-flow.md`
- `docs/handoffs/architect-014-plan.md`

## Tests and coverage

No code changed, so no test or coverage figures apply to this round. Contract validation:
`npx @redocly/cli@2.78.0 lint --config specs/014-practice-flow/contracts/redocly.yaml
practice-api.openapi.yaml worker-practice.openapi.yaml` -> "Your API descriptions are valid",
3 warnings (2x `operation-4xx-response` on `listSetlists`/`getPracticeSettings`, which have no
client errors; 1x `no-unused-components` for the re-declared `LoopRegion`).

Spikes (throw-away, scratchpad, main checkout's worker venv): structure S-01 (novelty: 6/8
boundaries within one bar, precision 6/7, 0.55 s for 120 s; Laplacian over-segments); Verovio S-02
(bar boxes from staff bounding boxes, 120 bars in 0.03 s, SVG script-free, Leipzig used for tempo
text).

## Quality checks

Not applicable (documents only). SIG limits are addressed in plan.md (component split) and T015.

## Dependencies added

None in this round. Planned (Phase 0 part 2 / devops):

| name | version | licence | url |
|---|---|---|---|
| reportlab | 5.1.x | BSD (ReportLab open-source licence) | https://pypi.org/project/reportlab/ |
| fonts-dejavu-core (Debian package) | distro | DejaVu fonts licence (Bitstream Vera derived, permissive) | https://dejavu-fonts.github.io/ |

## Docs impact

User manual: new chapters for sections, score follow, setlists, keyboard and pedals, practice log
and privacy. Technical manual: `practice` worker lane, `STEMSTUDIO_STRUCTURE_ANALYZER`, worker
fonts, new API endpoints. ROADMAP: web-interop 1.1.0 for 014 (recorded by the CTO).

## Open issues / requests

1. CTO: ADR number 023 is provisional (012/013/015/016 ADRs are written in parallel).
2. 012 author: confirm A-012-1..A-012-5 (track-second position at every speed, speed/transpose in
   `MixerState`, trainer on the loop region, no loop-pass callback).

## Next owner

architect (Phase 0 part 2, T002-T011) once 012 and 013 are merged; then the wave per tasks.md.
