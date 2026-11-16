# Contract delta: Audio engine JS interop 2.8.0 (feature 019)

**Additive minor** on top of the engine version current when 019 starts (017's 2.7.0 by the
roadmap allocation; 012 = 2.3.0 speed/transpose, 014 = 2.4.0 sections). Every call that is
valid in 2.7.0 behaves exactly as before while no timeline is set. Phase 0 (tasks.md T005)
merges this delta into `src/audio-engine/src/types.ts` and the full `audio-engine.md`.
Design: ADR-028 D4-D6; law: data-model.md 3; vectors: `render-vectors.json` 1.2.0
`timeline.spans`, `timeline.clock`, `timeline.metronome`, `timeline.samples`.

**Changelog entry**: 2.8.0 (additive, feature 019) - arranged timelines: `setTimeline`,
`getTimelinePosition`, `getTimelineDuration`, `seekTimeline`, `setTimelineLoop`, callback
`OnTimelinePositionChanged`, types `TimelinePlan`, `TimelineSegment`. Song order (no timeline)
is byte-for-byte the 2.7.0 path.

## 1. Types (added to `types.ts`)

```ts
/** Semantic version of this contract. */
export type EngineContractVersion = '2.8.0';

/** One part of an arranged timeline (a region repeat), in frames at 44.1 kHz (contract 2.8.0). */
export interface TimelineSegment {
  /** First source frame (>= 0, < track length in frames). */
  readonly sourceStartFrame: number;
  /** Length in frames (>= 1, > crossfadeFrames). */
  readonly frameCount: number;
  /** Channels silent in this segment; a parent mutes its detail channels. */
  readonly mutes: readonly ChannelId[];
}

/** An arranged timeline: the engine subset of the backend `ResolvedTimeline`. */
export interface TimelinePlan {
  /** Always 44100. */
  readonly sampleRate: 44100;
  /** Even crossfade length F in frames, 0..4410 (0 = hard cuts). */
  readonly crossfadeFrames: number;
  /** 1..256 segments laid end to end; total <= 1800 s. */
  readonly segments: readonly TimelineSegment[];
}

/** Callback names (2.8.0 adds `OnTimelinePositionChanged`). */
export type DotNetCallbacks = 'OnPositionChanged' | 'OnPlaybackStateChanged' | 'OnTimelinePositionChanged'
  /* | names added by 2.3.0-2.7.0 */;
```

`AudioEngine` gains:

```ts
/**
 * Plays the track along `plan` (gapless, sample-locked, crossfaded per data-model 3) or, with
 * null, in song order. While playing, the arranged position is kept (clamped to the new length;
 * at or beyond the end: stopped at 0); a timeline loop is cleared when the new plan is shorter
 * than its end. Throws `Error("Invalid timeline: <reason>")`; the previous plan stays active.
 */
setTimeline(plan: TimelinePlan | null): void;
/** Arranged seconds heard now; equals `getPosition()` when no timeline is set. */
getTimelinePosition(): number;
/** Arranged length in seconds; equals the track duration when no timeline is set. */
getTimelineDuration(): number;
/** Jumps to arranged `seconds` (clamped to 0..duration); keeps playing if playing. */
seekTimeline(seconds: number): void;
/**
 * Loop on the arranged timeline (seconds). Rejects B <= A or a region outside the timeline;
 * `enabled: false` clears it. Throws `Error("No timeline: use setLoop")` without a timeline.
 */
setTimelineLoop(loop: LoopRegion): void;
```

## 2. Behaviour

| Topic | Rule |
|---|---|
| Graph | Per run: `AudioBufferSourceNode -> GainNode(run fade) -> <channel input>`; the channel strip (fader x gate, pan, 012/017 processing, meter) and master are unchanged. Region mutes are therefore pre-fader/pre-effects. |
| Runs | Built from the plan per loaded channel at the *audible level* (collapsed parents and the details of expanded parents) by the law of data-model 3.3 (vectors `timeline.spans`). Expanding/collapsing while playing builds runs for the new channels from the switch time (same rule as `addBuffers`). |
| Scheduling | The existing look-ahead scheduler (wake-up only) starts every run whose arranged start lies within the next 2.0 s: `source.start(when + (runStart - anchorOffset) / rate, sourceOffset, durationSeconds)`, `rate` = the 012 speed factor (1 when 1.0). Fades: `gain.setValueCurveAtTime(curve, t, F / 44100 / rate)` with `curve[k]` = data-model 3.4 for `k = 0..F-1`; a run that starts mid-way (seek) uses the remaining part of its curve. |
| Sample lock | All runs are timed from the one transport anchor, so every channel crosses a boundary at the same context time; a run started late (stall) is started with its offset advanced by the delay, never shifted. |
| Positions | `getPosition()`, `OnPositionChanged(seconds)` and the karaoke clock report **source** seconds (data-model 3.5); `OnTimelinePositionChanged(arrangedSeconds, segmentIndex)` is sent in the same tick (<= 10 Hz while playing, once after seek/stop/`setTimeline`). End of timeline: state `stopped`, both positions 0. |
| Metronome | Clicks per data-model 3.6 (vectors `timeline.metronome`); count-in (012) uses the arranged grid; `bpmOverride` lays a constant grid on arranged time. |
| `seek(source)` | data-model 3.7 (first occurrence at/after the playhead, wrapping; else the next segment starting after it; else the end). |
| `setLoop` | With a timeline: `enabled: true` throws `Error("Timeline active: loop regions on the arranged timeline (setTimelineLoop)")`; `enabled: false` is accepted (no-op). |
| Timeline loop | Wrap end -> start is a boundary: crossfaded when not contiguous (data-model 3.7). Any 012 loop-pass event fires on timeline loop wraps too. |
| `loadTrack` | Clears the timeline and the timeline loop (song order). |
| Recording (015) | The engine refuses to start a recording while a timeline is set: `Error("Timeline active: switch to song order to record")`. |
| Validation | `Invalid timeline: ` + one of `sampleRate must be 44100`, `crossfadeFrames must be even and 0..4410`, `1..256 segments`, `segments[i].frameCount must be > crossfadeFrames`, `segments[i].sourceStartFrame outside the track`, `total length above 1800 s`, `segments[i].mutes[j] is not a channel id`. |
| Disposal | `dispose()` stops runs and clears the plan. |

## 3. Testing (audio-engine owner)

- Vitest with the mocked `AudioContext` (start times, offsets, durations and curve arguments
  recorded per node): `timeline.spans` runs per channel exact; curves equal data-model 3.4 to
  1e-9; `timeline.clock` and `timeline.metronome` exact to 1e-9; scheduling window, late starts,
  live `setTimeline` while playing, expand during timeline, seek/loop rules, errors.
- qa runs `timeline.samples` through the built engine in an `OfflineAudioContext` (research.md
  R-12), tolerance 1e-4.
- Song-order regression: the full 2.7.0 suite passes unchanged with `setTimeline` never called.
