# Implementation Plan: Sheet Music per Instrument

**Branch**: `008-sheet-music` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/008-sheet-music/spec.md`; constitution 1.2.0 (ADR-009
weights exception, ADR-011 GPU runtime); `docs/ROADMAP.md`; research
`docs/research/roadmap-003-011.md` section 7; feature 007 (asset library, lanes, library mount).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions
are in ADR-017 (`docs/decisions/ADR-017-sheet-music-transcription-and-engraving.md`);
measurements in [research.md](research.md); contract changes in [contracts/DELTA.md](contracts/DELTA.md).
Phase 0 executed 2026-11-15 together with 006 (hand-off `docs/handoffs/architect-006-008-phase0.md`):
worker 1.6.0, backend 1.7.0, UI ids 1.7.0 in [contracts/](contracts/); "Phase 0 outcome" in DELTA.md
and ADR-017, measurements in research.md "Phase 0 measurements".

## Summary

The user picks a stem, detail stem or library audio item and an instrument profile; the API
resolves the source path(s), the tempo grid (the track's beat grid, shifted by a range, or a
manual tempo) and the defaults, creates a library asset of kind `score` and submits a
transcription job. The worker's `transcribe` lane runs the profile's engine (basic-pitch ONNX
for vocals/bass/guitar/keys and fast piano; the ByteDance piano CRNN through the 003
`DeviceRunner` for accurate piano; onset detection on the 002 drum detail stems for drums),
cleans the notes per profile, quantises them on the beat grid, estimates and spells the key,
builds a music21 score with the profile's clefs/staves (grand staff, two-voice percussion), and
writes MusicXML, MIDI and an A4 PDF (Verovio SVG -> CairoSVG -> pypdf, Leipzig font installed).
The API copies the three files into the library; the UI previews the PDF and offers the three
downloads, the key/tempo facts, warnings and the profile's quality limits.

Measured (research.md): basic-pitch 10-28 s per 4:22 stem on 8 CPU threads; drums 3.9 s;
notation + PDF about 1 s; piano CRNN about 7 min on the CPU (GPU expected in seconds, measured in
Phase 0). Synthetic accuracy: single voice F1 1.00, piano F1 0.96.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0, engine untouched).

**Primary Dependencies (new, worker, locked in `uv.lock`)**: `music21` 10.5.2 (BSD-3) with
chardet (0BSD), jsonpickle (BSD-3), more-itertools (MIT), webcolors (BSD-3) [matplotlib excluded
by a uv override, Phase 0]; `verovio` 6.4.0 (LGPL-3.0-only); `cairosvg` 2.9.1 (LGPL-3.0-or-later) with
cairocffi 1.7.1 (BSD-3), cssselect2 0.10.1, tinycss2 1.5.1, webencodings 0.6.1 (BSD), defusedxml
0.7.1 (PSF-2.0); `pypdf` 6.21.4 (BSD-3); `torchlibrosa` 0.1.0 (MIT). Vendored (no package):
basic-pitch 0.4.0 post-processing (Apache-2.0 + NOTICE), piano_transcription_inference 0.0.6 model
and post-processor (MIT). Weights (model cache, first use): basic-pitch ONNX (Apache-2.0), piano
CRNN (CC BY 4.0). Image: Leipzig font (OFL-1.1) installed at build (fontTools MIT, build stage
only). No new .NET or npm packages.

**Storage**: `Assets.Score` JSON column (migration `AddScoreFacts`); files
`assets/<id>/{score.pdf,score.musicxml,score.mid}`; worker `transcription-jobs/`.

**Testing**: notation (cleaning, time base, key/spelling, score builders per profile, drum
notation) and engraving tested with small synthetic note lists and golden MusicXML assertions
(bars, clefs, key, time signature) plus PDF/SVG glyph checks; transcription runtimes with fake
ONNX sessions / fake torch modules; the synthetic accuracy tests (SC-002) run the real
basic-pitch ONNX (and the piano model) as `@pytest.mark.integration` (model download, excluded
from the default CI run like 002's detail model tests). Backend/frontend/qa as 007.

**Target Platform**: unchanged; CPU everywhere, GPU used for the piano model when available.

**Performance Goals**: SC-001 melodic stem and drums <= 2 min for 5 min of audio on the reference
machine; SC-005 exports unaffected.

**Constraints**: OSI code only; no GPL imports; no shell (`os.system`) - downloads through the
model manager; transcribe lane thread cap; PDFs without missing glyphs.

**Scale/Scope**: one source per score; songs up to 15 min (multi-page PDFs).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every implementation task has a failing test first (tasks.md). Models sit behind `Transcriber` ports with fakes; the notation and engraving layers are pure and tested on synthetic note lists; real models only in marked integration tests (SC-002) on synthetic audio. Vendored third-party code is excluded from our coverage/SIG figures as in 002 (`detail/vendor/msst`), with thin adapters tested. | PASS |
| II | SIG 4 stars | New worker packages `transcription/` (runtimes + vendor), `notation/` (profiles, cleaning, time base, key, builders), `engraving/` (Verovio/Cairo/pypdf), `analysis/drum_onsets.py`; backend `Scores/` feature folder; frontend `Scores/` components. Dependency rules: `notation` and `engraving` import neither `transcription` nor torch/onnxruntime; `transcription/vendor` excluded from SIG. | PASS |
| III | Contracts first | Worker and backend API minor bumps, UI test ids minor bump, engine unchanged; Phase 0 writes OpenAPI 3.1 (Redocly-valid), `schemas.py`, `ports.py` (`PitchTranscriber`, `DrumTranscriber`, `ScoreEngraver`, `NoteEvent`, `DrumHit`, `TempoGrid`), Contracts DTOs; ADR-017. | PASS |
| IV | Open source, lawful use | All code OSI: BSD/MIT/Apache/PSF; Verovio and CairoSVG are LGPL and used unmodified as separately installed packages (relinkable), like libsndfile; no GPL code is imported (YourMT3+ rejected, ADTOF excluded). Weights: basic-pitch Apache-2.0; piano CRNN CC BY 4.0 (attribution in UI, help, THIRD-PARTY-LICENSES), downloaded at first use with SHA-256, never committed; both permissive, so the ADR-009 exception is not even needed. Leipzig font OFL-1.1. Transcriptions are personal practice material (notice repeated in the dialog). | PASS |
| V | Simplicity and honesty | One pipeline for all pitched profiles, own simple drum transcription from existing detail stems instead of a second model stack; no tablature, chords, lyrics, triplets (documented). Quality limits per profile and per-item warnings are shown (FR-010); CPU piano time is estimated and shown with a fast alternative. | PASS |
| - | Tech & security | Stack unchanged (Python worker, ASP.NET API, Blazor UI). Inputs validated (profile/source combinations, key names, tempo ranges, paths per 007). Model downloads through the model manager (HTTPS, pinned SHA-256), no subprocess or shell. Engraving input is our own MusicXML (no user XML parsed); MusicXML downloads are served as attachments. No secrets. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths (ownership map); hand-off per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS; licence isolation and the GPL rejection are
recorded in Complexity Tracking and ADR-017 D2.

## Project Structure

### Documentation (this feature)

```text
specs/008-sheet-music/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                    # this planning round
    ├── worker-api.openapi.yaml     # Phase 0
    ├── backend-api.openapi.yaml    # Phase 0
    ├── ui-test-ids.md              # Phase 0
    └── redocly.yaml                # Phase 0
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py          # + NoteEvent, DrumHit, TempoGrid, ScoreDocument, PitchTranscriber, DrumTranscriber, Notator, ScoreEngraver, TranscriptionError, NoNotesError (architect)
├── schemas.py        # + TranscriptionJobRequest/Job/Result, ScoreSummary, ... , Health.transcription (architect)
├── config.py         # + transcriber, transcribe_threads (architect)
├── transcription/    # stem-separation
│   ├── __init__.py       # create_pitch_transcriber(settings, devices), registry entries (2.10)
│   ├── basic_pitch.py    # ONNX session (CPU), windowing, vendored post-processing adapter
│   ├── piano.py          # piano CRNN via DeviceRunner + vendored post-processor adapter
│   ├── fake.py           # deterministic notes/hits (data-model 2.6)
│   └── vendor/{basic_pitch,piano_transcription}/   # LICENSE/NOTICE kept, excluded from SIG
├── analysis/drum_onsets.py   # beat-analysis: DrumTranscriber on the five detail stems
├── notation/         # beat-analysis
│   ├── profiles.py, cleaning.py, timebase.py, keys.py
│   ├── builders.py       # music21 parts per profile (single staff, grand staff, percussion)
│   └── writer.py         # MusicXML + MIDI bytes
├── engraving/        # audio-ingest
│   ├── verovio_svg.py, pdf.py (CairoSVG + pypdf), fonts.py (Leipzig check)
├── jobs/transcription_{store,service}.py, api/transcription_routes.py, main.py   # stem-separation
└── (worker/scripts/install_music_font.py - build-time font conversion, devops runs it in the Dockerfile; architect writes it in Phase 0)

