# Contract delta: feature 014 Practice flow

Feature 014 adds sections, score views, setlists and the practice log to the backend API, three
short job kinds on a new worker lane, a score follower and a loop-pass callback to the audio
engine, a data-driven key map to web-interop, and UI test ids. Everything is **additive** (minor
bumps). The help-content contract is unchanged (topics go into existing areas). Decisions:
[ADR-023](../../../docs/decisions/ADR-023-practice-flow.md).

## Status (planning 2026-11-16; Phase 0 part 2 done 2026-11-17, see section 7)

Features 012 (backend 1.11.0, engine 2.3.0, ui-test-ids 1.11.0) and 013 (backend 1.12.0,
worker 1.7.0, ui-test-ids 1.12.0) are specified in parallel and not merged, so 014 cannot write
full documents yet. Like 011, 014 publishes **standalone deltas** now and the full documents in
Phase 0 part 2.

| Contract | Base when 014 is built | 014 document now | Full document (Phase 0 part 2) |
|---|---|---|---|
| Backend API | 1.12.0 (013) | [practice-api.openapi.yaml](practice-api.openapi.yaml) `1.13.0-delta.1`, standalone, Redocly-valid (OpenAPI 3.1; 3 accepted warnings) | `backend-api.openapi.yaml` **1.13.0** = 013's 1.12.0 + this delta, unchanged |
| Worker API | 1.7.0 (013) | [worker-practice.openapi.yaml](worker-practice.openapi.yaml) `1.8.0-delta.1`, standalone, Redocly-valid | `worker-api.openapi.yaml` **1.8.0** = 013's 1.7.0 + this delta + the `Health`/allow-list additions below |
| Audio engine | 2.3.0 (012) | [audio-engine.delta.md](audio-engine.delta.md) **2.4.0** | `audio-engine.md` 2.4.0 |
| Web interop | 1.0.0 (004) | [web-interop.delta.md](web-interop.delta.md) **1.1.0** (allocated to 014 in the roadmap by the CTO, 2026-11-16) | `web-interop.md` 1.1.0 |
| UI test ids | 1.12.0 (013) | [ui-test-ids.delta.md](ui-test-ids.delta.md) (names binding) | `ui-test-ids.md` **1.13.0** |
| Help content | 1.0.0 | unchanged | - |
| Agent tool catalogue | 1.0.0 (011) | unchanged; tools for sections/setlists/practice come in a later catalogue minor (roadmap rule) | - |

The readable summary is below; on any difference the OpenAPI documents win.

## Assumed 012 surface (to reconcile in Phase 0 part 2)

| Id | 014 assumes | Used by |
|---|---|---|
| A-012-1 | Engine position and karaoke clock are in track seconds at every speed | score follower, practice recorder |
| A-012-2 | The speed in percent (50-120) and transpose (-12..12) are in `MixerState` and in the mix (backend `MixSettings`, field names from 012) | practice log (`maxSpeedPercent`), Score panel transpose note, shortcuts `mixer.speed.*`, `mixer.transpose.*` |
| A-012-3 | The speed trainer runs on the mix's loop region; its on/off state is in `MixerState` | section loop = trainer region; `trainerReachedPercent` |
| A-012-4 | Count-in is a mixer setting with on/off | shortcut `mixer.count-in.toggle` |
| A-012-5 | No loop-pass callback in engine 2.3.0 | engine 2.4.0 `OnLoopPass` |
| A-012-6 | 012's keys are those of the CTO decision of 2026-11-16 (`-` `=` speed, `,` `.` transpose, `0` reset speed and transpose, `T` trainer, count-in see below); 014 owns the complete catalogue (spec FR-020 with an owner column) and 012 registers handlers for the command ids of data-model 5.2 | key map |

Reconciliation items (architect, Phase 0 part 2; none changes 014 behaviour):

- **R-1** Rename 014's references to 012's actual field/method names (A-012-2..4).
- **R-2** Shortcut catalogue (CTO decisions 2026-11-16, applied in spec FR-020): 012 owns `-` `=`
  `,` `.` `0` `T` and count-in; 015 owns `R` (record/stop) and `C` (compare), reserved in 014's
  catalogue until 015 ships. **Decided** (CTO, 2026-11-16): the metronome keeps `K` (shipped in
  004), count-in is `Shift+K`; `keymap-defaults.json` (T006) carries exactly these defaults. If 012's
  code binds keys of its own, they are removed in favour of the catalogue.
