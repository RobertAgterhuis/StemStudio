# Contract: Audio engine JS interop (v2.3.0)

The binding TypeScript definition is [`src/audio-engine/src/types.ts`](../../../src/audio-engine/src/types.ts).
This document explains the behaviour behind those types. Design rationale: ADR-005 (v1) and
ADR-010 (v2: stem groups), ADR-015 (v2.1: karaoke), ADR-021 (v2.3: practice tempo).

**Base**: 2.1.0 (`specs/006-synced-lyrics-karaoke/contracts/audio-engine.md`). Version 2.2.0 is
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
  visual only, not audible timing). Pause, stop or seek during a count-in cancels it (future clicks
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

## Callbacks to .NET

| Method (JSInvokable) | Arguments | When |
|---|---|---|
| `OnPositionChanged` | `double seconds` | at most 10 Hz while playing; once after seek, pause, stop |
| `OnPlaybackStateChanged` | `string state` (`stopped`\|`playing`\|`paused`) | on every state change |
| `OnPracticeChanged` (2.3.0) | `PracticeStatus status` (JSON object) | after every applied practice change, stretch state change, trainer pass start, count-in beat and underrun burst; at most 10 Hz |

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
