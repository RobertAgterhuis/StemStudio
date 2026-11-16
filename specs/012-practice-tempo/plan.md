# Implementation Plan: Practice Tempo

**Branch**: `012-practice-tempo` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/012-practice-tempo/spec.md`; constitution 1.3.1;
`docs/MoSCoW.md` M1-M3 and decisions; `docs/ROADMAP.md` contract allocation (backend-api 1.11.0,
audio-engine 2.3.0, ui-test-ids 1.11.0); [research.md](research.md); ADR-021 (builds on ADR-005,
ADR-010, ADR-015, ADR-016).

## Summary

Speed and key are changed for the whole mix in the browser. Every `AudioBufferSourceNode` plays at
`playbackRate = speed`, which keeps all stems sample-locked and native loops intact; one stereo
SoundTouch 2.4.1 stage (LGPL-2.1, compiled to standalone WASM, run in an AudioWorklet) on the summed
mix corrects the pitch by `2^(st/12) / speed`. The stage has a fixed, measured delay that the
transport compensates by starting sources earlier, so the metronome, the karaoke clock and the
position only need a rate factor in the anchor (`offset + (t - when) * rate`). Nothing is loaded at
100 % / 0 semitones. The speed trainer and the count-in before every loop pass run the loop as
explicit passes (one source set per pass with its own rate), created ahead by the existing
look-ahead timer; the count-in continues the song's beat grid with metronome clicks. Practice
settings are saved in the mix (`MixSettings.practice`, backend 1.11.0); exports stay at the
original speed and key.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; TypeScript 6.0; Python 3.12 untouched); new C++17
shim compiled with Emscripten.

**Primary Dependencies (new)**: SoundTouch **2.4.1** (LGPL-2.1, `https://codeberg.org/soundtouch/soundtouch`,
unmodified source, separate `soundtouch.wasm` module); build tool Emscripten SDK **6.0.7**
(`emscripten/emsdk:6.0.7` Docker image; MIT / University of Illinois NCSA; not distributed). No new
npm, NuGet or Python packages.

**Storage**: SQLite column `Mixes.Practice` (TEXT NULL, JSON; migration `AddMixPractice`). No
browser storage keys.

**Testing**: Vitest (mocked `AudioContext` + `audioWorklet`/`AudioWorkletNode`/`AudioParam`
automation recorder; fake `TimeStretchStage`; fake `AudioWorkletGlobalScope` and fake WASM ABI for
the processor); Node suite `npm run test:wasm` against the real WASM; xUnit (domain, persistence,
API, contract against `backend-api.openapi.yaml`); bUnit (practice panel, trainer, count-in,
export note, levels, help markers); Vitest for the new `src/web-interop` bindings; Playwright on the
fake stack in Chromium and Firefox with real AudioWorklet + WASM (output probe, click probe,
karaoke probe).

**Target Platform**: current Chromium-based browsers and Firefox (AudioWorklet, WebAssembly,
optional WASM SIMD).

**Performance Goals**: SC-001 change heard <= 250 ms; SC-004 0 dropouts in 10 min with 20 channels
at 50 %, stage <= 35 % of the quantum budget, >= 10x real time in Node; constant latency
`L <= 230 ms`.

**Constraints**: audible timing only on the audio clock (constitution V); one stage for the whole
mix; neutral settings bit-identical to 2.1.0; LGPL module separately replaceable, source shipped
with binaries; exports unchanged.

**Scale/Scope**: one user; up to 20 channels; tracks up to 15 minutes.

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every owner task is a red/green pair. Engine: tempo math, anchor, passes, trainer, count-in plan and the processor are pure or port-based and fully unit-testable with the mock clock; the real WASM is covered by a separate Node suite (like the one marked integration test per heavy dependency); backend domain/persistence/API; bUnit for every new component; E2E per story and per instrument family. Coverage gates unchanged (engine, web-interop, API, Web). | PASS |
| II | SIG 4 stars | Engine split into `stretch/` (constants, params, fifo, abi, processor, worklet-stage) and `practice/` (settings, tempo, passes, trainer, count-in, controller) of small modules; transport change confined to the anchor; frontend one component per control group; dependency rules added to `tools/quality` (T014). The C++ shim (< 120 lines) is ours and kept within the unit-size limits; `third_party/soundtouch/upstream` is excluded from SIG as third-party code. | PASS |
| III | Contracts first | backend-api 1.11.0 (additive; full document `1.11.0-delta.1` now, merged with 1.10.0 in Phase 0), audio-engine 2.3.0 (additive, full text), ui-test-ids 1.11.0 (additive delta, full document in Phase 0), WASM ABI v1; C# DTOs and `types.ts` written in Phase 0 before the wave; contract tests. | PASS |
| IV | Open source, lawful use | SoundTouch is LGPL-2.1 (OSI). Used as a separate, run-time-loaded, replaceable module with a documented ABI (LGPL 2.1 section 6(b)); unmodified upstream source pinned by SHA-256; licence text, exact source archive, shim and build script shipped in every binary distribution (`/third-party/soundtouch/`); MIT project stays MIT; recorded in THIRD-PARTY-LICENSES (CTO). Rubber Band (GPL) excluded. No new data leaves the machine. | PASS |
| V | Simplicity and honesty | One stage on the sum (no per-stem processing), no new backend endpoints, worker untouched, exports unchanged. Quality limits shown (hint < 70 %, help topic, missing formant correction, meter lead). Timing: `setValueAtTime`, `source.start(when)` and the existing look-ahead scheduler (timer only wakes it). | PASS |
| - | Tech & security | No secrets, no new network egress (WASM served by the app itself); input validated in the engine and the backend; the processor receives bytes, never URLs; no `eval`; future CSP must allow `'wasm-unsafe-eval'` (documented). | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-offs per checkpoint (ADR-008); manuals updated in the integration wave (1.3.1). | PASS |

