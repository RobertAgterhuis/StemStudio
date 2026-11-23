# Hand-off: backend / 020 US1 stem quality report (T042-T045)

| Task pair | Red | Green |
|---|---|---|
| T042/T043 + T044/T045 | 25d885b9 | f0c22de6 (refactor 7913eed5) |

## What was built (`src/StemStudio.Api/Insights/`)

- `GET/POST /api/tracks/{trackId}/quality` (`getQualityReport`, `requestQualityReport`; moved to `InsightsOperations`).
- `QualityService`: check + store + start under **one lock** (`QualityGate.RunAsync`, LESSONS 023; test with six parallel
  POSTs -> one 202, five 409). 409 `track-not-ready`, 409 `quality-running`, 503 `worker-unavailable` (nothing stored);
  a worker refusal (4xx) is stored as a `failed` report with the worker's reason (202).
- `QualityInputs`: the six standard stems in catalogue order, then the kept detail stems (`detail`) and `rest` stems; split
  parents in `detailParents`; `StemsVersion` = hash of channel, size, mtime of the files.
- `QualityJobApplier` (job sync tick): progress, ready (previous ratings kept), failed (last ready stems kept), lost job;
  **the worker result is checked** (`QualityResultRules`: version, channel allow-list, standard `mainSource`, finite 0..1
  indices, unique kinds) before it is stored.
- `QualityTrigger`: after standard and detail completion; setting `StemStudio:AutoQualityReport` (compose passes
  `StemStudio__AutoQualityReport`, default true).
- `QualityView`/`QualityMapper`: `outdated` = analyzer version differs from `/health.insights.analyzerVersion` (cached
  1 min; unknown when the worker is down) or the stem files changed; reason texts of data-model 4.6; fixed disclaimer;
  `Track.quality` badge on `getTrack`/`listTracks` (via `TrackViewsProjection`).

## Decisions (please confirm)

1. **Four-stem tracks** (htdemucs fallback): the worker needs all six standard stems (422), so POST answers 409
   `track-not-ready` with the detail "The quality report needs the six-stem separation ..." and the automatic run skips
   them. `track-not-ready` was described as "not completed"; this is a wider reading.
2. `detailParents[].modelResidualDb/reconstructionErrorDb` are sent as null: 002 does not store them.
3. `QualitySummary` counts every row of the report (standard, detail and rest stems).
4. The automatic run is skipped while a report is queued/running (a detail split finishing during a run starts no
   second run; "Check again" does).
5. Quality changes bump `Track.UpdatedAt` (like the detail phase).

## For qa (their assumptions, 020 TEST-STRATEGY 23.4)

- Reports are awaited by `measuredAt` changing: yes (set when ready; no job id exposed).
- Tip codes change after the fake detail split: yes, without re-analysis (`try-detail-separation` disappears); the
  automatic report after the split then measures the children (`use-detail-stem` when a child is better).