- **R-3** If 012 has a loop-pass callback with region and speed, drop `OnLoopPass` from 2.4.0.
- **R-4** If 012 defines test ids for the trainer region label, keep them (ui-test-ids delta note).
- **R-5** Confirm 013's backup/restore treats the new tables as part of the database (no 013
  change expected; restore compares migration ids).

## 1. Backend API 1.13.0 (summary)

| Method + path | operationId | Request | Responses |
|---|---|---|---|
| `GET /api/tracks/{trackId}/sections` | `getSections` | - | `200 SectionSet`; `404` |
| `PUT /api/tracks/{trackId}/sections` | `replaceSections` | `SectionsUpdate {revision, sections[SectionInput]}` | `200 SectionSet`; `404`; `409 sections-changed`, `track-not-ready`, `detection-running`; `422` |
| `POST /api/tracks/{trackId}/sections/detect` | `detectSections` | `{overwrite?}` | `202 SectionSet`; `404`; `409 sections-edited`, `detection-running`, `track-not-ready`; `503 worker-unavailable` |
| `GET /api/tracks/{trackId}/scores` | `listTrackScores` | - | `200 TrackScore[]`; `404` |
| `GET /api/assets/{assetId}/score-view` | `getScoreView` | - | `200 ScoreView`; `404`; `409 not-a-score` |
| `POST /api/assets/{assetId}/score-view` | `createScoreView` | - | `202 ScoreView`; `404`; `409 not-a-score`, `score-not-ready`; `503` |
| `GET /api/assets/{assetId}/score-view/pages/{page}` | `getScoreViewPage` | - | `200 image/svg+xml` (CSP, nosniff, immutable cache with `?v=`); `404`; `409 score-view-not-ready` |
| `GET /api/setlists` | `listSetlists` | - | `200 SetlistSummary[]` |
| `POST /api/setlists` | `createSetlist` | `{name, note?}` | `201 Setlist`; `422` |
| `GET /api/setlists/{setlistId}` | `getSetlist` | `?acknowledgeRemoved` | `200 Setlist`; `404` |
| `PATCH /api/setlists/{setlistId}` | `updateSetlist` | `{name?, note?}` | `200`; `404`; `422` |
| `DELETE /api/setlists/{setlistId}` | `deleteSetlist` | - | `204`; `404` |
| `PUT /api/setlists/{setlistId}/entries` | `replaceSetlistEntries` | `{entries[{id?, trackId, mixId?, note?}]}` 0..100 | `200 Setlist`; `404`; `422` (`track-not-found`, `mix-track-mismatch`) |
| `GET /api/setlists/{setlistId}/pdf` | `getSetlistPdf` | `?lyrics&sections&notes&pageBreakPerSong` (all default true) | `200 application/pdf` attachment; `404`; `409 setlist-empty`; `503`; `504 document-timeout` |
| `GET /api/practice/settings` | `getPracticeLogSettings` | - | `200 PracticeLogSettings {enabled, noticeAcknowledged, sessions, updatedAt}` |
| `PUT /api/practice/settings` | `updatePracticeLogSettings` | `PracticeLogSettingsUpdate {enabled?, noticeAcknowledged?}` | `200`; `422` |
| `GET /api/practice/sessions` | `listPracticeSessions` | `?trackId&before&limit` | `200 PracticeSession[]`; `422` |
| `DELETE /api/practice/sessions` | `deletePracticeSessions` | `?trackId` (absent = all) | `200 {deleted}` |
| `PUT /api/practice/sessions/{sessionId}` | `upsertPracticeSession` | `PracticeSessionInput` (cumulative) | `200 PracticeSession`; `404` (track); `409 practice-log-off`; `422` |
| `DELETE /api/practice/sessions/{sessionId}` | `deletePracticeSession` | - | `204`; `404` |
| `GET /api/practice/summary` | `getPracticeSummary` | `?trackId&days(1..365, 30)&utcOffsetMinutes(-840..840, 0)` | `200 PracticeSummary`; `404`; `422` |
| `GET /api/practice/overview` | `getPracticeOverview` | `?utcOffsetMinutes` | `200 PracticeTrackTotal[]`; `422` |

Changed existing schema (additive): `LoopRegion.sectionId: uuid | null` (default null).

