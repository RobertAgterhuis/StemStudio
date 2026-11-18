# Implementation Plan: Instrument Views

**Branch**: `016-instrument-views` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/016-instrument-views/spec.md`; constitution 1.3.1
(principle IV with the ADR-009 weights exception); `docs/MoSCoW.md` (S1, S4, S9, S10, "every
instrument counts equally"); `docs/ROADMAP.md` (016: backend-api 1.15.0, worker-api 1.9.0,
audio-engine 2.6.0, ui-test-ids 1.15.0; depends on 008, 014, 015); features 002, 003, 006, 008.

**Phase 0 (2026-11-18)**: done on `016-round` (after 014+015 merged); labels confirmed (worker 1.9.0, backend
1.15.0, engine 2.6.0, UI ids 1.15.0, help-content 1.1.0, chord vectors 1.0.0); outcome in DELTA.md and
`docs/handoffs/architect-016-design.md`; task amendments in tasks.md "Phase 0 ready-check amendments".

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions
are in ADR-031 (chords), ADR-032 (singing pitch) and ADR-033 (architecture, tablature, parts);
evidence in [research.md](research.md); entities in [data-model.md](data-model.md); contract
changes in [contracts/DELTA.md](contracts/DELTA.md). 014 and 015 are specified in parallel; their
artefacts are assumptions reconciled in Phase 0 (T001).

## Summary

One view per instrument family, all on the CPU:

- **Harmony**: the worker's new `instrument-analysis` lane sums the harmonic stems, runs the BTC
  chord transformer (vendored MIT code, MIT weights, PyTorch through the 003 `DeviceRunner`; a
  librosa chroma + HMM fallback without weights), snaps chord changes to the beat grid and stores
  a chord chart per track. The engine draws a chord lane and chord symbols above the karaoke
  words; the backend builds a chord sheet for the lyrics card and a ChordPro download; the 008
  score pipeline can place chord symbols above lead sheets. Display spelling follows the 012
  transpose and a capo from one shared rule.
- **Harmony, scope addition (CTO 2026-11-16)**: the same chords job detects the **song key and
  key regions** (own chroma HMM with key profiles plus chord evidence, research R-13), shown next
  to the tempo for every family (transposed key with 012, written key for transposing
  instruments) and used for chord spelling per region; **capo mode** for the guitar family shows
  shapes relative to a capo with a badge, a deterministic backend `CapoAdvisor` suggests up to
  three easy capo positions and the "original shapes" capo after a transpose (research R-14).
- **Guitar and bass**: the 008 notator gains a tablature step (Viterbi fingering over the
  catalogue tunings and capo; MusicXML TAB staff; Verovio engraving), and every channel gets a
  one-click MIDI action (GM drum map; concert pitch).
- **Drums**: the 008 drum onset detection runs as a `drum-hits` analysis after the drum split;
  the engine draws five hit lanes; "Drumless" applies the drum family's minus-one mix with
  metronome and count-in; the 008 drum score is shown with 014 score-follows-playback.
- **Vocals**: the worker analyses the lead vocal's reference melody with pYIN once; the engine
  tracks the singer live with an own YIN in an AudioWorklet on the 015 input, aligns with the 015
  round-trip latency, draws a pitch view and scores lyric lines octave-independently; a 015 take
  can be scored offline. No audio or score leaves the browser.
- **Strings, brass, woodwinds**: new 008 profiles and a written-instrument catalogue (B-flat,
  E-flat, F, concert) with clef choice; music21 transposes the part, MIDI stays at concert pitch;
  parts keep their bars so 014's score view follows them unchanged.
- **Equal entry point**: "Practise as ..." applies the family's minus-one mix and opens its view;
  the 004 `play-along-<stem>` presets mute whole families.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0).

**Primary Dependencies**: **none new** in Python, .NET or npm. Worker reuses torch 2.15.0,
librosa (CQT, chroma, pyin, viterbi), numpy, music21 10.5.2, verovio 6.4.0, soundfile. Vendored
(no package): BTC model definition, transformer layers and label tables from
`jayg996/BTC-ISMIR19` at `2682317b` (MIT, LICENSE kept). Weights (model cache, first use):
`btc-majmin` and `btc-large-voca` (MIT, 12 MB each, SHA-256 in data-model 1). Engine: own YIN and
chord speller (no `pitchy`/`pitchfinder`).

**Storage**: SQLite table `TrackAnalyses` (migration `AddTrackAnalyses`); `Assets.Score` JSON
gains tablature/part facts (no migration: JSON column); worker `instrument-analysis-jobs/`.

**Testing**: worker post-processing, fingering, parts and pitch packing on synthetic data
(triads rendered with numpy, sung sines, golden fingering fixtures, transposition vectors for
every catalogue instrument); BTC runtime with a fake torch module; real BTC and pYIN only in
`@pytest.mark.integration` tests. Backend: xUnit with the worker examples and shared chord
spelling vectors. Engine: Vitest with fake worklet/canvas/rAF, YIN accuracy on generated
signals. Frontend: bUnit with a fake API client and level context. E2E: Playwright on the fake
stack, one spec per family plus "Practise as" for all families, singing with Chromium fake audio
capture fixtures.

**Target Platform**: unchanged; CPU everywhere; the chord model may use the GPU (small); live
pitch tracking runs in the user's browser.

**Performance Goals**: chords and drum hits <= 30 s for 5 min (SC-001, SC-003); reference melody
<= 90 s for 5 min (P0); worklet <= 0.5 ms per block average (SC-004); all views <= 4 ms per frame,
95th-percentile frame time <= 16.7 ms (SC-007).

**Constraints**: OSI code only, no GPL import; `torch.load(weights_only=True)` only; no
subprocess/shell for downloads; no microphone data to the server; no timers for drawing.

**Scale/Scope**: one chord chart, one drum hit map, one reference melody per track; songs up to
15 min (4,000 segments, 40,000 hits, 90,000 frames).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every implementation task follows a failing-test task in the same owner sequence (tasks.md). Models sit behind ports (`ChordRecognizer`, `PitchAnalyzer`, existing `DrumTranscriber`) with fakes; post-processing, fingering, transposition, scoring and YIN are pure and tested on synthetic data; real BTC/pYIN only in marked integration tests (SC-001). Vendored BTC code excluded from coverage/SIG as earlier vendor folders, thin adapters tested. | PASS |
| II | SIG 4 stars | New worker packages `harmony/` (recognizers, post-processing, vendor), `analysis/vocal_pitch.py`, `notation/{tablature,parts,chord_symbols}.py`, `analysis_inputs/mixdown.py`; one generic job store/service for three kinds; backend one `TrackAnalyses` service with per-kind strategies; engine modules `timeline/`, `chords/`, `drums/`, `pitch/`, `singing/` (data-model 9). Dependency rules in tools/quality (T009). | PASS |
| III | Contracts first | Worker 1.9.0, backend 1.15.0, engine 2.6.0, UI ids 1.15.0, help-content minor; standalone OpenAPI 3.1 deltas are Redocly-valid now; Phase 0 writes the full documents, `schemas.py`, `ports.py`, Contracts DTOs, `types.ts`, shared chord-spelling vectors. All additive; ADR-031..033. | PASS |
| IV | Open source, lawful use | Code: BTC vendored (MIT), librosa (ISC), own YIN; GPL/AGPL candidates rejected (Chordino, Essentia, aubio, pitchfinder, Rubber Band via BTC's training code). Weights MIT (ADR-009 not needed, its download rules applied: cache, SHA-256, first use, never committed). Lyrics/chord sheets are personal practice material (`no-store`, existing notice). Microphone audio never leaves the browser. | PASS |
| V | Simplicity and honesty | No new packages; one analysis job resource; reuse of 006 clock/frame loop, 008 drum onsets and notation, 014 score view, 015 input/latency. Limits shown per analysis (FR-064), fast-method and out-of-date states visible; drum/instrument playing judgement explicitly out of scope. Audible timing unchanged; views draw from `AudioContext` time, never timers. | PASS |
| - | Tech & security | Stack unchanged. Inputs validated (Harte label pattern, tuning/profile combinations, capo, tolerance, paths per 007). Safe weight loading (allow-listed globals only). Engine sets text via `textContent` only. No secrets. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths (ownership map); hand-off per checkpoint (ADR-008); user stories per family independent so the wave runs in parallel. | PASS |

**Post-design re-check (after Phase 1)**: PASS. Two refinements recorded in Complexity Tracking
(fourth worker lane, second worklet module).

## Project Structure

### Documentation (this feature)

```text
specs/016-instrument-views/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md
    ├── worker-api.delta.openapi.yaml     # 1.9.0-delta.1 (valid); Phase 0: worker-api.openapi.yaml 1.9.0
    ├── backend-api.delta.openapi.yaml    # 1.15.0-delta.1 (valid); Phase 0: backend-api.openapi.yaml 1.15.0
    ├── audio-engine.delta.md             # Phase 0: audio-engine.md 2.6.0
    ├── ui-test-ids.delta.md              # Phase 0: ui-test-ids.md 1.15.0
    ├── redocly.yaml
    └── (Phase 0) chord-spelling-vectors.json, generate_chord_spelling_vectors.py
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py, schemas.py, config.py            # architect (Phase 0): data-model 1-5, DELTA 1
├── harmony/                                   # stem-separation (runtimes) + beat-analysis (post-processing)
│   ├── __init__.py          # create_chord_recognizer(settings, devices)            (stem-separation)
│   ├── btc.py               # CQT features, normalisation, 108-frame windows, DeviceRunner (stem-separation)
│   ├── vendor/btc/          # MIT LICENSE, commit header; excluded from SIG       (stem-separation)
│   ├── chroma_hmm.py        # fallback engine                                     (beat-analysis)
│   ├── postprocess.py       # beat snapping, merge, slash bass, preferFlats       (beat-analysis)
│   ├── key.py               # key HMM, regions, global key (S11)                  (beat-analysis)
│   └── labels.py            # Harte labels, vocabularies                          (beat-analysis)
├── analysis/vocal_pitch.py  # pYIN / YIN reference melody                         (beat-analysis)
├── analysis_inputs/mixdown.py   # harmonic stem sum, resampling                   (audio-ingest)
├── notation/tablature.py, parts.py, chord_symbols.py, drum_midi.py               (beat-analysis)
├── engraving/tablature.py   # TAB checks / fallback drawing (P0 decides)          (audio-ingest)
├── views_fake.py            # fake chords, pitch (drum hits reuse 008 fake)       (stem-separation)
├── jobs/instrument_analysis_{store,service}.py, api/instrument_analysis_routes.py, main.py (stem-separation)

