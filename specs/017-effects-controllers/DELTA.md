# Contract delta: Mixer Effects and Controllers (017)

Versions (docs/ROADMAP.md allocation): **backend-api 1.16.0**, **audio-engine 2.7.0**,
**ui-test-ids 1.16.0**. Unchanged: worker-api (render path reused, research R-03),
render-vectors (1.0.0 gate cases reused by the engine), help-content (1.0.0 format, area `mixer`),
web-interop, agent tool catalogue (effects tools for the 011 agent come in a later catalogue minor
version, as the roadmap says for all backlog features).

Every change is **additive**: a request, mix or engine call without the new members behaves
exactly as in the previous version. The full documents are produced in Phase 0 (T001-T004) on top
of whatever versions are current when 017 starts (012-016 bump the same documents first; they are
being specified in parallel). The delta documents next to this file are binding:

| Contract | Delta document | Base when written | Validation |
|---|---|---|---|
| backend-api 1.16.0 | [contracts/backend-api.delta.openapi.yaml](contracts/backend-api.delta.openapi.yaml) (standalone OpenAPI 3.1) + section 2 below | 1.15.0 (016) | Redocly 2.78.0: valid, 3 warnings (`no-unused-components` for `ChannelEffects`, `MixEffects`, `ExportEffects`, which attach to existing schemas in T002) |
| audio-engine 2.7.0 | [contracts/audio-engine.delta.md](contracts/audio-engine.delta.md) | 2.6.0 (016) | `tsc --noEmit` in Phase 0 T003 |
| ui-test-ids 1.16.0 | [contracts/ui-test-ids.delta.md](contracts/ui-test-ids.delta.md) | 1.15.0 (016) | id-presence test (frontend) |
| help topics | [contracts/help-topics.md](contracts/help-topics.md) | help-content 1.0.0 | help tests (004) |

## 1. Backend API: new operations

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| POST | `/api/tracks/{trackId}/prerenders` | `createPrerender` | `audio/wav` (float32, 2 ch, 44.1 kHz), `Content-Length` <= 320,000,000 | `201 Prerender`; `404`; `409 track-not-ready` / `insufficient-disk-space`; `411`; `413 prerender-too-large`; `415 unsupported-prerender` |
| DELETE | `/api/prerenders/{prerenderId}` | `deletePrerender` | - | `204` (idempotent); `409 prerender-consumed` |
| GET | `/api/controller-profiles` | `listControllerProfiles` | - | `200 ControllerProfile[]` (by display name) |
| POST | `/api/controller-profiles` | `createControllerProfile` | `ControllerProfileInput` | `201`; `409 profile-exists` (extension `profileId`); `422` |
| GET | `/api/controller-profiles/{profileId}` | `getControllerProfile` | - | `200`; `404` |
| PUT | `/api/controller-profiles/{profileId}` | `updateControllerProfile` | `ControllerProfileInput` | `200`; `404`; `409 profile-exists`; `422` |
| DELETE | `/api/controller-profiles/{profileId}` | `deleteControllerProfile` | - | `204` (idempotent) |

New schemas: `PassBand`, `ShelfBand`, `PeakBand`, `PresetId`, `Eq`, `Compressor`,
`ChannelEffects`, `ReverbKind`, `ReverbBus`, `MixEffects`, `ExportEffects`, `Prerender`,
`MidiControlKind`, `MidiControl`, `ControllerTargetKind`, `ControllerCommand`, `ControllerTarget`,
`BindingMode`, `ControllerBinding`, `ControllerProfileFields`, `ControllerProfileInput`,
`ControllerProfile`. New tags `prerenders`, `controllers`.

## 2. Backend API: additions to existing schemas

