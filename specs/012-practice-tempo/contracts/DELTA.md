# Contract delta: feature 012 (practice tempo)

Feature 012 adds one optional field group to mixes in the backend API, practice methods and a
callback to the audio engine, and additive UI test ids. The **worker API**, the help-content
contract (1.0.0), the web-interop contract (1.0.0) and the render vectors (1.0.0) are
**unchanged**. Every change is additive ("newer contains older"): all older operations, schemas,
engine calls and ids keep their meaning.

## Status (012 spec, 2026-11-16; superseded by section 5 "Phase 0 outcome")

| Contract | Base on this branch | 012 document | State |
|---|---|---|---|
| Backend API | 1.9.0 (`specs/010-lyrics-to-song`) | [backend-api.openapi.yaml](backend-api.openapi.yaml) `1.11.0-delta.1` = 1.9.0 + 012; Redocly-valid (OpenAPI 3.1, 6 warnings, all inherited from 1.9.0); `check_superset.py` against 1.9.0: **superset: yes** | binding for the wave; becomes `1.11.0` in Phase 0 (T003) by merging the 1.10.0 agent paths of 011 unchanged (no shared names: 012 adds only `PracticeSettings`, `SpeedTrainerSettings`, `CountInSettings`, the example `MixSettingsWithPractice` and the property `MixSettings.practice`) |
| Audio engine | 2.1.0 (`specs/006-synced-lyrics-karaoke`) | [audio-engine.md](audio-engine.md) **2.3.0** (full document: 2.1.0 text + 2.3.0 sections) | binding; 2.2.0 is reserved for 011 ("if needed") and is merged unchanged if 011 publishes it |
| UI test ids | 1.9.0 (+ 011 delta, expected 1.10.0) | [ui-test-ids.delta.md](ui-test-ids.delta.md) (names binding) | full `ui-test-ids.md` 1.11.0 written in Phase 0 (T003) |
| C# DTOs | - | `src/StemStudio.Contracts/Mixes/{PracticeSettingsDto,SpeedTrainerSettingsDto,CountInSettingsDto}.cs`, `MixSettingsDto.Practice` | written in Phase 0 (T004) |
| `types.ts` | 2.1.0 | 2.3.0 additions (audio-engine.md "Practice tempo") | written in Phase 0 (T005) |
| WASM ABI | - | audio-engine.md "WASM ABI", ABI version 1 | binding for the shim and `stretch/abi.ts` |

