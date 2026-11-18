# Contract: Audio engine JS interop (v2.6.0)

The binding TypeScript definition is [`src/audio-engine/src/types.ts`](../../../src/audio-engine/src/types.ts).
This document explains the behaviour behind those types. Design rationale: ADR-005 (v1) and
ADR-010 (v2: stem groups), ADR-015 (v2.1: karaoke), ADR-021 (v2.3: practice tempo), ADR-023 (v2.4:
score follower, loop passes), ADR-024 (v2.5: play-along recording), ADR-031..033 (v2.6: instrument views).

**Base of 2.4.0**: 2.3.0 (`specs/012-practice-tempo/contracts/audio-engine.md`) plus the 014 delta
(`audio-engine.delta.md`, merged unchanged in 014 Phase 0 part 2, T005). **Base of 2.3.0**: 2.1.0
(`specs/006-synced-lyrics-karaoke/contracts/audio-engine.md`). Version 2.2.0 is
reserved for feature 011 ("if needed"; 011 Phase 0 part 1 left the engine unchanged). If 011 does
publish a 2.2.0, its additions are merged into this document unchanged before the 012 wave
(tasks.md T003); 2.3.0 always contains every 2.1.0/2.2.0 call with unchanged behaviour.

**Changelog**: 2.0.0 (feature 002) - channels are addressed by `ChannelId` (a stem name or
`<parent>.<detail>`, e.g. `drums.snare`); `loadTrack` accepts optional detail `groups`; new
`setExpanded(parent, expanded)`; the solo/mute gate is evaluated over the *audible level* only
(collapsed parents plus the detail channels of expanded parents). Calls valid in 1.0.0 behave
exactly as before when no groups are loaded, but the gate rule change is a semantic break, hence
the major version. 2.1.0 (additive, feature 006) - karaoke banner: `attachKaraoke`,
`setKaraokeOffset`, `detachKaraoke` and the `Karaoke*` types; a shared animation-frame loop for
meters and karaoke (meter behaviour unchanged). Every 2.0.0 call behaves exactly as before.
2.3.0 (additive, feature 012) - practice tempo: `setPractice(settings)`, `getPracticeStatus()`,
the callback `OnPracticeChanged(status)`, the types `PracticeSettings`, `SpeedTrainerSettings`,
`CountInSettings`, `PracticeStatus`, `StretchState`, and `EngineOptions.timeStretch` /
`EngineOptions.timeStretchFactory`; a time-stretch stage (SoundTouch 2.4.1, LGPL-2.1, WASM in an
AudioWorklet) on the summed mix; rate-aware transport anchor; count-in clicks; speed trainer on
explicit loop passes. **Without a `setPractice` call, or with neutral settings (100 %, 0
semitones, trainer off, no count-in), every 2.1.0 call behaves exactly as before and no worklet
or WASM file is loaded.**
2.4.0 (additive, feature 014) - score follower (`attachScoreFollower`, `setScoreFollowOptions`,
`detachScoreFollower`, `ScoreView*` types) drawn on the shared animation frame against the heard
position; `.NET` callback `OnLoopPass(LoopPassInfo)`. Every 2.3.0 call behaves exactly as before; without
an attached follower nothing is drawn, and `OnLoopPass` adds at most one call per completed loop pass.
2.5.0 (additive, feature 015) - play-along recording: input (`recordingSupport`, `listInputDevices`,
`openInput`, `closeInput`, `setMonitoring`), loopback latency calibration (`calibrateLatency`),
recording with loop passes (`startRecording`, `stopRecording`, `captureWav`, `releaseCapture`),
the `take` channel strip with compare and timing adjustment (`loadTake`, `unloadTake`,
`setTakeAdjustment`, `setTakeCompare`), `ChannelId` `take`, `MeterTarget` `input`, five
callbacks, `EngineOptions.testInput` / `mediaDevices` / `captureWorkletUrl`, and a third Vite
entry (the capture worklet). **Without `openInput` and `loadTake` the graph and the gate are
exactly the 2.4.0 ones (= the 2.3.0 ones).**
2.6.0 (additive, feature 016) - instrument views: chord lane (`attachChordLane`, `detachChordLane`), chord
symbols in the karaoke banner (`setKaraokeChords`), key-region-aware spelling and capo mode
(`setChordDisplay`, host `data-capo` / `data-current-key`), drum hit lanes (`attachHitLanes`,
`setHitLaneOptions`, `detachHitLanes`), singing feedback (`getRoundTripLatency`,
`startSingingFeedback`, `setSingingTolerance`, `stopSingingFeedback`, pitch-tracker AudioWorklet,
pitch view, unit scoring) and offline take scoring (`scoreTake`), callbacks `OnSingingUnitScored` and
`OnSingingStateChanged`, `EngineOptions.pitchWorkletUrl` and a fourth Vite entry
(`audio-engine-pitch-worklet.js`). **Without these calls nothing is drawn and the graph is exactly the
2.5.0 one.** Merged from `specs/016-instrument-views/contracts/audio-engine.delta.md` in 016 Phase 0
(T004) with the reconciliation of section "Instrument views" below.