src/StemStudio.Contracts/InstrumentViews/*.cs, Scores additions, ApiRoutes, ContractLimits      (architect)
src/StemStudio.Domain/InstrumentViews/{TrackAnalysis,AnalysisRules,HarteLabel,ChordSpeller,
    KeyFormatter,CapoAdvisor,ChordSheetBuilder,ChordProWriter,InstrumentCatalogue,FamilyRule}.cs  (backend)
src/StemStudio.Persistence (TrackAnalyses, migration AddTrackAnalyses)                           (backend)
src/StemStudio.Api/InstrumentViews/{ChordEndpoints,DrumHitEndpoints,VocalPitchEndpoints,
    CatalogueEndpoints,TrackAnalysisService,AnalysisKindStrategies,AnalysisJobApplier,
    AnalysisAutoStarter}.cs; Scores/ additions; Worker/WorkerClient.InstrumentAnalysis.cs        (backend)
src/audio-engine/src/{timeline/,chords/,drums/,pitch/,singing/}                                (audio-engine)
src/audio-engine/{package.json, vite.pitch.config.ts (fourth entry), src/types.ts}               (architect, Phase 0)
src/StemStudio.Web/Components/InstrumentViews/{PractiseAsMenu,ChordLane,ChordSheet,ChordEditDialog,
    DrumTrainer,DrumlessToggle,SingingPanel,PitchView,MidiAction,FamilyMixService}.razor/.cs,
    Scores/ScoreDialog additions, Help/topics/instruments/*.md                                  (frontend)
e2e/tests/instrument-{chords,tab,drums,singing,parts,practise-as}.spec.ts, playwright fake-mic projects,
    tests/fixtures/generate_instrument_fixtures.py                                               (qa)
docker/web image (worklet asset), docker-compose.yml env, .github CI (CONTRACTS_DIR, fake-mic)  (devops)
tools/quality rules                                                                              (quality-gate)
```

**Dependency direction**: worker `api -> jobs -> ports <- {harmony, analysis, analysis_inputs,
notation, engraving}`; `harmony.postprocess`/`labels`/`chroma_hmm` import only `ports`, `schemas`,
numpy, librosa; `harmony.btc` imports `ports`, `schemas`, `config`, torch, librosa and its vendor;
`notation` stays free of `transcription`/`harmony`. Engine: `pitch/yin.ts` and
`pitch/decimate.ts` are pure (no DOM/Web Audio), `singing/` uses `pitch/` and `timeline/`; views
use `timeline/`; nothing imports `engine.ts` except `index.ts`. .NET: `Api.InstrumentViews ->
Domain.InstrumentViews`, `-> Worker client`, `Api.Scores -> Domain.InstrumentViews` (catalogue
validation).

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/016-*/**`, ADR-031..033, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock` (unchanged deps), `StemStudio.Contracts/**`, `src/audio-engine/{package.json,vite.pitch.config.ts,src/types.ts}` | architect |
| `worker/.../harmony/{__init__,btc}.py`, `harmony/vendor/**`, `views_fake.py`, `jobs/instrument_analysis_*`, `api/instrument_analysis_routes.py`, `main.py`, their tests | stem-separation |
| `worker/.../harmony/{chroma_hmm,postprocess,labels,key}.py`, `analysis/vocal_pitch.py`, `notation/{tablature,parts,chord_symbols,drum_midi}.py`, tests | beat-analysis |
| `worker/.../analysis_inputs/**`, `engraving/tablature.py`, tests | audio-ingest |
| `src/audio-engine/src/**` (except `types.ts`), `src/audio-engine/tests/**` | audio-engine |
| `src/StemStudio.{Domain,Persistence,Api}/**`, tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, `docs/public/**` | CTO |

