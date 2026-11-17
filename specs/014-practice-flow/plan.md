# Implementation Plan: Practice Flow

**Branch**: `014-practice-flow` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/014-practice-flow/spec.md`; constitution 1.3.1;
`docs/MoSCoW.md` (S2, S5, S6, S7, S8; every instrument counts equally); `docs/ROADMAP.md`
(backend 1.13.0, worker 1.8.0, audio-engine 2.4.0, ui-test-ids 1.13.0; depends on 008 and 012);
features 006 (karaoke clock and frame loop), 008 (score assets, Verovio, `TempoGridResolver`),
004 (design system, help, key map, preferences), 007 (assets, lanes, read-only library mount).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions
in [ADR-023](../../docs/decisions/ADR-023-practice-flow.md); spikes in [research.md](research.md);
contract deltas in [contracts/DELTA.md](contracts/DELTA.md).

## Summary

- **Sections (S2)**: after a separation completes (or on the first mixer open of an older track)
  the API submits a `section` job to the worker's new `practice` lane. Our analyser
  (`stems-novelty`, librosa/scipy/scikit-learn already locked) computes bar-synchronous features
  from the stems, finds boundaries by Foote novelty plus vocals-stem activity switches, clusters
  repeats and labels kinds by rules (solo with its instrument). The API stores a `SectionSet` per
  track; the mixer shows a section lane; one click sets the loop (with `sectionId`), which is also
  the 012 speed-trainer region. Advanced users edit sections in an editor (whole-list PUT with a
  revision).
- **Score follow (S5)**: for every 008 score asset the API asks the worker for a **score view**
  (SVG pages with the Leipzig font embedded + a bar map from Verovio bounding boxes and the
  score's own tempo grid), shifts bar times to track seconds and serves pages as images. The
  engine (2.4.0) draws the highlight on the shared animation frame against the heard position
  (karaoke clock, works at every 012 speed) and auto-scrolls.
- **Setlists (S6)**: backend CRUD with ordered entries (track + optional mix + note), a play mode in
  the mixer (setlist bar, next/previous), and a PDF rendered by the worker with ReportLab from the
  content the API sends.
- **Shortcuts and pedals (S7)**: the 004 key map becomes data (web-interop 1.1.0) with defaults,
  user overrides in the browser preferences, a validator (reserved and conflicting chords), a
  single-key switch (WCAG 2.1.4), a pedal layer with presets and "Learn", and a live overview.
- **Practice log (S8)**: a `PracticeRecorder` in the Web app measures wall-clock playing time per
  section, loop passes (engine `OnLoopPass`) and the trainer speed reached, and upserts cumulative
  sessions every 30 s; the API stores them locally and serves summaries for the Practice panel and
  page (Radzen charts with table views). Switch off and delete at any time.

Measured (research.md): novelty segmentation on a synthetic song 6/8 boundaries within one bar,
precision 6/7, 0.55 s for 120 s; Verovio boxes for 120 bars in 0.03 s; Verovio SVG pages are
script-free.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0).

**Primary Dependencies (new)**: worker `reportlab` 5.1.x (BSD-3; runtime deps pillow and
charset-normalizer already locked) - locked in Phase 0 part 2. Worker image: Debian
`fonts-dejavu-core` (DejaVu fonts licence, permissive). Structure analysis uses the already locked
librosa 1.0.1 (ISC), scipy, scikit-learn 1.9.1 (BSD-3), numpy. Score views use the already locked
verovio 6.4.0 (LGPL-3.0, unmodified wheel). Frontend charts: Radzen Blazor (MIT, already used).
**No new .NET or npm packages.**

**Storage**: SQLite tables `TrackSections`, `Sections`, `ScoreViews`, `Setlists`,
`SetlistEntries`, `PracticeLogSettings`, `PracticeSessions`, `PracticeSectionTimes` (migration
`AddPracticeFlow`); mix loop JSON gains `sectionId`; files `assets/<id>/view/page-NNN.svg`; worker
stores `section-jobs/`, `score-view-jobs/`, `setlist-document-jobs/` (24 h). Browser
`localStorage` preferences schemaVersion 2 (key map).

**Testing**: worker analyser on synthetic structured audio generated in the tests (6 forms, one
solo instrument per family; SC-002) plus unit tests of each step (features on fixed arrays,
novelty, vocal switches, merge, clustering, label rules); score view on music21-written MusicXML
(bar count, boxes inside the page, times from grid and manual tempo, pickup, font embedded, no
script); setlist PDF with pypdf text extraction (order, notes, lyrics, page count, glyph
replacement). Backend: domain validators, summary maths (day split at local midnight), EF
persistence, worker client against the delta examples, endpoint tests, contract tests against the
delta documents. Engine: Vitest with mocked clock and DOM. Web: bUnit for lane, editor, panels,
setlists, settings; pure C# tests for `SectionNavigator`, `PracticeRecorder` (fake
`TimeProvider`), `KeyMapValidator`; web-interop Vitest for the key map, guards and capture. E2E on
the fake stack (fake structure analyser, real score views and PDFs).

**Target Platform**: unchanged; CPU only for every 014 job.

**Performance Goals**: SC-001 sections <= 30 s after separation (analysis <= 10 s for 5 min);
SC-004 follower + lane <= 2 ms per frame; SC-008 20-song PDF < 10 s; score view < 3 s for a
5-page score.

**Constraints**: no network for any 014 job; no practice data leaves the machine (SC-009); no
GPL/AGPL code; no inline SVG from the server (images only); no high-frequency interop; WCAG 2.2 AA
for new screens (axe-core in E2E).

**Scale/Scope**: up to 64 sections per track, 2,000 bars and 200 pages per score view, 100 entries
per setlist, sessions <= 12 h.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every implementation task in tasks.md is preceded by its failing test task; the analyser, score view and PDF renderer are tested on synthetic inputs generated in the tests; no real model is involved (structure analysis is DSP), so no new integration marker is needed; fakes for the worker in backend tests, fake API client in bUnit, fake clock for the recorder; E2E on the fake stack. Coverage per component reported in each hand-off. | PASS |
| II | SIG 4 stars | Worker: `analysis/structure/` split by step (features, novelty, boundaries, clusters, labels), `engraving/score_view.py`, `documents/setlist_pdf.py`; backend feature folders `Sections/`, `ScoreFollow/`, `Setlists/`, `Practice/` with pure domain classes; Web: small components per concern, navigation and recorder as pure classes; key map as data. Dependency rules added to `tools/quality` (T007). | PASS |
| III | Contracts first | Standalone OpenAPI 3.1 deltas (Redocly-valid), engine/web-interop/UI-id deltas now; full documents, `schemas.py`, `ports.py`, Contracts DTOs and `types.ts` in Phase 0 part 2 before the wave; ADR-023 for the architecture; all changes additive (minor bumps). | PASS |
| IV | Open source, lawful use | New: ReportLab (BSD) and DejaVu fonts (permissive); reused: librosa (ISC), scipy/scikit-learn/numpy (BSD), Verovio (LGPL, unmodified, separate package as in 008). AGPL PDFsharp, AGPL Essentia and unclear-weight all-in-one rejected (ADR-023). No model weights. No network calls; practice data local only. Setlist PDFs contain lyrics the user already has for personal practice (006 notice applies). | PASS |
| V | Simplicity and honesty | DSP analyser on libraries we have instead of a second ML stack; labels presented as a guess and editable; one PDF route; shortcuts reuse the 004 mechanism; the log is cumulative upserts, no event store. Limits documented: label quality, rubato songs, CJK in PDFs, notation not transposed. Audible timing untouched; the follower and loop-pass detection use the audio clock (no timers for timing). | PASS |
| - | Tech & security | Stack unchanged. Inputs validated (sections, setlists, sessions, key chords, query ranges); worker paths via the allow-list + path guard; SVG served only as `image/svg+xml` with CSP `default-src 'none'` and `nosniff`, rendered as `<img>`; PDF file names sanitised; no subprocess or shell; no secrets. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths (ownership map); hand-off per checkpoint (ADR-008); manuals updated by the CTO (constitution 1.3.1). | PASS |

**Post-design re-check (after Phase 1)**: PASS. The only cross-feature risk is the dependency on
012's unmerged contracts, handled by the delta-first Phase 0 split (Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/014-practice-flow/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                        # summary, status, assumed 012 surface, reconciliation
    ├── practice-api.openapi.yaml       # backend 1.13.0-delta.1 (now)
    ├── worker-practice.openapi.yaml    # worker 1.8.0-delta.1 (now)
    ├── audio-engine.delta.md           # engine 2.4.0 (now)
    ├── web-interop.delta.md            # web-interop 1.1.0 (now)
    ├── ui-test-ids.delta.md            # ui-test-ids 1.13.0 names (now)
    ├── redocly.yaml
    └── (Phase 0 part 2) backend-api.openapi.yaml 1.13.0, worker-api.openapi.yaml 1.8.0,
        audio-engine.md 2.4.0, web-interop.md 1.1.0, ui-test-ids.md 1.13.0
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py, schemas.py, config.py          # + StructureAnalyzer, ScoreViewRenderer, SetlistDocumentRenderer, section/score-view/setlist schemas, Health.lanes.practice, Health.structure, allow-list (architect)
├── analysis/structure/                      # beat-analysis
│   ├── features.py     # stems -> bar-synchronous chroma, MFCC, RMS, vocal activity, stem shares
│   ├── novelty.py      # self-similarity, checkerboard novelty, peak picking
│   ├── boundaries.py   # union with vocal switches, merge short segments, snap
│   ├── clusters.py     # agglomerative repetition letters
│   ├── labels.py       # kind rules, numbering, solo instrument (data-model 2.3)
│   ├── analyzer.py     # StemsNoveltyAnalyzer (StructureAnalyzer)
│   └── fake.py         # FakeStructureAnalyzer (data-model 2.4)
├── engraving/score_view.py                  # audio-ingest: Verovio passes, bar boxes, bar times, font embedding
├── documents/setlist_pdf.py, documents/fonts.py   # audio-ingest: ReportLab layout, DejaVu check
├── jobs/practice_{lane,store,service}.py    # stem-separation: lane, stores for 3 job kinds, services
├── api/practice_routes.py, main.py          # stem-separation: routes, composition, health
src/StemStudio.Contracts/{Sections,ScoreFollow,Setlists,Practice}/*.cs, LoopRegionDto.SectionId, ApiRoutes.*, ContractLimits.*   (architect)
src/StemStudio.Domain/
├── Sections/{SectionRules,SectionSetUpdate,SectionSnapper}.cs
├── Setlists/{SetlistRules,SetlistEntriesUpdate}.cs
└── Practice/{PracticeSessionRules,PracticeSummaryCalculator,LocalDay}.cs          (backend)
src/StemStudio.Persistence (entities, configurations, migration AddPracticeFlow)   (backend)
src/StemStudio.Api/
├── Sections/{SectionEndpoints,SectionService,SectionJobApplier,SectionDetectTrigger}.cs
├── ScoreFollow/{ScoreFollowEndpoints,ScoreViewService,ScoreViewJobApplier}.cs
├── Setlists/{SetlistEndpoints,SetlistService,SetlistPdfService,SetlistContentBuilder}.cs
├── Practice/{PracticeEndpoints,PracticeService}.cs
└── Worker/{WorkerClient.Practice,WorkerSectionJob,WorkerScoreViewJob,WorkerSetlistDocumentJob}.cs  (backend)
src/audio-engine/src/types.ts (architect) + score-follow/{follower,cursor,dom,scroll}.ts, loop-pass.ts (audio-engine)
src/web-interop/src/{shortcuts,keymap,capture,preferences}.ts                      (frontend)
src/StemStudio.Web/
├── Practice/{SectionNavigator,PracticeRecorder,KeyMapValidator,KeyMapDefaults,PedalPresets}.cs
├── Components/Practice/{SectionLane,SectionEditor,ScorePanel,PracticePanel,PracticeCharts,SetlistBar}.razor
├── Components/Pages/{Setlists,SetlistEditor,PracticePage,ShortcutSettings}.razor
├── Components/Shell/ShortcutOverlay.razor (extended), Preferences (single-key switch)
└── Help/topics/**/*.md, Help/tours/{practice-section,hands-free}.json              (frontend)
e2e/tests/{sections,score-follow,setlists,shortcuts-pedals,practice-log}.spec.ts, fixtures (qa)
docker/worker.Dockerfile (fonts-dejavu-core), docker-compose.yml (STEMSTUDIO_STRUCTURE_ANALYZER), CI contract dirs (devops)
tools/quality (rules for analysis/structure, documents, engraving/score_view; TS score-follow) (quality-gate)
```