**Merge order of this document** (re-merged at the 014+015 round start, 2026-11-17): it is 014's
2.4.0 (= 012's 2.3.0 + the 014 delta) plus the 015 delta, built in feature order as described in
[DELTA.md](DELTA.md) "Re-merge". Labels identify the merged deltas, not an order. The recorder does
not use `OnLoopPass`; it reads the engine's internal segment log.

## Packaging and loading

- Source: `src/audio-engine/` (TypeScript, Vite library mode). `npm run build` writes one ES
  module to `src/StemStudio.Web/wwwroot/js/audio-engine.js` (+ source map). The file is a build
  artefact and is not committed.
- Blazor loads it once per mixer page:

  ```csharp
  var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/audio-engine.js");
  var engine = await module.InvokeAsync<IJSObjectReference>("createEngine", DotNetObjectReference.Create(this));
  var duration = await engine.InvokeAsync<double>("loadTrack", new { stems, beatGrid });
  await engine.InvokeVoidAsync("play");
  ```

- Module exports: `createEngine(dotnetRef, options?) => AudioEngine` and
  `ENGINE_CONTRACT_VERSION` (`"2.3.0"`).
- 2.3.0 build adds two separately served files (section "Practice tempo"):
  `wwwroot/js/time-stretch-processor.js` (the AudioWorklet processor, MIT, a second Vite entry)
  and `wwwroot/js/soundtouch/soundtouch.wasm` (SoundTouch, LGPL-2.1, built by
  `scripts/build-soundtouch.*`). Both are build artefacts and are not committed. They are
  fetched only on the first non-neutral `setPractice`. Everything else is reached through the returned
  `AudioEngine` object (methods listed in `types.ts`).
- Argument objects are plain JSON (camelCase), so Blazor can pass the Contracts DTOs
  (`ChannelSettingsDto` minus `stem`, `MetronomeSettingsDto`, `LoopRegionDto`, `BeatGridDto`).

## Audio graph

```text
per stem:  AudioBufferSourceNode -> GainNode(fader x gate) -> StereoPannerNode -> AnalyserNode(meter) --+
                                                                                                        +-> master GainNode -> AnalyserNode(master meter) -> destination
metronome: AudioBufferSourceNode (accent | normal click) -> GainNode(click volume) ----------------------> destination
```

- One `AudioContext` per engine (`sampleRate` 44100 requested; stems are 44.1 kHz FLAC).
- Gate (v1, still valid without groups): `gate = mute ? 0 : (anySolo && !solo ? 0 : 1)`; channel
  gain = `volume x gate`. With groups the v2 rule below applies.
  Gain changes use `setTargetAtTime` / linear ramps of at most 10 ms (no zipper noise; SC-004).
- The meter tap is post-fader and post-pan (what the user hears from that channel).
- The metronome bypasses the master fader so it stays audible when the mix is turned down.

2.3.0 graph (practice tempo engaged; at neutral settings the `sum` node connects straight to the
master gain, which is the 2.1.0 graph):

```text
per channel: AudioBufferSourceNode(playbackRate = speed) -> Gain -> Panner -> Analyser(meter) --+
                                                                                               +-> sum GainNode(1)
sum -> AudioWorkletNode "stemstudio-time-stretch" (SoundTouch pitch = 2^(st/12) / speed, fixed delay D)
    -> master GainNode -> AnalyserNode(master meter) -> destination
metronome + count-in: AudioBufferSourceNode (click) -> GainNode(click volume) -> destination (not stretched, not delayed)
```

## Stem groups and expansion (v2.0.0, FR-012..FR-014)

Types (in `types.ts`):

```ts
type ParentStemName = 'vocals' | 'drums' | 'guitar' | 'piano' | 'other';
type DetailStemName = 'kick' | 'snare' | 'toms' | 'hihat' | 'cymbals' | 'lead' | 'backing'
  | 'strings' | 'brass' | 'woodwinds' | 'keys' | 'synth' | 'acoustic-guitar'
  | 'electric-guitar' | 'percussion' | 'rest';
type ChannelId = StemName | `${ParentStemName}.${DetailStemName}`;
interface DetailGroup { readonly parent: ParentStemName; readonly stems: readonly { name: DetailStemName; url: string }[] }
interface LoadTrackRequest { stems; beatGrid; groups?: readonly DetailGroup[] }   // groups default []
setExpanded(parent: ParentStemName, expanded: boolean): Promise<void>;
setChannel(id: ChannelId, settings: ChannelSettings): void;
attachMeter(target: ChannelId | 'master', elementId: string): void;
```

- **Graph**: every detail channel has the same chain as a standard stem (source -> fader x gate ->
  panner -> meter analyser -> master). Detail channels exist from `loadTrack`; their buffers are
  **loaded lazily** on the first `setExpanded(parent, true)` (fetch + decode of that group only,
  then kept until `dispose()`), so a track opened collapsed costs no extra memory or download.
- **Expansion state**: all parents start collapsed. `setExpanded(parent, true)` resolves once the
  group is decoded and the swap is scheduled; `setExpanded(parent, false)` resolves immediately.
- **Audible level**: the set of channels that may sound = every standard stem whose parent is
  collapsed (or that has no group) plus every detail channel of an expanded parent. A collapsed
  parent's detail channels and an expanded parent's own channel are *hidden* (gate 0).
- **Gate (replaces the v1 rule)**: `anySolo` = any *audible-level* channel with `solo`;
  `gate = hidden || mute ? 0 : (anySolo && !solo ? 0 : 1)`. Solo/mute flags of hidden channels
  are kept (restored on the next expand/collapse, US2 AS3) but ignored.
- **Swap**: expand and collapse change only gates, scheduled at one context time
  `t = ctx.currentTime + 0.01` with the usual <= 10 ms ramps on both the parent and its detail
  channels, so the sum stays the same at default settings (sub-stems + rest == parent, FR-005).
- **Sample lock**: while playing, a newly decoded group's sources are started with
  `start(t, offsetAt(t))`, where `offsetAt` uses the same transport anchor (`when`, `offset`) as
  every other source and wraps into the loop region; loop properties are copied. Every later
  play/seek/loop change treats detail sources exactly like standard sources (one shared `when`).
  Tolerance is the v1 one: <= 1 ms between any two audible channels after 50 loop repetitions
  (SC-004).
- **Meters**: `meter-<parent>-<detail>` elements are attached with id `<parent>.<detail>`; a
  hidden channel's meter decays to -60 dB.

## Transport (sample lock, SC-002)

- `play()`: `await ctx.resume()`, then create one new `AudioBufferSourceNode` per stem and call
  `start(when, offset)` with the **same** `when = ctx.currentTime + startDelay` (default 0.1 s) and
  the **same** `offset` for every stem. Sources are never re-used.
- Position = `offset + (ctx.currentTime - when)` while playing (wrapped into the loop region when
  looping); it is never accumulated from timers.
- `pause()` stores the position and stops all sources; `stop()` also resets it to 0 (also while
  looping; not to the loop start);
  `seek(t)` while playing = stop all + start all at the new offset with a fresh shared `when`.
- End of track (no loop): the engine stops, sets position 0 and reports state `stopped`.
- 2.3.0: the anchor carries a rate `r = speedPercent / 100` (1 at neutral) and maps **heard**
  (output) context time to track time: `position(t) = offset + (t - when) * r`, wrapped into the
  loop. While the stretch stage is engaged its heard latency `L(p) = (D + c(p)) / sampleRate` is
  compensated by starting every source `L(p)` earlier (`source.start(when - L(p), offset)`,
  `when >= currentTime + L(p) + startDelay`), so the metronome, the karaoke clock and
  `getPosition()` keep using the anchor unchanged. `D` is the fixed FIFO delay and `c(p)` the
  content offset of SoundTouch at pitch factor `p` (section "Stretch stage"; Phase 0 P0-1). At
  neutral settings `r = 1`, `L = 0`: the 2.1.0 formula.

## Loop (FR-013)

- `setLoop({start, end, enabled})` validates `0 <= start < end <= duration`, otherwise throws
  `Error("Invalid loop region: ...")` and leaves the previous loop unchanged.
- Enabled: every source gets `loop = true`, `loopStart = start`, `loopEnd = end` (seamless,
  sample-accurate looping by the browser). If the position is outside the region when looping is
  enabled, playback restarts at `start`. Disabled: `loop = false` on the running sources.
- 2.3.0: native looping also works with `playbackRate != 1` (the browser loops in buffer time).
  When the loop is enabled **and** (`practice.trainer.enabled` or `practice.countIn.everyPass` with
  `bars > 0`), the engine switches to **explicit passes** (section "Practice tempo"); otherwise
  the native loop above is used. Changing `start` or `end` restarts the trainer.

## Metronome (FR-014, FR-015, SC-003)

- Two click `AudioBuffer`s are rendered once at start-up (accent: higher pitch/louder; normal).
- Scheduler: a timer (default every 25 ms) only **wakes** the scheduler. Each run schedules every
  click whose time falls in `[now, now + scheduleAhead)` (default 100 ms) with
  `source.start(exactContextTime)`. Click times are computed from `ctx.currentTime`, the
  transport anchor (`when`, `offset`) and the beat grid, never from the timer.
- Grid:
  - `bpmOverride == null`: clicks at `beats[i] + offsetMs/1000`; accent when the beat is a
    downbeat (bar position derived from the nearest preceding downbeat and `beatsPerBar`).
  - `bpmOverride != null`: constant grid `t0 + k * 60/bpm + offsetMs/1000` with
    `t0 = downbeats[0] ?? beats[0] ?? 0`; accent every `beatsPerBar` beats from `t0`.
- Loop-aware: when the look-ahead window crosses `loopEnd`, the remainder is mapped to
  `loopStart`, so clicks continue in the repeated region. Seek/pause/loop changes cancel
  already-scheduled future clicks and reschedule from the new position.
- If the track has no reliable grid and no override, enabling the metronome is allowed but
  silent until a BPM override is set (the UI disables it by default).
- 2.3.0: clicks stay in **track time** (the grid, `bpmOverride` and `offsetMs` describe the
  original recording) and are mapped to context time through the rate-aware anchor:
  `contextTime = segment.contextStart + (click.time - segment.trackFrom) / r`. At 50 % the audible
  click rate halves with the music. Clicks are never stretched or delayed (they bypass the stretch
  stage; the anchor already includes the latency compensation). A rate change inside the look-ahead
  window cancels and reschedules the future clicks like a loop change.

## Meters (FR-011, ADR-001 #4)

- `attachMeter(target, elementId)` with `target` a `ChannelId` (stem or `<parent>.<detail>`) or `"master"`.
- On each `requestAnimationFrame` the engine reads `getFloatTimeDomainData`, computes the peak
  `p` and `dB = 20*log10(p)`, clamps to -60..0 and writes to the element:
  - `style.setProperty("--meter-level", (dB + 60) / 60)` (0..1, decay ~ 20 dB/s),
  - `dataset.peakDb` = rounded dB (string),
  - `dataset.clip` = `"true"` for 1.5 s after any sample with `|x| >= 1.0`, else `"false"`.
- No meter data is sent to .NET (no 60 fps interop). The frame loop runs only while meters are
  attached and stops on `dispose()`.
- 2.3.0: channel meters tap the signal before the stretch stage, so while it is engaged they
  run `L` (at most 230 ms, measured value in research.md) ahead of the sound; the master meter
  taps after it and matches the sound. Documented limitation (help topic `practice-sound-quality`).

## Karaoke (v2.1.0, feature 006, ADR-015 D5)

Types: `KaraokeWord`, `KaraokeLine`, `KaraokeLyrics`, `KaraokeOptions`, `KaraokeState` in
`types.ts`; methods `attachKaraoke(elementId, lyrics, options)`, `setKaraokeOffset(offsetMs)`,
`detachKaraoke()`. `KaraokeLyrics` is the render subset of the backend `Lyrics` DTO (only word-
and line-synced documents are attached; every word has `startMs`/`endMs` in integer ms from the
start of the track).

- **Host ownership**: Blazor renders the host (`data-testid="karaoke-banner"`) empty and never
  re-renders its content (`@key`, no child content). The engine owns all children. Text is set
  with `textContent` only (never `innerHTML`). `attachKaraoke` replaces an attached karaoke;
  `detachKaraoke` removes the engine's children and stops drawing (no-op when none is attached).
- **DOM produced**: three `.karaoke-line` elements with `data-slot` = `previous` | `current` |
  `next`, each holding `.karaoke-word` elements with `data-state` = `past` | `current` | `future`;
  one `.karaoke-ball`; one `.karaoke-countdown` (three dots). Host attributes written by the
  engine: `data-state` (`KaraokeState`), `data-line-index`, `data-word-index` (-1 when none),
  `data-position-ms` (rounded karaoke clock with the offset applied, for tests). Styling comes
  from the app's CSS (004 tokens); the engine sets no colours or fonts.
- **Slots**: `previous` = line L-1 (all words `past`), `current` = line max(L, 0), `next` = L+1
  (all `future`). Before the first line the `current` slot already shows line 0 (all `future`)
  and the host has `data-line-index="-1"`. The last started word of the current line stays
  `current` until the next word starts or the line's `endMs` passes (then all its words are
  `past` and `data-word-index="-1"`). `detachKaraoke` also removes the engine's host attributes
  (`data-state`, `data-line-index`, `data-word-index`, `data-position-ms`); attributes written
  by Blazor stay.
