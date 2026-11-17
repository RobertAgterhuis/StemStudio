# Hand-off: backend 015 (lane L4) - HO:foundational + HO:us1

Lane L4 of round 014+015, branch `014-015-round-lane-l4-backend015` (from design head 5e67d97a, merged L3
786a55af, 89bf0338 and HO:foundational b66b033f). Contract labels: backend-api 1.14.0 (full document
`specs/015-play-along-recording/contracts/backend-api.openapi.yaml`), render-vectors 1.1.0 (consumer in US6).

## Tasks done (ticked)

015: T020-T025, T025a, T025b (foundational), T036-T039 (US1), and ahead of plan T047-T048 (US2 calibrations),
T054-T055 (US3 batch delete), T061-T062 (US4 mix take), T066-T067 (US5 update/delete/cascade/takeCount).
014: T056-T057 (`LoopRegion.sectionId`). Endpoint `replaceRecordingTakeAudio` (US7) is implemented; its own
test task T079 follows. Open: 015 T071-T072 (export with take, incl. the contract amendment below), T079-T080
tests, 014 T107, T108, T086 HO:polish.

## What was built

- Domain `Recording/`: `TakeWavReader` (forward-only header validation, data-model 2.3), `PeakScanner`
  (streamed 24-bit peak/clipped/all-zero), `TakeWavPadder` + `PaddedCapture` + `ExactCopy` (canonical header,
  zero frames, exact data copy), `LatencyConversion`, `TakeRules`, `TakeNaming`, `RecordingTake`,
  `InputCalibration` + `CalibrationValues`; `AssetOrigin.Recording`, `Provenance.Recording`
  (`RecordingSource`), `Asset.CreateRecording`, `Asset.Rerender`; `Mixes/TakeChannelSettings`, `Mix.Take`,
  `Mix.RemoveTake`, `LoopRegion.SectionId`.
- Persistence: `StemStudioDbContext.Recording.cs`, `Configurations/RecordingConfiguration.cs`,
  `MixConfiguration` (`TakeChannel` JSON), `Repositories/RecordingRepository.cs`; migration
  `20261117215712_AddRecordingTakes` (after 014's `AddPracticeFlow`; `has-pending-model-changes` clean).
  `AssetRepository`/`LibraryRepository` (ownership granted by the orchestrator): origin filter and
  "not a recording" in SQL (`json_extract(Provenance,'$.Origin')` sub-query, also covers rows restored
  without their take).
- Api `Recording/`: endpoints of tag `recording` (all 11 operations served), `TakeUploadReader`
  (multipart, parts in any order), `TakeUploadService`, `TakeAudioReplacement`, `TakeService`,
  `TakeDeletion` (+ `ITrackTakesCleanup` in the track delete), `CalibrationService`, `TakeAssetFinisher`
  (hook in `AssetSynchronizer`), `TakeRenderer`, `TakeDiskGuard`, `TakeLock`, `TakeFiles`, `TakeMapper`,
  `RecordingRegistration` (filled stub). `Assets/AssetOriginPolicy` (409 `managed-by-take` on rename,
  delete, retry), `AssetJobApplier` (FLAC 24 for takes; a failed copy keeps a take's capture),
  `AssetService.GetFileAsync` serves a listed file in any status (the previous take audio stays playable
  while it is stored again). `Mixes/MixReferences` drops section and take ids the track does not have.
  `Tracks/TrackService` sets `takeCount`.
- 013/022: `BackupScrubber` removes take rows of scrubbed items and clears `Mixes.TakeChannel`;
  `MergePlanner` + `RecordingMerge` (calibrations by label, live wins; takes only with their item; a take's
  mix kept only when merged or live); `LibraryFilingRules.IsFileable` false for recordings.

## Decisions (no contract deviation)

| # | Decision | Why |
|---|---|---|
| D1 | Upload parts may come in either order: the audio data is spooled (`assets/<id>/<guid>.upload.tmp`) after the header was validated and the disk checked, then padded into `capture.wav` | the contract does not fix the part order |
| D2 | Take number = highest existing + 1 (data-model "max + 1"); deleting the newest take frees its number | as written in data-model 2.1 |
| D3 | Unknown `calibrationId`/`mixId` in the upload metadata are stored as null instead of failing | informational links; avoids an FK error |
| D4 | 422 problems of the take endpoints carry an `errors` map (`metadata.<field>` or `audio`) | the full document's `TakeUnprocessable` is `ValidationProblemDetails` with required `errors` |
| D5 | Digital silence is never written as `capture.wav` (a silent replacement keeps the old capture) | retry of a failed take must still work |
| D6 | Take ranges in mix values (`take.volume`/`pan`) are 400 like every other mix value | existing 002 mix behaviour |
| D7 | 014 T107 (approved by the orchestrator): backups do not archive `assets/<id>/view/`; the scrub deletes `ScoreViews` rows; merge brings sections, not score views; views regenerate lazily (`getScoreView` -> `none`) | 013's layout allow-list stays closed |
| D8 | Contract amendment approved: `ExportMix.take` and `ExportRecipe.takeId` (optional, absent when null) in 1.14.0 | follows in the T071 commit |

## Security self-review (uploads, WAV headers, paths)

- Body: endpoint-specific limit (`StemStudio:Recording:MaxUploadBytes` + 1 MiB) via
  `IHttpMaxRequestBodySizeFeature` and `MultipartReader.BodyLengthLimit`; `Content-Length` above it is 413
  before reading; metadata part capped at 64 KiB.
- WAV: every size checked before use (chunks before `data` <= 64 KiB in total, so a 4 GB chunk size never
  allocates or skips; declared `data` > limit is 413; odd/partial frames and truncation are 400); extensible
  subformat must be PCM. Tests: hostile sizes, truncated header/data, wrong format fields, duplicate parts,
  no multipart, oversized metadata.
- Paths: no request value reaches a path; take files are `assets/<GUID>/capture.wav` and
  `<GUID>.upload.tmp`, resolved through `DataDirectory.RequireContainedPath`; the worker input is built from
  the asset GUID only. A refused upload leaves no folder (asserted in every refusal test).
- Concurrency: one `TakeLock` around count/number check, capture, insert and render start (LESSONS);
  calibration upserts under the same lock.
- Gate: `deleteRecordingTake`, `deleteRecordingTakes`, `replaceRecordingTakeAudio` use `RequireGateOpen`
  (test: 409 `backup-running`); maintenance 503 comes from the 013 middleware.

## Evidence (this checkpoint)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 1415 passed | 98.99 % | 94.12 % |
| StemStudio.Persistence | 164 passed (+1 L3 test, see merge note) | 99.39 % | 95.12 % |
| StemStudio.Api (+Contracts) | 3035 passed, 4 skipped | 97.76 % | 90.47 % |

`tools/quality/run_all.py src/StemStudio.{Domain,Persistence,Api}`: PASS, 0 errors, 0 clones (jscpd ran).
`dotnet format StemStudio.slnx --verify-no-changes`: clean. Build: 0 warnings. No new dependencies.

## Merge notes

- After L3+L4 merge, L3's `PracticeFlowPersistenceTests.MixLoopJson_WithASectionIdKey_StillLoads` must
  expect the region **with** `SectionId` (T057, commit fdd5aa4d) - recorded by the orchestrator.
- `BackendContractTests.StemsAndMixes.cs`: `PendingRecordingOperations` keeps the 11 ids (the L3-owned
  operation-id test lists them through it); the served-operations test no longer excludes them.

## Help/docs

No user-facing text in this lane; L6 owns the recording help topics.