src/StemStudio.Contracts/Scores/*.cs, AssetDto.Score, ApiRoutes.Scores        (architect)
src/StemStudio.Domain/Scores/{ScoreRecipe,ScoreFacts,ProfileCatalog,TempoGridResolver}.cs
src/StemStudio.Persistence (Score column, migration AddScoreFacts)
src/StemStudio.Api/Scores/{ScoreEndpoints,ScoreService,ScoreOptionsService,ScoreJobApplier}.cs
src/StemStudio.Api/Worker/{WorkerClient.Transcription,WorkerTranscriptionJob}.cs   (backend)
src/StemStudio.Web/Components/Scores/{ScoreDialog,ScoreFactsPanel,PdfPreview}.razor
    + track page and library actions, help topics                               (frontend)
e2e/tests/sheet-music.spec.ts                                                    (qa)
docker/worker.Dockerfile (font build stage, fontconfig), docker-compose.yml (STEMSTUDIO_TRANSCRIBER)   (devops)
tools/quality (rules for transcription/notation/engraving, vendor exclusion)     (quality-gate)
```

**Dependency direction**: worker `api -> jobs -> ports <- {transcription, notation, engraving,
analysis}`; `notation` imports only `ports`, `schemas` and music21; `engraving` only `ports`,
verovio, cairosvg, pypdf; `transcription` only `ports`, `schemas`, `config`, its vendor folder,
onnxruntime/torch; `jobs` composes nothing (composition in `main.py`). .NET:
`Api.Scores -> Domain.Scores`, `Api.Scores -> Api.Assets` (shared asset storage/sync),
`-> Worker client`.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/008-*/**`, ADR-017, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `worker/scripts/install_music_font.py`, `StemStudio.Contracts/**` | architect |
| `worker/src/stemstudio_worker/{transcription,jobs,api}/**` (transcription parts), `main.py`, their tests | stem-separation |
| `worker/src/stemstudio_worker/{notation/**,analysis/drum_onsets.py}`, `worker/tests/{notation,analysis}/**` | beat-analysis |
| `worker/src/stemstudio_worker/engraving/**`, `worker/tests/engraving/**` | audio-ingest |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

audio-engine has no work in this feature.

## Key flows

1. **Options**: dialog opens -> `GET /api/scores/options?trackId&channel` -> suggested profile,
   drums split status, grid status, estimates (device from `GET /api/device`), limits.
2. **Create**: `POST /api/assets/scores` -> `ScoreService`: validate source/profile, resolve
   inputs (paths) and the tempo grid (data-model 4.3), disk guard, `Asset(kind score, queued)`
   with recipe -> worker `POST /v1/transcription-jobs` -> `202 Asset`.
3. **Worker** (`transcribe` lane): load model (download on first use) -> transcribe (piano via
   `DeviceRunner`) -> clean -> quantise -> key/spelling -> music21 score -> MusicXML + MIDI ->
   Verovio SVG pages -> PDF -> result with `ScoreSummary` and `device`.
4. **Sync**: the 007 `AssetSyncService` handles both job kinds (render, transcription) by a
   `WorkerJobKind` on the asset; on completion three hashing copies into `assets/<id>/`, `Score`
   filled, worker job deleted.
5. **View**: library row of kind score -> PDF preview (`<iframe>`/`<object>` with the inline PDF
   URL), downloads, facts, warnings, limits, attribution; "Sheet music" action on audio items.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Vendored subsets of basic-pitch and piano_transcription_inference | The packages pull TensorFlow (basic-pitch) or import matplotlib/audioread and download via `os.system` (piano), violating the "no shell" rule and bloating the image | Installing the packages as-is breaks numpy 2.5 / adds GBs / uses a shell; licences (Apache-2.0, MIT) allow vendoring with LICENSE/NOTICE |
| Two LGPL libraries (Verovio, CairoSVG) | Server-side PDF for the agent and downloads; Verovio is the only maintained OSI engraver usable in-process | LilyPond/MuseScore are GPL executables and much heavier; browser printing does not serve the agent |
| A third worker lane (`transcribe`) | CPU piano transcription takes minutes; it must not block exports (SC-005) or wait for separations | Sharing the `render` lane would delay exports by minutes |
| Drum notation needs the 002 drum detail stems | No OSI-code drum transcription model with acceptable deps exists (ADTOF NC code; YourMT3+ GPL, stale, heavy) | A second ML stack/container for one instrument; deferred behind the same contract if quality is insufficient |
| Font install step in the image | Verovio's text glyphs (tempo mark) need the Leipzig font as a system font for Cairo (research.md S-08) | Post-processing SVG text into paths is fragile; dropping the tempo mark loses information |