**Dependency direction**: worker `api -> jobs -> ports <- {analysis.structure, engraving,
documents}`; `analysis/structure` imports only `ports`, `schemas`, numpy/scipy/sklearn/librosa/
soundfile; `engraving/score_view` only `ports`, `schemas`, verovio; `documents` only `ports`,
`schemas`, reportlab; composition in `main.py`. .NET: `Api.{Sections,ScoreFollow,Setlists,Practice}
-> Domain.*`, `Api.ScoreFollow -> Api.Scores` (008 grid resolver) and `Api.Assets`; `Api.Setlists ->
Api.Lyrics` (plain lines) and `Api.Sections`; Web: `Components -> Practice (pure) -> Api client`.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/014-*/**`, ADR-023, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `StemStudio.Contracts/**`, `src/audio-engine/src/types.ts`, project files | architect |
| `worker/src/stemstudio_worker/analysis/structure/**`, `worker/tests/analysis/structure/**` | beat-analysis |
| `worker/src/stemstudio_worker/{engraving/score_view.py,documents/**}`, their tests | audio-ingest |
| `worker/src/stemstudio_worker/{jobs,api}/**` (practice parts), `main.py`, their tests | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/audio-engine/**` (except `types.ts`) | audio-engine |
| `src/StemStudio.Web/**`, `src/web-interop/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/public/src/**`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, `docs/ROADMAP.md` | CTO |

## Key flows

1. **Detect**: separation sync completes -> `SectionDetectTrigger` -> `TrackSections queued` ->
   worker `POST /v1/section-jobs` (stem paths from the library layout, kept detail stems, grid)
   -> `SectionJobApplier` polls -> sections stored (`Revision + 1`, `ready`). Mixer opens with
   status `none` -> UI calls `detectSections`.
2. **Loop a section**: click `section-<id>` -> `MixerState.LoopSection(section)` ->
   `engine.setLoop({start, end, enabled: true})`, `loop.sectionId = id` (saved with the mix) ->
   012 trainer reads the loop region.
3. **Edit**: editor works on a copy (`SectionSetUpdate`, snapping by `SectionSnapper`) -> Done ->
   `PUT sections {revision}` -> 200 or 409 (reload prompt).
4. **Score view**: 008 asset sync marks score `ready` -> `ScoreViewService.Queue(asset)` -> worker
   `score-view-jobs` (MusicXML path + grid from `TempoGridResolver` + duration) -> applier copies
   pages to `assets/<id>/view/`, stores bars shifted by `OffsetSeconds`. Score panel: `listTrackScores`
   -> choose -> `getScoreView` (POST when `none`) -> render pages as `<img>` in `score-page-<n>`
   -> `engine.attachScoreFollower('score-view', view, {follow})`.
5. **Setlist play**: `/setlists/{id}` -> Play -> navigate to the first entry's mixer with
   `?setlist=<id>&entry=<entryId>` -> `SetlistBar` (prev/next via `setlist.next`/`setlist.previous`
   commands) -> mixer unsaved-changes guard -> next entry's mixer (mix loaded when set).
6. **Setlist PDF**: `GET /api/setlists/{id}/pdf?...` -> `SetlistContentBuilder` (tracks, mixes,
   sections, lyrics lines cut to 10,000 characters) -> worker `setlist-document-jobs` -> poll
   every 200 ms up to the timeout -> stream PDF -> delete the worker job.
7. **Shortcuts**: layout loads preferences -> `KeyMapDefaults` + overrides -> `KeyMapValidator`
   -> `registerShortcuts(dotnet, scope, keyMap)`; settings page edits overrides (capture via
   `captureChord`), saves preferences, `updateShortcuts`.
8. **Practice**: mixer `PracticeRecorder` subscribes to state/position/loop-pass events ->
   cumulative session -> `PUT /api/practice/sessions/{id}` every 30 s and at stop/leave -> Practice
   panel `getPracticeSummary(trackId, days, utcOffsetMinutes)`; Practice page `getPracticeOverview`.

## Phases

- **Phase 0 part 1 (done in this planning round)**: spec, research with spikes, ADR-023,
  data-model, standalone contract deltas (Redocly-valid), quickstart, tasks.
- **Phase 0 part 2 (architect, after 012 and 013 are merged into main)**: reconcile with 012
  (DELTA R-1..R-5), write the full contract documents, worker `schemas.py`/`ports.py`/`config.py`,
  ReportLab lock, Contracts DTOs, `types.ts` 2.4.0, the segmentation threshold spike on the
  6-form synthetic set (T010), the score-view font check in Chromium/Firefox (T011); skeleton green.
- **Wave (parallel owners)**: tasks.md Phases 1-8.
- **Integration (CTO + qa)**: merge, E2E on the fake stack, a11y and visual suites, manuals and PDFs,
  STATUS, THIRD-PARTY-LICENSES.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Delta-first contracts with a Phase 0 part 2 | 012/013 contracts are not merged; full documents now would collide | Writing full documents on 011's base would need re-basing by hand and risks silent divergence (011 used the same split) |
| A fourth worker lane `practice` | Section, score-view and setlist jobs are seconds long and user-facing; they must not wait behind piano transcription (minutes) or delay exports | Sharing `render` would put up to 10 s of analysis in front of a 15 s export target; sharing `transcribe` would make sections wait for minutes |
| Own DSP structure analyser instead of a model | No OSI, CPU-friendly model with clear weights licence and simple deps; we already have stems, which give the strongest cue (vocals on/off, solo instrument) | all-in-one needs NATTEN/madmom and a second Demucs; MSAF lacks functional labels |
| Server-made score view + image pages + engine highlight | Safe (no inline SVG), cheap per frame, same clock as karaoke, works for the agent too | Inline SVG + sanitiser + `getBBox` adds an injection surface and layout reads; Verovio timemaps ignore the beat grid |
| PDF in the worker (ReportLab) though the data is in the API | No maintained permissive .NET PDF library (PDFsharp 6.5 AGPL, QuestPDF non-OSI) | Writing a layout engine on PdfPig; browser print does not serve the agent |
| Key map stored in the browser, practice log in the API | Key map is per device like theme (004); the log must survive browser resets, be deletable as a whole and be refused server-side when off | Storing both in one place would either lose the log on cache clears or put per-device pedal setups in backups |