`StemStudio.Contracts` (Phase 0 part 2): `Sections/{SectionSetDto, SectionDto, SectionInputDto,
SectionsUpdateRequest, DetectSectionsRequest, SectionKindDto, SectionSetStatusDto,
SectionWarningDto}`, `ScoreFollow/{TrackScoreDto, ScoreViewDto, ScoreViewPageDto, ScoreBarDto,
ScoreViewStatusDto}`, `Setlists/{SetlistDto, SetlistSummaryDto, SetlistEntryDto,
SetlistCreateRequest, SetlistUpdateRequest, SetlistEntryInputDto, SetlistEntriesUpdateRequest,
SetlistPdfOptions}`, `Practice/{PracticeLogSettingsDto, PracticeLogSettingsUpdateRequest,
PracticeSessionInputDto, PracticeSessionDto, PracticeSectionInputDto, PracticeSummaryDto,
PracticeDayDto, PracticeSectionTotalDto, PracticeTempoPointDto, PracticeTrackTotalDto,
DeletedCountDto}`, `LoopRegionDto.SectionId`, `ApiRoutes.{Sections, SectionsDetect, TrackScores,
ScoreView, ScoreViewPage, Setlists, Setlist, SetlistEntries, SetlistPdf, PracticeLogSettings,
PracticeSessions, PracticeSession, PracticeSummary, PracticeOverview}`, `ContractLimits.{MaxSections
64, MinSectionSeconds 1, MaxSectionLabel 40, MaxSetlistEntries 100, MaxSetlistName 80,
MaxSetlistNote 2000, MaxEntryNote 1000, MaxLyricsCharsPerSong 10000, MaxSessionHours 12,
UtcOffsetRange}`; operation ids staged in `PendingPracticeOperations` (agreed one-time edit in the
backend contract tests, as 008).

## 2. Worker API 1.8.0 (summary)

| Method + path | operationId | Notes |
|---|---|---|
| `POST /v1/section-jobs` | `createSectionJob` | `SectionJobRequest {stems, details[], grid \| null, durationSeconds, maxSections}` -> `202 SectionJob`; `400 invalid-input-path`; `404 input-not-found`; `422` |
| `GET /v1/section-jobs/{jobId}` | `getSectionJob` | result `SectionResult {analyzer, snap, sections[DetectedSection], warnings[]}` |
| `DELETE /v1/section-jobs/{jobId}` | `deleteSectionJob` | idempotent |
| `POST /v1/score-view-jobs` | `createScoreViewJob` | `{musicxml (assets/<id>/score.musicxml), grid (008 TempoGrid), durationSeconds}` |
| `GET /v1/score-view-jobs/{jobId}` | `getScoreViewJob` | result `{pages[{page, widthPx, heightPx}], bars[BarBox], engine}` |
| `GET /v1/score-view-jobs/{jobId}/pages/{page}` | `getScoreViewPageFile` | `image/svg+xml`; `409 job-not-completed` |
| `DELETE /v1/score-view-jobs/{jobId}` | `deleteScoreViewJob` | idempotent |
| `POST /v1/setlist-document-jobs` | `createSetlistDocumentJob` | `SetlistDocumentRequest` (content inline, no paths) |
| `GET /v1/setlist-document-jobs/{jobId}` | `getSetlistDocumentJob` | `pages` when completed |
| `GET /v1/setlist-document-jobs/{jobId}/file` | `getSetlistDocumentFile` | `application/pdf`; `409 job-not-completed` |
| `DELETE /v1/setlist-document-jobs/{jobId}` | `deleteSetlistDocumentJob` | idempotent |

Additions to existing worker schemas at the merge: `Health.lanes.practice: LaneHealth`;
`Health.structure: {analyzer: "stems-novelty" | "fake", version: string}` (required; Python
default from settings); `ports.LIBRARY_PATH_PATTERNS` + `assets/<uuid>/score\.musicxml`;
`WorkerSettings.structure_analyzer` from `STEMSTUDIO_STRUCTURE_ANALYZER`; `CONTRACT_VERSION =
"1.8.0"`. Ports (data-model 2.7): `StructureAnalyzer`, `ScoreViewRenderer`, `BarBox`,
`ScoreView`, `SetlistDocumentRenderer`, errors `StructureError`, `ScoreViewError`,
`DocumentError`; texts `SECTION_WARNING_TEXTS`, `SCORE_VIEW_FAILED_TEXT`,
`DOCUMENT_FAILED_TEXT`.