- **Clock**: `positionMs = round(1000 * transportPosition(audioNow - outputLatency)) - offsetMs`
  where `audioNow = ts.contextTime + (performance.now() - ts.performanceTime) / 1000` from
  `ctx.getOutputTimestamp()` (fallback `ctx.currentTime` when the method is missing or returns
  zeros), `outputLatency` = `ctx.outputLatency` when defined (else `ctx.baseLatency`, else 0), and
  `transportPosition(t)` is the ADR-005 formula `offset + (t - when)` wrapped into the loop. The
  latency is subtracted **before** the loop wrap, so the banner wraps when the wrap is heard
  (subtracting after the wrap would show `loopStart - latency` while the loop end is still
  audible). Paused or stopped: the stored transport position minus `offsetMs` (no latency).
  Never accumulated from frame deltas (no
  drift, SC-002). Positive `offsetMs` makes the lyrics come later.
  2.3.0: unchanged formula; `transportPosition` is the rate-aware anchor (section Transport), so
  the ball follows slowed or sped-up playback, and the lyrics offset stays in track milliseconds.
  During a count-in the position is the start position (the banner shows that point).
- **Frame loop**: one shared `requestAnimationFrame` loop per engine serves the meters and the
  karaoke (`frame-loop.ts`; the meters move onto it with unchanged behaviour). Karaoke is drawn
  while attached, playing and `document.visibilityState === 'visible'`, plus exactly one frame
  after every attach, seek, pause, stop, loop change, offset change and resize. No timers
  (`setTimeout`/`setInterval`) are used for drawing.
- **Cursor**: current line and word advance forward in O(1) per frame; binary search after a
  seek, a loop wrap or an offset change.
