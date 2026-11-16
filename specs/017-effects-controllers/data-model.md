# Data Model: Mixer Effects and Controllers (017)

All JSON is camelCase; absent optional members mean "default". Ranges are inclusive. The backend
DTOs live in `src/StemStudio.Contracts` (`Mixes/`, `Controllers/`, `Assets/`), the domain in
`src/StemStudio.Domain` (`Mixes/Effects/`, `Controllers/`), the engine types in
`src/audio-engine/src/types.ts` (contract 2.7.0).

## 1. Overview

```text
Mix ──settings──> MixSettings ──channels[]────────> ChannelSettings ──effects?──> ChannelEffects
                              ├─detailChannels[]──> DetailChannelSettings ─effects?─> ChannelEffects
                              └─effects?─────────> MixEffects ──reverb──> ReverbBus
ExportMix (007) reuses ChannelSettings/DetailChannelSettings, so an export snapshot carries effects.
CreateAudioExportRequest + effects (ignore|apply) + prerenderId ──> Prerender (staging file)
ControllerProfile 1──* ControllerBinding (control -> target, mode)
EffectPreset: engine catalogue only (not persisted)
```

## 2. Effects

### 2.1 `ChannelEffects` (per strip; `null`/absent = no effects)

| Field | Type | Range / default | Notes |
|---|---|---|---|
| `bypass` | bool | false | strip bypass: inserts and send off (saved) |
| `eq` | `Eq` \| null | null | null = EQ off, no nodes |
| `compressor` | `Compressor` \| null | null | null = compressor off, no nodes |
| `reverbSend` | number | 0..1, default 0 | linear send gain, post-fader |

`Eq`

| Field | Type | Range / default |
|---|---|---|
| `enabled` | bool | true (false keeps values, nodes removed) |
| `presetId` | string \| null | `^[a-z0-9]+(-[a-z0-9]+)*$`, <= 48; informational |
| `highPass` | `{enabled: bool, frequencyHz: 20..2000 (80), slope: 12\|24 (12)}` | default disabled |
| `lowShelf` | `{frequencyHz: 30..500 (120), gainDb: -18..18 (0)}` | |
| `mid1` | `{frequencyHz: 40..16000 (500), gainDb: -18..18 (0), q: 0.3..12 (1.0)}` | peaking |
| `mid2` | `{frequencyHz: 40..16000 (2500), gainDb: -18..18 (0), q: 0.3..12 (1.0)}` | peaking |
| `highShelf` | `{frequencyHz: 1000..16000 (8000), gainDb: -18..18 (0)}` | |
| `lowPass` | `{enabled: bool, frequencyHz: 500..20000 (12000), slope: 12\|24 (12)}` | default disabled |

Engine mapping: HP/LP 12 dB/oct = one biquad `highpass`/`lowpass` with Q 0.7071; 24 dB/oct = two
cascaded biquads with Butterworth Q 0.5412 and 1.3066. Shelves are `lowshelf`/`highshelf`
biquads (Q ignored by the spec), mids `peaking` with the given Q. A band at 0 dB is exactly unity
(cookbook formula, A = 1), so flat bands are left in the chain.

`Compressor`

| Field | Type | Range / default | `DynamicsCompressorNode` |
|---|---|---|---|
| `enabled` | bool | true | |
| `presetId` | string \| null | as above | |
| `thresholdDb` | number | -60..0 (-24) | `threshold` |
| `ratio` | number | 1..20 (3) | `ratio` |
| `kneeDb` | number | 0..40 (6) | `knee` |
| `attackMs` | number | 0..200 (10) | `attack` = ms/1000 |
| `releaseMs` | number | 10..1000 (200) | `release` = ms/1000 |
| `makeupDb` | number | 0..24 (0) | following `GainNode` = 10^(dB/20) |

### 2.2 `MixEffects` (per mix; `null`/absent = reverb bus at defaults, no sends active)

| Field | Type | Range / default |
|---|---|---|
| `reverb.kind` | enum `room`, `plate`, `hall` | `room` |
| `reverb.returnLevel` | number | 0..1 (0.5) |
| `reverb.predelayMs` | integer | 0..200 (20) |
| `reverb.irVersion` | integer | const 1 (research R-04) |

### 2.3 Derived rules

- **Active strip**: `effects != null && !bypass && (eq?.enabled || compressor?.enabled ||
  reverbSend > 0)`. **Mix has effects**: any active strip (standard or detail, hidden included).
