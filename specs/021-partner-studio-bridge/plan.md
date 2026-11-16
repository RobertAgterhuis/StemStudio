# Implementation Plan: Practice Partner and Studio Bridge

**Branch**: `021-partner-studio-bridge` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/021-partner-studio-bridge/spec.md`, MoSCoW C9/C10/C11,
constitution 1.3.1, ADR-009, ADR-016, ADR-018, ADR-019, the 014/015/016/017/019 designs (assumed
contracts, see Assumptions in spec.md).

## Summary

Three independent capabilities in one feature, each usable on its own:

1. **Practice partner (C9)**: a new generator kind `partner` runs ACE-Step 1.5 **base** with the
   `lego` task, conditioned on the track's own stems plus tempo, time signature, key, sections and
   chords, and produces 1-2 takes of one instrument family. A new worker CPU job `conform` checks
   how each take fits (offset, drift, key, chords, loudness) and writes the conformed 44.1 kHz part.
   The backend turns a take into a **part**: an extra mixer channel `part-<8 hex>` saved in mixes,
   rendered by 007 and exported to DAWs (ADR-0211).
2. **Amp and pedal simulation (C10)**: NeuralAmpModelerCore (MIT) compiled by us to WebAssembly and
   run in an AudioWorklet inside the mixer's AudioContext; chain gate -> pedal -> amp (capture or
   built-in) -> cabinet IR -> 017 effects on a 015 input; dry and wet takes; re-amp; curated
   download catalogue with per-file licences; own `.nam` and IR uploads; rig presets (ADR-0212).
3. **DAW export (C11)**: the API builds a DAWproject 1.0 file (XML + ZIP, XSD-validated) from 007
   raw renders, the beat grid tempo map, 014 sections as markers, the 019 arrangement as clips and
   the mix settings incl. 017 EQ/compressor; stored as a new 007 asset kind (ADR-0213).

## Technical Context

**Language/Version**: C# / .NET 10 (API, domain, persistence, Blazor UI); Python 3.12 (worker,
generator); TypeScript 5 (audio engine); C++17 compiled with Emscripten (NAM shim, build-time only).
**Primary Dependencies**: existing stack. New: NeuralAmpModelerCore (MIT) + Eigen (MPL-2.0) +
nlohmann/json (MIT) + AudioDSPTools (MIT) compiled into `nam-engine.wasm` (build-time, committed
binary with provenance); `emscripten/emsdk` image (MIT/NCSA, CI build only); DAWproject XSDs (MIT,
vendored data); ACE-Step 1.5 base weights (MIT, downloaded at first use). No new NuGet, PyPI or npm
runtime package.
**Storage**: SQLite (new tables `TrackParts`, `AmpProfiles`, `CabinetIrs`, `RigPresets`; new
columns on `Generations`, `GenerationTakes`, `Takes`); files under the API data dir (`tracks/<id>/parts`,
`amp/profiles`, `amp/irs`, take `wet.flac`, `assets/<id>/*.dawproject`).
**Testing**: xUnit + bUnit + coverlet; pytest + pytest-cov (worker, generator); Vitest (engine) incl.
WASM golden vectors in Node; Playwright E2E on the fake stack (fake generator, fake catalogue host,
Chromium fake audio input); XSD validation in unit tests.
**Target Platform**: Docker compose on Windows 11 / Docker Desktop (reference machine); browser
Chromium-based.
**Project Type**: web application (existing multi-component layout).
**Performance Goals**: SC-001 (60 s part <= 3 min warm), SC-004 (NAM <= 50 % of a quantum,
<= 2 ms extra latency), conform <= 5 s per minute of audio on the CPU, DAW export of a 4:22 track with
6 stems <= 60 s.
**Constraints**: generator GPU-only (constitution IV exception); amp and DAW export work on CPU-only
installs; mixer context 44.1 kHz; captures and weights never committed; every contract change
additive.
**Scale/Scope**: single local user; <= 16 parts per track; <= 200 profiles; DAW export <= 80 channels.

## Constitution Check

*GATE: passed before Phase 0 research; re-checked after Phase 1 design (below).*

| Principle | Check | Status |
|---|---|---|
| I Test-first, >= 90 % | Every lane task pair is red -> green (tasks.md); models, WASM inference timing, network downloads and the browser audio device are behind ports/fakes; one marked GPU integration test (generator, CI-skipped) and one marked WASM rebuild job | pass |
| II SIG 4 stars | New domain units are pure and small (`PartnerCaptionBuilder`, `ChordProgressionText`, `NamFileValidator`, `WavHeaderReader`, `DawProjectBuilder` split per element writer); quality-gate rules for new module boundaries (T007/T008) | pass |
| III Contracts first | Five contract deltas (DELTA.md) written and lint-clean before code; contract tests per lane | pass |
| IV Open source only, lawful | Code: MIT/MPL-2.0 only; weights: ACE-Step base MIT (ADR-009 listing); captures: per-file licences, downloaded, never committed (ADR-0212 D4); no DRM; no paid service | pass |
| IV GPU-only exception | Only the partner kind is GPU-only, in the opt-in generator; controls disabled with reason; amp + DAW export run on CPU (SC-008) | pass |
| IV ethics | Imitation guard on the style hint; AI labels in UI, files and DAW exports; no voice profiles involved | pass |
| V Simplicity and honesty | Limits documented (lego quality unpublished, browser latency, Ableton unsupported, unmappable settings in a report); YAGNI: no MIDI parts, no CPU partner, no VST | pass |
| V Audio timing | Parts start with the same `start(when, offset)` as stems; NAM in the audio thread; no timers for audible timing | pass |
| Workflow | Hand-offs per checkpoint (ADR-008), public manuals updated (T090) | pass |

Post-design re-check: no new violation. Two justified items in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/021-partner-studio-bridge/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md
    ├── backend-api.delta.openapi.yaml      # 1.20.0-delta.1
    ├── worker-api.delta.openapi.yaml       # 1.12.0-delta.1
    ├── generator-api.delta.openapi.yaml    # 1.2.0-delta.1
    ├── audio-engine.delta.md               # 2.9.0
    ├── ui-test-ids.delta.md                # 1.20.0
    ├── dawproject-mapping.md               # 1.0.0
    ├── amp-catalogue.schema.json           # v1
    └── redocly.yaml
docs/decisions/ADR-0211-practice-partner-generation.md
docs/decisions/ADR-0212-amp-simulation-nam-wasm.md
docs/decisions/ADR-0213-dawproject-export.md
```

### Source Code (repository root; new or changed, by owner lane)

```text
generator/src/stemstudio_generator/          # generative
├── schemas.py, ports.py                     # (architect, Phase 0) PartnerParams, LEGO_BASE needs, base WeightsPin
├── engines/acestep_partner.py               # lego (+ extract) adapter
├── engines/fake.py                          # fake partner part (BPM pattern, offset, quiet mode)
└── api/, jobs/                              # kind partner, multipart context
worker/src/stemstudio_worker/                # stem-separation (lane owner of jobs/API) + beat-analysis (fit maths)
├── conform/{fit.py, onset_align.py, key_chords.py, writer.py}
├── api/routes_conform.py, jobs/conform_runner.py
└── library_paths.py                         # + parts pattern
src/StemStudio.Contracts/{Partner,Amp,DawExport}/*        # architect (Phase 0): DTOs, routes, limits
src/StemStudio.Domain/                       # backend
├── Partner/{PartFamily,PartnerCaptionBuilder,ChordProgressionText,PartnerRequestResolver,FitRanking,Part}.cs
├── Amp/{NamFileValidator,WavHeaderReader,RigSettings,RigPreset,AmpCatalogue}.cs, Amp/catalogue.v1.json
└── DawProject/{DawProjectModel,DawProjectBuilder,DawXmlWriter*,DawSchemaValidator,TempoMapBuilder,ArrangementClipMapper,MixMapper}.cs, DawProject/Schemas/*.xsd
src/StemStudio.Persistence/                  # backend: configurations + migration AddPartnerAmpDaw
src/StemStudio.Api/{Partner,Amp,DawExport}/  # backend: endpoints, services, worker/generator client additions, catalogue downloader
src/StemStudio.Web/                          # frontend
├── Components/Partner/{PartnerDialog,PartnerTakes,FitSummary,PartStrip}.razor
├── Components/Amp/{AmpPanel,RigPicker,LatencyBadge,LoadBadge}.razor, Pages/AmpProfiles.razor
├── Components/DawExport/{DawExportDialog,DawReport}.razor
└── Help/topics/{mixer,mixes,tracks,glossary}/*.md (data-model 10)
src/audio-engine/src/                        # audio-engine
├── parts.ts, amp/{rig.ts, builtin-amps.ts, cabinets.ts, nam-node.ts, nam-worklet.ts, load-meter.ts}, recorder-wet.ts, reamp.ts
└── vendor/nam/{nam-engine.wasm, PROVENANCE.md}
tools/nam-wasm/{shim.cpp, build.sh, Dockerfile, make_test_model.py}   # audio-engine (build), devops (CI job)
tools/quality/                               # quality-gate rules
e2e/tests/{partner,amp,daw-export}.spec.ts, e2e/fixtures/amp/*        # qa
docker/, docker-compose*.yml, .github/workflows/*                     # devops
```

**Structure Decision**: existing component layout; no new service or image. The generator image
gains no package (the base model is weights only). The worker gains a job kind in the existing
render lane. The engine gains a WASM asset.

## Lanes and agents

| Lane | Agent | Scope |
|---|---|---|
| Phase 0 | architect | merge contract deltas, contract code (.NET DTOs, generator `schemas.py`/`ports.py`, engine `types.ts`), XSD vendoring, catalogue curation (S-04), skeleton tests, ADR acceptance |
| generative | generative | partner kind, lego adapter, fakes, GPU needs (S-01/S-02 with the architect) |
| worker | stem-separation + beat-analysis | conform job (API, runner, lane, allow-list) / fit maths (onset alignment, key, chords, loudness) |
| backend | backend | domain, persistence, endpoints, generator/worker client additions, catalogue downloader, DAW builder and validator |
| audio-engine | audio-engine | part channels, rig chain, built-in amps, NAM WASM build + worklet, wet recording, re-amp, benchmark page (S-03) |
| frontend | frontend | partner dialog/takes/strips, amp panel, profiles page, DAW export dialog/report, help topics, levels, a11y |
| qa | qa | fixtures (guitar DI, NAM test models, IRs, fake catalogue), E2E specs, a11y checks, golden DAW files review |
| devops | devops | compose fake stack (catalogue host, fake input flags), CSP, NAM WASM CI job, scripts |
| quality-gate | quality-gate | dependency rules for new modules, SIG checks |
| CTO | CTO | integration wave, reference-machine validation (Q-list), manuals, THIRD-PARTY-LICENSES, STATUS |

## Phase 0 (architect, before the wave)

See tasks.md T001-T012. Outcome must include: merged contracts with lint output; Phase 0 spikes
S-01 (ACE-Step base lego, GPU needs, weights pin) and S-04 (catalogue licences) recorded in
research.md and ADR-0211/0212 "Phase 0 outcome"; S-03 is the audio-engine's first foundational task
(it needs the WASM build); S-02 and S-05 run in the wave/integration.

## Dependencies and licences (for THIRD-PARTY-LICENSES, CTO)

| Name | Version / pin | Licence | Use | URL |
|---|---|---|---|---|
| ACE-Step 1.5 base DiT (`acestep-v15-base`) | HF revision pinned in S-01 | MIT (weights) | partner generation | https://huggingface.co/ACE-Step/acestep-v15-base |
| ACE-Step 1.5 code | existing pin (S-01 may move it) | MIT | generator | https://github.com/ace-step/ACE-Step-1.5 |
| NeuralAmpModelerCore | commit pinned in T040 | MIT | compiled into `nam-engine.wasm` | https://github.com/sdatkinson/NeuralAmpModelerCore |
| Eigen | as bundled by the core pin | MPL-2.0 (unmodified) | compiled into WASM | https://eigen.tuxfamily.org |
| nlohmann/json | as bundled | MIT | compiled into WASM | https://github.com/nlohmann/json |
| AudioDSPTools | as bundled | MIT | resampler in WASM | https://github.com/sdatkinson/AudioDSPTools |
| Emscripten SDK image | pinned tag in T040 | MIT / University of Illinois NCSA | build only (CI) | https://github.com/emscripten-core/emsdk |
| DAWproject XSDs | commit pinned in T005 | MIT | validation data | https://github.com/bitwig/dawproject |
| Catalogue captures | per entry (S-04) | per entry (ADR-0212 D4) | downloaded on request | per entry |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Lego quality is unpublished and may be weak for some families | SC-002/003 missed | S-01 measures before the wave; the fit check shows honest verdicts; help states limits; targets may be lowered with a recorded reason |
| Pinned ACE-Step commit lacks lego or the output is a mix | rework | S-01 decides the pin; `extract` fallback in the same lease |
| Base model memory > 16 GB with CFG | partner unusable on the reference GPU | low-memory profile (offload); S-01 measures; fewer steps (30) as Expert option |
| NAM standard WaveNet too heavy in WASM | glitches | S-03 budget; "heavy" marking; lite/feather in the catalogue; built-ins always available |
| Browser round-trip latency on Windows > 30 ms | uncomfortable monitoring | shown honestly; "record dry, hear amp on playback"; interface direct monitoring tips |
| Too few captures with clear per-file licences | small catalogue | built-in amps + own uploads keep the feature complete (ADR-0212 D4) |
| 015/017/019 contracts differ from the assumptions | merge rework | all 021 deltas are additive and name-stable; DELTA.md lists the assumptions; T001 reconciles |
| DAWs ignore some DAWproject elements | user confusion | export report + help topic with S-05 observations |
| Large DAW exports fill the disk | failed exports | size estimate x 2 + 1 GiB guard |

## Complexity Tracking

| Violation / addition | Why needed | Simpler alternative rejected because |
|---|---|---|
| Committed WASM binary (`nam-engine.wasm`) | app build, unit tests and CI must not need Emscripten; provenance and a CI rebuild check keep it honest | building in every CI run adds a 1-2 GB toolchain image to every pipeline; the npm package brings React runtime dependencies |
| Second DiT checkpoint in the generator (base next to turbo) | lego is base-only (README) | turbo cannot do lego; a separate service for one model would duplicate the GPU gate |
