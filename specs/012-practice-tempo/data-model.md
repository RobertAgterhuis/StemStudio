# Data model: Practice Tempo (012)

Contracts: [contracts/DELTA.md](contracts/DELTA.md). Design: ADR-021. All values are integers;
times inside the engine are seconds (track time unless marked "context time").

## 1. Configuration

No new environment variables and no new backend or worker settings. Build-time constants:

| Name | Where | Value |
|---|---|---|
| `STRETCH_DELAY_FRAMES` | `src/audio-engine/src/stretch/constants.ts` | **5120** (Phase 0 P0-1; multiple of 128; 116.1 ms at 44.1 kHz) |
| `STRETCH_CONTENT_FRAMES_PER_OCTAVE` | `src/audio-engine/src/stretch/constants.ts` | **-1100** (Phase 0 P0-1; content offset `c(p) = round(-1100 * log2(p))` frames) |
| `STRETCH_ABI_VERSION` | `src/audio-engine/src/stretch/constants.ts` | 1 (`st_abi_version()`) |
| `MIN_PASS_SECONDS` | `src/audio-engine/src/practice/passes.ts` | 0.5 |
| `CHANGE_LEAD_SECONDS` | `src/audio-engine/src/practice/tempo.ts` | 0.02 (added to L for a live change) |
| `SOUNDTOUCH_VERSION` | `third_party/soundtouch/soundtouch.lock` | `2.4.1`, tarball SHA-256 `e07abf20...6733`, emsdk 6.0.13 image digest, flags |

## 2. Practice settings (persisted in the mix)

### 2.1 Fields and defaults

| Field | Type | Range | Default | Meaning |
|---|---|---|---|---|
| `speedPercent` | int | 50..120 | 100 | playback speed, pitch kept |
| `transposeSemitones` | int | -12..12 | 0 | pitch shift, speed kept |
| `trainer.enabled` | bool | | false | speed trainer on (effective only with an enabled loop >= 0.5 s) |
| `trainer.startPercent` | int | 50..119 | 70 | speed of pass 1 |
| `trainer.stepPercent` | int | 1..20 | 5 | increase per step |
| `trainer.targetPercent` | int | 51..120 | 100 | final speed; must be > `startPercent` |
| `trainer.passesPerStep` | int | 1..8 | 1 | completed passes per speed |
| `countIn.bars` | int | 0..2 | 0 | 0 = off |
| `countIn.everyPass` | bool | | false | count-in before every loop pass too |

"Neutral" = `speedPercent == 100 && transposeSemitones == 0 && !trainer.enabled` (the stretch stage
is not needed; count-in does not need it either).

### 2.2 Validation (backend domain and engine, same rules)

1. Every field present after defaulting; integers only (JSON numbers with a fraction -> 422 / engine
   error).
2. Ranges as in 2.1.
3. `trainer.startPercent < trainer.targetPercent` (always, also when the trainer is disabled), error
   path `practice.trainer.targetPercent`, message "The target must be above the start speed.".
4. No rule couples practice to the loop: trainer / `everyPass` without a loop are valid and inactive.

Backend error paths: `practice.speedPercent`, `practice.transposeSemitones`,
`practice.trainer.<field>`, `practice.countIn.<field>` (as `metronome.bpmOverride` in 001).

### 2.3 Layers

| Layer | Type | Notes |
|---|---|---|
| Contracts (architect) | `PracticeSettingsDto(int SpeedPercent, int TransposeSemitones, SpeedTrainerSettingsDto Trainer, CountInSettingsDto CountIn)` with `static Default`; `SpeedTrainerSettingsDto(bool Enabled, int StartPercent, int StepPercent, int TargetPercent, int PassesPerStep)`; `CountInSettingsDto(int Bars, bool EveryPass)`; `MixSettingsDto.Practice { get; init; } = PracticeSettingsDto.Default` | init-only property keeps the positional record constructor of 1.2.0 source-compatible |
| Domain (backend) | `Domain/Mixes/PracticeSettings.cs`, `SpeedTrainerSettings.cs`, `CountInSettings.cs` (records with defaults and `Validate(ValidationErrors)`); `MixSettings.Practice` (default `PracticeSettings.Default`) | `MixSettings.Validate` calls `Practice.Validate(errors.Nested("practice"))` |
| Persistence (backend) | `Mixes.Practice` TEXT NULL (JSON), EF `ComplexProperty(mix => mix.Practice, p => p.ToJson())` with nested complex `Trainer`, `CountIn` | migration `AddMixPractice`; `NULL` -> `PracticeSettings.Default` on read (mixes from before 012) |
| API (backend) | `MixMapper` maps both ways; absent in a request -> default | GET always returns `practice` |
| Web (frontend) | `MixerState.Practice` (current settings, dirty tracking like metronome) | sent to the engine as JSON (same camelCase shape) |
| Engine (audio-engine) | `PracticeSettings` (types.ts 2.3.0) | validated again by `practice/settings.ts` |