- **Neutral strip** (UI sends `effects: null`): no EQ or disabled EQ with all gains 0 and HP/LP
  disabled, no or disabled compressor, `reverbSend` 0, `bypass` false.
- **Global bypass** (`setEffectsBypassed(true)`): engine-only, never stored, never exported.
- **Practice EQ markers** (UI): `eq.presetId` starting `practice-focus-` or `practice-room-`
  gives the strip `data-practice-eq="focus"` / `"make-room"`; Off clears `eq` of every strip whose
  `eq.presetId` starts with `practice-` (other effects untouched).

### 2.4 Validation (domain `ChannelEffects.Validate`, `MixEffects.Validate`)

Every range above; `slope` in {12, 24}; `presetId` pattern; unknown members refused
(`additionalProperties: false`). Paths in problems: `channels[2].effects.eq.mid1.q`,
`detailChannels[0].effects.compressor.ratio`, `effects.reverb.predelayMs`. Messages are readable
("Enter a Q between 0.3 and 12."). Presets are not validated against the catalogue (engine data).

### 2.5 Persistence

- `ChannelSettings`/`DetailChannelSettings` (domain records) gain `ChannelEffects? Effects`;
  stored inside the existing `Channels` / `DetailChannels` JSON columns as an optional nested
  complex type (research R-12). Old rows: member absent -> `null`.
- `Mix` gains `MixEffects? Effects` as a nullable JSON column `Effects` (migration
  `AddMixEffects`, nullable, no data change).
- `ExportMix` (007 snapshot inside `Provenance.recipe`) gains optional `effects` (MixEffects); the
  channel snapshots carry their `effects` through the shared schemas.

## 3. Controller profiles

### 3.1 `ControllerProfile` (table `ControllerProfiles`)

| Field | Type | Rules |
|---|---|---|
| `id` | uuid | server-generated |
| `deviceName` | string | 1..120, as reported by `MIDIInput.name` |
| `manufacturer` | string | 0..120, `MIDIInput.manufacturer` ("" when unknown) |
| `displayName` | string | 1..60, default = `deviceName` |
| `enabled` | bool | default true; a disabled profile ignores the device |
| `stripsPerBank` | integer | 1..16, default 8 |
| `bindings` | `ControllerBinding[]` | 0..256 (JSON column) |
| `createdAt`, `updatedAt` | date-time | |

Unique index `(DeviceName, Manufacturer)` (ordinal). Deleting a profile deletes its bindings.

### 3.2 `ControllerBinding`

| Field | Type | Rules |
|---|---|---|
| `id` | string | `^[a-z0-9]{8}$`, unique in the profile (client-generated) |
| `control.kind` | enum `cc`, `cc14`, `note`, `pitch-bend`, `program` | |
| `control.channel` | integer | 1..16 |
| `control.number` | integer \| null | `cc`, `note`, `program`: 0..127; `cc14`: 0..31 (MSB; LSB = n+32); `pitch-bend`: null |
| `target` | `ControllerTarget` | 3.3 |
| `mode` | enum 3.4 | must suit the target kind (3.5) |
| `invert` | bool | default false |
| `pickup` | bool | default true for absolute continuous targets, else false |

### 3.3 `ControllerTarget`

| `kind` | Extra | Value | Same as on screen |
|---|---|---|---|
| `strip-volume` | `slot` 1..16 | continuous 0..1 -> fader | `fader-<key>` |
| `strip-pan` | `slot` | continuous -> pan -1..1 | `pan-<key>` |
| `strip-mute` | `slot` | boolean | `mute-<key>` |
| `strip-solo` | `slot` | boolean | `solo-<key>` |
| `strip-reverb-send` | `slot` | continuous 0..1 | `fx-reverb-send` |
| `strip-bypass` | `slot` | boolean | `fx-bypass-<key>` |
| `master-volume` | - | continuous | `master-fader` |
| `reverb-return` | - | continuous | `reverb-return` |
| `speed` | - | continuous -> 50..120 % (012) | 012 speed control |
| `transpose` | - | continuous -> -12..12 semitones, rounded (012) | 012 transpose control |
| `command` | `command` (enum below) | trigger | the matching button/shortcut |