## Key flows

1. **Chords auto**: standard job completes -> `AnalysisAutoStarter` -> `TrackAnalysisService`
   (kind chords): inputs (harmonic stems), grid (reliable or null), fingerprint -> worker
   `POST /v1/instrument-analysis-jobs` -> `AssetSyncService`-style poller (`AnalysisJobApplier`,
   job kind `instrument-analysis`) stores the `ChordChart` -> `Track.instrumentViews.chords =
   ready` -> mixer `GET /api/tracks/{id}/chords` -> engine `attachChordLane`, `setKaraokeChords`.
1a. **Key (S11)**: the chords job's `key`/`keyRegions` -> `Track.key` (track page and mixer next
   to the BPM; `KeyFormatter` with the mixer's transpose and, for the orchestral family, the
   chosen written instrument via `GET /api/tracks/{id}/key`) -> engine `ChordChartData.keyRegions`
   for spelling and the chord lane host's `data-current-key`, which Blazor mirrors in
   `mixer-key-current`.
1b. **Capo mode (S12)**: guitar family -> `GET .../chords/capo-suggestions?transpose` ->
   user picks -> `setChordDisplay({capo, showSounding})`, chord sheet `?capo=`, badge, capo
   prefilled in the tablature options; stored per track in the browser.
2. **Chord sheet**: lyrics card -> `GET .../chords/sheet?transpose&capo` (Domain
   `ChordSheetBuilder` over lyrics + chart) -> Blazor rows; ChordPro the same through
   `ChordProWriter`.
