# Implementation Plan: Insights and Handouts

**Branch**: `020-insights-handouts` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/020-insights-handouts/spec.md`; constitution 1.3.1;
`docs/MoSCoW.md` (C7, C8; every instrument counts equally); `docs/ROADMAP.md` (allocation backend
1.19.0, worker 1.11.0, ui-test-ids 1.19.0; depends on 014 and 016); specs 002 (detail metrics),
003 (device policy), 006 (lyrics), 007 (library, lanes, mount), 008 (score PDFs); `tools/docs`
(manual PDF style).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding decisions:
[ADR-029](../../docs/decisions/ADR-029-stem-quality-report-and-handouts.md). Research and the
Phase 0 measurement plan: [research.md](research.md). Contracts: [DELTA.md](DELTA.md) and
[contracts/](contracts/). **Phase 0 is not executed yet**: it needs 014 and 016 merged (their
contract minors are the base of 020's) and runs the G1/G2 measurements.

## Summary

**Quality report (C7)**: after each separation (and on request) the API submits a quality job
with the track's stems to the worker's new `insights` lane. The worker computes reference-free
measures (leakage share with a co-occurrence guard, flicker and dropout ratios relative to the
mix, activity) and returns levels and a rating per stem with reliability labels from the Phase 0
validation. The API stores the report on the track and derives up to two tips per stem from
pure rules that know the track state; tip buttons start existing actions (detail split, open
detail stem, mute) or, if G2 passes, the new high-quality vocal re-separation.

**Handouts (C8)**: the user picks a family from the "Lesson handout" menu (song or setlist). The
API resolves everything (facts, sections with bars, chords transposed and placed over lyric
words, notation items, notes, notice, attributions) into a `HandoutDocument`, creates a library
asset of kind `handout` and submits a handout job. The worker lays the document out with Jinja2
templates and WeasyPrint in the app's style, merges the notation PDF pages into frame pages with
pypdf and returns one PDF. The 007 asset sync copies it into the library.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript untouched).

**Primary Dependencies (new, worker, locked in `uv.lock` in Phase 0)**: `weasyprint` 70.0
(BSD-3-Clause) with `pydyf` (BSD-3), `tinyhtml5` (MIT), `pyphen` (MPL-1.1 option of its
tri-licence), `fonttools[woff]` (MIT; `brotli` MIT already locked, `zopfli` Apache-2.0); already
locked and now direct: `jinja2` 3.1.6 (BSD-3), `markupsafe` 3.0.4 (BSD-3), `pypdf` 6.21.4,
`tinycss2`, `cssselect2`, `pillow`, `scipy`, `soxr`, `librosa`. System packages (both images):
Pango >= 1.44 (LGPL-2.1+), HarfBuzz (MIT), fontconfig, `fonts-dejavu-core` (Bitstream Vera
licence, free). Fonts: Archivo (OFL-1.1, copied from the app). G2 only: one RoFormer vocal
checkpoint (licence status per ADR-009, downloaded at first use). Research-only, never locked:
`museval` 0.4.1 (MIT), MUSDB18-HQ (local). No new .NET or npm packages; e2e may add `pdf-lib`
1.17.1 (MIT, already used by `tools/docs`) for page counts.

**Storage**: `Tracks.QualityReport` JSON (migration `AddQualityReport`), `PracticeNotes` table
(`AddPracticeNotes`), G2: `Tracks.StemSet`, `Tracks.Reseparation` (`AddStemSet`); asset files
`assets/<id>/handout.pdf`; worker `quality-jobs/`, `handout-jobs/`, G2 `reseparation-jobs/` and
`cache/by-audio/<sha>/vocals-hq/`.

**Testing**: measures tested on generated signals (exact bleed/masking injections, V1 subset in
unit tests, full V1 as a normal test), renderer tested by rendering small documents and
inspecting the PDF with pypdf (text, outline, page count, footer on every page, fonts without
`.notdef`), url_fetcher tested against path traversal and remote URLs; backend domain rules
(tips, chord placement, transposition, bars, assembly) as pure unit tests; WorkerClient against
examples; bUnit for panels/dialogs; Playwright on the fake stack with per-family fixtures. Real
RoFormer model only in one `@pytest.mark.integration` test (G2).

**Target Platform**: unchanged; everything works on the CPU; G2 pipeline uses the GPU when
available.

**Performance Goals**: SC-001 report <= 60 s after separation (analysis <= 30 s for 5 min, 18
stems); SC-004 song handout <= 15 s, 20-song setlist <= 90 s; exports keep their 15 s target.

**Constraints**: no network in the renderer; allow-listed library reads only; autoescaped
templates; no literal colours in handout CSS; ASCII chord accidentals; fixed notice texts.

**Scale/Scope**: up to 64 stems per report; handouts up to 50 songs and 8 notation items per
song.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every implementation task is preceded by its failing test (tasks.md). Measures and rules are pure functions on generated data; the renderer is tested on real small PDFs; fakes for analyzer, renderer and re-separator in E2E; one integration test for the real RoFormer model (G2). | PASS |
| II | SIG 4 stars | New worker packages `insights/` and `handouts/` with one-way imports (only `ports`, `schemas`, numpy/scipy/soxr resp. jinja2/weasyprint/pypdf); backend feature folders `Insights/`, `Handouts/` with pure domain parts (`TipRules`, `ChordPlacer`, `Transposer`, `BarMapper`, `IngredientPlanner`) and thin services; source ports per 014/016 dependency (one adapter each). | PASS |
| III | Contracts first | Standalone, additive, Redocly-valid deltas for worker 1.11.0 and backend 1.19.0, UI ids 1.19.0, help contract; Phase 0 merges them into full documents and writes `schemas.py`, `ports.py`, Contracts DTOs before the wave; ADR-029. | PASS |
| IV | Open source, lawful use | WeasyPrint BSD-3, Jinja2 BSD-3, Pyphen used under MPL-1.1, Pango LGPL as an unmodified system library (like libsndfile/Cairo); fonts OFL/free; G2 weights under the ADR-009 exception with SHA-256 and first-use download. Handouts carry a fixed personal-use notice and attributions on every PDF; MUSDB18-HQ only local for calibration, never committed. | PASS |
| V | Simplicity and honesty | Estimates labelled as such, validated labels only where measured (G1); the false GPU tip is excluded; the only better-model action is gated (G2); missing ingredients are listed, not faked; PDF accessibility limits documented. Reused building blocks: 007 library and sync, 008 PDFs, 002 metrics, 003 device policy, `tools/docs` style. | PASS |
| - | Tech & security | Stack unchanged (ADR-029 D7 records the new renderer). Inputs validated (families/ingredients/transpositions, note length and control characters, setlist size); renderer url_fetcher blocks network and paths outside the allow-list; Jinja2 autoescape; subprocess-free. No secrets. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-off per checkpoint (ADR-008); manuals updated by the CTO before the integration wave closes (constitution 1.3.1). | PASS |

**Post-design re-check (after Phase 1)**: PASS. The two new system libraries (Pango, HarfBuzz)
and the gated model are recorded in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/020-insights-handouts/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md, DELTA.md
├── checklists/requirements.md
└── contracts/
    ├── worker-api.delta.openapi.yaml    # 1.11.0-delta.1 (planning)
    ├── backend-api.delta.openapi.yaml   # 1.19.0-delta.1 (planning)
    ├── ui-test-ids.delta.md             # -> 1.19.0
    ├── help-topics.md
    ├── redocly.yaml
    └── (Phase 0) worker-api.openapi.yaml 1.11.0, backend-api.openapi.yaml 1.19.0, ui-test-ids.md 1.19.0
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py      # + QualityAnalyzer, StemAudio, QualityMeasures, HandoutRenderer, RenderedHandout, NotationReader, (G2) Reseparator; errors HandoutRenderError, QualityAnalysisError (architect)
├── schemas.py    # + 1.11.0 models (architect)
├── config.py     # + quality_analyzer, insights_threads, (G2) reseparator (architect)
├── insights/     # beat-analysis: representation, bleed, artefacts, detail, rating, thresholds, analyzer, fake
├── handouts/     # audio-ingest: document, templates/, styles/, assets/, fetcher, layout, notation, renderer
├── separation/hq/ (G2) # stem-separation: roformer_vocals, pipeline, fake
├── jobs/{quality,handout,reseparation}_{store,service}.py   # stem-separation
├── api/{insights,handout,reseparation}_routes.py, main.py   # stem-separation
└── (worker/scripts/sync_handout_style.py, architect, Phase 0)

src/StemStudio.Contracts/{Insights,Handouts,Separation}/*.cs, additions to AssetDto/ProvenanceDto/TrackDto/ApiRoutes/ContractLimits  (architect)
src/StemStudio.Domain/Insights/{TipRules,QualityRatingRules,StemsVersion}.cs
src/StemStudio.Domain/Handouts/{Families,IngredientPlanner,ChordSymbol,Transposer,ChordPlacer,BarMapper,HandoutNotice,HandoutNaming}.cs
src/StemStudio.Persistence (QualityReport column, PracticeNotes table, G2 columns, migrations)
src/StemStudio.Api/Insights/{QualityEndpoints,QualityService,QualityJobApplier,QualityTrigger}.cs
src/StemStudio.Api/Handouts/{HandoutEndpoints,HandoutService,HandoutAssembler,HandoutOptionsService,PracticeNoteEndpoints,Sources/*Adapter.cs}.cs
src/StemStudio.Api/Separation/{ReseparationEndpoints,ReseparationService,StemSetReplacer}.cs (G2)
src/StemStudio.Api/Worker/{WorkerClient.Insights,WorkerClient.Handouts,WorkerClient.Reseparation}.cs
src/StemStudio.Web/Components/Insights/{QualityPanel,QualityRow,QualityBadge,TipList,ReseparateDialog}.razor
src/StemStudio.Web/Components/Handouts/{HandoutMenu,HandoutDialog,PracticeNotesEditor,HandoutAssetRow}.razor + help topics
e2e/tests/{stem-quality,handouts,setlist-handout,reseparation}.spec.ts, tests/fixtures/families/** (qa)
docker/worker.Dockerfile (Pango, HarfBuzz, fontconfig, DejaVu), docker-compose.yml env (devops)
tools/quality (rules + style drift check) (quality-gate)
```

