# Contract delta: Insights and Handouts (feature 020)

Feature 020 adds additive minors to three contracts. The audio-engine contract is **unchanged**
(no engine work). Machine-readable documents are in [contracts/](contracts/); on any difference
the OpenAPI documents win.

## 1. Status (Phase 0, 2026-11-23)

| Contract | Base on `main` | 020 document | Full document | State |
|---|---|---|---|---|
| Worker API | 1.9.0 (016) | [worker-api.delta.openapi.yaml](contracts/worker-api.delta.openapi.yaml) `1.11.0-delta.2` | [worker-api.openapi.yaml](contracts/worker-api.openapi.yaml) **1.11.0** | merged by `merge_full.py`; G2 parts deferred |
| Backend API | 1.15.0 (016) | [backend-api.delta.openapi.yaml](contracts/backend-api.delta.openapi.yaml) `1.19.0-delta.2` | [backend-api.openapi.yaml](contracts/backend-api.openapi.yaml) **1.19.0** (`x-stemstudio-contains` ends `1.15.0, 1.19.0`) | merged; G2 parts deferred |
| UI test ids | 1.15.0 (016) | [ui-test-ids.delta.md](contracts/ui-test-ids.delta.md) | [ui-test-ids.md](contracts/ui-test-ids.md) **1.19.0** | merged; G2 ids deferred |
| Help content | 1.0.0 (004) | [help-topics.md](contracts/help-topics.md) | unchanged format | topics + feature catalogue binding (G2 topic/entry deferred) |
| Audio engine | 2.6.0 (016) | - | unchanged | - |

The plan (2026-11-16) assumed 019's documents as the base. The owner changed the build order: 020 is
built before 017, 018, 019 and 021, so the deltas are merged onto 016's documents and keep their
allocated labels (ROADMAP "Full-document labels after an out-of-order merge").

Build and check: `python -I specs/020-insights-handouts/contracts/merge_full.py [--check]` (PyYAML;
`--check` = freshness and superset of the 016 documents). Lint:
`npx @redocly/cli@2.78.0 lint --config specs/020-insights-handouts/contracts/redocly.yaml` on the two
full documents and the two deltas -> valid, 33 warnings (full backend 18 as 016, full worker 7 = 016's
5 + the two idempotent DELETEs, deltas 8; listed in `redocly.yaml`).

## 2. Worker API 1.11.0 (summary)

| Method | Path | operationId | Notes |
|---|---|---|---|
| POST | `/v1/quality-jobs` | `createQualityJob` | `QualityJobRequest` (six standard stems + detail/rest stems + 002 metrics); 202 `QualityJob`; 400/404 paths; 422 |
| GET | `/v1/quality-jobs/{qualityJobId}` | `getQualityJob` | result `QualityResult` with `StemQuality[]` |
| DELETE | `/v1/quality-jobs/{qualityJobId}` | `deleteQualityJob` | 204 idempotent |
| POST | `/v1/handout-jobs` | `createHandoutJob` | `HandoutJobRequest {document: HandoutDocument, paper}`; notice texts validated; 202 |
| GET | `/v1/handout-jobs/{handoutJobId}` | `getHandoutJob` | result `HandoutResult` (pages, outline, warnings) |
| GET | `/v1/handout-jobs/{handoutJobId}/file` | `getHandoutFile` | `application/pdf`; 409 `handout-not-completed` |
| DELETE | `/v1/handout-jobs/{handoutJobId}` | `deleteHandoutJob` | 204 |
| POST/GET/DELETE | `/v1/reseparation-jobs[/{id}[/stems/{stem}]]` | `createReseparationJob`, `getReseparationJob`, `deleteReseparationJob`, `getReseparationStem` | **G2, deferred: not merged** |

Additions: per-job allow-lists (quality jobs: stem and detail stem paths only; handout jobs:
`assets/<uuid>/<name>.pdf` and `tracks/<uuid>/artwork/(auto|user)/cover.jpg` only; the general
allow-list of render/transcription jobs is unchanged); `Health.lanes.insights`, `Health.insights
{analyzer, analyzerVersion, renderer {weasyprint, pango}, fonts {archivo, fallback}}`;
`Health.separationPipelines` (G2, deferred). `CONTRACT_VERSION = "1.11.0"`.

## 3. Backend API 1.19.0 (summary)

