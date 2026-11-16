# Implementation Plan: Mixer Effects and Controllers

**Branch**: `017-effects-controllers` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `specs/017-effects-controllers/spec.md`; constitution 1.3.1;
`docs/MoSCoW.md` (C2, C3; every instrument counts equally); `docs/ROADMAP.md` (017 after 012;
backend 1.16.0, engine 2.7.0, ui-test-ids 1.16.0); 007 export library (ADR-016,
render-vectors 1.0.0).

**Note**: produced by the architect agent (`/speckit-plan` equivalent). Binding decisions are in
[ADR-026](../../docs/decisions/ADR-026-mixer-effects-and-midi-controllers.md); evidence and
alternatives in [research.md](research.md); contract changes in [DELTA.md](DELTA.md).

## Summary

Each mixer strip gets a 6-band parametric EQ and a compressor as pre-fader inserts and a
post-fader send into one shared, generated-IR reverb bus, all built from native Web Audio nodes
that exist only while switched on. A built-in preset catalogue in the engine gives every
instrument family a practice EQ ("Focus on X", "Make room for X") plus tone and dynamics presets.
Effects are optional members of a mix (old mixes unchanged). Exports "as heard" are rendered in
the browser by the same graph builder in an `OfflineAudioContext`, uploaded as a float WAV
pre-render and encoded by the unchanged 007 worker render job, so the file equals what the user
hears; browser-less exports stay as in 007 and are labelled "without effects". A Web MIDI module
in the engine package parses, maps and coalesces controller input and hands batched events to
.NET, which drives the same mixer methods as the screen; mappings are backend profiles per device,
edited by MIDI learn in the mixer and on a new Controllers page. Levels: Beginner practice EQ
only, Advanced everything, Expert detail.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; TypeScript 6.0; Python 3.12 untouched except
one worker test).

**Primary Dependencies (new)**: none. Web Audio (`BiquadFilterNode`, `DynamicsCompressorNode`,
`ConvolverNode`, `OfflineAudioContext`) and Web MIDI are browser APIs.

**Storage**: SQLite: mix JSON columns gain optional `Effects` members; new nullable column
`Mixes.Effects`; new tables `Prerenders` and `ControllerProfiles` (bindings as a JSON column);
migration `AddEffectsAndControllers`. Files: `<DataDir>/assets/<prerenderId>/prerender.wav`
(staging) and `<DataDir>/assets/<assetId>/prerender.wav` (until the asset completes).

**Testing**: engine Vitest with the mocked `AudioContext`, extended with mocked biquad,
compressor, convolver, delay and offline contexts (graph topology and parameter assertions), pure
functions (IR generator, WAV encoder, MIDI parser, pick-up, coalescer, offline gate against
`render-vectors.json`). Backend xUnit: domain validation, WAV header reader, endpoints with
`FakeTimeProvider` (TTL sweep), persistence round trip including a 1.15.0-shaped mix row. bUnit:
panels, menus, level gates, export dialog, Controllers page, dispatcher. Playwright: Chromium
with real Web Audio for effects and offline renders, fake MIDI device (research R-13), axe.

**Target Platform**: unchanged (Docker compose, Windows reference machine); MIDI on Chromium.

**Performance Goals**: SC-004 (< 50 ms p95 MIDI to audible), SC-005 (worst-case graph realtime on
the reference machine; offline 30 s <= 10 s on CI), effects parameter change audible <= 50 ms.

**Constraints**: constitution V timing rule (no timer determines audible timing; disconnects of
silent paths and coalescing are not audible scheduling), SIG limits, >= 90 % coverage per
component, ui-test-ids, help coverage tests, no secrets.

**Scale/Scope**: up to 28 strips, 256 bindings per profile, 100 profiles, pre-renders up to
320,000,000 bytes.

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I Test-first, >= 90 % | PASS | every task pair is test-then-code; pure modules for parser, IR, WAV, gate; fake MIDI in E2E |
| II SIG | PASS | effects split into small modules (one node type per file); `ChannelEffects` validation as small rule methods; parameter objects (`PrerenderRequest`, `ControllerEvent`) keep <= 4 params |
| III Contracts first | PASS | backend 1.16.0, engine 2.7.0, ui-test-ids 1.16.0 deltas; contract tests in each lane |
| IV Open source only | PASS | no new dependency; IRs generated (no recordings with licences) |
| V Simplicity / honesty | PASS | one reverb bus, built-in presets, no MIDI output; "without effects" label; Chromium-only stated |
| Timing rule | PASS | parameter ramps on `AudioParam`; transport via the engine's look-ahead start; coalescing timers only decide *when .NET hears*, not when audio is scheduled |
| Manuals (1.3.1) | PASS | CTO tasks T108-T110 |
| Every instrument counts equally | PASS | 7 families x 2 practice modes, unit band test per family (T030), E2E per family (T038) |