**Post-design re-check (after Phase 1)**: PASS; deviations in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/012-practice-tempo/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                      # summary of every contract change
    ├── backend-api.openapi.yaml      # 1.11.0-delta.1 (= 1.9.0 + 012); 1.11.0 after T003
    ├── check_superset.py             # "newer contains older" check
    ├── redocly.yaml
    ├── audio-engine.md               # 2.3.0 (full)
    └── ui-test-ids.delta.md          # 1.11.0 additions; full ui-test-ids.md 1.11.0 after T003
```

### Source Code (additions)

```text
third_party/soundtouch/
├── README.md, NOTICE.md, COPYING.TXT          # licence, ABI, how to replace the module (architect, T006)
├── soundtouch.lock                            # version, tarball URL + SHA-256, emsdk image digest (architect, T001)
├── include/stemstudio_soundtouch.h            # ABI v1 (architect, T006)
└── shim/stemstudio_soundtouch.cpp             # ABI implementation over SoundTouch (audio-engine)
scripts/build-soundtouch.{sh,ps1}              # fetch + verify + emcc in emsdk 6.0.7 (devops)
src/audio-engine/
├── src/types.ts                               # 2.3.0 additions (architect, T005)
├── src/transport.ts                           # rate-aware anchor, latency-aware starts (audio-engine)
├── src/metronome/timeline.ts                  # rate + anchor queue (audio-engine)
├── src/stretch/{constants,params,fifo,abi,processor,worklet-stage,fake-stage}.ts   (audio-engine)
├── src/practice/{settings,tempo,passes,trainer,count-in,controller}.ts             (audio-engine)
├── vite.config.ts                             # second entry time-stretch-processor.js (audio-engine)
└── tests/{stretch,practice}/**, tests-wasm/** # Vitest + Node WASM suite (audio-engine)
src/StemStudio.Contracts/Mixes/{PracticeSettingsDto,SpeedTrainerSettingsDto,CountInSettingsDto}.cs,
    MixSettingsDto.Practice                                                         (architect, T004)
src/StemStudio.Domain/Mixes/{PracticeSettings,SpeedTrainerSettings,CountInSettings}.cs,
    MixSettings.Practice                                                            (backend)
src/StemStudio.Persistence/Configurations/MixConfiguration.cs, Migrations/*_AddMixPractice.cs   (backend)
src/StemStudio.Api/Mixes/{MixMapper,MixValidator}.cs                                (backend)
src/StemStudio.Web/
├── Components/Mixer/Practice/{PracticePanel,SpeedControl,TransposeControl,CountInControl,
│       CountInIndicator,SpeedTrainerPanel}.razor(.cs)
├── Mixer/{MixerState.Practice.cs, PracticeRules.cs}
├── Audio/{IAudioEngine,AudioEngineInterop,EngineMethods,PracticeStatus}.cs (+ OnPracticeChanged)
├── Components/Mixer/Mixer.Practice.cs, Mixer.Shortcuts.cs (commands), Export dialog note
├── Experience/FeatureCatalog.cs (+5 entries)
└── Help/topics/{mixer,glossary}/*.md, Help/tours/{mixer-basics,mixer-advanced}.json      (frontend)
src/web-interop/src/shortcuts.ts (+ practice bindings), tests                       (frontend, "web-interop" lane)
docker/web.Dockerfile (emsdk stage, /third-party/soundtouch), .github/workflows (wasm build, test:wasm),
    scripts/test-all.* (test:wasm when built)                                       (devops)
e2e/tests/practice-{tempo,trainer,count-in,levels,families}.spec.ts,
    e2e/support/{output-probe,click-probe}.ts, tests/fixtures (tone_440hz_20s.flac)  (qa)
tools/quality (dependency rules for stretch/ and practice/)                          (quality-gate)
```

**Dependency direction**: engine `engine -> practice/controller -> {practice/*, stretch/worklet-stage}`;
`practice/*` read the transport through `TransportClock`-style read-only views and command it through
a narrow `PracticeTransport` interface; `transport.ts` never imports `practice/` or `stretch/`;
`stretch/processor.ts` (worklet scope) imports only `stretch/{constants,params,fifo,abi}` (no DOM, no
engine code); `karaoke/` and `metronome/` never import `practice/`. .NET unchanged:
Web -> API client and `IAudioEngine`; Api -> Domain; Persistence -> Domain.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/012-*/**`, ADR-021, `src/audio-engine/src/types.ts`, `StemStudio.Contracts/**`, `third_party/soundtouch/{README.md,NOTICE.md,COPYING.TXT,soundtouch.lock,include/**}` | architect |
| `src/audio-engine/**` (except `types.ts`), `third_party/soundtouch/shim/**` | audio-engine |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**`, `src/web-interop/**` (lane "web-interop") | frontend |
| `tests/fixtures/**`, `e2e/**`, `docs/testing/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**`, `sonar-project.properties` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, `docs/public/**` | CTO |

Worker owners (audio-ingest, stem-separation, beat-analysis) and the generative/ai-agent owners have
no work in this feature.

## Key flows

1. **Open a mix** (any, incl. pre-012): `GET /api/mixes/{id}` -> `practice` (defaults for old mixes)
   -> `MixerState.Practice` -> `engine.setPractice(...)` only when it differs from neutral (old
   mixes: no call, no module request; SC-007).
2. **First slow-down while playing**: slider -> `MixerState` (dirty) -> `SetPracticeAsync` (at most
   one per 50 ms while dragging) -> engine `stretch = loading` -> `addModule` + `fetch(wasm)` ->
   processor `ready` -> engage, restart at the heard position -> `OnPracticeChanged(active)` -> panel
   `data-stretch="active"`.
3. **Live change**: `setPractice` -> `T = now + L + 0.02` -> `playbackRate.setValueAtTime(r, T - L)`
   on every source, `setPitch(p, frame)`, re-anchor at `T`; metronome reschedules future clicks;
   karaoke reads the new anchor on the next frame.
4. **Trainer**: loop enabled + trainer on + play -> explicit pass 1 at `startPercent` ->
   look-ahead creates pass k+1 at `T_k + (B - A)/r_k` (+ count-in gap) with the trainer's next speed
   -> `OnPracticeChanged` per pass -> `trainer-status`.
5. **Count-in**: play -> plan clicks on the grid -> clicks via the metronome output -> sources start
   at `T0` -> indicator beats via `OnPracticeChanged`.
6. **Save**: `PUT /api/mixes/{id}` with `practice` -> domain validation -> JSON column.
7. **Export**: unchanged request (`ExportMix` has no practice); the dialog shows
   `export-practice-note` when not neutral.

## Phases

- **Phase 0 (architect, before the wave)**: spikes P0-1/P0-2 (WASM build, latency, CPU, browser),
  contract merge P0-3, C# DTOs, `types.ts` 2.3.0 with stubs, `third_party/soundtouch` skeleton,
  verification; tasks T001-T007.
- **Wave (parallel owners)**: setup/foundational per owner, then US1 (speed/transpose), US2 (trainer),
  US3 (count-in), polish.
- **Integration wave (CTO)**: merge, full suites, reference-machine validation, licences, README,
  manuals and PDFs.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| C++ shim + Emscripten build step in the repo | owner decision "SoundTouch compiled to WASM"; a reproducible, pinned build is the only way to know exactly which LGPL source we ship | prebuilt community WASM (unclear provenance, JS glue that does not run in a worklet); soundtouchjs (JS port, not WASM; kept as fallback) |
| Rate on sources + pitch stage on the sum (instead of a "tempo" worklet) | a push-graph worklet cannot change duration; keeps sample lock, native loops, gates, meters | pull-based stretch source re-implements the mixer and copies all PCM into the worklet |
| Fixed FIFO delay and earlier source starts | constant, known latency keeps clicks, karaoke and position exact without per-consumer corrections | variable SoundTouch latency would make every consumer track it and drift |
| Explicit loop passes next to the native loop | trainer steps and count-in gaps need exact pass boundaries and silence between passes | rate automation at native wrap times cannot insert silence; using explicit passes always would change 2.1.0 loop behaviour |
| Node WASM suite outside the coverage run | real SoundTouch must be tested, but its C++ is third-party and not part of the TS coverage | testing only the fake would leave the shim and build unverified |
| Backend contract as `1.11.0-delta.1` until Phase 0 | 011's full 1.10.0 does not exist yet; 013-016 are specified in parallel | writing 1.10.0 here would duplicate and possibly diverge from 011's merge |