| Method | Path | operationId | Notes |
|---|---|---|---|
| GET | `/api/tracks/{trackId}/quality` | `getQualityReport` | `QualityReport` (status `none` when never made), tips per stem |
| POST | `/api/tracks/{trackId}/quality` | `requestQualityReport` | 202; 409 `track-not-ready`, `quality-running`; 503 |
| GET | `/api/handouts/options` | `getHandoutOptions` | `trackId` or `setlistId`, `family`, `transposition`; ingredients with status and create-actions, notation choices, estimate |
| POST | `/api/assets/handouts` | `createHandout` | `CreateHandoutRequest`; 202 `Asset` kind `handout`; 409 `track-not-ready`, `setlist-empty`, `too-many-songs`, `insufficient-disk-space`; 422; 503 |
| GET | `/api/tracks/{trackId}/practice-notes` | `getPracticeNotes` | |
| PUT | `/api/tracks/{trackId}/practice-notes/{family}` | `setPracticeNote` | empty text deletes (204) |
| GET | `/api/tracks/{trackId}/reseparation/options` | `getReseparationOptions` | **G2, deferred** |
| POST/DELETE | `/api/tracks/{trackId}/reseparation` | `startReseparation`, `cancelReseparation` | **G2, deferred** |

Schema additions: `AssetKind` += `handout`, `AssetOrigin` += `handout`, `Asset.handout`
(`HandoutFacts`), `Provenance.handoutRecipe` (`HandoutRecipe`; `recipe` and `scoreRecipe` null for
handouts, as 008 did for `scoreRecipe`), `listAssets?kind=handout`, `Track.quality`
(`QualityBadge`); the three properties are optional (absent = null), like every backend addition
since 1.13.0. G2, deferred: `Track.stemSet`, `Track.reseparation`, `TipCode` `rerun-high-quality`,
`TipActionKind` `reseparate`. `getAssetFile` serves the handout `pdf` with the 1.7.0 rule.

**StemStudio.Contracts (Phase 0, T002)**: `Insights/{QualityReportDto, StemQualityViewDto,
BleedViewDto, ArtefactViewDto, TipDto, TipActionDto, TipCodeDto, TipActionKindDto,
QualityRatingDto, QualityLevelDto, ReliabilityDto, QualitySummaryDto, QualityBadgeDto,
QualityReportStatusDto}`, `Handouts/{CreateHandoutRequest, HandoutSourceDto, HandoutFamilyDto,
TranspositionDto, PaperDto, IngredientCodeDto, IngredientDto, IngredientStatusDto,
IngredientActionDto, NotationChoiceDto, HandoutOptionsDto, HandoutFactsDto, HandoutRecipeDto,
OmittedIngredientDto, HandoutWarningDto, PracticeNoteDto, PracticeNotesDto,
SetPracticeNoteRequest}` (G2 `Separation/*` deferred); `AssetDto.Handout`,
`ProvenanceDto.HandoutRecipe`, `TrackDto.Quality`, `AssetKindDto.Handout`,
`AssetOriginDto.Handout`, `ApiRoutes.{Quality, HandoutOptions, Handouts, PracticeNotes,
PracticeNote}`, `ContractLimits.{MaxPracticeNoteLength (4000),
MaxSetlistHandoutSongs (50), MaxTipsPerStem (2), HandoutNoticeFooter, HandoutNoticeFull,
QualityDisclaimer}`.

## 4. UI test ids 1.19.0

See [contracts/ui-test-ids.delta.md](contracts/ui-test-ids.delta.md) (quality panel, rows, tips,
badges, handout menu/dialog, practice notes, setlist handout, library rows, G2 dialog).

## 5. Help

See [contracts/help-topics.md](contracts/help-topics.md): 9 topics (+1 G2), 7 feature-catalogue
entries (+1 G2).

## 6. Reconciliation with 014 and 016 (Phase 0, T004)

Specs 012-019 are written in parallel. 020 assumed (data-model 8):

| Id | Assumed | If different |
|---|---|---|
| A-014-1 | sections `{id, kind, label, startSeconds, endSeconds}` per track | change `SectionSourceAdapter` only |
| A-014-2 | setlist `{id, name, notes, entries[{id, position, trackId, mixId?, notes}]}` | change `SetlistSourceAdapter` and the `setlistId` query parameter name only |
| A-014-3 | best tempo percent per (track, section) in the practice log | change `PracticeLogSourceAdapter`; drop `practice-progress` if 014 has no per-section tempo |
| A-016-1 | chord track `{status, key, chords[{startSeconds, endSeconds, symbol}]}` | change `ChordSourceAdapter` and the symbol parser |
| A-016-2 | tab/drum/transposed notation = `score` assets with `pdf` + facts `notation`, `transposition` | change `NotationCatalogAdapter`; the worker only needs a PDF path |