## 3. Audio engine 2.4.0

See [audio-engine.delta.md](audio-engine.delta.md): `attachScoreFollower`,
`setScoreFollowOptions`, `detachScoreFollower`, `ScoreViewPage`, `ScoreViewBar`, `ScoreViewMap`,
`ScoreFollowOptions`, `LoopPassInfo`, callback `OnLoopPass`.

## 4. Web interop 1.1.0

See [web-interop.delta.md](web-interop.delta.md): `KeyChord`, `KeyBinding`, `KeyMap`, scope
`setlist`, `registerShortcuts(dotnet, scope, keyMap?)`, `updateShortcuts`, `defaultKeyMap`,
`captureChord`, `isReservedChord`, preferences schemaVersion 2, `mixer-keys.js` retired.

## 5. UI test ids 1.13.0

See [ui-test-ids.delta.md](ui-test-ids.delta.md).

## 6. In-app help

Topics and tours: data-model 6 (areas `mixer`, `mixes`, `start`, `glossary`; no new area, so
help-content stays 1.0.0). The existing `shortcuts` topic is reviewed by 014 and links to the live
overview.

## 7. Phase 0 outcome (part 2, architect, 2026-11-17)

Built on `main` e8d6a5d8 (001-013 incl. 012 and 013) in branch `r14-p0-014`. All documents below are
full documents; the deltas stay as the merge sources.

| Contract | Full document | Delta (merge source) | Checks |
|---|---|---|---|
| Backend API | [backend-api.openapi.yaml](backend-api.openapi.yaml) **1.13.0** = 013's 1.12.0 + the delta; `x-stemstudio-contains` ends `1.12.0, 1.13.0` | [practice-api.openapi.yaml](practice-api.openapi.yaml) **1.13.0-delta.2** | Redocly 2.78.0 valid, 13 warnings (11 of 1.12.0 + 2 known: `listSetlists`, `getPracticeLogSettings` have no 4XX); `check_superset.py` 1.12.0 -> 1.13.0 and delta -> 1.13.0: superset yes; `PracticeFlowContractTests` |
| Worker API | [worker-api.openapi.yaml](worker-api.openapi.yaml) **1.8.0** = 013's 1.7.0 + the delta + `Health.structure`, `Health.lanes.practice`, the MusicXML allow-list line | [worker-practice.openapi.yaml](worker-practice.openapi.yaml) **1.8.0-delta.2** | Redocly valid, the 4 warnings of 1.7.0; `tests/contract/test_practice_contract.py`; `PracticeFlowContractTests.Worker180_*` |
| Audio engine | [audio-engine.md](audio-engine.md) **2.4.0** = 012's 2.3.0 + the delta | [audio-engine.delta.md](audio-engine.delta.md) | `types.ts` 2.4.0, `tests/practice-flow-contract.test.ts` |
| Web interop | [web-interop.md](web-interop.md) **1.1.0** = 004's 1.0.0 + the delta + section 5.4 (catalogue) | [web-interop.delta.md](web-interop.delta.md) | `keymap-defaults.json`, `KeyMapDefaultsTests` |
| UI test ids | [ui-test-ids.md](ui-test-ids.md) **1.13.0** = 013's 1.12.0 + the delta ids | [ui-test-ids.delta.md](ui-test-ids.delta.md) | `PracticeFlowUiTestIdsTests` (no 014 id repeats an earlier one) |

Changes against the Phase 0 part 1 deltas (all additive or renames of not-yet-built names):

- **delta.2 (backend)**: practice log settings are `PracticeLogSettings` / `PracticeLogSettingsUpdate`
  with `getPracticeLogSettings` / `updatePracticeLogSettings` (012's 1.11.0 has `PracticeSettings`);
  `LoopRegion` keeps the 1.12.0 description and documents `sectionId` on the property (pure addition);
  the score view example URL is quoted (PyYAML read `?v=1` in a flow mapping as a complex key).
- **delta.2 (worker)**: examples only (queued/failed section jobs, score-view request and completed
  job, setlist document request and completed job), copied to `tests/StemStudio.Api.Tests/Worker/Examples/`.
- **UI ids**: the practice log panel's ids are `practice-log-*` (012 has `practice-panel` and
  `practice-panel-toggle` for the speed and key panel); new `trainer-region-label` (R-4).