## Project Structure

### Documentation (this feature)

```text
specs/017-effects-controllers/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md, DELTA.md
├── checklists/requirements.md
└── contracts/
    ├── backend-api.delta.openapi.yaml   # standalone OpenAPI 3.1, Redocly clean
    ├── redocly.yaml
    ├── audio-engine.delta.md            # 2.7.0
    ├── ui-test-ids.delta.md             # 1.16.0
    └── help-topics.md
    # Phase 0 adds: backend-api.openapi.yaml (1.16.0), audio-engine.md (2.7.0), ui-test-ids.md (1.16.0)
```

### Source code (per lane; new unless marked "mod")

```text
src/audio-engine/src/                         # audio-engine lane
├── types.ts (mod), index.ts (mod), engine.ts (mod), mixer.ts (mod), channel.ts (mod)
├── effects/
│   ├── strip-chain.ts      # buildStripChain: fxIn, dry/wet, insert order, send tap
│   ├── eq.ts               # EqSettings -> biquad sections; cascade for 24 dB
│   ├── compressor.ts       # CompressorSettings -> DynamicsCompressor + make-up
│   ├── reverb-bus.ts       # pre-delay, convolver, return, meter tap
│   ├── impulse.ts          # deterministic IR generator (irVersion 1)
│   ├── presets.ts          # catalogue v1 (data-model 5)
│   ├── families.ts         # ChannelId -> InstrumentFamily
│   ├── validate.ts         # ranges/clamps for effects input
│   ├── eq-curve.ts         # SVG path from getFrequencyResponse
│   ├── reduction-meter.ts  # GR meter on the shared frame loop
│   └── load.ts             # cost units / renderCapacity
├── render/
│   ├── offline-gate.ts     # live gate vs 007 "selected channels" rule
│   ├── prerender.ts        # OfflineAudioContext build, pre-roll, progress, upload
│   └── wav-encoder.ts      # float32 WAV chunks
└── midi/
    ├── controller.ts       # createMidiController, ports, statechange
    ├── availability.ts     # support / secure context / permission
    ├── parser.ts           # bytes -> channel voice messages, cc14 pairing
    ├── bindings.ts         # message -> binding -> ControllerEvent (modes, invert)
    ├── pickup.ts           # soft takeover
    ├── coalescer.ts        # latest-wins batching on the frame loop
    └── learn.ts            # learn mode, inferred mode, timeout
tests/ (Vitest, mirrors the tree) + tests/fakes/{fake-audio-nodes.ts, fake-midi.ts}

src/StemStudio.Domain/                         # backend lane
├── Mixes/ChannelSettings.cs, DetailChannelSettings.cs, MixSettings.cs (mod: Effects)
├── Mixes/Effects/{ChannelEffects.cs, Eq.cs, EqBands.cs, Compressor.cs, MixEffects.cs, ReverbBus.cs}
├── Controllers/{ControllerProfile.cs, ControllerBinding.cs, MidiControl.cs, ControllerTarget.cs, BindingRules.cs}
└── Assets/{Prerender.cs, ExportEffectsRule.cs}
src/StemStudio.Contracts/{Mixes/*Effects*Dto.cs, Controllers/*Dto.cs, Assets/PrerenderDto.cs, ContractLimits.cs (mod)}
src/StemStudio.Persistence/{Configurations/MixConfiguration.cs (mod), PrerenderConfiguration.cs, ControllerProfileConfiguration.cs, Migrations/*_AddEffectsAndControllers.cs}
src/StemStudio.Api/
├── Prerenders/{PrerenderEndpoints.cs, WavHeaderReader.cs, PrerenderStore.cs, PrerenderSweeper.cs}
├── Controllers/ControllerProfileEndpoints.cs
├── Assets/ExportPlanner.cs, AssetService (mod: effects apply, retry, cleanup), ContentsText (mod)
├── Mixes/MixMapper.cs, MixValidator.cs (mod)
└── Program/Cors + Kestrel limits (mod)

src/StemStudio.Web/                            # frontend lane
├── Interop/{IAudioEngine.cs (mod), AudioEngineInterop.cs (mod), IMidiController.cs, MidiControllerInterop.cs}
├── Services/{EffectsState.cs, PracticeEqService.cs, ControllerSession.cs, ControllerDispatcher.cs, ControllerProfilesClient.cs, StripSlots.cs}
├── Components/Mixer/Effects/{EffectsPanel, EqSection, CompressorSection, SendSection, PresetMenu, PracticeEqMenu, ReverbBusSection, BypassAllToggle, EffectsLoad, ReductionMeter}.razor(+.cs)
├── Components/Mixer/{ChannelStrip (mod), MasterSection (mod), Mixer.Effects.cs, Mixer.Midi.cs, MidiStatusChip.razor, MidiLearnLayer.razor}
├── Components/Export/{ExportDialog (mod), EffectsOption.razor}
├── Components/Library/(asset row mod: effects badge)
├── Components/Controllers/{ProfileCard, BindingTable, BindingRow, TargetPicker, ControllerMonitor}.razor
├── Components/Pages/Controllers.razor
└── Help/topics/mixer/*.md (13), Help/topics/glossary/*.md (6), Help/tours/practice-eq.json

worker/tests/render/test_prerender_input.py    # stem-separation lane (test only)
e2e/                                           # qa lane
├── fixtures/{fake-midi.ts, no-midi.ts, pink-noise stems}
├── helpers/{band-energy.ts, wav-decode.ts}
├── tests/{effects-practice-eq, effects-panel, effects-persist, effects-export, effects-levels, effects-budget, midi-learn, midi-control, midi-fallback}.spec.ts
└── a11y/{effects.a11y.spec.ts, controllers.a11y.spec.ts}
```

