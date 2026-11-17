# Hand-off: backend 015 (lane L4) - HO:us2..us7 + HO:polish (T086)

Lane L4 of round 014+015, branch `014-015-round-lane-l4-backend015`. Previous checkpoint:
[backend-015-foundational-us1.md](backend-015-foundational-us1.md) (decisions D1-D8, security self-review).

## Tasks done since HO:us1 (all ticked)

| Story | Tasks | Content |
|---|---|---|
| US2 | 015 T047-T048 | calibrations: list (by label), upsert by trimmed label under the take lock, delete (takes keep their latency) |
| US3 | 015 T054-T055 | `deleteRecordingTakes`: unknown/other-track ids ignored, count returned, 1..200 distinct ids |
| US4 | 015 T061-T062, 014 T056-T057 | `MixSettings.take` and `loop.sectionId`; ids the track does not have are dropped on save and load (`MixReferences`); duplicate copies the take; deleting a take nulls it |
| US5 | 015 T066-T067 | update (rename syncs the asset name, star, part), delete (render cancelled, files, hashes, mix references), track-delete cascade, `Track.takeCount` |
| US6 | 015 T071-T072 | exports with the take: `RenderTake`, `TakePlan` (gate rule with the take strip, take last, contents "Take n - ..."), raw take over its span (explicit range intersected), `take-required` / `take-not-in-track` / `take-not-ready` / 404, `Provenance.sourceAssetIds`, recipe `takeId`, retry re-resolves the take; render-vectors **1.1.0** consumed by `RenderPlannerTests` (20 cases; `Domain.Tests.csproj` link switched to the 015 file) |
| US7 | 015 T079-T080 | `replaceRecordingTakeAudio`: old audio served until the new one is ready (and after a failed render), revision + 1, summed adjustment, `take-saving`, start sample 0 with a shorter WAV (engine cut), silence/length/range refusals change nothing |
| 014 | T107, T108 | merge restore brings `TrackSections`+`Sections` of merged tracks; setlists and practice sessions are not merged; the backup scrub drops `ScoreViews` (views are made again, D7); `ManifestBuilder.BackendApiVersion` = 1.14.0 = `info.version` of the linked full document |

## Contract change (orchestrator decision, D8)

backend-api 1.14.0 amended in place, additive and optional (absent when null): `ExportMix.take`
(`TakeChannelSettings`), `ExportRecipe.takeId`. Edited: `recording-api.openapi.yaml` (`ExportMixAdditions`,
`ExportRecipeAdditions`, changelog line), `merge_recording_delta.py` (rule + changelog), the regenerated full
`backend-api.openapi.yaml` (`--check` up to date), `check_superset.py` (`ExportMix` may grow; superset yes against
014's 1.13.0 and against the delta). C#: `ExportMixDto.Take`, `ExportRecipeDto.TakeId`.
`RecordingContractTests.Changed1130Entries` lists `ExportMix`. Commit bf224362 (contract), e5498aa2 (behaviour).
Redocly was not run in the lane (no stack / npx run); please include it in the integration lint.

Behaviour: request `mix.take` uses the sent strip; `mixId` uses the saved mix's `TakeChannel` (a stale take there is
ignored); raw mode uses `takeId`; the recipe stores and returns `takeId`.

## Further decisions

| # | Decision | Why |
|---|---|---|
| D9 | A take export that is retried after its take was deleted is `409 source-unavailable` | same rule as a deleted track (007) |
| D10 | Raw export of the take alone: no range = the take's span; a requested range is intersected with the span, no overlap = 422 `range` | data-model 2.7 |
| D11 | The audio file of an item is served whenever it is listed (any status) | a take being stored again or whose re-render failed keeps its previous audio playable (data-model 2.4); exports never list files before ready, so 007 is unchanged |

## Evidence (HO:polish, head 612ab136 + this note)

| Component | Tests | Line | Branch | Method |
|---|---|---|---|---|
| StemStudio.Domain | 1425 passed | 99.00 % | 94.07 % | 98.43 % |
| StemStudio.Persistence | 164 passed (1 L3 test excluded, see merge note) | 99.39 % | 95.12 % | 92.73 % |
| StemStudio.Api (+Contracts 100 %) | 3053 passed, 4 skipped | 98.13 % | 90.93 % | 98.94 % |

`dotnet build StemStudio.slnx`: 0 warnings. `dotnet format StemStudio.slnx --verify-no-changes`: clean.
`tools/quality/run_all.py src/StemStudio.{Domain,Persistence,Api}`: PASS (0 errors, 0 clones - jscpd ran, 0
exemptions). No new dependencies.

## Security self-review (delta since HO:us1)

- `replaceRecordingTakeAudio` uses the same reader (body limit, WAV header checks, spool in the asset's own GUID
  folder) and is write-gated; a silent or refused replacement never overwrites `capture.wav` or `audio.flac`.
- Export inputs for the take are built from the take's asset GUID only (`assets/<GUID>/audio.flac`), never from the
  request; the worker allow-list is unchanged.
- Merge restore adds archived rows only through the planner (no DDL from the archive); score-view rows are scrubbed so
  no restored row points at files that are not in the archive.

## Merge notes

- L3's `PracticeFlowPersistenceTests.MixLoopJson_WithASectionIdKey_StillLoads` must expect the region with
  `SectionId` after the L3+L4 merge (T057, fdd5aa4d; recorded by the orchestrator).
- Migration `20261117215712_AddRecordingTakes` sorts after `AddPracticeFlow`; the snapshot contains L3's model plus
  015 (`has-pending-model-changes` clean). Any later model change of either lane needs a new migration on top.
- L6: the export UI sends `mix.take` for an unsaved mixer state; `PUT /api/takes/{id}/audio` accepts start 0 and a
  shorter WAV.

## Help/docs

No user-facing text in this lane (L6 owns the recording help topics).

## Refactor: 015 T007 dependency rule (Assets/Tracks -/- Api.Recording)

The rule (cherry-picked from L2, 194770bc) flagged four files. Fixed by dependency inversion, behaviour unchanged:

- `Tracks/ITrackTakesCleanup.cs`: the interface moved from `Recording/TakeDeletion.cs` to Tracks (namespace
  `StemStudio.Api.Tracks`); `TakeDeletion` still implements it.
- `Assets/IAssetFinisher.cs`: new seam for `AssetSynchronizer`; `TakeAssetFinisher` implements it
  (`AddScoped<IAssetFinisher, TakeAssetFinisher>`).
- `Assets/ITakeExportSource.cs` (with `ResolvedTake`): new seam for `ExportPlanner` and `AssetRetryService`;
  `TakeExportResolver` moved to `Recording/` and implements it (`AddScoped<ITakeExportSource, TakeExportResolver>`).
- `managed-by-take` moved from `RecordingProblems` to `AssetProblems.ManagedByTake()` (same type, title, detail,
  status; type code from `Contracts.Recording.RecordingProblemTypes`). `AssetOriginPolicy` stays a static Assets policy:
  the origin check is a Domain concept, only the problem text pointed at Recording, so no `IAssetOriginPolicy` was needed.
- New test `Recording/RecordingSeamWiringTests.cs`: `AddRecording` resolves each seam to the recording implementation,
  registered once, scoped. The track delete still requires `AddRecording` (as before; it is always composed).