- **Library gate (013)**: no 014 operation deletes or replaces library files, so none carries
  `x-stemstudio-write-gate`; the maintenance 503 is the middleware's; appliers skip ticks during
  maintenance. `ManifestBuilder.BackendApiVersion` (013, `src/StemStudio.Api`) is still "1.12.0": the
  backend lane bumps it to "1.13.0" (backups record the API version).

Reconciliation results:

| Item | Result |
|---|---|
| R-1 | 012's names: mix fields `practice.speedPercent`, `practice.transposeSemitones`, `practice.trainer`, `practice.countIn.bars` (count-in "on" = `bars > 0`); engine `PracticeStatus` / `OnPracticeChanged`; command ids `mixer.speed:-5`, `mixer.speed:5`, `mixer.transpose:-1`, `mixer.transpose:1`, `mixer.practice.reset`, `mixer.countin.toggle`, `mixer.trainer.toggle` (data-model 5.2 updated). |
| R-2 | Catalogue `keymap-defaults.json` as decided: metronome `K`, count-in `Shift+K`, reset `0`; 012's shipped `r`/`c` move (ui-test-ids 1.13.0 "Changed default keys"); `R`/`C` reserved for 015. |
| R-3 | 2.3.0 has no loop-pass callback with region and speed: `OnLoopPass` stays in 2.4.0. A-012-1 holds: the heard anchor maps context time to track seconds at every speed. |
| R-4 | 012 defines no trainer region label id: `trainer-region-label` added in 1.13.0. |
| R-5 | Full restore: the rebuild from migrations copies every table, so the new tables travel (no 013 change). **Merge restore**: 013's `MergePlanner` has fixed categories and would not bring sections, score views, setlists or practice sessions of merged tracks: open item, decision requested from the CTO (hand-off `architect-014-phase0.md`). |

## 8. Re-merge after 022/023 land on `main`

022 (backend 1.22.0, ui-test-ids 1.22.0, web-interop 1.2.0) and 023 (backend 1.23.0, ui-test-ids
1.23.0) are integrated before the 014+015 wave; then `main` is merged into this round and the full
documents are re-merged in order (labels are never renumbered; ROADMAP "Full-document labels"):

1. **Backend**: run the 014 merge onto 023's full 1.23.0 (`specs/023-lyric-writing-assistant/contracts/backend-api.openapi.yaml`,
   which contains 1.12.0 and 1.22.0). Result label **1.13.0**, `x-stemstudio-contains` `[..., 1.12.0, 1.22.0,
   1.23.0, 1.13.0]`. Dry run 2026-11-17 against the `022-023-round` head: no shared path, operation id, tag or
   example; the shared components are the same five schemas, two parameters and three responses as on 1.12.0;
   `check_superset.py` 1.23.0 -> merged and delta -> merged: superset yes. Then `check_superset.py`'s default
   `OLDER` becomes the 1.23.0 document, `PracticeFlowContractTests.Changed1120Entries` compares with 1.23.0 and
   the contains list gains 1.22.0 and 1.23.0; 015's 1.14.0 is merged after 014 the same way.
2. **UI test ids**: 1.13.0 = 023's full 1.23.0 + the 014 section ("Additions in 1.13.0" after "Additions in
   1.23.0"); dry run: no id collision with 1.22.0/1.23.0. `PracticeFlowUiTestIdsTests` then reads 1.23.0 as
   the base.
3. **Web interop**: the full document after this round contains 1.1.0 (014) and 1.2.0 (022, scope
   `library`, `/` -> `library.search.focus`); `ShortcutScope = 'global' | 'mixer' | 'setlist' | 'library'`. The
   catalogue already lists `library.search.focus` (owner 022), so the frontend lane's data-driven key map
   replaces 022's hard-coded `LIBRARY` binding with the catalogue row (same key, same command).
4. **Worker / engine**: unchanged by 022/023 (1.8.0 / 2.4.0 stay as built here).
5. **Key catalogue**: 023's lyrics editor keys (Ctrl+Alt+C, F8, Shift+F8, Alt+Enter, Alt+Backspace, Alt+I) are
   `externalChords`; `LyricsEditorPage` `/lyrics/{id}` registers no key map scope.