**Structure Decision**: existing component layout; effects and MIDI are new sub-folders of the
existing engine package and Web components; no new project.

## Phases

- **Phase 0 (architect)**: accept ADR-026; full contract documents (backend 1.16.0 merged from
  the delta, engine 2.7.0 contract + `types.ts` stubs + `index.ts` export of
  `createMidiController` stub, ui-test-ids 1.16.0); DTO and domain record stubs with XML docs;
  spikes T007 (EF optional JSON complex type with a 1.15.0 database; IR generation time; offline
  worst-case render time in headless Chromium) recorded in research.md.
- **Phase 1 (foundational, parallel lanes)**: engine strip chain and validation; backend domain,
  persistence, mix API; frontend interop wrappers and `EffectsState`; qa fixtures (fake MIDI,
  pink noise, band-energy helper).
- **Phase 2..7**: user stories US1-US6 (tasks.md), each independently testable.
- **Phase 8 (polish)**: help topics and tour, a11y, budget test, SIG/Sonar, manuals, STATUS.

## Integration with parallel features (CTO at merge)

| With | Item | Rule |
|---|---|---|
| 012 practice tempo | MIDI targets `speed`, `transpose`, commands `speed.*`, `transpose.*`, `count-in.toggle` | dispatcher calls 012's mixer methods; when 012 is absent on a track the binding row shows "not available here" |
| 012 | tempo in exports | the pre-render uses the same graph builder; whether exports include speed follows 012's export rule (default original tempo) - `PrerenderRequest` gets 012's option if 012 defines one |
| 014 practice flow | command ids and shortcut registry | rename 017 command ids to 014's registry ids if they differ (contract text only; enum values in backend 1.16.0 follow the merged names) |
| 014 | foot pedals as keyboards | unaffected; MIDI pedals map like any note/CC |
| 015 recorded takes | take strips | `effects` member on 015's strip settings schema; take ids accepted by `setChannelEffects`; take family = take instrument or `other` |
| 016 instrument views | engine 2.6.0 | additive; no shared method names (checked at Phase 0) |
| 013 library care | backups | `ControllerProfiles` and `Prerenders` tables: profiles are backed up; pre-renders are excluded (temporary) |
| 011 agent | exports | agent exports keep `effects` absent (`ignore`); later catalogue version may add effects tools |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| EF Core optional nested complex type inside JSON collections not supported as expected | old mixes fail to load | spike T007; fallback `JsonColumn<T>` value converter as for assets |
| Browser memory for long pre-renders (~640 MB at 15 min) | tab crash on small machines | warning above 10 min; Blob chunks; range exports; documented |
| Compressor differs between Chromium and Firefox | listener on Firefox hears Firefox's compressor | export made in the same browser; documented; E2E effects run on Chromium |
| Upload 320 MB through a proxy | 413 from a proxy | devops checks compose has no proxy body limit below 320 MB (T104) |
| Blazor Server re-render cost with fast faders | sluggish UI | <= 30 Hz coalescing, strip components re-render only for their slot (`ShouldRender`) |
| 012/014 names differ | broken bindings at merge | integration table above; enum is additive, rename before release |
| ADR number collides with 012-016 | confusion | CTO renumbers at merge |
| Practice EQ curves miss SC-001 for a family | weaker practice value | T030 tunes values against the band test; data-model 5.2 updated |

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Browser pre-render + upload path for exports with effects | 007's "exports sound like the mixer" with a compressor | worker DSP cannot match the browser compressor and needs unallocated contract bumps; dropping effects from exports breaks FR-003 of 007 |
| MIDI module inside the audio-engine package | allocated contract version, shared frame loop | `web-interop` has no allocated version; a new package adds build/CI cost |