3. **Tab / part / chords in scores**: sheet music dialog -> `GET /api/scores/options` (+ tablature,
   parts, chords) -> `POST /api/assets/scores` with `tablature`/`part`/`includeChords` -> worker
   transcription job with the extension fields -> notator: clean -> quantise -> key -> parts
   transposition -> tablature -> chord symbols -> MusicXML/MIDI -> engraving (TAB staff) -> 008
   sync.
4. **MIDI action**: `midi-<key>` -> `GET /api/assets?kind=score&trackId&channel` -> ready item:
   download `files/midi`; none: `POST /api/assets/scores` (defaults) -> poll -> download.
5. **Drum trainer**: drums split completes -> auto drum-hits analysis -> `GET .../drum-hits` ->
   `attachHitLanes`; "Drumless" -> `FamilyMixService.applyMinusOne(drums)` + metronome/count-in
   (012) -> restore on second click; drum score item -> 014 score view.
6. **Singing**: panel open -> `GET .../vocal-pitch` (none -> `POST`, estimate shown) -> units
   from lyrics (or sections/blocks) -> mute lead vocal -> engine `startSingingFeedback` (015 input,
   round trip) -> worklet frames -> scorer -> `OnSingingUnitScored` -> `karaoke-score` and
   announcer -> stop -> summary.
7. **Parts**: profile `strings|brass|woodwinds` suggested for the detail stems -> instrument and
   clef -> transposed MusicXML/PDF, concert MIDI -> 014 score view follows the same bars.

## Phase 0 (architect, before the wave)

T001 reconcile with merged 014/015; T002-T004 full contract documents and contract code
(worker, .NET, engine types, UI ids, help schema, spelling vectors); T005 BTC safe-load, CPU/GPU
time and `BTC_GPU_NEED`; T006 pYIN time and the Verovio TAB spike (decides ADR-033 D6); T007 YIN
benchmark (Node and Chromium); T008 skeleton green and hand-off `docs/handoffs/architect-016-plan.md`.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Vendored BTC subset | The repository is not a package; its training/eval imports pull pandas, mir_eval, pretty_midi and the GPL Rubber Band wrapper | Installing from git would import GPL code paths and unpinned dependencies |
| A fourth worker lane `instrument-analysis` | Chords/drum hits must be ready within 30 s even while a 25-minute CPU piano transcription or a separation runs | Sharing `transcribe` would delay chords by minutes; sharing `separate` blocks behind GPU work |
| A second AudioWorklet module (`pitch-tracker`) | Live pitch must run on the audio thread with sample-accurate stamps and no main-thread jank | Main-thread `AnalyserNode` polling is frame-rate bound and drifts; a Worker needs SharedArrayBuffer and COOP/COEP |
| Chord spelling in C# and TS | Lyrics card/ChordPro are server-rendered; live views re-spell on every transpose change without a round trip | Server-only spelling needs a round trip per transpose/capo change; the duplication is a 12-entry rule pinned by shared vectors |
| Behaviour fix of 004 presets | Expanded detail strips stayed audible in `play-along-<stem>` | A separate "Drumless" rule only for drums would treat families unequally |