**Dependency direction**: worker `api -> jobs -> ports <- {insights, handouts, separation/hq}`;
`insights` imports only `ports`, `schemas`, numpy, scipy, soxr, soundfile; `handouts` only
`ports`, `schemas`, jinja2, weasyprint, pypdf; neither imports the other, `jobs` or `api`
(composition in `main.py`). .NET: `Api.Insights -> Domain.Insights`, `Api.Handouts ->
Domain.Handouts`, `Api.Handouts -> Api.Assets` (asset creation and sync), source adapters ->
the 014/016 services, `-> Worker client`; Domain has no dependencies on Api or Persistence.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/020-*/**`, ADR-029, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `worker/scripts/sync_handout_style.py`, `StemStudio.Contracts/**` | architect |
| `worker/src/stemstudio_worker/insights/**`, `worker/tests/insights/**` | beat-analysis |
| `worker/src/stemstudio_worker/handouts/**`, `worker/tests/handouts/**` | audio-ingest |
| `worker/src/stemstudio_worker/{jobs,api}/**` (020 parts), `separation/hq/**`, `main.py`, their tests | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `docs/public/src/**`, `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

audio-engine has no work in this feature.

## Key flows

1. **Automatic report**: the job-sync services of 001/002 (and G2's re-separation applier) call
   `QualityTrigger` when a phase completes and `StemStudio:AutoQualityReport` is on ->
   `QualityService.Request` -> worker `POST /v1/quality-jobs` -> `QualityJobApplier` polls
   (asset-sync pattern) -> stores `QualityReport` (previous ratings kept) -> UI polls the track.
2. **Report on request**: `POST /api/tracks/{id}/quality` (button `quality-check`).
3. **Tips**: `GET /api/tracks/{id}/quality` -> `TipRules` with the current track state and
   device estimate -> tips with actions; the UI maps actions to existing flows (002 split,
   mixer expand, mute in the current mix, G2 dialog).
4. **Handout**: menu -> `POST /api/assets/handouts` -> `HandoutAssembler` (sources -> pure
   planners -> `HandoutDocument`) -> `Asset(kind handout, queued)` -> worker `POST
   /v1/handout-jobs` -> 007 `AssetSyncService` (new `WorkerJobKind.Handout`) copies
   `handout.pdf`, fills `Asset.handout` -> library preview.
5. **Setlist handout**: same with the setlist source; assembler iterates entries (<= 50).
6. **Re-separation (G2)**: dialog -> `POST /api/tracks/{id}/reseparation` -> worker
   reseparation job (separation queue) -> on completion `StemSetReplacer` (temp + rename,
   detail discard, mix pruning, `StemSet`) -> quality trigger.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| New PDF engine (WeasyPrint) and system libraries (Pango, HarfBuzz) in the worker image | Handouts must be made in the worker in the app's CSS style with paged-media features | Chromium/Node in the worker image (about 300 MB, second runtime); ReportLab cannot reuse CSS; Verovio/Cairo (008) render music, not documents |
| A fourth worker lane (`insights`) | Handouts and reports must not delay exports (15 s target) or wait for separations | Sharing `render` would delay exports; sharing the separation queue would block handouts for minutes |
| Tips derived in the backend, measures in the worker | Tips depend on track state that changes without new audio | Tips in the worker would need re-analysis after each state change |
| Gated second separation pipeline (G2) | The only honest "better model" action; the backlog's GPU tip is false | Shipping a GPU re-run tip that changes nothing would mislead; shipping an unmeasured model would break principle V |
| Five source adapters to 014/016 | Those specs are written in parallel; shapes may differ | Coding against assumed shapes directly would spread changes over the assembler |