JSON (camelCase, as in the OpenAPI document):

```json
"practice": {
  "speedPercent": 75,
  "transposeSemitones": -2,
  "trainer": { "enabled": true, "startPercent": 60, "stepPercent": 5, "targetPercent": 100, "passesPerStep": 2 },
  "countIn": { "bars": 1, "everyPass": true }
}
```

## 3. Practice status (engine, live, not persisted)

See `PracticeStatus` in contracts/audio-engine.md. Derived UI values:

- `effectiveBpm = round(trackBpm * speedHeard / 100)` where `trackBpm` = `metronome.bpmOverride` ??
  (grid reliable ? `beatGrid.bpm` : none).
- `qualityHint = speedHeard < 70 || abs(transposeSemitones) > 7`.

### 3.1 Stretch state machine

```text
bypass --setPractice(non-neutral)--> loading --ready--> active
loading --error--> unavailable (reason; speed 100, st 0, trainer off; count-in still applied)
active --restart at neutral settings (play from paused/stopped, seek, loadTrack)--> bypass (stage kept loaded, disconnected)
active --processor error message--> unavailable (sources restarted on the bypass path at the heard position)
unavailable --setPractice(non-neutral), first time after a loadTrack--> loading (one retry per track)
```

### 3.2 Trainer state machine

```text
off --enabled && loop enabled && loop >= 0.5 s--> waiting
waiting --play--> running (pass 1 at startPercent)
running --pass completed, completed % passesPerStep == 0--> running (speed = min(speed + step, target)) | target (when speed == target)
running/target --pause--> waiting (pass and speed kept; play resumes them)
any --stop | loadTrack | loop start/end changed | any trainer field changed--> waiting (pass 1) or off
any --disabled | loop disabled | loop < 0.5 s--> off
```

### 3.3 Count-in plan

Input: start position `A`, rate `r`, metronome click grid, `bars`, `beatsPerBar`.
`d` = `60 / bpmOverride` or the grid interval at the first click `b >= A` (`b = A` with an override
and no grid). `N = bars * beatsPerBar`. Heard music start `T0`. Click *k* (k = N..1) at context
time `T0 + (b - A) / r - k * d / r`, accent when `(N - k) % beatsPerBar == 0`. Indicator beats are
the same times. Not available: `bars == 0`, or no override and (no grid or grid not reliable).

## 4. Engine-internal transport anchor (not part of types.ts)

```ts
interface TransportAnchor { when: number; offset: number; rate: number }   // rate = 1 before 012
// heard position:  offset + (t - when) * rate  (wrapped into a native loop)
// source start:    start(when - latency, offset)   latency = stage engaged ? L(p) : 0
//                  L(p) = (D + c(p)) / sampleRate, c(p) = round(-1100 * log2(p)) frames (Phase 0 P0-1)
```

Explicit passes keep a short queue of anchors `{when: T_k, offset: A, rate: r_k}`; `positionAt(t)`
uses the last anchor with `when <= t` and returns `A` inside a count-in gap.

## 5. UI state and preferences

- No new `localStorage` keys: practice settings live in the mix. The count-in bar count chosen at
  Advanced is part of the settings (`bars`); the Beginner switch toggles between 0 and the last
  non-zero value (default 1).
- Feature catalogue entries (frontend, `FeatureCatalog`): `practice.speed-reset` (Advanced),
  `practice.trainer` (Advanced), `practice.count-in.options` (Advanced), `practice.numeric`
  (Expert), `practice.stretch-status` (Expert); help topics as in plan.md.

## 6. Static files served by the web app

| Path | Licence | Source |
|---|---|---|
| `/js/time-stretch-processor.js` | MIT | Vite entry `src/audio-engine/src/stretch/processor.ts` |
| `/js/soundtouch/soundtouch.wasm` | LGPL-2.1 | `scripts/build-soundtouch.*` |
| `/third-party/soundtouch/{COPYING.TXT, NOTICE.md, soundtouch-2.4.1.tar.gz, shim/*, build-soundtouch.sh}` | LGPL-2.1 (source) | copied by the web image build |