Commands: `transport.play-pause`, `transport.stop`, `transport.to-start`, `loop.toggle`,
`loop.set-a`, `loop.set-b`, `metronome.toggle`, `count-in.toggle`, `speed.up`, `speed.down`,
`speed.reset`, `transpose.up`, `transpose.down`, `transpose.reset`, `effects.bypass-all`,
`bank.next`, `bank.previous`. (Names are aligned with 014's command registry at merge; a rename
is a CTO integration task, plan "Integration".)

**Slots**: slot s in bank b addresses the `(b * stripsPerBank + s)`-th visible strip in mixer order
(standard strips in catalogue order with expanded groups' detail strips in place of their parent,
then 015 take strips). An empty slot ignores input. The bank is runtime state (starts at 0 per
page load), never saved.

### 3.4 Modes

`absolute` (7-bit 0..127 -> 0..1; `cc14`/`pitch-bend` 14-bit 0..16383), `relative-twos`,
`relative-signed`, `relative-offset` (research R-09; step 1/127 per tick), `toggle` (each press,
i.e. note-on or CC >= 64 after < 64, flips), `momentary` (on while held), `trigger` (fires on
press).

### 3.5 Validation matrix

| Target kind | Allowed modes |
|---|---|
| continuous (`strip-volume`, `strip-pan`, `strip-reverb-send`, `master-volume`, `reverb-return`, `speed`, `transpose`) | `absolute`, `relative-*` |
| boolean (`strip-mute`, `strip-solo`, `strip-bypass`) | `toggle`, `momentary` |
| `command` | `trigger` |

Plus: `pickup` only with `absolute`; `cc14`/`pitch-bend` only with `absolute`; `program` only with
`trigger`; no two bindings may share a control (a `cc14` on n also occupies `cc` n and n+32 on the
same channel). Violations -> 422 with paths `bindings[3].mode`.

## 4. Pre-render (table `Prerenders`, staging)

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | also the staging folder: `assets/{id}/prerender.wav` (allowed worker input) |
| `trackId` | uuid | FK to the track, cascade delete (files removed) |
| `sizeBytes` | long | <= `StemStudio:MaxPrerenderBytes` (320,000,000) |
| `frames` | long | from the WAV header; `durationSeconds = frames / 44100` |
| `createdAt` | date-time | swept when unused after 60 min (`StemStudio:PrerenderTtlMinutes`) |
| `assetId` | uuid \| null | set when an export consumes it (file moved to `assets/{assetId}/prerender.wav`) |

Accepted file: RIFF/WAVE, `fmt ` format 3 (IEEE float), 32 bits, 2 channels, 44,100 Hz, a `data`
chunk whose size is a multiple of 8, no other required chunks; otherwise `415
unsupported-prerender` (detail names the field).

