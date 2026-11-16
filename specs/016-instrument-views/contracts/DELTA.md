# Contract delta: feature 016 Instrument Views

Feature 016 changes four contracts additively (roadmap allocation, ADR-033 D8) and adds one
shared test-vector file. Every change is **additive** -> next **minor** of each document; no
existing operation, schema value, id or engine call changes meaning, with one documented
behaviour fix (the 004 `play-along-<stem>` presets now mute whole families, section 6).

## Status (planning, 2026-11-16)

| Contract | Base (assumed merged before 016 Phase 0) | 016 document now | Phase 0 result |
|---|---|---|---|
| Worker API | 1.8.0 (014; full doc on `main`: 008 1.6.0) | [worker-api.delta.openapi.yaml](worker-api.delta.openapi.yaml) `1.9.0-delta.1`, standalone, Redocly-valid (OpenAPI 3.1) | full `worker-api.openapi.yaml` **1.9.0** (T002) |
| Backend API | 1.14.0 (015; full doc on `main`: 010 1.9.0) | [backend-api.delta.openapi.yaml](backend-api.delta.openapi.yaml) `1.15.0-delta.1`, standalone, Redocly-valid | full `backend-api.openapi.yaml` **1.15.0** (T003) |
| Audio engine | 2.5.0 (015; full doc on `main`: 006 2.1.0) | [audio-engine.delta.md](audio-engine.delta.md) (draft) | `audio-engine.md` **2.6.0** + `types.ts` (T004) |
| UI test ids | 1.14.0 (015) | [ui-test-ids.delta.md](ui-test-ids.delta.md) (draft) | `ui-test-ids.md` **1.15.0** (T004) |
| Help content | 004 1.0.0 + later minors | section 5 (area `instruments`) | next minor of `help-content.md` / `help-topic.schema.json` (T004) |
| Chord spelling vectors | - | section 7 (format) | `chord-spelling-vectors.json` **1.0.0** (T004) |

Lint (2026-11-16): `npx @redocly/cli@2.78.0 lint --config specs/016-instrument-views/contracts/redocly.yaml
<both delta files>` -> valid, 15 warnings: 13 `no-unused-components` (the `*Extension` merge
schemas, `TrackKeySummary` for `Track.key` and the `channel` parameter for `listAssets`, which are
merge instructions) and 2
`operation-4xx-response` (idempotent `DELETE` of an analysis job; static `GET
/api/instruments/catalogue`). The OpenAPI documents win over this summary on any difference.

## Assumptions about 014 and 015 (reconciled in Phase 0, T001)

| 016 needs | From | Assumed shape | If different |
|---|---|---|---|
| Score view with score-follows-playback for any score item | 014 (UI, engine 2.4.0) | a Blazor component taking an asset id, highlighting the current bar from the item's bar timeline | 016 passes drum/tab/part items to whatever 014 exposes; no 016 contract change |
| Sections of a track | 014 (backend 1.13.0) | list of `{startMs, endMs, label}` | singing units fall back to 4-bar blocks (data-model 3.4) |
| Count-in on/off and bars | 012 (backend 1.11.0, engine 2.3.0) | mixer/metronome setting | "Drumless" sets it through the 012 call; restore stores it |
| Transpose in semitones | 012 (engine 2.3.0) | engine-internal state | chord display reads it; no new public call |
| Selected input, round-trip latency, takes | 015 (backend 1.14.0, engine 2.5.0) | engine-internal input node and latency state; takes with a file URL | singing wiring adapts in the engine only |

## 1. Worker API (1.9.0)

