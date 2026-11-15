# Contract: Audio engine JS interop (v2.1.0)

The binding TypeScript definition is [`src/audio-engine/src/types.ts`](../../../src/audio-engine/src/types.ts).
This document explains the behaviour behind those types. Design rationale: ADR-005 (v1) and
ADR-010 (v2: stem groups), ADR-015 (v2.1: karaoke).

**Changelog**: 2.0.0 (feature 002) - channels are addressed by `ChannelId` (a stem name or
`<parent>.<detail>`, e.g. `drums.snare`); `loadTrack` accepts optional detail `groups`; new
`setExpanded(parent, expanded)`; the solo/mute gate is evaluated over the *audible level* only
(collapsed parents plus the detail channels of expanded parents). Calls valid in 1.0.0 behave
exactly as before when no groups are loaded, but the gate rule change is a semantic break, hence
the major version. 2.1.0 (additive, feature 006) - karaoke banner: `attachKaraoke`,
`setKaraokeOffset`, `detachKaraoke` and the `Karaoke*` types; a shared animation-frame loop for
meters and karaoke (meter behaviour unchanged). Every 2.0.0 call behaves exactly as before.

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
  `ENGINE_CONTRACT_VERSION` (`"2.1.0"`). Everything else is reached through the returned
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

## Loop (FR-013)

- `setLoop({start, end, enabled})` validates `0 <= start < end <= duration`, otherwise throws
  `Error("Invalid loop region: ...")` and leaves the previous loop unchanged.
- Enabled: every source gets `loop = true`, `loopStart = start`, `loopEnd = end` (seamless,
  sample-accurate looping by the browser). If the position is outside the region when looping is
  enabled, playback restarts at `start`. Disabled: `loop = false` on the running sources.

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

## Meters (FR-011, ADR-001 #4)

- `attachMeter(target, elementId)` with `target` a `ChannelId` (stem or `<parent>.<detail>`) or `"master"`.
- On each `requestAnimationFrame` the engine reads `getFloatTimeDomainData`, computes the peak
  `p` and `dB = 20*log10(p)`, clamps to -60..0 and writes to the element:
  - `style.setProperty("--meter-level", (dB + 60) / 60)` (0..1, decay ~ 20 dB/s),
  - `dataset.peakDb` = rounded dB (string),
  - `dataset.clip` = `"true"` for 1.5 s after any sample with `|x| >= 1.0`, else `"false"`.
- No meter data is sent to .NET (no 60 fps interop). The frame loop runs only while meters are
  attached and stops on `dispose()`.

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
  `data-position-ms` (rounded karaoke clock, for tests). Styling comes from the app's CSS (004
  tokens); the engine sets no colours or fonts.
- **Clock**: `positionMs = round(1000 * (transportPosition(audioNow) - outputLatency)) - offsetMs`
  where `audioNow = ts.contextTime + (performance.now() - ts.performanceTime) / 1000` from
  `ctx.getOutputTimestamp()` (fallback `ctx.currentTime` when the method is missing or returns
  zeros), `outputLatency` = `ctx.outputLatency` when defined (else `ctx.baseLatency`, else 0), and
  `transportPosition` is the ADR-005 formula `offset + (audioNow - when)` wrapped into the loop.
  Paused or stopped: the stored transport position. Never accumulated from frame deltas (no
  drift, SC-002). Positive `offsetMs` makes the lyrics come later.
- **Frame loop**: one shared `requestAnimationFrame` loop per engine serves the meters and the
  karaoke (`frame-loop.ts`; the meters move onto it with unchanged behaviour). Karaoke is drawn
  while attached, playing and `document.visibilityState === 'visible'`, plus exactly one frame
  after every attach, seek, pause, stop, loop change, offset change and resize. No timers
  (`setTimeout`/`setInterval`) are used for drawing.
- **Cursor**: current line and word advance forward in O(1) per frame; binary search after a
  seek, a loop wrap or an offset change.
- **Layout**: word centres are measured once after the DOM is built
  (`getBoundingClientRect`, relative to the host) and again on `ResizeObserver` callbacks and
  size-class changes. Per frame the engine writes only `style.transform` of the ball and
  `data-state` of at most two words and two lines (no layout reads in the frame callback; SC-004).
- **Ball**: lands on word *i* exactly at `startMs(i)`. Between word *i* and *i+1*:
  `p = clamp((t - start_i) / (start_{i+1} - start_i), 0, 1)`, `x = lerp(x_i, x_{i+1}, p)`,
  `y = -h * 4p(1 - p)` with `h` = 0.6 x line height (scaled by `duration / 150 ms` for words
  shorter than 150 ms). Across a line change the ball arcs to the first word of the next line
  (shown in the `next` slot until that line becomes current). Written as
  `translate3d(x, y, 0)`.
- **States**: `hidden` when `lines` is empty; `countdown` from `firstLine.startMs - 3000` until
  the first line, and inside gaps longer than `countdownGapMs` (default 5000) from 3 s before the
  next line (one dot per second); `singing` otherwise while inside the lyrics; `ended` after the
  last line's `endMs`.
- **Reduced motion**: `options.reducedMotion`, default
  `matchMedia('(prefers-reduced-motion: reduce)').matches` (a runtime change of the media query is
  followed): no ball; the current word gets `data-state="current"` (styled as a fill).
- **Validation and errors**: unknown element id -> `Error("Karaoke host not found: <id>")`;
  invalid lyrics (line or word starts decreasing, `endMs <= startMs`, a word outside its line,
  more than 400 lines or 40 words per line) -> `Error("Invalid karaoke lyrics: <reason>")`;
  `offsetMs` outside -5000..5000 -> `Error("Karaoke offset out of range: <value>")`; calls after
  `dispose()` throw `Error("Audio engine has been disposed")` like every other method.
- **Skeleton (Phase 0)**: until 006 T026/T036 the three methods throw
  `Error("Karaoke is not implemented yet ...")` (`KARAOKE_NOT_IMPLEMENTED` in `engine.ts`); the
  Blazor wrapper does not call them before the frontend's 006 tasks.
- **Testing**: Vitest with the `AudioContext` mock extended by `getOutputTimestamp` and
  `outputLatency`, a fake `requestAnimationFrame`, a stubbed `getBoundingClientRect` and
  `ResizeObserver`; coverage >= 90 % (lines, branches, functions, statements).

## Callbacks to .NET

| Method (JSInvokable) | Arguments | When |
|---|---|---|
| `OnPositionChanged` | `double seconds` | at most 10 Hz while playing; once after seek, pause, stop |
| `OnPlaybackStateChanged` | `string state` (`stopped`\|`playing`\|`paused`) | on every state change |

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
