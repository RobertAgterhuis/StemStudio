# Contract delta: feature 019 Arrangement editor

Feature 019 adds the `arrangements` tag to the backend, an optional `timeline` to worker render
jobs, timeline methods to the audio engine, UI test ids, help topics and the `timeline` section
of the shared render vectors. Every change is **additive** (minor bumps). Design: ADR-028.

## Status (planning round, 2026-11-16)

Specs 012-018 are written in parallel and not merged, so the full documents cannot be produced
yet. This folder holds standalone, validated deltas; Phase 0 (tasks.md T001-T006) merges them
onto the versions current when 019 starts.

| Contract | Expected base at 019 start | 019 document here | State |
|---|---|---|---|
| Worker API | 1.9.0 (016) | [worker-api.delta.openapi.yaml](worker-api.delta.openapi.yaml) `1.10.0-delta.1` - Redocly 2.78.0 valid, 0 warnings | binding; merged into full **1.10.0** in T003 |
| Backend API | 1.17.0 (018) | [backend-api.delta.openapi.yaml](backend-api.delta.openapi.yaml) `1.18.0-delta.1` - Redocly valid, 1 expected warning (`ArrangementSnapshot` used only from `x-stemstudio-schema-additions`) | binding; merged into full **1.18.0** in T003 |
| Audio engine | 2.7.0 (017) | [audio-engine.md](audio-engine.md) **2.8.0** delta | binding; merged into `types.ts` + full `audio-engine.md` in T005 |
| UI test ids | 1.17.0 (018) | [ui-test-ids.delta.md](ui-test-ids.delta.md) **1.18.0** delta | binding; merged in T003 |
| Render vectors | 1.1.0 (015, CTO allocation change 2026-11-16) | [render-vectors.json](render-vectors.json) **1.2.0** + [generator](generate_render_vectors.py) | binding for the `timeline` section; T004 regenerates on top of the merged 1.1.0 (the generator copies every base section unchanged; today the base is 007's 1.0.0, `baseVersion` records it) |
| Help content | 1.0.0 (004) | section 6 below | no version bump (area `mixer` exists) |
| remote-protocol (018), agent tool catalogue (011), backup-archive (013), generator-api | - | unchanged | - |

On any difference the vectors win, then the OpenAPI deltas, then this summary.

## 1. Worker API 1.10.0

- `RenderJobRequest.timeline: RenderTimeline | null` (new, optional).
- New `RenderTimeline { crossfadeFrames: even 0..4410, segments: RenderSegment[1..256] }` and
  `RenderSegment { sourceStartFrame >= 0, frameCount >= 1 (> crossfadeFrames), mutedInputs:
  unique int[] < len(inputs) }`; arranged total <= 79,380,000 frames.
- `RenderRange` is arranged time when a timeline is present.
- `RenderResult.durationSeconds` maximum 900 -> 1800.
- Python (`schemas.py`, architect T001): `RenderTimeline`, `RenderSegment`, field on
  `RenderJobRequest`, `CONTRACT_VERSION = "1.10.0"`; model validators for the cross-field rules
  (index bounds, `frameCount > crossfadeFrames`, total) so they answer 422. `ports.py`: no new
  protocol (the executor signature is unchanged; the timeline travels in the request).

## 2. Backend API 1.18.0

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| GET | `/api/tracks/{trackId}/arrangements` | `listArrangements` | - | `200 ArrangementList`; `404` |
| POST | `/api/tracks/{trackId}/arrangements` | `createArrangement` | `CreateArrangementRequest { name?, seed?: song\|sections, document? }` (exactly one of seed/document) | `201 Arrangement` + `Location`; `404`; `409 arrangement-limit`, `409 no-sections`, `409 track-not-ready`; `422` |
| POST | `/api/tracks/{trackId}/arrangements/resolve` | `resolveArrangement` | `ArrangementInput` | `200 ResolvedTimeline`; `404`; `409 track-not-ready`; `422` |
| GET | `/api/arrangements/{arrangementId}` | `getArrangement` | - | `200 Arrangement`; `404` |
| PUT | `/api/arrangements/{arrangementId}` | `updateArrangement` | `UpdateArrangementRequest { name, revision, document }` | `200 Arrangement` (revision + 1); `404`; `409 arrangement-conflict` (`currentRevision`); `422` |
| DELETE | `/api/arrangements/{arrangementId}` | `deleteArrangement` | - | `204` (referencing mixes -> `arrangementId: null`); `404` |
| POST | `/api/arrangements/{arrangementId}/duplicate` | `duplicateArrangement` | `{ name? }` | `201 Arrangement`; `404`; `409 arrangement-limit`; `422` |

Additions to existing schemas (`x-stemstudio-schema-additions`):

- `MixSettings.arrangementId` / `Mix.arrangementId`: uuid | null (same track, else 422).
- `CreateAudioExportRequest.arrangementId` (uuid | null) and `.arrangement` (`ArrangementInput` |
  null), mutually exclusive (`422 arrangement`); `range` is arranged time; works in modes `raw`
  and `mix`.
- `ExportRecipe.arrangement: ArrangementSnapshot | null`; `Provenance.contents` gets
  `", arranged (<name>)"`.

C# (`StemStudio.Contracts`, architect T002): `Arrangements/{ArrangementDto, ArrangementSummaryDto,
ArrangementListDto, ArrangementInputDto, ArrangementRegionDto, SnapModeDto,
CreateArrangementRequest, UpdateArrangementRequest, DuplicateArrangementRequest,
ResolvedTimelineDto, TimelineSegmentDto, BarSpanDto, MixReferenceDto, ArrangementSnapshotDto}.cs`;
`MixSettingsDto.ArrangementId`, `MixDto.ArrangementId`, `CreateAudioExportRequest.ArrangementId`
/ `.Arrangement`, `ExportRecipeDto.Arrangement`; `ApiRoutes.Arrangements*`; `ContractLimits`
(data-model 8). New operationIds staged in `PendingArrangementOperations` (backend tests,
un-staged per story by the backend).

## 3. Audio engine 2.8.0

`setTimeline(plan | null)`, `getTimelinePosition()`, `getTimelineDuration()`,
`seekTimeline(seconds)`, `setTimelineLoop(loop)`, callback `OnTimelinePositionChanged(arranged,
segmentIndex)`, types `TimelinePlan`, `TimelineSegment`; source-time semantics of
`getPosition`/`OnPositionChanged`/karaoke kept; `setLoop(enabled: true)` refused while arranged;
recording refused while arranged. Phase 0 stubs throw `Error("TIMELINE_NOT_IMPLEMENTED")` (as
006 did for karaoke) until the engine tasks land; Web `EngineMethods.ContractVersion = "2.8.0"`.

## 4. Render vectors 1.2.0 (`timeline` section)

| Group | Cases | Consumers |
|---|---|---|
| `resolve` | 17: song-order, chorus-4x, reorder-mute, pickup-bar, hard-cut, max-crossfade, free-timing, unreliable-grid; errors off-bar-line, too-short, beyond-end, repeat-17, too-many-segments, too-long, unknown-mute, no-bars, crossfade-60 | backend `TimelineResolver` |
| `spans` | 6: single, jumps, hard-cut, contiguous-mute, parent-mute, repeat-self (runs per channel + `workerSegments` with `mutedInputs`) | engine run builder, worker renderer, backend `TimelineExportMapper` |
| `samples` | 3: crossfades, hard-cuts, mute-edges (every output frame, tiny F, two synthetic inputs) | worker renderer (1e-9), browser `OfflineAudioContext` (1e-4) |
| `clock` | chorus-4x at 9 arranged times (incl. boundaries and the end) | engine, frontend timeline display |
| `metronome` | reorder-mute: 64 arranged clicks with accents | engine metronome |

`gate`, `pan` and every section 015 adds in 1.1.0 are copied unchanged from the base.

## 5. UI test ids 1.18.0

See [ui-test-ids.delta.md](ui-test-ids.delta.md) (toolbar, song strip, region lane, inspector,
Expert fields, dialogs, export option, mix badge, Beginner chip, record-disabled reason; level
rules R19-1..R19-3).

## 6. In-app help (004 framework, area `mixer`)

| Topic id | Anchors (`data-help-id`) | Levels |
|---|---|---|
| `arrange` (panel topic) | `arrange-panel` | advanced, expert (Beginner text: "available from Advanced") |
| `arrange-regions` | `arrange-lane`, `arrange-region-*`, `arrange-song-strip` | advanced, expert |
| `arrange-repeat` | `region-repeat` | advanced, expert |
| `arrange-mutes` | `region-mute-*` | advanced, expert |
| `arrange-follow` (lyrics, score, sections, metronome follow what you hear) | `arrange-playhead` | all |
| `arrange-crossfade` | `arrange-crossfade` | expert |
| `arrange-snap` (bars vs free, unreliable grids, bar lines changed) | `arrange-snap`, `arrange-warning` | expert (summary for advanced) |
| `arrange-keyboard` | `arrange-lane` (F1) | all |
| `arrange-limits` | `arrange-error` | advanced, expert |
| `arrange-export` | `export-arrangement` | advanced, expert |
| `arrange-chip` (Beginner read-only view) | `arrange-chip` | all |
| glossary `arrangement` | - | all |

Feature catalogue ids (004 design-spec 5): `mixer.arrange` (Advanced),
`mixer.arrange.crossfade` (Expert), `mixer.arrange.free-timing` (Expert).