The architect records the actual 014/016 shapes and any adapter change here under "Phase 0
outcome". If 014 or 016 add their own handout or PDF export of setlists (S6 mentions "export as
PDF with lyrics/chords"), 020's setlist handout **replaces or reuses** it: Phase 0 decides which
and records it here, so the product has one setlist PDF.

## 7. Agent (011)

Proposed tools for the next agent-tool-catalogue minor (not part of 020, roadmap rule):
`get_stem_quality(trackId)`, `check_stem_quality(trackId)`, `create_handout(source, family,
options)`, `set_practice_note(trackId, family, text)`; risk levels: read / job / job / write.

## 7a. Gate outcomes

- **G1** decides the `validated`/`indicative` labels only; the contract does not change.
- **G2 fails** -> remove from the merged documents: tag `reseparation` (worker and backend),
  `Health.separationPipelines`, `Track.stemSet`, `Track.reseparation`, `TipCode.rerun-high-quality`,
  `TipActionKind.reseparate`, the G2 ui ids and help topic; record here.

## Phase 0 outcome

Architect, 2026-11-23 (T001-T008). Measurements: research.md "Phase 0 measurements".

### P0-1 Base documents and labels

`main` holds 016's full documents (backend 1.15.0, worker 1.9.0, ui-test-ids 1.15.0); 017, 018 and
019 are not built. 020 needs nothing from them (no schema, path or id of 017-019 is referenced), so
the deltas were merged onto 016's documents and keep the allocated labels backend **1.19.0**, worker
**1.11.0**, ui-test-ids **1.19.0**. The schemas copied for standalone validity are dropped
(`merge_full.py` `shared`). When 017-019 are built later, their deltas are merged onto the 1.19.0
documents by containment; no 020 label changes.

### P0-2 Gate G2: pending owner (dataset needed) - User Story 5 deferred (measured later: failed, P0-7)

No MUSDB18-HQ: G2 cannot be measured. Following 7a, the G2 parts are **not merged**: the paths
tagged `reseparation` (worker and backend), `Health.separationPipelines`, the `Reseparation*`,
`SeparationPipeline` and `StemSet` schemas, `Track.stemSet`, `Track.reseparation`, `TipCode`
`rerun-high-quality`, `TipActionKind` `reseparate`, the G2 ui ids (`reseparate-*`,
`reseparation-*`, `stem-set-pipeline`), the help topic `reseparate-hq` and the catalogue entry
`reseparation.hq`. They stay in the delta files as the design record. Tasks T088-T099 do not run in
this wave; spec US5, FR-007 and SC-007 are marked deferred. If the owner provides the dataset and G2
passes, the parts need a **new contract minor** (allocated by the CTO then); the recommended
checkpoint is in research.md.

### P0-7 G1 and G2 measured on MUSDB18-HQ (2026-11-23): both failed

Measurements: research.md "Gates G1 and G2 on MUSDB18-HQ" (25-track seeded subset of the `test` split).

- **G1 failed** on all 8 cells (best: drums bleed rho 0.49 against the 0.5 gate; drums artefacts -0.60). The worker
  records `failed` for vocals, drums, bass and other in both dimensions and `analyzerVersion` becomes **1.0.1**;
  no contract change (the label table only).
- **Owner decision**: the stem quality report (US1/US2) ships **switched off** behind
  `StemStudio:QualityReport:Enabled` (default false; ADR-029 D8). While off, `requestQualityReport` answers the
  existing `503` (ProblemDetails with a readable detail; no new problem type: the contract has no code for
  "switched off"), `getQualityReport` answers status `none`, `Track.quality` is absent and the web hides the quality
  card, badges and tips. The e2e override switches it on. US3/US4 and practice notes are not affected.
- **G2 failed** as specified (vocals SDR +2.70 dB passes, other stems' SIR +0.09 dB against +0.5 dB fails; on the
  original mixture +3.21 dB and +0.24 dB): **User Story 5 stays removed**. The deferred G2 parts listed in P0-2
  stay out of the contracts; re-scoping (vocals-only HQ, or the original audio as input) needs a new spec decision
  and contract minor.

### P0-3 Gate G1: not run - every label `indicative`, levels shown (superseded by P0-7)

The reliability table starts with every dimension `indicative`. Low/medium are masked as `unknown`
only for a dimension that was measured and failed G1 (data-model 4.5 clarified). T039 runs G1 when
the dataset is available; its result changes `thresholds.py` and bumps `analyzerVersion`.