New tag `instrument-analysis`, lane `instrument-analysis` (one job at a time):

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| POST | `/v1/instrument-analysis-jobs` | `createInstrumentAnalysisJob` | `InstrumentAnalysisJobRequest` = `oneOf` `ChordsJobRequest` / `DrumHitsJobRequest` / `VocalPitchJobRequest` (discriminator `kind`) | `202 InstrumentAnalysisJob`; `400 invalid-input-path`; `404 input-not-found`; `422` |
| GET | `/v1/instrument-analysis-jobs/{analysisJobId}` | `getInstrumentAnalysisJob` | - | `200 InstrumentAnalysisJob` (result inline: `ChordChart` / `DrumHits` / `VocalPitch`); `404` |
| DELETE | `/v1/instrument-analysis-jobs/{analysisJobId}` | `deleteInstrumentAnalysisJob` | - | `204` (cancel + remove, idempotent) |

Scope addition S11 (key detection): the `chords` job also returns the song key -
`ChordChart.key` (`AnalysisKey` + `alternative`) and `ChordChart.keyRegions` (`KeyRegion[]`, >= 8
bars, <= 50), warning `key-uncertain`; no separate job or kind (research R-13). Ports:
`KeyEstimator` (`estimate(chroma, chords, grid) -> (AnalysisKey | None, tuple[KeyRegion, ...])`).

Jobs persist as `instrument-analysis-jobs/<id>.json`; restart -> `failed` "interrupted by
restart"; 24 h sweep (as 007/008 jobs). Fixed texts: `NO_MELODY_TEXT` ("The vocal stem is silent,
so no melody can be shown."), `ANALYSIS_WARNING_TEXTS` (data-model 2.4, 3.1).

Score pipeline (`/v1/transcription-jobs`, additive): `TranscriptionJobRequest` + `tablature`
(`TablatureSettings`), `part` (`PartSettings`), `chords` (`ScoreChordSymbol[]`), `preferFlats`,
`singlePiece`; `ScoreProfile` + `strings`, `brass`, `woodwinds`; `ScoreSummary` + `tablature`,
`part`, `chordsIncluded`; `ScoreWarningCode` + `tab-octave-moved`, `tab-notes-dropped`,
`tab-wide-stretch`, `part-out-of-range`, `section-reduced`. Validation (422): tablature only for
guitar/bass with a matching tuning family, capo only for guitar; part not for drums; clef not for
keys/piano; chords not for bass/drums; `singlePiece` only for drums with exactly that piece in
`drumInputs`. Behaviour: GM drum MIDI map; MIDI at sounding pitch.

`Health` + `instrumentAnalysis {models {btc-majmin, btc-large-voca: ready|not-downloaded},
chordEngine, pitchEngine}`; `LanesHealth` + `instrumentAnalysis`.

Python (Phase 0, architect): `schemas.py` (`CONTRACT_VERSION = "1.9.0"`, all schemas above);
`ports.py`: `ChordFrames`, `ChordRecognizer` (`engine`, `recognize(audio, progress) ->
ChordFrames`), `ChordPostProcessor` (`segments(frames, grid, vocabulary, bass) -> ChordChart`),
`PitchAnalyzer` (`analyze(path, progress) -> VocalPitch`), `TabFingerer`, `PartWriter`,
`HarmonicMixer` (`mix(paths) -> mono 22,050 Hz array`), errors `AnalysisError` >
`NoMelodyError`, `AnalysisCancelledError`; constants `BTC_MAJMIN_WEIGHTS`,
`BTC_LARGE_VOCA_WEIGHTS`, `BTC_GPU_NEED`, `TUNINGS`, `WRITTEN_INSTRUMENTS`, `GM_DRUM_MAP`;
`config.py` `InstrumentAnalysisSettings(chord_engine, pitch_engine, threads)`.

## 2. Backend API (1.15.0)

