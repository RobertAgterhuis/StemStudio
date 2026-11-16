# Contract delta: audio engine 2.4.0 (feature 014)

Status: **binding delta** (014 planning, 2026-11-16). Base: engine contract **2.3.0** of feature
012 (not yet merged; written in parallel). In Phase 0 part 2 the architect merges this delta into
the full `specs/014-practice-flow/contracts/audio-engine.md` **2.4.0** and sets
`EngineContractVersion = '2.4.0'` in `src/audio-engine/src/types.ts`. Additive: every 2.3.0 call
behaves exactly as before. ADR-023 D3, D7.

Changelog line for the merged document:

> 2.4.0 (additive, feature 014) - score follower (`attachScoreFollower`, `setScoreFollowOptions`,
> `detachScoreFollower`, `ScoreView*` types) drawn on the shared animation frame against the
> heard position; `.NET` callback `OnLoopPass(LoopPassInfo)`.

## 1. Assumptions on 2.3.0 (feature 012)

| Id | Assumption | If 012 differs |
|---|---|---|
| A-012-1 | `getPosition()` and the karaoke clock report **track seconds** at every speed (the transport formula scales context time by the playback rate and the heard position subtracts the time-stretch latency). | The follower uses exactly the function the karaoke banner uses; reconcile names only. |
| A-012-2 | The speed in percent is known to the engine (`setTempo` or similar). | `LoopPassInfo.speedPercent` reports 100 when speed is unsupported. |
| A-012-3 | 012 has no loop-pass callback. | If it has one with the same information, keep 012's and drop `OnLoopPass` (DELTA.md R-3). |

## 2. Types (`types.ts`)

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

## 3. Methods (`AudioEngine`)

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

## 4. Behaviour

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

## 5. `OnLoopPass`

- Fired once per completed pass of an **enabled** loop at the moment the wrap is heard (computed on
  the frame loop and on the scheduler tick, whichever sees the wrap first; never twice for one
  pass), with the region and the speed of the pass. A seek or loop change resets `pass` to 0 and
  the partial pass is not reported.
- Frequency is at most one call per pass (a 1 s loop: 1 Hz), so no high-frequency interop.

## 6. Testing

Vitest with the existing `AudioContext` mock (`getOutputTimestamp`, `outputLatency`), fake
`requestAnimationFrame`, a stubbed `scrollIntoView`, jsdom host with page containers: bar changes
at boundaries, loop wrap at heard time, seek -> binary search, speed (A-012-1 position at 50 %
and 120 %), reduced motion, manual-scroll pause with fake timers, validation messages, detach
cleans up, OnLoopPass once per pass with the right speed. Coverage >= 90 % lines/branches/
functions/statements.