Why delta documents: the 011 full backend 1.10.0 and ui-test-ids 1.10.0 do not exist yet on
`main` (011 Phase 0 part 1 published standalone deltas), and features 013-016 are specified in
parallel. Writing 1.11.0 on top of an unmerged 1.10.0 would duplicate 011's work; the merge is
mechanical because the name sets are disjoint: no 011 path or component name equals a 012
addition. (The two response names 011 shares with 1.9.0, `NotFound` and `ValidationProblem`, are
011's own merge question; 012 does not touch them.)

## 1. Backend API 1.11.0 (additive)

| Change | Detail |
|---|---|
| `MixSettings.practice` | optional `PracticeSettings`; absent in `POST /api/mixes` / `PUT /api/mixes/{mixId}` = defaults; always present in `Mix` responses (`GET`, `POST`, `PUT`, duplicate); mixes saved before 1.11.0 return the defaults |
| `PracticeSettings` | `speedPercent` int 50..120 (100), `transposeSemitones` int -12..12 (0), `trainer`, `countIn` |
| `SpeedTrainerSettings` | `enabled` (false), `startPercent` 50..119 (70), `stepPercent` 1..20 (5), `targetPercent` 51..120 (100), `passesPerStep` 1..8 (1); `startPercent < targetPercent` -> else 400 validation problem at `practice.trainer.targetPercent` |
| `CountInSettings` | `bars` 0..2 (0 = off), `everyPass` (false) |
| Unchanged | `MixSummary`, `ExportMix` and every export request (practice never affects exports), all other paths |

Validation errors use the existing `ValidationProblem` (400, `errors` map) of the mixes endpoints.

## 2. Audio engine 2.3.0 (additive)

Summary (full text: [audio-engine.md](audio-engine.md) "Practice tempo"):

- `setPractice(settings: PracticeSettings): Promise<void>`, `getPracticeStatus(): PracticeStatus`,
  callback `OnPracticeChanged(PracticeStatus)`.
- Types `PracticeSettings`, `SpeedTrainerSettings`, `CountInSettings`, `PracticeStatus`,
  `StretchState`, `TrainerState`, `TimeStretchOptions`, `TimeStretchStage`;
  `EngineOptions.timeStretch`, `EngineOptions.timeStretchFactory` (test seam).
- Errors: `Invalid practice settings: ...`, `Time stretch unavailable: ...`.
- Behaviour: rate-aware anchor, latency-compensated source starts, one SoundTouch stage on the sum,
  live changes at `now + L + 20 ms`, explicit loop passes for trainer / count-in every pass,
  count-in on the song's grid. Neutral settings = 2.1.0 behaviour, no module loaded.
- Packaging: `time-stretch-processor.js` (MIT) and `soundtouch/soundtouch.wasm` (LGPL-2.1) as
  separately served build artefacts.

## 3. UI test ids 1.11.0 (additive)

See [ui-test-ids.delta.md](ui-test-ids.delta.md): `practice-*` (panel, speed, transpose, reset,
effective BPM, quality hint, unavailable, stretch status), `countin-*`, `trainer-*`,
`export-practice-note`; level rules; help markers; shortcut commands (`-`, `=`, `,`, `.`, `r`,
`c`, `t`).

## 4. Help content (004 help-content 1.0.0, no version change)

New topics in area `mixer` (files `src/StemStudio.Web/Help/topics/mixer/<id>.md`, `reviewed: "012"`):

| id | title | feature | levels | anchors |
|---|---|---|---|---|
| `practice-speed` | Speed (slow down) | - | all | `practice-speed` |
| `practice-transpose` | Transpose (change key) | - | all | `practice-transpose` |
| `practice-count-in` | Count-in | `practice.count-in.options` | all | `practice-count-in` |
| `practice-speed-trainer` | Speed trainer | `practice.trainer` | advanced, expert | `practice-speed-trainer` |
| `practice-sound-quality` | Sound quality at other speeds and keys | `practice.stretch-status` | all | `practice-sound-quality` |
| `semitone` (area `glossary`) | Semitone | - | all | - |
| `count-in` (area `glossary`) | Count-in | - | all | - |

`practice-sound-quality` explains the artefacts below 70 % and beyond +-7 semitones, the missing
formant correction, the meter lead while engaged, that exports keep the original speed and key, and
links `/third-party/soundtouch/` (SoundTouch, LGPL-2.1). Existing topics gain a "related" link:
`mixer-metronome` -> `practice-count-in`, `mixer-loop` -> `practice-speed-trainer`,
`mixer-screen` -> `practice-speed`; `export-dialog` gains one sentence on the original speed and key
(`reviewed: "012"`). Tours: `mixer-basics` gains a step on `practice-speed` (tour `version` 2),
`mixer-advanced` a step on `practice-speed-trainer` (tour `version` 2).

## 5. Phase 0 outcome (architect, 2026-11-16, tasks.md T001-T007)

| Contract | Version | Document | Notes |
|---|---|---|---|
| Backend API | **1.11.0** | [backend-api.openapi.yaml](backend-api.openapi.yaml) | = the full **1.21.0** on `main` (011 + claude-key follow-up) + the 012 additions unchanged; label 1.11.0 per the ROADMAP rule "full-document labels after an out-of-order merge"; `info.x-stemstudio-contains` lists the merged labels. Redocly 2.78.0: valid, 9 warnings (all `operation-4xx-response`, identical to 1.21.0). `check_superset.py`: **superset: yes** against 1.21.0 (new default) and 1.9.0; against 011's 1.10.0 it reports exactly the 6 entries 1.21.0 itself changed (inherited) |
| UI test ids | **1.11.0** | [ui-test-ids.md](ui-test-ids.md) | = 1.21.0 + [ui-test-ids.delta.md](ui-test-ids.delta.md) unchanged (section "Additions in 1.11.0"); example text of `practice-stretch-status` now "latency 106 ms" |
| Audio engine | **2.3.0** | [audio-engine.md](audio-engine.md), `src/audio-engine/src/types.ts` | 2.2.0 stays unused (011 needed no engine change). Phase 0 refinements below |
| WASM ABI | **1** | audio-engine.md "WASM ABI", `third_party/soundtouch/include/stemstudio_soundtouch.h` | no imports; `_initialize` called once first |
| C# | - | `StemStudio.Contracts/Mixes/{PracticeSettingsDto,SpeedTrainerSettingsDto,CountInSettingsDto,PracticeLimits}.cs`, `MixSettingsDto.Practice` | |

Refinements of the engine contract from the spikes (binding for the wave; ADR-021 "Phase 0 outcome"):

1. `STRETCH_DELAY_FRAMES = 5120` (116.1 ms at 44.1 kHz).
2. Heard latency `L(p) = (D + c(p)) / sampleRate`, `c(p) = round(-1100 * log2(p))` frames
   (`STRETCH_CONTENT_FRAMES_PER_OCTAVE`): sources start `L(p)` early; live changes enter the stage
   at `U = now + 0.02`, are heard from `T = U + D / sampleRate` and re-anchor to
   `when = T + c(p) / sampleRate`. `PracticeStatus.latencyMs` = `L(p)` in ms.
3. The processor applies a pitch message when `outFrames + (inFrames - outFrames) / 2 >= atFrame -
   firstFrame` (not at the input frame).
4. SoundTouch settings ids/values fixed (quick seek 0, AA filter 1 with 64 taps, overlap 8 ms);
   build flag `SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER` mandatory.
5. The Node WASM suite also checks "no drift over 100 jumps" and the mean content offset at 0.5/2.