New tag `instrument-views`:

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| GET | `/api/tracks/{trackId}/chords` | `getChords` | - | `200 ChordsAnalysis`; `404` |
| POST | `/api/tracks/{trackId}/chords` | `detectChords` | `DetectChordsRequest {vocabulary, engine, overwriteEdits}` (optional body) | `202`; `404`; `409 track-not-ready`, `edits-exist`, `analysis-running`; `422`; `503` |
| DELETE | `/api/tracks/{trackId}/chords` | `deleteChords` | - | `204`; `404` |
| PUT | `/api/tracks/{trackId}/chords/segments/{segmentIndex}` | `editChordSegment` | `EditChordRequest {label}` (Harte pattern) | `200 ChordsAnalysis`; `404`; `409 chords-not-ready`; `422` |
| GET | `/api/tracks/{trackId}/chords/sheet?transpose&capo` | `getChordSheet` | - | `200 ChordSheet` (`no-store`); `404`; `409 chords-not-ready` |
| GET | `/api/tracks/{trackId}/chords/chordpro?transpose&capo` | `downloadChordPro` | - | `200 text/plain` attachment `<title>.cho`; `404`; `409` |
| GET / POST / DELETE | `/api/tracks/{trackId}/drum-hits` | `getDrumHits` / `detectDrumHits` / `deleteDrumHits` | - | as chords; POST `409 drums-not-split`, `analysis-running` |
| GET / POST / DELETE | `/api/tracks/{trackId}/vocal-pitch` | `getVocalPitch` / `analyseVocalPitch` / `deleteVocalPitch` | - | as chords; POST `409 track-not-ready`, `no-vocals`, `analysis-running` |
| GET | `/api/instruments/catalogue` | `getInstrumentCatalogue` | - | `200 InstrumentCatalogue` |
| GET | `/api/tracks/{trackId}/key?transpose&instrument` | `getKey` | - | `200 KeyView` (S11: key, display at the transpose, written key, regions with start bars); `404` |
| GET | `/api/tracks/{trackId}/chords/capo-suggestions?transpose` | `getCapoSuggestions` | - | `200 CapoSuggestions` (S12: shortcut + up to 3 suggestions, research R-14); `404`; `409 chords-not-ready` |

Additive changes: `Track.instrumentViews {chords, drumHits, vocalPitch}` (statuses);
`Track.key` (`TrackKeySummary`, S11, shown next to the BPM); `ChordChartDocument.key`
(`SongKey`) and `keyRegions`; chord sheet, ChordPro and every display spell by the region key
(data-model 2.5);
`CreateScoreRequest`/`ScoreRecipe` + `tablature`, `part`, `includeChords`; `ScoreOptions` +
`tablature`, `parts`, `chords`; `ScoreFacts` + `tablature`, `part`, `chordsIncluded`;
`ScoreProfile` and `ScoreWarningCode` values as the worker; `ScoreConflict` + `chords-not-ready`;
`ScoreUnprocessable` + `tablature-not-allowed`, `capo-not-allowed`, `tuning-mismatch`,
`clef-not-allowed`, `part-not-allowed`; `listAssets` + `channel` (with `trackId`); sources
`{trackId, channel: drums.<piece>}` with profile `drums`.

.NET (Phase 0, architect): `src/StemStudio.Contracts/InstrumentViews/{AnalysisStatusDto,
InstrumentViewsStatusDto, TrackAnalysisDto<T>, ChordChartDto, ChordSegmentDto, ChordQualityDto,
DetectChordsRequest, EditChordRequest, ChordSheetDto, DrumHitsDto, VocalPitchDto,
InstrumentCatalogueDto, TuningDto, WrittenInstrumentDto, ClefChoiceDto, InstrumentFamilyDto}.cs`;
`Scores/{ScoreTablatureDto, ScorePartDto, TablatureFactsDto, PartFactsDto}.cs` and the additive
properties; `TrackDto.InstrumentViews`; `KeyViewDto`, `SongKeyDto`, `KeyRegionDto`,
`TrackKeySummaryDto`, `CapoSuggestionsDto`; `ApiRoutes.{Chords, ChordSegment, ChordSheet,
ChordPro, Key, CapoSuggestions, DrumHits, VocalPitch, InstrumentCatalogue}`; `ContractLimits` additions (data-model 7.6); the new
operation ids staged in `PendingInstrumentViewOperations` (un-staged by the backend per story).

## 3. Audio engine (2.6.0)