- **Layout**: word centres are measured once after the DOM is built
  (`getBoundingClientRect`, relative to the host's border box) and again on `ResizeObserver`
  callbacks (host and the words in the slots; after a line change the new words are observed and
  measured in the same rendering step, before paint) and on size changes (a `MutationObserver` on
  the host's `class` and `data-size`). Without `ResizeObserver` the slots are measured right
  after they are filled. Per frame the engine writes only `style.transform` of the ball and
  `data-state` of at most two words and two lines (no layout reads in the frame callback; SC-004).
- **Ball**: lands on word *i* exactly at `startMs(i)`. Between word *i* and *i+1*:
  `p = clamp((t - start_i) / (start_{i+1} - start_i), 0, 1)`, `x = lerp(x_i, x_{i+1}, p)`,
  `y = -h * 4p(1 - p)` with `h` = 0.6 x line height (scaled by `duration / 150 ms` for words
  shorter than 150 ms; the duration is the hop from the start of word *i* to the start of word
  *i+1*). Across a line change the ball arcs to the first word of the next line (shown in the
  `next` slot until that line becomes current). `x` is the word centre and `y` the word top
  (relative to the host's border box) minus the arc, written as
  `style.transform = translate3d(<x>px, <y>px, 0px)`. The engine owns `transform`: the app's CSS
  places the ball at `left: 0; top: 0` (absolute; host `position: relative`) and centres it with
  negative margins, never with `transform`.
- **States**: `hidden` when `lines` is empty; `countdown` for the whole time before the first
  line, and inside gaps (`next.startMs - line.endMs`) longer than `countdownGapMs` (default 5000)
  from 3 s before the next line; `singing` otherwise while inside the lyrics; `ended` after the
  last line's `endMs`.
- **Countdown**: `.karaoke-countdown` has `data-count` and three `.karaoke-dot` children; the app's
  CSS lights the first `data-count` dots. Before the first line `data-count` is `0` until 3 s
  before it, then `3`, `2`, `1` (one per second); in a long gap it counts `3`, `2`, `1` likewise.
- **Reduced motion**: `options.reducedMotion`, default
  `matchMedia('(prefers-reduced-motion: reduce)').matches` (a runtime change of the media query is
  followed): no ball; the current word gets `data-state="current"` (styled as a fill).
- **Validation and errors**: unknown element id -> `Error("Karaoke host not found: <id>")`;
  invalid lyrics (`lines` not an array, more than 400 lines, a line with no words or more than
  40 words, non-finite times, `endMs <= startMs`, line starts decreasing, word starts decreasing
  (also across lines), a word outside its line, a word without string text) ->
  `Error("Invalid karaoke lyrics: <reason>")`; a negative `countdownGapMs` ->
  `Error("Invalid karaoke options: <reason>")`; `offsetMs` outside -5000..5000 ->
  `Error("Karaoke offset out of range: <value>")`; calls after `dispose()` throw
  `Error("Audio engine has been disposed")` like every other method. Everything is validated
  before an attached banner is replaced (a failing `attachKaraoke` keeps the old banner).
  `setKaraokeOffset` without an attached banner validates and stores the offset (the next
  `attachKaraoke` sets its own). Loading a new track does not redraw the banner; call
  `attachKaraoke` after `loadTrack`.
- **Testing**: Vitest with the `AudioContext` mock extended by `getOutputTimestamp` and
  `outputLatency`, a fake `requestAnimationFrame`, a stubbed `getBoundingClientRect` and
  `ResizeObserver`; coverage >= 90 % (lines, branches, functions, statements).

## Practice tempo (v2.3.0, feature 012, ADR-021)

### Types (added to `types.ts`)

```ts
export type EngineContractVersion = '2.3.0';

/** Mirrors `PracticeSettingsDto` / backend `PracticeSettings` (integers). */
export interface PracticeSettings {
  readonly speedPercent: number;        // 50..120, default 100; pitch kept
  readonly transposeSemitones: number;  // -12..12, default 0; speed kept
  readonly trainer: SpeedTrainerSettings;
  readonly countIn: CountInSettings;
}
export interface SpeedTrainerSettings {
  readonly enabled: boolean;            // default false; effective only with an enabled loop
  readonly startPercent: number;        // 50..119, default 70
  readonly stepPercent: number;         // 1..20, default 5
  readonly targetPercent: number;       // 51..120, > startPercent, default 100
  readonly passesPerStep: number;       // 1..8, default 1
}
export interface CountInSettings {
  readonly bars: number;                // 0 (off), 1 or 2
  readonly everyPass: boolean;          // also before every loop pass; default false
}
/** Where the time-stretch stage is. */
export type StretchState = 'bypass' | 'loading' | 'active' | 'unavailable';
export type TrainerState = 'off' | 'waiting' | 'running' | 'target';

/** Snapshot sent with `OnPracticeChanged` and returned by `getPracticeStatus()`. */
export interface PracticeStatus {
  readonly speedPercent: number;        // speed heard now (trainer speed while it runs)
  readonly transposeSemitones: number;
  readonly stretch: StretchState;
  readonly stretchReason: string | null;  // set only when 'unavailable'
  readonly latencyMs: number;           // L in ms while engaged, else 0
  readonly underruns: number;           // stretch FIFO underruns since loadTrack (Expert diagnostics)
  readonly trainer: { readonly state: TrainerState; readonly pass: number; readonly speedPercent: number };
  readonly countIn: {
    readonly available: boolean;        // false without a usable tempo
    readonly active: boolean;
    readonly beatsRemaining: number;    // N..1 while active, 0 otherwise
    readonly beatsTotal: number;        // bars x beatsPerBar of the metronome settings
  };
}

/** Loading options of the stretch stage (defaults shown). */
export interface TimeStretchOptions {
  readonly processorUrl?: string;       // './js/time-stretch-processor.js'
  readonly wasmUrl?: string;            // './js/soundtouch/soundtouch.wasm'
}

// EngineOptions additions
readonly timeStretch?: TimeStretchOptions;
/** Test seam: replaces the AudioWorklet + WASM stage (Vitest fake). */
readonly timeStretchFactory?: (context: AudioContext) => Promise<TimeStretchStage>;

/** The stage as the engine sees it (implemented by the worklet wrapper and by test fakes). */
export interface TimeStretchStage {
  readonly input: AudioNode;
  readonly output: AudioNode;
  readonly latencyFrames: number;       // D, constant for the stage's lifetime
  /** Pitch factor applied from the input frame `atFrame` (context sample frames). */
  setPitch(pitchFactor: number, atFrame: number): void;
  /** Drops buffered audio and refills the fixed delay with silence (seek, restart). */
  reset(): void;
  /** Underrun counter (frames output as silence because the FIFO was empty). */
  underruns(): number;
  dispose(): void;
}

// AudioEngine additions
/** Validates and applies practice settings; resolves when audible (stretch loaded if needed). */
setPractice(settings: PracticeSettings): Promise<void>;
/** Current snapshot. */
getPracticeStatus(): PracticeStatus;

// DotNetCallbacks additions
type DotNetCallbacks = 'OnPositionChanged' | 'OnPlaybackStateChanged' | 'OnPracticeChanged';
```

### Validation and errors

- `setPractice` validates everything first (integers, ranges above, `startPercent <
  targetPercent`) and rejects with `Error("Invalid practice settings: <field> <reason>")`
  without changing anything.
- When the settings need the stretch stage (`speedPercent != 100` or `transposeSemitones != 0`,
  or the trainer is enabled) and the stage cannot be created (no `AudioWorklet`, the processor or
  the WASM file fails to load or to instantiate), `setPractice` rejects with `Error("Time stretch
  unavailable: <reason>")`; the engine then keeps speed 100 % and 0 semitones, disables the
  trainer, **applies the count-in settings**, sets `stretch = 'unavailable'` with the reason and
  calls `OnPracticeChanged`. Playback keeps working. A later `setPractice` retries the load once
  per `loadTrack`.
- Calls after `dispose()` throw `Error("Audio engine has been disposed")`.

### Loading and engagement

- Nothing is loaded at neutral settings. The first `setPractice` that needs the stage:
  `stretch = 'loading'`, `context.audioWorklet.addModule(processorUrl)` (once per engine),
  `fetch(wasmUrl)` (once per engine; the bytes are passed to the processor in
  `processorOptions.wasmBytes`; the processor instantiates them with `WebAssembly.instantiate`;
  no `SharedArrayBuffer`, no COOP/COEP needed), then `stretch = 'active'`.
- **Engage**: the stage is inserted between `sum` and the master gain. While playing, engaging
  restarts all sources at the current heard position with the new anchor (one gap of at most
  `startDelay + L`, faded over 10 ms). While paused/stopped it is silent.
- Once engaged the stage stays in the graph until the next transport restart (`play()` from
  paused/stopped, `seek`, `loadTrack`) at neutral settings, where it is bypassed (`L = 0`) again.
  At pitch factor 1 SoundTouch passes audio through practically unchanged (P0-1: max error
  3.2e-6, no time offset), so returning to 100 %/0 st while playing causes no gap.
- Every restart of the sources while engaged (`play()`, `seek`, engaging, explicit-pass start
  after a count-in gap) first calls `stage.reset()` so no audio of the previous position is heard;
  a pass that follows the previous one without a gap does **not** reset (seamless loop).

### Live changes (no gap)

- A speed or transpose change while engaged and playing enters the stage at context time
  `U = currentTime + 0.02` (s): every running source gets `playbackRate.setValueAtTime(r, U)` (the
  same value and time for every source: sample lock is kept) and the stage gets
  `setPitch(p, round(U * sampleRate))` with the new factor `p`. It is heard from
  `T = U + D / sampleRate`; the anchor is re-anchored to
  `{when: T + c(p) / sampleRate, offset: positionOld(T + c(pOld) / sampleRate), rate: r}` (the
  position may jump by the difference of the two content offsets, at most ~50 ms; every later
  start uses `L(p)`). Changes arriving before `U` replace the pending one (the UI may send at most
  one change per 50 ms while dragging). Heard change latency: `0.02 + D / sampleRate + c(p) /
  sampleRate` <= 0.17 s plus the device output latency (SC-001 250 ms; P0-2 measured 87-151 ms at
  the stage output in Chromium).
- Pitch factor `p = 2^(transposeSemitones / 12) / (speedPercent / 100)`; range 0.42..4.0.
- `OnPositionChanged` keeps reporting track seconds; `OnPracticeChanged` fires after every applied
  change, trainer step, count-in beat, stretch state change and underrun burst (at most 10 Hz).

### Stretch stage (AudioWorklet processor `stemstudio-time-stretch`)

- One stereo SoundTouch instance on the summed mix (never per stem: per-stem WSOLA would pick
  different splice points per stem and break the <= 1 ms sample lock, and costs up to 20x CPU).
- SoundTouch settings, sent once after `st_create` with `st_set_setting`: `SETTING_USE_QUICKSEEK`
  (2) = 0, `SETTING_USE_AA_FILTER` (0) = 1, `SETTING_AA_FILTER_LENGTH` (1) = 64, `SETTING_OVERLAP_MS`
  (5) = 8; sequence and seek window stay automatic; then `st_set_pitch(p)` (tempo and rate stay 1).
  Build flag `SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER` is mandatory (`soundtouch.lock`).
- **Fixed delay D**: the processor keeps a FIFO prefilled with `D` frames of silence and appends
  everything SoundTouch outputs, so SoundTouch output frame `m` is played at stage output frame
  `m + D` regardless of SoundTouch's momentary output count; `D = STRETCH_DELAY_FRAMES = 5120`
  frames (116.1 ms at 44.1 kHz; Phase 0 P0-1: largest initial latency 4,852 frames and largest
  measured output lag 4,736 frames over factors 0.42..4.0 incl. 120 jumps 0.42 <-> 4, + 2 render
  quanta, rounded up to 128). FIFO empty -> silence and `underruns++`; FIFO above `D + 1024` -> the
  oldest surplus frames are dropped (counted with the underruns). `reset()` clears SoundTouch and the
  FIFO and refills `D` frames of silence. The engine requests `sampleRate` 44100; `D` is defined in
  frames at that rate.
- **Content offset c(p)**: WSOLA places the audio of input frame `n` near SoundTouch output frame
  `n + c(p)` (measured mean: +32 ms at p = 0.42, 0 at p = 1, -28 ms at p = 2, -40 ms at p = 4;
  per-onset spread up to +-23 ms, inherent to WSOLA). The engine compensates the mean with the
  constant model `c(p) = round(STRETCH_CONTENT_FRAMES_PER_OCTAVE * log2(p))`,
  `STRETCH_CONTENT_FRAMES_PER_OCTAVE = -1100` (`stretch/constants.ts`); heard latency
  `L(p) = (D + c(p)) / sampleRate` (66-147 ms over the product range). `PracticeStatus.latencyMs`
  reports `L(p)` in ms.
- **When a pitch change applies**: SoundTouch applies `setPitch` to every sample it has not processed
  yet, including buffered input. The processor therefore keeps the counters `inFrames` (frames put
  since the last reset) and `outFrames` (frames received) and calls `st_set_pitch` in the first
  quantum where `outFrames + (inFrames - outFrames) / 2 >= atFrame - firstFrame` (`firstFrame` =
  context frame of the first input after the last reset). P0-1: wrong-pitch time around a combined
  speed change 12-23 ms with this rule versus 70-81 ms when applying at `atFrame` on input.
- Message protocol on `port` (JSON): main -> processor `{type: 'pitch', value, atFrame}`,
  `{type: 'reset'}`, `{type: 'dispose'}`; processor -> main `{type: 'ready', latencyFrames}`,
  `{type: 'error', message}`, `{type: 'stats', underruns}` (at most 4 Hz). Until `ready` the
  processor outputs silence.
- The processor allocates no memory in `process()` after `ready` (WASM heap buffers allocated
  once; 128-frame blocks).

### WASM ABI (binding for `third_party/soundtouch/shim/` and `src/audio-engine/src/stretch/abi.ts`)

`soundtouch.wasm` is a standalone reactor module (`em++ -O3 -sSTANDALONE_WASM --no-entry`, flags
in `third_party/soundtouch/soundtouch.lock`, no JS glue, 30 KB). Phase 0: the module has **no
imports**; the processor instantiates it with `WebAssembly.instantiate(bytes, {})` and must call the
export `_initialize()` once before any other export. C header:
`third_party/soundtouch/include/stemstudio_soundtouch.h`. Linear memory is fixed at 4 MiB (one
stereo instance uses about 0.3 MiB; 1.8 MiB stay free). Exported C functions:

| Export | Signature | Meaning |
|---|---|---|
| `st_abi_version` | `() -> i32` | `1` |
| `st_create` | `(sampleRate: i32, channels: i32) -> i32` | handle (pointer), 0 on failure |
| `st_destroy` | `(h: i32) -> void` | |
| `st_set_pitch` | `(h: i32, pitch: f64) -> void` | `setPitch` |
| `st_set_setting` | `(h: i32, id: i32, value: i32) -> i32` | `setSetting` |
| `st_initial_latency` | `(h: i32) -> i32` | `getSetting(SETTING_INITIAL_LATENCY)` in frames |
| `st_put` | `(h: i32, samples: i32, frames: i32) -> void` | interleaved float32 at `samples` |
| `st_receive` | `(h: i32, samples: i32, maxFrames: i32) -> i32` | frames written |
| `st_clear` | `(h: i32) -> void` | `clear()` |
| `st_malloc` / `st_free` | `(bytes: i32) -> i32` / `(ptr: i32) -> void` | heap buffers |
| `memory` | exported memory | |

Any build of SoundTouch that exports this ABI can replace the shipped file (LGPL-2.1 section 6
relinking; `third_party/soundtouch/README.md` explains how).

### Count-in

- Tempo source = the metronome click grid (`bpmOverride`, beat grid, `offsetMs`): beat interval
  `d` (track seconds) = `60 / bpmOverride`, else the grid interval at the first click `b >= start`.
  No usable tempo (no reliable grid and no override) -> `countIn.available = false`, play starts
  without a count-in.
- `N = bars x beatsPerBar` clicks. With the start position `A` and the music's heard start `T0`,
  clicks are at `T0 + (b - A) / r - k * d / r` for `k = N..1`, so the count continues the
  song's grid into the music; accent on `k = N, N - beatsPerBar, ...` (beat 1 of each counted
  bar). Clicks use the metronome output and volume (also when the metronome is off).
- Before play: `play()` resolves at once; state is `playing`; sources start at `T0`;
  `getPosition()` = `A` until `T0`. `countIn.active` with `beatsRemaining` counting down (one
  `OnPracticeChanged` per beat, scheduled by the look-ahead timer like clicks, so the indicator is
  visual only, not audible timing). Each number is shown for its beat interval `d / r` from its click,
  so `active` ends at `max(T0, last click + d / r)`: "1" stays until the song's beat `b` is heard,
  also when its click coincides with `T0` (`b - A = d`, a grid without the song's first beat). Pause, stop or seek during a count-in cancels it (future clicks
  stopped); the next `play()` counts in again. Seeks while playing never count in.
- `everyPass` (with an enabled loop): explicit passes with a count-in gap of `N` beats (at the
  next pass's rate) between passes; during the gap the position is `A`.

### Explicit passes (speed trainer and count-in every pass)

- Used while the loop is enabled, at least `MIN_PASS_SECONDS = 0.5` long (track time), and
  (`trainer.enabled` or (`countIn.everyPass` and `bars > 0`)). A shorter loop keeps the native
  loop; the trainer state is then `off` (the UI shows `trainer-needs-loop` with "The loop must
  be at least 0.5 s long."). Pass *k* is one set of sources started together with
  `start(T_k - L, A, (B - A))` (all sources the same `T_k`, `A`, duration and rate `r_k`); its
  anchor is `{when: T_k, offset: A, rate: r_k}`. The next pass is created by the look-ahead timer
  at least 0.3 s before `T_{k+1} = T_k + (B - A) / r_k + gap_k` (gap = count-in or 0), so a pass
  without a gap starts sample-exactly where the previous one stops. The first pass starts at the
  current position when it is inside `[A, B)`, else at `A`.
  *Implemented (ADR-021 E4, clarified in the 012 integration):* the sources of pass k+1 start
  where those of pass k stop (source time `S_{k+1} = S_k + (B - A) / r_k + gap_k`, no gap or
  overlap); its heard anchor is `S_{k+1} + L(p_{k+1})`, so a trainer step moves the heard pass
  start by `L(p_{k+1}) - L(p_k)` (a few ms). A count-in gap is `(N d - (b - A)) / r`: the first
  click of the next count-in sounds when the previous pass ends.
- Detail groups decoded during a pass are started on the current pass anchor (2.0.0 rule).
- Leaving the conditions (loop off, trainer and everyPass off) switches back to the native loop
  at the next pass boundary.

### Speed trainer

- States: `off` (disabled or no enabled loop), `waiting` (enabled, not playing), `running`,
  `target` (speed reached `targetPercent`).
- Pass 1 plays at `startPercent`. After every `passesPerStep` **completed** passes the speed of the
  next pass is `min(current + stepPercent, targetPercent)`; at the target the trainer stays there.
  A completed pass is one that reached `B`.
- While the trainer runs, `speedPercent` of the settings is ignored (the trainer speed is heard
  and reported); `transposeSemitones` applies live. Disabling the trainer returns to
  `speedPercent` live.
- `stop()`, `loadTrack`, a change of `start`/`end` or of any trainer field resets the trainer to
  pass 1 at `startPercent`; `pause()` and seeks inside the loop keep pass and speed.

## Score follower (v2.4.0, feature 014, ADR-023 D3)

Reconciled with 2.3.0 (014 Phase 0 part 2, DELTA.md R-1/R-3): the transport's heard anchor maps heard
context time to track seconds at every speed (`position(t) = offset + (t - when) * rate`, the stretch
latency subtracted), so the follower reads exactly what the karaoke clock reads (`KaraokeClock` over
`transport.positionAt(audioNow())`, without the karaoke offset). 2.3.0 has no loop-pass callback with
region and speed (`OnPracticeChanged` reports only the trainer's current pass), so `OnLoopPass` stays;
`speedPercent` is the speed heard in that pass (`PracticeStatus.speedPercent`, the trainer's speed while it
runs).

### Types (added to `types.ts`)

```ts
/** One page of a score view; mirrors `ScoreViewPage` of the backend (1.13.0). */
export interface ScoreViewPage {
  /** 1-based page number. */
  readonly page: number;
  /** API-relative URL of the SVG page (used as `<img src>` by Blazor, not fetched by the engine). */
  readonly url: string;
  readonly widthPx: number;
  readonly heightPx: number;
}

/** One bar; box in page fractions, times in track seconds; mirrors `ScoreBar`. */
export interface ScoreViewBar {
  readonly index: number;
  readonly number: number;
  readonly page: number;
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  readonly startSeconds: number;
  readonly endSeconds: number;
}

/** Argument of `attachScoreFollower`: the render subset of the backend `ScoreView`. */
export interface ScoreViewMap {
  readonly pages: readonly ScoreViewPage[];
  /** 1..2000 bars, ascending and contiguous (`bars[i].endSeconds === bars[i+1].startSeconds`). */
  readonly bars: readonly ScoreViewBar[];
}

/** Options of the follower. */
export interface ScoreFollowOptions {
  /** Auto-scroll on/off (the highlight is always drawn). */
  readonly follow: boolean;
  /** Jump instead of smooth scrolling; default `matchMedia('(prefers-reduced-motion: reduce)')`. */
  readonly reducedMotion?: boolean;
  /** Pause of auto-scroll after a manual scroll (wheel, touch, scroll keys) in ms; default 4000, 0..60000. */
  readonly scrollPauseMs?: number;
}

/** Argument of the `.NET` callback `OnLoopPass`. */
export interface LoopPassInfo {
  /** Loop start and end (seconds) of the pass that just completed. */
  readonly start: number;
  readonly end: number;
  /** 1-based number of completed passes since looping was (re-)enabled or the region changed. */
  readonly pass: number;
  /** Playback speed of that pass in percent (100 without 012 speed). */
  readonly speedPercent: number;
}
```

`DotNetCallbacks` gains `'OnLoopPass'`.

### Methods

```ts
/**
 * Binds the score follower to the host element `elementId` (contract 2.4.0). The host contains one
 * child per page with `data-score-page="<n>"` (Blazor renders `<img>` inside, position relative);
 * the engine adds one `.score-highlight` element per page container and owns it.
 * Throws `Error("Score host not found: <id>")`, `Error("Invalid score view: <reason>")`.
 */
attachScoreFollower(elementId: string, view: ScoreViewMap, options: ScoreFollowOptions): void;
/** Changes `follow`, `reducedMotion`, `scrollPauseMs`; takes effect on the next frame. */
setScoreFollowOptions(options: ScoreFollowOptions): void;
/** Removes the highlight elements and host attributes; no-op when none is attached. */
detachScoreFollower(): void;
```

### Behaviour

- **Clock**: the karaoke clock of 2.1.0 (output timestamp, output latency subtracted before the loop
  wrap) without an offset: `t = heardPosition()` in track seconds (A-012-1). Paused/stopped: the
  stored position.
- **Current bar**: the bar with `startSeconds <= t < endSeconds`; before the first bar: none
  (`data-bar-index="-1"`, highlight hidden); after the last bar: none. Forward cursor per frame,
  binary search after a seek, loop wrap or attach.
- **DOM**: per page container one absolutely positioned `.score-highlight` (`aria-hidden="true"`);
  on a bar change the engine sets on the highlight of the bar's page `style.left/top/width/height`
  in percent (`x*100 %` ...), `data-state="current"`, and `data-state="idle"` on the previous
  page's highlight. Host attributes: `data-bar-index`, `data-bar-number`, `data-page`,
  `data-position-ms` (for tests). No layout reads in the frame callback; colours/outline come from
  the app's CSS (004 tokens: outline + translucent fill, not colour alone).
- **Scrolling**: when `follow` is true and the current bar's page or vertical position (`y` change
  > 0.02, i.e. a new system) changes, the engine calls `highlight.scrollIntoView({block: 'center',
  inline: 'nearest', behavior: reducedMotion ? 'auto' : 'smooth'})` once (not per frame). A
  `wheel`, `touchmove` or `keydown` of a scroll key (PageUp/PageDown/Home/End/arrows) inside the
  host pauses auto-scroll for `scrollPauseMs`; the pause timer only gates scrolling, never timing.
  With reduced motion the engine scrolls only when the bar leaves the visible part of the host.
- **Frames**: drawn on the shared frame loop (`frame-loop.ts`) while attached, playing and visible,
  plus one frame after attach, seek, pause, stop, loop change and option change. Cost budget
  <= 0.3 ms per frame (SC-004 measures the total with the karaoke banner).
- **Validation**: `pages` 1..200 with unique `page` numbers; `bars` 1..2000, every `page` present
  in `pages`, box values finite in 0..1 with `width, height > 0`, `startSeconds < endSeconds`,
  ascending and contiguous within 1 ms; otherwise `Error("Invalid score view: <reason>")`, and an
  attached follower stays unchanged. `scrollPauseMs` outside 0..60000 ->
  `Error("Invalid score follow options: <reason>")`. After `dispose()` every call throws as before.
- **Loading a new track** does not detach the follower; Blazor detaches it on track change.

## Loop passes (v2.4.0, feature 014, ADR-023 D7)

- Fired once per completed pass of an **enabled** loop at the moment the wrap is heard (computed on
  the frame loop and on the scheduler tick, whichever sees the wrap first; never twice for one
  pass), with the region and the speed of the pass. A seek or a change of the loop region (start,
  end or enabled) resets `pass` to 0 and the partial pass is not reported. Re-sending an identical
  region with `setLoop` is not a change and does not reset the count (CTO decision 2026-11-17).
- Frequency is at most one call per pass (a 1 s loop: 1 Hz), so no high-frequency interop.

## Play-along recording (v2.5.0, feature 015, ADR-024)

Merged from [audio-engine.delta.md](audio-engine.delta.md) (015 Phase 0 T002/T004). Binding names and shapes are in `types.ts`; on any difference `types.ts` wins. Algorithms: research.md R-03, R-04, R-09, R-10; state and mapping: data-model.md 3.

### 1. Types (binding names and shapes)

```ts
/** Channel ids gain the literal 'take' (the one take channel strip). */
type ChannelId = StemName | `${ParentStemName}.${DetailStemName}` | 'take';
/** Meters may also be attached to the open input. */
type MeterTarget = ChannelId | 'master' | 'input';

type RecordingPart = 'none' | 'vocals' | 'guitar' | 'bass' | 'piano' | 'drums' | 'other';
type InputChannelMode = 'mono-1' | 'mono-2' | 'stereo';
type LatencyMethod = 'loopback' | 'manual' | 'estimated';
type RecordingState = 'closed' | 'open' | 'countIn' | 'recording' | 'finishing';
type TakeCompareMode = 'both' | 'take' | 'original';
type PermissionStateValue = 'granted' | 'prompt' | 'denied' | 'unknown';

interface RecordingSupport {
  readonly supported: boolean;
  /** Machine-readable reason (Phase 0): 'insecureContext' | 'noMediaDevices' | 'noAudioWorklet' | 'rateMismatch'; null when supported. */
  readonly reasonCode: RecordingUnsupportedReason | null;
  /** Readable reason when not supported (insecure context, no AudioWorklet, no getUserMedia). */
  readonly reason: string | null;
  readonly permission: PermissionStateValue;   // navigator.permissions 'microphone', 'unknown' when not queryable
}

interface InputDevice {
  readonly deviceId: string;
  /** Empty until permission is granted (browser rule). */
  readonly label: string;
  readonly groupId: string;
}

interface OpenInputRequest {
  /** null = the browser default input. */
  readonly deviceId: string | null;
  readonly mode: InputChannelMode;
}

interface InputInfo {
  readonly deviceId: string;
  readonly label: string;
  /** Channels the device delivers (1 or 2). 'mono-2' and 'stereo' need 2. */
  readonly channelCount: 1 | 2;
  readonly mode: InputChannelMode;
  /** Device rate reported by the track (the context stays at 44100). */
  readonly deviceSampleRate: number | null;
  /** round((baseLatency + outputLatency + track latency) * 44100). */
  readonly estimatedLatencySamples: number;
  /** round((baseLatency + outputLatency) * 44100), compared with a stored calibration. */
  readonly outputLatencySamples: number;
}

interface MonitoringSettings { readonly enabled: boolean; readonly volume: number }   // volume 0..1

interface CalibrationOptions {
  /** Number of clicks, 6..12, default 8. */
  readonly clicks?: number;
  /** PRNG seed for the click gaps (tests); default random. */
  readonly seed?: number;
}

interface CalibrationResult {
  readonly ok: boolean;
  readonly latencySamples: number | null;
  readonly latencyMilliseconds: number | null;
  readonly detectedClicks: number;
  readonly spreadSamples: number | null;
  readonly outputLatencySamples: number;
  readonly inputPeakDbfs: number;
  readonly reason: 'noSignal' | 'tooFewClicks' | 'inconsistent' | 'tooLong' | null;
  readonly warnings: readonly 'clipped'[];
}

interface RecordOptions {
  /** L + P is computed by the engine: this is L (measured, manual or estimated), 0..44100. */
  readonly latencySamples: number;
  readonly latencyMethod: LatencyMethod;
  readonly part: RecordingPart;
  /** Gate the part's stem (and its detail channels) to 0 while recording. */
  readonly muteOriginal: boolean;
  /** 0..2 bars; uses 012's count-in (and its "before every loop pass" setting). */
  readonly countInBars: number;
  /** 1..50, default 50. */
  readonly maxPasses?: number;
  /** Seconds of capture per session, default 1800. */
  readonly maxSeconds?: number;
}

interface CapturedTake {
  readonly captureKey: string;            // engine-local id
  readonly sessionId: string;             // UUID, shared by the passes of one Record press
  readonly passIndex: number | null;      // 1-based; null without loop
  readonly partial: boolean;
  readonly startSample: number;
  readonly lengthSamples: number;
  readonly channels: 1 | 2;
  readonly inputMode: InputChannelMode;
  readonly latencySamples: number;        // L + P as applied
  readonly latencyMethod: LatencyMethod;
  readonly peakDbfs: number;
  readonly clippedSamples: number;
  readonly dropouts: number;
  readonly recordedAt: string;            // ISO 8601 UTC
}

type TakeSource =
  | { readonly captureKey: string }                       // local capture, right after recording
  | { readonly url: string; readonly startSample: number; readonly lengthSamples: number; readonly channels: 1 | 2 };

interface LoadTakeRequest {
  readonly takeId: string;                // backend id (or the captureKey before it is saved)
  readonly source: TakeSource;
  readonly part: RecordingPart;
  /** Playback-only timing adjustment in samples (US7), -8820..8820, default 0. */
  readonly adjustmentSamples?: number;
}

interface EngineOptions {
  // ...2.4.0 options...
  /** E2E only: 'loopback' feeds the engine's output into its input (research R-13). */
  readonly testInput?: 'loopback';
  /** Test override of navigator.mediaDevices. */
  readonly mediaDevices?: Pick<MediaDevices, 'getUserMedia' | 'enumerateDevices' | 'addEventListener' | 'removeEventListener'>;
  /** Worklet module URL; default './js/audio-engine-capture-worklet.js'. */
  readonly captureWorkletUrl?: string;
}
```

### 2. AudioEngine methods (added)

| Method | Behaviour |
|---|---|
| `recordingSupport(): Promise<RecordingSupport>` | No side effects; never opens the microphone |
| `listInputDevices(): Promise<InputDevice[]>` | `enumerateDevices()` filtered to `audioinput`; labels empty before permission |
| `openInput(request): Promise<InputInfo>` | `getUserMedia` with the constraints of research R-02; replaces an open input; adds the capture worklet (once per context); attaches `'input'` meters. Rejects with `Error` whose message starts with a code: `permissionDenied:`, `notFound:`, `busy:` (NotReadableError), `modeUnavailable:` ('mono-2'/'stereo' on a 1-channel device), `unsupported:` (incl. Firefox rate mismatch), followed by a readable sentence |
| `closeInput(): void` | Stops every track of the stream, disconnects the nodes; allowed in `open` only (throws `Error("Stop the recording first")` otherwise) |
| `setMonitoring(settings): void` | Input -> GainNode(volume) -> destination, bypassing master and the take channel; volume ramps <= 10 ms; forced off during calibration |
| `calibrateLatency(options?): Promise<CalibrationResult>` | Needs `open`, transport not playing (else rejects `Error("Pause playback to calibrate")`); research R-04; never throws for a failed measurement (`ok: false` + reason) |
| `startRecording(options): Promise<void>` | Needs `open`, a loaded track, speed 1 and transpose 0 (else rejects `recordingSpeed:`), no speed trainer (`speedTrainer:`); starts capture, then the transport from the current position with 012's count-in; disables seek, loop, speed, transpose and track changes until `stopRecording` (they throw `Error("Stop the recording first")`); the take channel is gated to 0 while recording |
| `stopRecording(): Promise<CapturedTake[]>` | Stops the transport, waits `L + P` + one batch (state `finishing`), returns every take of the session (also reported by `OnTakesCaptured`) |
| `captureWav(captureKey): Blob` | 24-bit PCM WAV of the take (research R-07); Blazor reads it as `IJSStreamReference`. With the take id of the **loaded** take (instead of a capture key) it returns that take re-encoded as a 24-bit WAV starting at song sample `max(0, startSample + adjustmentSamples)`: a shift before the song start drops the leading samples (the re-encoded span for `PUT /api/takes/{id}/audio`, data-model 2.4). Unknown key: throws `Unknown capture "<key>"` |
| `releaseCapture(captureKey): void` | Frees the local audio (unknown key: no-op) |
| `loadTake(request): Promise<void>` | Decodes/slices (R-09), creates the `take` channel (fader x gate -> panner -> meter -> master) if needed, schedules it sample-locked with the shared transport anchor; replaces a loaded take; rejects with the stem-loader messages on fetch/decode errors |
| `unloadTake(): void` | Removes the take buffer; the `take` channel stays silent; compare resets to `both` |
| `setTakeAdjustment(samples): void` | Playback-only shift of the loaded take, re-scheduled at `ctx.currentTime + 0.01`. The value is **absolute**, relative to the loaded `startSample` (not a delta); integer `-8820..8820` samples (else throws `Invalid take adjustment`); no-op without a loaded take |
| `setTakeCompare(mode): void` | R-10 gate factor, swapped at one context time with <= 10 ms ramps; with part `none`, `original` = take gated |

`setChannel('take', settings)` and `attachMeter('take' | 'input', elementId)` work like for stems;
before `loadTake` they store the settings (meter decays at -60 dB).

### 3. Callbacks to .NET (added)

| Method (JSInvokable) | Arguments | When |
|---|---|---|
| `OnRecordingStateChanged` | `string state` | every `RecordingState` change |
| `OnRecordingProgress` | `{ elapsedSeconds, passIndex | null, countInBeatsLeft | null }` | at most 4 Hz while counting in or recording |
| `OnTakesCaptured` | `CapturedTake[]` | when a session ends (also returned by `stopRecording`) |
| `OnInputLost` | `string reason` | the device was unplugged or the track ended; a running recording stops and keeps a partial take |
| `OnInputDevicesChanged` | - | `devicechange` event (UI re-lists) |

### 4. Graph additions

```text
input:  MediaStreamSource -> [ChannelSplitter/Merger per mode] -> AudioWorkletNode(capture) -> Gain(0) -> destination
                                                                 -> Analyser(input meter)
                                                                 -> Gain(monitor volume) -> destination   (monitoring)
take:   AudioBufferSource -> Gain(fader x gate x compareGate) -> StereoPanner -> Analyser(meter) -> [012 tempo stage, as stems] -> master
clicks: calibration clicks -> destination (directly, like the metronome)
```

### 5. Requirements on 012 / 014 (internal, not public API; see "Phase 0 outcome")

1. The transport exposes `segments(): readonly TransportSegment[]` (or an equivalent listener)
   with the context frame and song sample of every play start, loop pass start and the end of
   each count-in gap (data-model 3.2).
2. The tempo stage exposes `processingLatencySamples(): number` (0 when bypassed at 1.0 / 0 st).
3. The tempo stage accepts additional channels (the `take`) and processes them identically to
   the stems.
4. Count-in is startable with an explicit bar count (`countInBars`), and its "before every loop
   pass" option is honoured during recording.

If the merged 012/014 engine lacks one of these, Phase 0 (T004) adds the missing internal hook
in the 2.5.0 work package (audio-engine lane task T014) without changing the 2.3.0/2.4.0 public
API.

### 6. Packaging

Vite builds a second entry `src/recording/capture-worklet.ts` to
`wwwroot/js/audio-engine-capture-worklet.js` (+ map; build artefact, not committed). The
worklet processor name is `stemstudio-capture`. No new npm dependency.

### 7. Testing

Vitest with the existing `AudioContext` mock extended by `audioWorklet.addModule`,
`AudioWorkletNode` (a fake port the test drives with stamped blocks), `createMediaStreamSource`,
`createMediaStreamDestination`, `getUserMedia`/`enumerateDevices` fakes and
`permissions.query`. Pure units (`estimateLatency`, `mapCaptureToTakes`, `encodeWav24`,
`sliceTake`, `compareGate`) are tested without the mock. Coverage >= 90 % lines, branches,
functions and statements.

### Phase 0 outcome (architect, 2026-11-17; binding for the audio-engine lane)

Checked against the merged 012 engine (2.3.0 on `main`, e8d6a5d8). 014's 2.4.0 is designed in
parallel and not merged; its delta (`specs/014-practice-flow/contracts/audio-engine.delta.md`)
touches none of the four hooks below.

| Request (section 5) | Exists in 2.3.0? | What the 2.5.0 work uses / adds (internal, no public API change) |
|---|---|---|
| 1. Transport segments | **Partly.** `Transport` keeps two `AnchorQueue`s (`heard`, `played`; `anchors.ts`) with `when`, `offset`, `rate` and `until` (explicit loop passes and count-in gaps); `metronome/timeline.ts` `segmentsFor(clock, window)` maps a context window to track segments. The queues are **pruned** as time passes. | audio-engine T015 adds a segment **log** (listener on anchor push/reset, kept for the whole session, at most 50 passes + 1) that `mapping.ts` reads after Stop. Mapping uses the **heard** anchors. |
| 2. Processing latency of the tempo stage | **Yes**, as `TransportTiming.latency` (seconds, 0 when bypassed); `TimeStretchStage.latencyFrames`. | No hook needed. Because the heard anchors already contain the stage latency (`when` = heard time, sources start `latency` earlier), the mapping is `songSample(f) = heardSampleAt(f - L)`: **`P` is applied implicitly** and must not be added a second time. `CapturedTake.latencySamples` reports `L + P` (P = `round(latency * 44100)` at record start; 0 at 100 %/0 st, which D3 requires anyway). |
| 3. Extra channels through the tempo stage | **Yes.** The stage sits between the master gain and the master meter (`Mixer.setStage`), so every channel into the master bus, including a `take` channel, is processed identically. | Nothing to add; the take strip connects to the master like a stem. |
| 4. Count-in with an explicit bar count, every pass | **Yes**, through `PracticeSettings.countIn { bars, everyPass }` and `practice/count-ins.ts` (`CountIns.gap(bars, rate)`, count-in before `play()` and before explicit passes). | `startRecording` applies `countInBars` through the practice controller for the session (restoring the stored setting afterwards); `everyPass` is the stored 012 setting. |

Further facts for the lane:

- The engine already has an AudioWorklet entry (012 `time-stretch-processor.js`); the capture
  worklet is a **third** Vite entry (`vite.capture.config.ts` -> `audio-engine-capture-worklet.js`),
  built by `npm run build`, ignored by git like the other build outputs.
- Phase 0 skeleton: `src/recording/pending.ts` (every recording method fails with
  `Error("not implemented: <method>")`) and `src/recording/capture-worklet.ts` (registers
  `stemstudio-capture`, posts nothing). The lane replaces `PendingRecording` with the real modules;
  `Mixer.analyser('input')` returns `undefined` until the input owns its analyser.
- `RecordingSupport.reasonCode` (added in Phase 0) is `insecureContext`, `noMediaDevices`,
  `noAudioWorklet` or `rateMismatch`; check order: `isSecureContext`, then
  `navigator.mediaDevices?.getUserMedia`, then `AudioContext.audioWorklet`. The UI maps it to
  `rec-unavailable` `data-reason` (ui-test-ids 1.14.0).

### Browser constraints (Phase 0 spike, 2026-11-17)

Measured with Playwright 1.65.1 (Chromium 159 full build with the fake capture device, Firefox
159 with `media.navigator.streams.fake`), each origin served by request interception:

| Origin | `isSecureContext` | `navigator.mediaDevices` | `AudioContext.audioWorklet` | Capture |
|---|---|---|---|---|
| `http://localhost:<port>` | true | present | present | Chromium: 60 s, 1,292 batches of 2,048 frames, **0 gaps**; Firefox: 60 s, 0 gaps |
| `http://127.0.0.1:<port>` | true | present | present | both: 3 s, 0 gaps |
| `http://<LAN IPv4>:<port>` | **false** | **undefined** | **undefined** (Chromium still defines the `AudioWorkletNode` constructor) | impossible |

Consequences (decision, recorded in ADR-024 "Phase 0 outcome"): recording is offered only in a
secure context, i.e. on the StemStudio computer through `http://localhost` / `http://127.0.0.1`
(or https, which StemStudio does not configure). Opened from another device over the LAN
(residual risk T7 until 018), `recordingSupport()` answers `reasonCode: 'insecureContext'` and
the panel explains "Open StemStudio on the computer it runs on to record". This is also the
privacy rule of FR-017 (audio would otherwise cross the network). The same applies to the 012
time-stretch stage (it needs `audioWorklet`), which already reports `unavailable` there. Browser
flags that treat an insecure origin as secure are not documented or suggested.

## Instrument views (v2.6.0, feature 016, ADR-031..033)

### Phase 0 reconciliation with 012/014/015 (T001, 2026-11-18)

| Need | Planned placeholder | Real 2.5.0 surface used |
|---|---|---|
| Playback transpose | internal `transposeSemitones` | `PracticeSettings.transposeSemitones` applied by `setPractice` (012); chord views and the scorer read the practice controller's state |
| Playback rate | `transportPosition(t)` | unchanged: the karaoke clock (`karaoke/clock.ts`, `heardTime`) already applies the 012 rate |
| Count-in of "Drumless" | 012 count-in setting | `PracticeSettings.countIn {bars, everyPass}` through `setPractice` (Blazor stores and restores it) |
| Selected input node | internal `InputSource` | the input opened by `openInput` (015, `recording/input.ts`); `startSingingFeedback` rejects `No input device is open` without it |
| Calibrated round trip | internal latency state | **the engine keeps no calibration**: Blazor passes the stored 015 calibration (`/api/recording/calibrations`) as `SingingFeedbackRequest.roundTripMs` + `latencyMethod` (`loopback`/`manual`); absent = `InputInfo.estimatedLatencySamples` (source `estimated`). `RoundTripLatency.source` and `SingingSummary.latencySource` use the 015 `LatencyMethod` |
| Take audio | `takeUrl` argument | `TakeScoreRequest {takeUrl, startSample, ...}` (take FLAC of backend 1.14.0, `startSample` of the take) |
| Worklet file | `audio-engine-pitch.worklet.js` | `audio-engine-pitch-worklet.js` (015 naming, `vite.pitch.config.ts`), `EngineOptions.pitchWorkletUrl` |
| Score view of the drum score / parts | "014 score view" | `attachScoreFollower` + Blazor `Components/Practice/ScorePanel.razor` (014), unchanged |
| Mixer dock | - | the karaoke banner lives in the fixed `.ss-mixer-dock` (014 integration fix); banner chords add one row to it, so `dock-inset.js` must re-measure after `setKaraokeChords` (its ResizeObserver does); the other views render in the scrolling mixer content above the dock |

Types: `src/audio-engine/src/types.ts` (section "instrument views (contract 2.6.0)") is binding;
data-model.md 9 describes the same shapes.

### New `AudioEngine` methods

| Method | Behaviour |
|---|---|
| `attachChordLane(elementId, chart, options?)` | Engine-owned DOM in the host: `.chord-lane-item` elements (text via `textContent`) with `data-state` = `past`/`current`/`future`, `data-start-ms`; positioned by `style.transform` on a horizontal timeline of `barsBehind` (1) to `barsAhead` (6) bars from the beat grid (or 4 s/12 s without a grid). Host attributes: `data-chord-index`, `data-current-symbol`, `data-next-symbol`, `data-position-ms`. Replaces an attached lane. |
| `detachChordLane()` | Removes the children and the host attributes; no-op when none. |
| `setKaraokeChords(chart \| null)` | Adds `.karaoke-chord` elements above the words of the three karaoke slots: a chord is anchored to the word whose `[start, nextStart)` holds its `startMs` (`data-lead="true"` before the first word of a line), with `data-state` like words; `null` removes them. Allowed before or after `attachKaraoke`; kept across `attachKaraoke` calls. Host gets `data-chord-index`. |
| `setChordDisplay(display)` | Sets the capo (capo mode, S12) for every chord view; symbols are re-spelled from `root`/`quality`/`bass`, the key region the chord starts in (fallback the global key), the engine's transpose and the capo (data-model 2.5, 2.9) on the next frame; with `showSounding` each item also gets a `.chord-sounding` child with the sounding symbol. A transpose change (012) re-spells automatically. The chord lane host additionally gets `data-capo` and `data-current-key` (display of the current region's key at the current transpose, data-model 2.8), so Blazor can show the current key without per-frame interop. |
| `attachHitLanes(elementId, hits, options?)` | Engine-owned `<canvas>` in the host (`role="img"`, `aria-label` set by Blazor on the host); five lanes (data-model 5.2), look-ahead `lookAheadMs` (4000; 2000..8000), only `pieces` drawn when given. Host attributes: `data-position-ms`, `data-next-hit-ms`, `data-next-hit-piece`, `data-visible-hits` (count), `data-look-ahead-ms`. |
| `setHitLaneOptions(options)` | Changes look-ahead or pieces without re-attaching. |
| `detachHitLanes()` | Removes the canvas and attributes. |
| `getRoundTripLatency(): RoundTripLatency` | The value singing feedback would use: the `roundTripMs` + `latencyMethod` of the running request (the 015 calibration Blazor holds), else the open 015 input's `estimatedLatencySamples` / 44.1 with source `estimated`. |
| `startSingingFeedback(request): Promise<void>` | Loads the `pitch-tracker` worklet module once (`EngineOptions.pitchWorkletUrl`, default `./js/audio-engine-pitch-worklet.js`), connects it to the 015 input (rejects with `Error("No input device is open")` when 015 has none), attaches the pitch view canvas to `pitchViewElementId`, and scores units while playing (data-model 3.3-3.4). State callbacks: `starting` -> `running`. Host attributes of the pitch view: `data-state`, `data-singer-cents` (rounded, `""` when unvoiced), `data-target-cents`, `data-unit-index`, `data-last-score`, `data-latency-source`. |
| `setSingingTolerance(cents)` | 10..200; applies to the current and later units. |
| `stopSingingFeedback(): SingingSummary` | Disconnects the worklet, keeps the canvas showing the last window, returns the summary; state `stopped`. Idempotent (returns the last summary). |
| `scoreTake(request: TakeScoreRequest): Promise<TakeScoreResult>` | Fetches and decodes the take (`takeUrl` = `GET /api/takes/{takeId}/audio`), places it at `startSample`, runs the same YIN core (shared pure module) offline over it and scores every unit the take covers. 015 takes are already latency-compensated (no round trip). Does not need the worklet. One request object (SIG: at most 4 parameters). |

### Timeline clock and frame loop

- All views use the ADR-015 karaoke clock (`positionMs` from `getOutputTimestamp`, output latency
  subtracted before the loop wrap, 012 rate applied by `transportPosition`); paused/stopped =
  stored position. No accumulation from frame deltas.
- One shared `requestAnimationFrame` loop (the 006 `frame-loop.ts`) draws meters, karaoke,
  banner chords, chord lane, hit lanes and pitch view while attached, playing and visible, plus
  one frame after every attach, seek, pause, stop, loop change, offset/capo/transpose change and
  resize. Budget: <= 4 ms per frame for all views (benchmark test with counted primitives).
- Cursors advance in O(1) per frame and binary-search after seeks, loop wraps and changes.
- Reduced motion (`options.reducedMotion`, default from the media query, followed at runtime): the
  lane, hit lanes and pitch view page instead of scrolling (data-model 3.5, 5.2).

### Pitch tracker worklet (`pitch-tracker`)

- Processor name `stemstudio-pitch-tracker`; parameters per research R-05 (binding); messages
  per data-model 3.2. Pure YIN core in `src/pitch/yin.ts` (no Web Audio types, used by the
  worklet and `scoreTake`), decimator in `src/pitch/decimate.ts`.
- Overrun guard: the main thread counts hops whose `t` lags the context time by more than 50 ms;
  50 such hops within 5 s -> `stopSingingFeedback()` internally, state `failed`, reason "Pitch
  tracking is too slow on this device." (`OnSingingStateChanged('failed', reason)`).
- No microphone audio leaves the engine; the worklet's output is silent (not connected to the
  destination).

## Callbacks to .NET

| Method (JSInvokable) | Arguments | When |
|---|---|---|
| `OnPositionChanged` | `double seconds` | at most 10 Hz while playing; once after seek, pause, stop |
| `OnPlaybackStateChanged` | `string state` (`stopped`\|`playing`\|`paused`) | on every state change |
| `OnPracticeChanged` (2.3.0) | `PracticeStatus status` (JSON object) | after every applied practice change, stretch state change, trainer pass start, count-in beat and underrun burst; at most 10 Hz |
| `OnLoopPass` (2.4.0) | `LoopPassInfo pass` (JSON object) | once per completed pass of an enabled loop, when the wrap is heard; at most one call per pass |
| `OnRecordingStateChanged` (2.5.0) | `string state` (`RecordingState`) | every recording state change |
| `OnRecordingProgress` (2.5.0) | `RecordingProgress` | at most 4 Hz while counting in or recording |
| `OnTakesCaptured` (2.5.0) | `CapturedTake[]` | when a recording session ends |
| `OnInputLost` (2.5.0) | `string reason` | the input device was unplugged or its track ended |
| `OnInputDevicesChanged` (2.5.0) | - | `devicechange` |
| `OnSingingUnitScored` (2.6.0) | `int index, int? score, int pass` | once per finished singing unit |
| `OnSingingStateChanged` (2.6.0) | `string state, string? reason` | every `SingingState` change |

## Errors

Methods throw (or reject) `Error` with a readable message; Blazor receives `JSException`:
loading a stem fails (HTTP status or decode error, message names the stem), invalid loop region,
unknown channel id in `setChannel`/`attachMeter`, `setExpanded` for a parent without a group,
calls after `dispose()`. A failing lazy group load rejects `setExpanded` (message names the
stem); the parent stays collapsed and audible.

## Testing

Vitest with a hand-written `AudioContext` mock (records `start(when, offset)` calls, gain values
and scheduled click times) and fake timers; coverage >= 90 % lines/branches/functions/statements
(`npm run test:coverage`). Real-browser behaviour is covered by the Playwright E2E suite, which locates meters and controls
through the ids in [ui-test-ids.md](ui-test-ids.md) (1.6.0: `karaoke-banner` and the class selectors of
the karaoke DOM).

2.3.0 testing:

- Vitest: the stretch stage is replaced through `EngineOptions.timeStretchFactory` by a fake
  (identity pitch, fixed `latencyFrames`, records `setPitch`/`reset` calls); the processor
  (`stretch/processor.ts`) is tested with a fake `AudioWorkletGlobalScope`
  (`registerProcessor`, `currentFrame`, `sampleRate`) and a TypeScript fake of the WASM ABI; the
  mock `AudioContext` gains `audioWorklet.addModule`, `AudioWorkletNode` and an `AudioParam`
  mock that records `setValueAtTime`. Coverage >= 90 % lines/branches/functions/statements.
- Node WASM suite (`npm run test:wasm`, runs only when `soundtouch.wasm` was built; CI builds it):
  loads the real module through the same `abi.ts`, checks a 440 Hz sine at pitch factors
  0.5/1/2 (FFT peak within 1 %), identity at factor 1 (after the delay, max error < 1e-4), the
  measured initial latency <= `STRETCH_DELAY_FRAMES - 256`, the output lag never above
  `STRETCH_DELAY_FRAMES - 256` across 100 pitch jumps 0.42 <-> 4 (no drift), the mean content offset
  at factors 0.5 and 2 within 25 ms of `c(p)`, and throughput >= 10x real time for
  stereo 44.1 kHz on the CI runner.
- Playwright (real browsers, real WASM): ui-test-ids 1.11.0 `practice-*`, `countin-*`,
  `trainer-*` ids, an output probe (tests tap `destination` connections) for pitch, the transport
  position for speed.

2.4.0 testing:

Vitest with the existing `AudioContext` mock (`getOutputTimestamp`, `outputLatency`), fake
`requestAnimationFrame`, a stubbed `scrollIntoView`, jsdom host with page containers: bar changes
at boundaries, loop wrap at heard time, seek -> binary search, speed (A-012-1 position at 50 %
and 120 %), reduced motion, manual-scroll pause with fake timers, validation messages, detach
cleans up, OnLoopPass once per pass with the right speed. Coverage >= 90 % lines/branches/
functions/statements.

2.6.0 errors (feature 016): `Error("Chord lane host not found: <id>")`, `Error("Hit lane host not found: <id>")`,
`Error("Pitch view host not found: <id>")`; `Error("Invalid chord chart: <reason>")` (segments not
ordered/contiguous, > 4000, root/bass outside 0..11, unknown quality); `Error("Invalid drum hits:
<reason>")` (> 40,000, unknown piece, strength outside 0..1, times decreasing); `Error("Invalid
pitch contour: <reason>")` (hopMs not 10, > 90,000 frames, values outside 0..12700);
`Error("Invalid singing request: <reason>")` (tolerance outside 10..200, units not ordered);
`Error("Capo out of range: <value>")`; `Error("Singing feedback is already running")`;
`Error("No input device is open")`. Validation happens before an attached view is replaced.

2.6.0 testing (feature 016): Vitest with the existing `AudioContext` mock plus a fake `AudioWorkletNode`/`audioWorklet.addModule`,
a fake canvas 2D context that records primitives, the fake `requestAnimationFrame`, stubbed
`getBoundingClientRect`/`ResizeObserver`; the YIN core is tested on generated sines, harmonics and
noise (SC-004 accuracy) and with a benchmark test (`yin.bench.test.ts`, reported, not gating in
CI); shared chord spelling vectors (`chord-spelling-vectors.json`). Coverage >= 90 % lines,
branches, functions, statements.