### P0-4 Reconciliation with 014 and 016 (section 6)

| Id | Merged shape on `main` | Effect |
|---|---|---|
| A-014-1 | `SectionSetDto {status, sections[SectionDto {id, kind, label, startSeconds, endSeconds, startBar?, instrument, confidence, origin}]}` (`GET /api/tracks/{id}/sections`; `SectionService`) | as assumed; `startBar` from 014 is used when set, else `BarMapper` computes it; `SectionSourceAdapter` reads `ISectionRepository` |
| A-014-2 | `SetlistDto {id, name, note (string, "" = none), entries[SetlistEntryDto {id, position, trackId, trackTitle, artist, mixId, mixName, note, durationSeconds, bpm, sectionCount, hasLyrics}], totalSeconds, removedEntries}`; entries of deleted tracks are **removed** by 014 (`removedEntries` counts them until acknowledged) | `SetlistSourceAdapter` maps `note` "" -> null; a setlist never holds a deleted track, so `SetlistEntryBlock.available` and the warning `track-missing` are **removed** from the contracts; spec US4 scenario 3 rewritten; query parameter `setlistId` unchanged |
| A-014-3 | `PracticeSummaryDto.sections[PracticeSectionTotalDto {sectionId, kind, label, seconds, loopPasses, bestTrainerPercent?}]` | `practice-progress` = `bestTrainerPercent` rounded (null = no progress); `PracticeLogSourceAdapter` |
| A-016-1 | `TrackAnalysisDto<ChordChartDocumentDto> {..., key: SongKeyDto?, keyRegions, preferFlats, segments[ChordSegmentDto {startMs, endMs, label (Harte), root?, quality?, bass?, confidence, edited}]}`; `Track.key` (`TrackKeySummaryDto.display`) | chords are structured, not symbols: no own parser. Spelling, transposition and chords over lyrics **reuse 016's domain** (`ChordSpeller`, `ChordDisplay`, `ChordSheetBuilder`, `KeyFormatter`); `ChordSourceAdapter` returns the chart, the key and the sheet |
| A-016-2 | notation items are `score` assets (`ScoreFactsDto {profile, ..., tablature?: TablatureFactsDto, part?: PartFactsDto {instrument: WrittenInstrumentDto, interval, writtenKey, clef}}`), PDF file `score.pdf` | tab = `tablature != null`; drum notation = profile `drums`; transposed part = `part.instrument` not `concert`: B-flat = `*-bb`, E-flat = `*-eb`, F = `*-f`; melodic C = profiles `strings`/`brass`/`woodwinds` or a concert part. `NotationCatalogAdapter` |

### P0-5 One setlist PDF? - keep 014's quick PDF, the 020 handout is the lesson PDF

014 already renders a setlist PDF (`GET /api/setlists/{id}/pdf`, ReportLab in the worker's
`practice` lane, streamed, not stored, options lyrics/sections/notes; ui ids `setlist-pdf-*`).
Replacing it would remove an operation and ui ids (a breaking change) and the agent can use it.
Decision: **reuse, not replace, in 1.19.0**: 014's PDF stays the quick "print the set" sheet; 020's
setlist handout is the stored, per-family lesson PDF in the library. The setlist page shows both:
the 014 PDF button and the new `setlist-handout-menu`; the help topic `handouts` says which to use
when. Folding 014's PDF into the `overview` handout is a candidate for a later major (CTO).

### P0-6 Contract refinements made in Phase 0

- Backend additions `Asset.handout`, `Provenance.handoutRecipe`, `Track.quality` are optional
  (absent = null), the 1.13.0+ convention (the planning delta had them required).
- Per-job allow-lists instead of extending the general one (a PDF or JPEG must never reach a render
  job); `ports.is_quality_input_path`, `ports.is_handout_input_path`.
- `HandoutConflict`: `track-not-ready` only for song handouts; a setlist song that is not completed
  is included with what it has (FR-010).
- `HandoutLyrics.chordPlacement` and `LyricLine.text` describe the 016 chord sheet rule (empty text =
  instrumental row).
- Renderer port: `HandoutRenderer.render(request, files, progress)`; the job layer resolves and
  checks every path once and passes the real paths, the renderer reads nothing else (FR-018).
- WeasyPrint 70 fetcher API (research.md P0-4): the renderer's fetcher subclasses
  `weasyprint.urls.URLFetcher`.
