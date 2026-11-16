# Contract delta: Insights and Handouts (feature 020)

Feature 020 adds additive minors to three contracts. The audio-engine contract is **unchanged**
(no engine work). Machine-readable documents are in [contracts/](contracts/); on any difference
the OpenAPI documents win.

## 1. Status (planning, 2026-11-16)

| Contract | Base | 020 document | Target | State |
|---|---|---|---|---|
| Worker API | 1.10.0 (019) | [worker-api.delta.openapi.yaml](contracts/worker-api.delta.openapi.yaml) `1.11.0-delta.1` | **1.11.0** | standalone, additive, Redocly-valid (OpenAPI 3.1; 6 known warnings: 3 idempotent DELETEs, 3 health schemas merged into `Health`) |
| Backend API | 1.18.0 (019) | [backend-api.delta.openapi.yaml](contracts/backend-api.delta.openapi.yaml) `1.19.0-delta.1` | **1.19.0** | standalone, additive, Redocly-valid (2 known warnings: `QualityBadge`, `StemSet` merged into `Track`) |
| UI test ids | 1.18.0 (019) | [ui-test-ids.delta.md](contracts/ui-test-ids.delta.md) | **1.19.0** | names binding |
| Help content | 1.0.0 (004) | [help-topics.md](contracts/help-topics.md) | unchanged format | topics + feature catalogue binding |
| Audio engine | 2.8.0 (019) | - | unchanged | - |

Lint: `npx @redocly/cli@2.78.0 lint --config specs/020-insights-handouts/contracts/redocly.yaml
specs/020-insights-handouts/contracts/worker-api.delta.openapi.yaml
specs/020-insights-handouts/contracts/backend-api.delta.openapi.yaml` -> valid, 8 warnings.

Phase 0 (T001-T004) merges the deltas into full documents `worker-api.openapi.yaml` 1.11.0,
`backend-api.openapi.yaml` 1.19.0 and `ui-test-ids.md` 1.19.0 in this folder, on top of the
documents current on `main` at that time, and drops the copied base schemas.

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
| POST/GET/DELETE | `/v1/reseparation-jobs[/{id}[/stems/{stem}]]` | `createReseparationJob`, `getReseparationJob`, `deleteReseparationJob`, `getReseparationStem` | **G2 only** |

Additions: allow-list `assets/<uuid>/<name>.pdf` and `tracks/<uuid>/artwork/(auto|user)/cover.jpg`;
`Health.lanes.insights`, `Health.insights {analyzer, analyzerVersion, renderer {weasyprint,
pango}, fonts {archivo, fallback}}`, `Health.separationPipelines` (G2). `CONTRACT_VERSION =
"1.11.0"`.

## 3. Backend API 1.19.0 (summary)

| Method | Path | operationId | Notes |
|---|---|---|---|
| GET | `/api/tracks/{trackId}/quality` | `getQualityReport` | `QualityReport` (status `none` when never made), tips per stem |
| POST | `/api/tracks/{trackId}/quality` | `requestQualityReport` | 202; 409 `track-not-ready`, `quality-running`; 503 |
| GET | `/api/handouts/options` | `getHandoutOptions` | `trackId` or `setlistId`, `family`, `transposition`; ingredients with status and create-actions, notation choices, estimate |
| POST | `/api/assets/handouts` | `createHandout` | `CreateHandoutRequest`; 202 `Asset` kind `handout`; 409 `track-not-ready`, `setlist-empty`, `too-many-songs`, `insufficient-disk-space`; 422; 503 |
| GET | `/api/tracks/{trackId}/practice-notes` | `getPracticeNotes` | |
| PUT | `/api/tracks/{trackId}/practice-notes/{family}` | `setPracticeNote` | empty text deletes (204) |
| GET | `/api/tracks/{trackId}/reseparation/options` | `getReseparationOptions` | **G2** |
| POST/DELETE | `/api/tracks/{trackId}/reseparation` | `startReseparation`, `cancelReseparation` | **G2**; 409 codes in the document |

Schema additions: `AssetKind` += `handout`, `AssetOrigin` += `handout`, `Asset.handout`
(`HandoutFacts`), `Provenance.handoutRecipe` (`HandoutRecipe`; `recipe` and `scoreRecipe` null for
handouts, as 008 did for `scoreRecipe`), `listAssets?kind=handout`, `Track.quality`
(`QualityBadge`), `Track.stemSet` and `Track.reseparation` (G2). `getAssetFile` serves the handout
`pdf` with the 1.7.0 rule.

**StemStudio.Contracts (Phase 0, T002)**: `Insights/{QualityReportDto, StemQualityViewDto,
BleedViewDto, ArtefactViewDto, TipDto, TipActionDto, TipCodeDto, TipActionKindDto,
QualityRatingDto, QualityLevelDto, ReliabilityDto, QualitySummaryDto, QualityBadgeDto,
QualityReportStatusDto}`, `Handouts/{CreateHandoutRequest, HandoutSourceDto, HandoutFamilyDto,
TranspositionDto, PaperDto, IngredientCodeDto, IngredientDto, IngredientStatusDto,
IngredientActionDto, NotationChoiceDto, HandoutOptionsDto, HandoutFactsDto, HandoutRecipeDto,
OmittedIngredientDto, HandoutWarningDto, PracticeNoteDto, PracticeNotesDto,
SetPracticeNoteRequest}`, G2: `Separation/{StemSetDto, ReseparationPhaseDto,
ReseparationOptionsDto, StartReseparationRequest, SeparationPipelineDto}`; `AssetDto.Handout`,
`ProvenanceDto.HandoutRecipe`, `TrackDto.Quality` (+ G2 fields), `AssetKindDto.Handout`,
`AssetOriginDto.Handout`, `ApiRoutes.{Quality, HandoutOptions, Handouts, PracticeNotes,
PracticeNote, ReseparationOptions, Reseparation}`, `ContractLimits.{MaxPracticeNoteLength (4000),
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

To be written in Phase 0.
