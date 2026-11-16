# Implementation Plan: Arrangement Editor (practice versions)

**Branch**: `019-arrangement-editor` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/019-arrangement-editor/spec.md`; constitution 1.3.1;
`docs/MoSCoW.md` C6; `docs/ROADMAP.md` (019 after 007 and 014; built in parallel with 017 and
018; contracts backend 1.18.0, worker 1.10.0, engine 2.8.0, UI test ids 1.18.0, render-vectors
**1.2.0** per the CTO allocation change of 2026-11-16).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding decisions:
`docs/decisions/ADR-028-arrangement-timeline-model.md`; alternatives: [research.md](research.md);
law and entities: [data-model.md](data-model.md); contract deltas: [contracts/DELTA.md](contracts/DELTA.md).

## Summary

An **arrangement** is a named, non-destructive edit list of a track: 1-64 source regions,
usually snapped to bar lines from the beat grid and the 014 sections, each repeated 1-16 times
with per-region channel mutes, plus a crossfade length. The API stores it (`Arrangements` table,
optional `Mix.arrangementId`) and resolves it with one C# `TimelineResolver` into a flat list of
frame-exact segments. The browser engine plays the resolved timeline gaplessly and sample-locked
by scheduling one buffer source per channel **run** (contiguous open segments) with half-overlap
equal-power crossfades on the existing look-ahead scheduler; its position clock stays in source
time so lyrics (006), score follow and sections (014) and the metronome (012) follow what is
heard without changes on their side. Export reuses 007: the worker renders arranged frames with
the same run law from a flat `timeline` in the render request. A shared generator pins the law in
`render-vectors.json` 1.2.0 for the C# resolver, the TS engine, the Python renderer and a browser
check. The editor (Advanced level) lives in the mixer as an "Arrange" panel with a song strip, a
region lane (listbox), a region inspector, undo/redo (100 steps), full keyboard operation and
in-app help.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0).

**Primary Dependencies (new)**: **none** (no NuGet, npm or Python package). Web Audio
`AudioBufferSourceNode`, `GainNode.setValueCurveAtTime`, numpy/soundfile already present.

**Storage**: SQLite table `Arrangements` and column `Mixes.ArrangementId` (migration
`AddArrangements`); asset recipes carry a timeline snapshot (JSON, no schema change).

**Testing**: backend xUnit (resolver against `timeline.resolve`, export mapper against
`timeline.spans`, endpoints with stub worker, persistence on file SQLite), bUnit for the editor
(commands, undo/redo, keyboard, levels, guards), engine Vitest with mocked `AudioContext` (runs,
curves, clock, metronome, scheduling), worker pytest (timeline reader on generated FLAC fixtures,
`timeline.samples` exact), Playwright on the fake stack (`@arrangement`) plus a stack-free
`timeline-law.spec.ts` in Chromium.

**Target Platform**: unchanged; CPU only; no GPU or model.

**Performance Goals**: SC-005 edit-to-ear <= 200 ms; SC-006 10-minute arranged export of 6 stems
<= 30 s; SC-003 no gaps, channels locked within 1 frame.

**Constraints**: song order stays byte-for-byte the 2.7.0 engine path and the 007 render path;
arrangements never modify stems; limits of data-model 8; Web Audio timing only from
`AudioContext.currentTime` (constitution V).

**Scale/Scope**: one user; <= 50 arrangements per track; documents of a few KB.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every owner task pair is red -> green (tasks.md). The law is a pure function in three languages, tested against generated vectors; the editor is tested with bUnit; E2E on the fake stack and a stack-free browser law test. No model involved, so no new integration test. | PASS |
| II | SIG 4 stars | Small units by construction: Domain `Arrangements/` (document, validator, bar lines, resolver, export mapper), API `Arrangements/` endpoints/service mirroring `Mixes/`, engine `timeline/` module (plan validation, runs, curves, scheduler, clock, metronome map), worker `render/timeline.py`, Web `Arrange/` components + state. quality-gate adds dependency rules (T007-T008). | PASS |
| III | Contracts first | Standalone validated OpenAPI 3.1 deltas (worker 1.10.0, backend 1.18.0), engine 2.8.0 delta, UI ids 1.18.0, vectors 1.2.0 with generator, ADR-028. Phase 0 merges them and writes `schemas.py`, Contracts DTOs and `types.ts` stubs before the wave. | PASS |
| IV | Open source, lawful use | No new dependency. Arranged versions are personal practice copies (existing notice). | PASS |
| V | Simplicity and honesty | One edit-list model reused for playback and export; no per-region gain/stretch, no unrolled score, no recording while arranged (documented). Limits and grid quality are shown to the user. Audible timing only from `currentTime` (scheduler wakes only). | PASS |
| - | Tech & security | Input validation of every document (limits, channel ids, bar lines, track ownership of `arrangementId`), closed schemas, worker index checks; no new file paths (the worker still gets API-chosen allow-listed inputs). | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; checkpoint hand-offs (ADR-008); manuals updated by the CTO (constitution 1.3.1). | PASS |

**Post-design re-check (after Phase 1)**: PASS; see Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/019-arrangement-editor/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                            # summary + merge status
    ├── worker-api.delta.openapi.yaml       # 1.10.0-delta.1 (Redocly-valid)
    ├── backend-api.delta.openapi.yaml      # 1.18.0-delta.1 (Redocly-valid)
    ├── audio-engine.md                     # 2.8.0 delta
    ├── ui-test-ids.delta.md                # 1.18.0 delta
    ├── render-vectors.json                 # 1.2.0 (generated)
    ├── generate_render_vectors.py          # generator (--check)
    └── redocly.yaml
docs/decisions/ADR-028-arrangement-timeline-model.md
```