[audio-engine.delta.md](audio-engine.delta.md): `attachChordLane`, `detachChordLane`,
`setKaraokeChords`, `setChordDisplay`, `attachHitLanes`, `setHitLaneOptions`, `detachHitLanes`,
`getRoundTripLatency`, `startSingingFeedback`, `setSingingTolerance`, `stopSingingFeedback`,
`scoreTake`; callbacks `OnSingingUnitScored`, `OnSingingStateChanged`; second build output
`wwwroot/js/audio-engine-pitch.worklet.js`.

## 4. UI test ids (1.15.0)

[ui-test-ids.delta.md](ui-test-ids.delta.md).

## 5. In-app help (004 framework, area `instruments`)

Help-content minor: area enum + `instruments`. Topics (`src/StemStudio.Web/Help/topics/instruments/`):

| Id | Anchors | Levels | Content |
|---|---|---|---|
| `instrument-views` (screen topic of the views region) | `practise-as` | all | the five views, "Practise as", minus-one mixes per family |
| `chords` | `chords`, `chord-sheet` | all | lane, banner, sheet, capo, transpose; limits; BTC credit (MIT, Park et al. 2019) |
| `song-key` | `song-key` | all | key next to the tempo, key changes, transposed and written keys, uncertainty, limits |
| `capo-mode` | `capo-mode` | all | capo badge, shapes vs sounding chords, suggestions, "original shapes" after a transpose, guitar only |
| `chords-advanced` | `chords-vocabulary`, `chord-edit` | advanced, expert | Full vocabulary, editing, ChordPro, fast method |
| `tablature` | `score-tablature`, `score-tab-tuning` | all | reading tab, tunings, capo, octave moves, limits of fingering |
| `midi-download` | `midi-*` | all | what the MIDI action does, concert pitch, GM drums |
| `drum-trainer` | `drum-trainer`, `drum-lanes`, `drumless` | all | lanes, guide piece, drum notation, split first |
| `singing-feedback` | `singing`, `pitch-view` | all | microphone and headphones, pitch view, scores, privacy (audio stays in the browser) |
| `singing-tolerance-latency` | `singing-tolerance`, `singing-latency` | advanced, expert | tolerance per level, calibration (015) |
| `transposed-parts` | `score-part`, `score-part-clef` | all | B-flat/E-flat/F instruments, clefs, MIDI at concert pitch, range warnings |

Feature-catalogue ids: data-model 6. Glossary additions (`glossary` area): `capo`, `tablature`,
`transposing-instrument`, `cent`, `minus-one`.

## 6. Behaviour fix in 004 presets

`play-along-<stem>` presets (004 data-model `PracticePresets`) mute every channel of the stem's
family (data-model 5.3), including the detail channels of expanded parents. Previously only the
standard stem was muted, which left expanded detail channels audible. Preset ids and test ids are
unchanged; the help topic `instrument-views` documents it.

## 7. Chord spelling vectors (`chord-spelling-vectors.json`, written in Phase 0)

```json
{ "version": "1.0.0",
  "cases": [ { "root": 9, "quality": "min7", "bass": 7, "keyTonic": 0, "keyMode": "major",
               "transpose": 0, "capo": 0, "symbol": "Am7/G" } ] }
```

At least 200 cases: every quality, every root at transpose 0, flat and sharp keys, region keys
(a chord spelled by its region, not the global key), transpose -12..12 samples, capo 0..12
samples, slash chords, `N` (symbol "N.C."). Plus `keys` (key display incl. transposed and written
keys), `capo` (`{root, quality, bass, transpose, capo, shape}` for every root x quality x capo
0..12) and `capoFixtures` (four progressions with expected suggestion lists, research R-14). Consumed by
`tests/StemStudio.Domain.Tests/InstrumentViews/ChordSpellingTests.cs` and
`src/audio-engine/tests/chords/chord-spelling.test.ts`; generated by
`specs/016-instrument-views/contracts/generate_chord_spelling_vectors.py` (architect, as 007's
render vectors) from the data-model 2.5 rule.