Export with effects (additions to 007's request and recipe):

| Field | Where | Rules |
|---|---|---|
| `effects` | `CreateAudioExportRequest`, `ExportRecipe` | enum `ignore` (default when absent), `apply`; `apply` only in mode `mix` |
| `prerenderId` | `CreateAudioExportRequest` | required with `apply`; must exist, be unconsumed, belong to `trackId`, and have `frames == round((end - start) * 44100)` +-2 (range null = track duration) |

`Provenance.contents` gains the suffix ", with effects" (`apply`) or ", without effects" (`ignore`
and the snapshot has effects). After the asset completes the pre-render file is deleted; a failed
asset keeps it for retry; deleting the asset deletes it.

## 5. Preset catalogue (engine `presets.ts`, catalogue version 1)

### 5.1 Family of a channel

Detail name first, then the standard stem; `<parent>.rest` takes the parent's family; 015 take
strips use the take's instrument family when 015 provides one, else `other`.

| Family | Channels |
|---|---|
| `vocals` | `vocals`, `*.lead`, `*.backing` |
| `drums` | `drums`, `*.kick`, `*.snare`, `*.toms`, `*.hihat`, `*.cymbals`, `*.percussion` |
| `bass` | `bass` |
| `guitar` | `guitar`, `*.acoustic-guitar`, `*.electric-guitar` |
| `keys` | `piano`, `*.keys`, `*.synth` |
| `orchestral` | `*.strings`, `*.brass`, `*.woodwinds` |
| `other` | `other`, `other.rest` |

### 5.2 Practice EQ (initial values; tuned by T030 against 5.3, changes recorded here)

Focus = HP and LP 24 dB/oct plus a presence peak on the strip itself. Make room = cuts on every
other strip. Ids: `practice-focus-<family>[-<variant>]`, `practice-room-<family>`.

| Family | Focus HP / LP (Hz) | Focus peak | Make room |
|---|---|---|---|
| vocals | 120 / 9,000 | mid2 3,000 Hz +3 dB Q 1.0 | mid1 1,000 Hz -18 dB Q 0.7; mid2 3,000 Hz -18 dB Q 1.0 |
| drums | 40 / 16,000 | mid1 80 Hz +2 Q 1.0; mid2 5,000 Hz +2 Q 1.0 | mid1 70 Hz -18 Q 1.0; mid2 220 Hz -18 Q 1.4; high shelf 8,000 Hz -12 |
| drums, variant `kick` | 30 / 6,000 | mid1 60 Hz +3 Q 1.2 | (family) |
| drums, variant `snare` | 100 / 12,000 | mid1 200 Hz +3 Q 1.2 | (family) |
| drums, variant `cymbals` (`hihat`, `cymbals`) | 2,500 / off | high shelf 10,000 Hz +2 | (family) |
| bass | 35 / 3,000 | mid1 100 Hz +3 Q 1.0 | low shelf 250 Hz -18; mid1 700 Hz -9 Q 1.0 |
| guitar | 80 / 7,000 | mid2 2,000 Hz +3 Q 1.0 | mid1 400 Hz -18 Q 1.0; mid2 2,000 Hz -18 Q 0.9 |
| keys | 50 / 10,000 | mid2 1,500 Hz +2 Q 1.0 | mid1 300 Hz -18 Q 0.8; mid2 1,200 Hz -18 Q 0.8 |
| orchestral | 150 / 12,000 | mid2 2,500 Hz +2 Q 1.0 | mid1 500 Hz -18 Q 0.8; mid2 2,500 Hz -18 Q 0.9 |
| other | 60 / 14,000 | none | mid1 600 Hz -18 Q 0.7; mid2 2,500 Hz -18 Q 0.7 |

Applying a practice EQ replaces the strip's `eq` (enabled, with the preset id); compressor and
send stay. Make room skips **only the chosen strip**, not other strips of the same family:
bleed of that range in a sibling strip is cut as well (Make room for `drums.kick` also cuts the
kick range on `drums.snare`).

### 5.3 Test bands (SC-001; pink-noise input, offline render, energy in dB)

- Focus: for HP at f: band [f/4, f/2]; for LP at f (when 2f <= 20 kHz): band [2f, min(4f, 20 kHz)];
  each >= 12 dB below the unprocessed render.
- Make room: each cut peak at fc: band [fc/2^(1/3), fc*2^(1/3)]; low shelf at f: [f/6, f/1.6];
  high shelf at f: [1.6f, min(2.5f, 20 kHz)]; each >= 12 dB below (shelves at -12: >= 9 dB).

### 5.4 Tone and dynamics presets

| Id | Scope | Values |
|---|---|---|
| `clean-low-end` | eq | HP 80 Hz 12 dB/oct |
| `warm` | eq | low shelf 200 Hz +3; high shelf 8,000 Hz -2 |
| `bright` | eq | high shelf 6,000 Hz +4 |
| `less-boxy` | eq | mid1 400 Hz -4 Q 1.4 |
| `presence` | eq | mid2 3,000 Hz +3 Q 1.0 |
| `gentle-glue` | compressor | -18 dB, 2:1, knee 10, 30 ms, 250 ms, +2 dB |
| `leveler` | compressor | -24 dB, 4:1, knee 6, 10 ms, 150 ms, +6 dB |
| `punch` | compressor | -20 dB, 4:1, knee 2, 25 ms, 100 ms, +4 dB |
| `tight-low-end` | compressor | -22 dB, 5:1, knee 4, 5 ms, 120 ms, +5 dB |

`EffectPreset` (engine type): `{ id, name, scope: 'eq'|'compressor', mode?: 'focus'|'make-room',
families: InstrumentFamily[] (empty = all), variantOf?: string, eq?: Eq, compressor?: Compressor }`.

## 6. UI state (Blazor, not persisted beyond the mix)

- `MixerState.Effects: Dictionary<ChannelId, ChannelEffects?>`, `MixerState.MixEffects`,
  `MixerState.GlobalBypass` (transient), `MixerState.Clipboard` (copy/paste, transient).
- `ControllerSession`: support/permission state, connected devices, active profile, bank, learn
  target, last message (Expert monitor).