| Schema | New member | Schema | Default / absent |
|---|---|---|---|
| `ChannelSettings` | `effects` | `ChannelEffects` or null | absent = null = no effects |
| `DetailChannelSettings` | `effects` | `ChannelEffects` or null | same |
| `MixSettings` | `effects` | `MixEffects` or null | absent = default reverb bus, no sends |
| `ExportMix` | `effects` | `MixEffects` or null | same |
| `CreateAudioExportRequest` | `effects` | `ExportEffects` | `ignore` |
| `CreateAudioExportRequest` | `prerenderId` | uuid or null | required when `effects` = `apply` |
| `ExportRecipe` | `effects` | `ExportEffects` | `ignore` (recipes stored before 1.16.0) |
| (015 take strip settings) | `effects` | `ChannelEffects` or null | CTO adds at merge if 015 defines its own strip schema |

New problem types on `createAudioExport`: `422 prerender-required`, `404 prerender-not-found`,
`409 prerender-consumed`, `422 prerender-mismatch`, `422 effects-not-allowed` (mode `raw`).
`retryAsset` of an effects export answers `409 prerender-missing` when the pre-render file is gone.
`Provenance.contents` gets the suffix ", with effects" / ", without effects" (text only, no schema
change). Mix validation problems use paths such as `channels[2].effects.eq.mid1.q`.

Examples to add in the full document: a mix with a practice EQ and a reverb send, an
`apply` export request, a `prerender-mismatch` problem, a controller profile (in the delta).

Behaviour notes for the full document: CORS allows `POST` with `Content-Type: audio/wav` from the
web origin on `/api/tracks/{trackId}/prerenders` only; Kestrel's body limit is raised for that
endpoint only; deleting a track deletes its unused pre-renders; deleting an asset deletes its
`prerender.wav`; completed assets have no pre-render.

## 3. Audio engine 2.7.0 (summary; binding text in contracts/audio-engine.delta.md)

- Types: `InstrumentFamily`, `PassBand`, `ShelfBand`, `PeakBand`, `EqSettings`,
  `CompressorSettings`, `ChannelEffects`, `ReverbKind`, `ReverbSettings`, `EffectPreset`,
  `EffectsLoad`, `PrerenderRequest`, `PrerenderResult`; `MeterTarget` adds `'reverb'`;
  `DotNetCallbacks` adds `'OnRenderProgress'`.
- `AudioEngine`: `setChannelEffects`, `setReverb`, `setEffectsBypassed`, `getEffectPresets`,
  `familyOf`, `attachEqCurve`/`detachEqCurve`, `attachReductionMeter`/`detachReductionMeter`,
  `getEffectsLoad`, `renderPrerender`.
- New factory `createMidiController(dotnetRef, options)` -> `MidiController` (`availability`,
  `connect`, `devices`, `setBindings`, `syncValues`, `startLearn`, `cancelLearn`, `dispose`);
  callbacks `OnControllerInput`, `OnControllerLearned`, `OnControllerLearnTimeout`,
  `OnControllerDevicesChanged`, `OnControllerAvailabilityChanged`.

## 4. UI test ids 1.16.0 (summary)

Mixer: `practice-eq-*`, `strip-has-effects-<key>`, `fx-*` (panel, EQ, compressor, send, presets,
bypass, numeric entry, load), `reverb-kind`, `reverb-return`, `reverb-predelay`, `meter-reverb`,
`midi-status`, `midi-learn-*`, `midi-bank-indicator`, attributes `data-practice-eq`,
`data-midi-learn`, `data-midi-bound`. Export dialog: `export-effects`, `export-effects-note`,
`export-prerender-progress`, `export-prerender-cancel`. Library: `asset-effects-<assetId>`. New
page `/controllers`: `nav-controllers`, `controllers-*`. Level rule R10.

## 5. Not changed (and why)

- **worker-api**: the effects export reuses `POST /v1/render-jobs` with one input
  `assets/{assetId}/prerender.wav` (pattern `assets/{uuid}/[a-z][a-z0-9-]{0,31}\.(flac|wav|mp3)`
  already allowed), gain 1, pan 0, master 1. A worker test confirms float32 WAV input renders
  bit-exact (T021, stem-separation lane, test only).
- **render-vectors 1.0.0**: the engine consumes its gate cases for the offline selection gate; no
  new vectors (effects parity is by construction, not by vectors).
- **Agent tool catalogue**: unchanged in 017; agent exports keep `effects` absent (= `ignore`).