Phase 0 adds the full `worker-api.openapi.yaml` 1.10.0, `backend-api.openapi.yaml` 1.18.0,
`ui-test-ids.md` 1.18.0 and the full `audio-engine.md` 2.8.0 to `contracts/`.

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── schemas.py          # + RenderTimeline, RenderSegment, RenderJobRequest.timeline, 1.10.0 (architect)
└── render/
    ├── timeline.py     # runs + gains (data-model 3.3/3.4), TimelineInputs reader over arranged frames (audio-ingest)
    └── executor.py     # uses TimelineInputs when request.timeline is set (audio-ingest)
worker/src/stemstudio_worker/api/render_routes.py   # 422 mapping of timeline validators (stem-separation)

src/StemStudio.Contracts/Arrangements/*.cs, Mixes (+ArrangementId), Assets (+Arrangement),
    ApiRoutes.Arrangements*, ContractLimits                                    (architect)
src/StemStudio.Domain/Arrangements/{ArrangementDocument,Region,SnapMode,BarLines,
    ArrangementValidator,TimelineResolver,ResolvedTimeline,TimelineSegment,
    TimelineRuns,TimelineExportMapper,Arrangement,ArrangementNaming}.cs       (backend)
src/StemStudio.Persistence/ (ArrangementConfiguration, Mix FK, migration AddArrangements)
src/StemStudio.Api/Arrangements/{ArrangementEndpoints,ArrangementService,SectionSeed}.cs
src/StemStudio.Api/Assets/ExportService (+ arrangement), Worker/WorkerRenderRequest (+timeline) (backend)

src/audio-engine/src/timeline/
├── plan.ts             # validation of TimelinePlan, running arranged starts
├── runs.ts             # per-channel runs (data-model 3.3)
├── curves.ts           # fade curves (3.4), cached per F
├── clock.ts            # arranged <-> source (3.5), seek rule (3.7)
├── scheduler.ts        # look-ahead run scheduling on the transport anchor
└── metronome-map.ts    # arranged clicks (3.6)
src/audio-engine/src/{transport.ts, engine.ts, metronome/timeline.ts, types.ts}  (audio-engine; types.ts stub by architect)

src/StemStudio.Web/Arrange/{ArrangeEditorState.cs, ArrangeCommands.cs, UndoHistory.cs,
    ArrangeKeyMap.cs, BarRange.cs, TimelineFormat.cs}
src/StemStudio.Web/Components/Arrange/{ArrangePanel.razor, ArrangeToolbar.razor, SongStrip.razor,
    RegionLane.razor, RegionInspector.razor, ArrangeChip.razor, ArrangeGuardDialog.razor}
    + mixer tab, export dialog option, mixes badge, record-disabled reason, help topics  (frontend)

e2e/tests/{arrangement.spec.ts, arrangement-keyboard.spec.ts, arrangement-levels.spec.ts,
    arrangement-export.spec.ts, timeline-law.spec.ts}, e2e/pages/arrange.page.ts        (qa)
CI CONTRACTS_DIR, scripts/test-all.*, fake stack sections/grid fixture check            (devops)
tools/quality (dependency rules)                                                         (quality-gate)
```

**Dependency direction**: worker `render/timeline.py` imports only `schemas` and `render.mixing`
types; `jobs`/`api` never import `render`. .NET: `Api.Arrangements -> Domain.Arrangements ->
(nothing in Api/Persistence)`; `Api.Assets.ExportService -> Domain.Arrangements.TimelineExportMapper`;
Web -> API client only (the editor never references Domain). Engine: `timeline/*` imports only
`types.ts`, `loop.ts` helpers and the scheduler interface; `transport.ts` composes it.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/019-*/**`, ADR-028, `schemas.py`, `StemStudio.Contracts/**`, `src/audio-engine/src/types.ts` (stub), project files | architect |
| `worker/src/stemstudio_worker/render/**`, `worker/tests/render/**` | audio-ingest |
| `worker/src/stemstudio_worker/api/**` (render routes), `worker/tests/api/**` | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**` (incl. help topics), `tests/StemStudio.Web.Tests/**` | frontend |
| `src/audio-engine/**` (except `types.ts` stub), its tests | audio-engine |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `docs/public/src/**` (manuals), `README.md`, `docs/STATUS.md` | CTO |

beat-analysis has no work in this feature (the 001 grid is reused).

## Key flows

1. **Open Arrange** (Advanced): `GET /api/tracks/{id}/arrangements` -> select -> `GET
   /api/arrangements/{id}` (document + timeline) -> `engine.setTimeline(plan)`; "Song order" ->
   `setTimeline(null)`.
2. **New from sections**: `POST .../arrangements { seed: sections }` -> 014 sections -> regions
   snapped to bar lines -> `201` (saved immediately with a default name, revision 1) -> step 1.
3. **Edit**: command -> local limit check -> undo snapshot -> debounce 150 ms -> `POST
   .../arrangements/resolve` -> `setTimeline(plan)` (live, playhead kept) -> lane/inspector update.
4. **Save**: `PUT /api/arrangements/{id} { name, revision, document }` -> `200` (revision + 1) or
   `409 arrangement-conflict` -> reload / save as copy (`POST ... { document }`).
5. **Save mix**: existing mix PUT with `arrangementId` (must be saved; unsaved edits prompt "save
   arrangement first").
6. **Playback**: transport anchor -> scheduler tick -> runs starting in the next 2 s -> sources +
   fade curves; `OnPositionChanged(source)` drives score/sections; `OnTimelinePositionChanged`
   drives the lane playhead; karaoke reads source time; metronome maps source beats.
7. **Export**: export dialog "Use arrangement" -> `POST /api/assets/audio-exports { ...,
   arrangementId | arrangement }` -> `RenderPlanner` (007) + `TimelineExportMapper` -> worker
   `timeline` -> arranged render -> library item with snapshot recipe.

## FR -> story -> owners

| FR | Story | Owners |
|---|---|---|
| FR-001, FR-016 | US4 (CRUD), US1 (create) | backend, frontend |
| FR-002, FR-003, FR-012 | US1, US2 | backend (resolver), frontend (commands) |
| FR-004 | US3 | backend, audio-engine, audio-ingest, frontend |
| FR-005, FR-006, FR-008 | US1, US2 | audio-engine, frontend |
| FR-007, FR-015 | US1, US3 | audio-engine, frontend |
| FR-009, FR-010 | US2, US4 | frontend, backend |
| FR-011 | US5 | backend, audio-ingest, stem-separation, frontend |
| FR-013 | US6 | frontend, qa |
| FR-014 | US7 | frontend |
| FR-017 | US4 (stale) | backend, frontend |

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Run law implemented three times (C# mapper subset, TS engine, Python renderer) | Playback in the browser, export without a browser (agent, retry) | Browser rendering cannot serve the agent (ADR-016 D2); drift is prevented by generated vectors consumed by all suites and a browser check |
| Second scheduling mode in the engine transport | Arranged playback needs per-run sources | Pre-rendered arranged buffers need up to ~14 GB (research R-04); song order stays the unchanged path |
| Stateless `resolve` endpoint used on every edit | One resolver for UI, save, mix load and export; Web must not reference Domain | A TS resolver would be a fourth implementation; a localhost round trip fits SC-005 |
| Two clocks (source and arranged) in the engine | Existing followers (006, 014, 012) are keyed to source time | Switching them to arranged time would change three features and their contracts |
