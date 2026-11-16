/**
 * Sample-locked transport: every stem (standard and decoded detail stems) gets a fresh source
 * started with the same `when` and the same offset; the position is derived from
 * `AudioContext.currentTime` only.
 *
 * Contract 2.3.0: the anchor carries a rate and maps heard context time to track time,
 * `position(t) = offset + (t - when) * rate`; while the time-stretch stage is engaged its heard latency `L` is
 * compensated by starting every source `L` earlier. A second, source-side anchor queue maps source time to buffer
 * time, so late sources (detail groups) start exactly where the running ones are.
 *
 * @packageDocumentation
 */
import { AnchorQueue, anchorOf, rateOf, type TransportAnchor } from './anchors';
import { NO_LOOP, applyLoop, isInside, wrapPosition } from './loop';
import { trackDuration, type ChannelBuffers } from './stem-loader';
import type { ChannelId, LoopRegion, PlaybackState } from './types';

export type { TransportAnchor } from './anchors';

/** Observer of transport events (callbacks to .NET, metronome). */
export interface TransportListener {
  /** Called on every state change. */
  onStateChanged(state: PlaybackState): void;
  /** Called after a seek, pause, stop, end of track or loop change with the new position. */
  onReposition(position: number): void;
  /** Called after a live rate or latency change (contract 2.3.0); the anchors changed, the position did not jump. */
  onTimingChanged?(): void;
}

/** Read-only timing view of the transport used by the metronome. */
export interface TransportClock {
  /** Anchor of the running sources, or null when not playing. */
  anchor(): TransportAnchor | null;
  /** All current anchors in `when` order (contract 2.3.0); absent = just {@link TransportClock.anchor}. */
  anchors?(): readonly TransportAnchor[];
  /** Active loop region. */
  readonly loop: LoopRegion;
  /** Track duration in seconds. */
  readonly duration: number;
}

/** Construction parameters of {@link Transport}. */
export interface TransportOptions {
  /** The engine's single AudioContext. */
  readonly context: AudioContext;
  /** Delay between the call and the shared `when` (s). */
  readonly startDelaySeconds: number;
  /** Mixer input node of a channel. */
  readonly output: (id: ChannelId) => AudioNode;
}

/** Rate of the sources and heard latency used for the next start (contract 2.3.0). */
export interface TransportTiming {
  /** Playback rate of every source (`speedPercent / 100`). */
  readonly rate: number;
  /** Heard latency `L` (s) between a source frame and the moment it is heard; 0 without the stretch stage. */
  readonly latency: number;
}

/** A live rate change (audio-engine.md 2.3.0 "Live changes"). */
export interface RateChange extends TransportTiming {
  /** Source-side context time `U` of the `playbackRate` automation. */
  readonly at: number;
  /** Heard anchor that applies from the time the change is heard. */
  readonly anchor: TransportAnchor;
}

interface PendingChange {
  readonly at: number;
  readonly heard: TransportAnchor;
  readonly played: TransportAnchor;
}

/** How long anchors are kept after the next one started (s); covers the karaoke clock reading slightly behind. */
const ANCHOR_HISTORY_SECONDS = 2;

/** Play/pause/stop/seek/loop over the loaded stems. */
export class Transport implements TransportClock {
  private buffers: ChannelBuffers = new Map();
  private sources: AudioBufferSourceNode[] = [];
  private readonly heard = new AnchorQueue({ when: 0, offset: 0 });
  private readonly played = new AnchorQueue({ when: 0, offset: 0 });
  private pending: PendingChange | null = null;
  private sourceStart = 0;
  private current: TransportTiming = { rate: 1, latency: 0 };
  private restartHook: () => void = () => undefined;
  private pausedAt = 0;
  private playState: PlaybackState = 'stopped';
  private region: LoopRegion = NO_LOOP;
  private length = 0;
  private readonly listeners: TransportListener[] = [];

  /** @param options - Context, start delay and mixer inputs. */
  public constructor(private readonly options: TransportOptions) {}

  /** Track duration in seconds (longest stem). */
  public get duration(): number {
    return this.length;
  }

  /** Current transport state. */
  public get state(): PlaybackState {
    return this.playState;
  }

  /** Active loop region. */
  public get loop(): LoopRegion {
    return this.region;
  }

  /** Rate and latency of the running sources (and of the next start). */
  public get timing(): TransportTiming {
    return this.current;
  }

  /** @param listener - Receives state and reposition events. */
  public addListener(listener: TransportListener): void {
    this.listeners.push(listener);
  }

  /** {@inheritDoc TransportClock.anchor} */
  public anchor(): TransportAnchor | null {
    return this.playState === 'playing' ? this.heard.at(this.options.context.currentTime) : null;
  }

  /** {@inheritDoc TransportClock.anchors} */
  public anchors(): readonly TransportAnchor[] {
    return this.playState === 'playing' ? this.heard.all : [];
  }

  /** @param hook - Called before every start of all sources (the engine resets the stretch stage there). */
  public setRestartHook(hook: () => void): void {
    this.restartHook = hook;
  }

  /** @param timing - Rate and latency for the next start of all sources (running sources are not changed). */
  public setTiming(timing: TransportTiming): void {
    this.current = { rate: timing.rate, latency: timing.latency };
  }

  /**
   * @param buffers - Decoded standard stems replacing the current track (playback stops, loop
   *   cleared, detail buffers dropped); the duration is the longest of them.
   */
  public load(buffers: ChannelBuffers): void {
    this.halt();
    this.buffers = buffers;
    this.length = trackDuration(buffers);
    this.region = NO_LOOP;
    this.pausedAt = 0;
    this.setState('stopped');
  }

  /**
   * Resumes the context and starts all stems at the stored position.
   *
   * @param at - Optional heard context time of the start (count-in); never before `now + startDelay + latency`.
   */
  public async play(at?: number): Promise<void> {
    await this.options.context.resume();
    if (this.playState === 'playing' || this.buffers.size === 0) {
      return;
    }
    this.startAll(this.startPosition(), at);
    this.setState('playing');
  }

  /** @returns The track time the next `play()` starts at (the stored position, or the loop start outside the loop). */
  public startPosition(): number {
    return this.startOffset(this.pausedAt);
  }

  /** Stops all sources and remembers the position. */
  public pause(): void {
    if (this.playState !== 'playing') {
      return;
    }
    this.pausedAt = this.position();
    this.halt();
    this.setState('paused');
    this.reposition(this.pausedAt);
  }

  /** Stops all sources and resets the position to 0. */
  public stop(): void {
    this.halt();
    this.pausedAt = 0;
    this.setState('stopped');
    this.reposition(0);
  }

  /** @param seconds - Target position, clamped to 0..duration (loop start when outside an active loop). */
  public seek(seconds: number): void {
    const target = this.startOffset(Math.min(Math.max(seconds, 0), this.length));
    this.moveTo(target);
    this.reposition(target);
  }

  /**
   * Restarts all sources at the heard position with the current timing (engaging the stretch stage).
   * No-op unless playing.
   */
  public restart(): void {
    if (this.playState === 'playing') {
      this.moveTo(this.position());
    }
  }

  /** @returns Current position (s), derived from `currentTime` and wrapped into an active loop. */
  public position(): number {
    return this.positionAt(this.options.context.currentTime);
  }

  /**
   * @param time - Context time (s), e.g. the time currently heard at the output (karaoke clock).
   * @returns Track time heard at `time` while playing (the ADR-005 formula with the rate, wrapped into an
   *   active loop, never before the start offset), else the stored position.
   */
  public positionAt(time: number): number {
    return this.playState === 'playing' ? this.offsetAt(this.heard, time) : this.pausedAt;
  }

  /**
   * Applies a live rate change: `playbackRate.setValueAtTime(rate, at)` on every running source and the heard
   * anchor from `change.anchor`. A change whose `at` is still in the future is replaced (same `at` expected).
   *
   * @param change - New rate, latency, automation time and heard anchor.
   */
  public retime(change: RateChange): void {
    if (this.playState !== 'playing') {
      return;
    }
    this.dropPending();
    const played = anchorOf(change.at, this.offsetAt(this.played, change.at), change.rate);
    this.sources.forEach((source) => source.playbackRate.setValueAtTime(change.rate, change.at));
    this.heard.push(change.anchor);
    this.played.push(played);
    this.pending = { at: change.at, heard: change.anchor, played };
    this.current = { rate: change.rate, latency: change.latency };
    this.prune();
    this.listeners.forEach((l) => l.onTimingChanged?.());
  }

  /**
   * Adds decoded buffers (a detail group) to the track; while playing they are started at once
   * with `start(at, bufferPositionAt(at))`, the current rate automation and the loop properties.
   *
   * @param buffers - Buffers keyed by channel id; the duration is unchanged.
   * @param at - Source-side context time of the start, not before the shared start (see {@link Transport.startTimeAfter}).
   */
  public addBuffers(buffers: ChannelBuffers, at: number): void {
    this.buffers = new Map([...this.buffers, ...buffers]);
    if (this.playState !== 'playing') {
      return;
    }
    const offset = this.offsetAt(this.played, at);
    buffers.forEach((buffer, id) => {
      this.sources.push(this.startSource(id, buffer, { when: at, offset }));
    });
  }

  /**
   * @param delay - Minimum delay from now (s).
   * @returns `currentTime + delay`, or the shared source start while it is still in the future.
   */
  public startTimeAfter(delay: number): number {
    const soon = this.options.context.currentTime + delay;
    return this.playState === 'playing' ? Math.max(soon, this.sourceStart) : soon;
  }

  /** @param region - Validated loop region applied to the running sources. */
  public setLoop(region: LoopRegion): void {
    const position = this.position();
    this.region = region;
    if (this.playState === 'playing') {
      this.applyRunningLoop(position, region);
    }
  }

  /** Stops all sources without notifying anyone and releases every decoded buffer (engine disposal). */
  public dispose(): void {
    this.listeners.length = 0;
    this.sources.forEach((source) => {
      source.buffer = null;
    });
    this.halt();
    this.buffers = new Map();
    this.length = 0;
  }

  private applyRunningLoop(position: number, region: LoopRegion): void {
    if (region.enabled && !isInside(position, region)) {
      this.moveTo(region.start);
      this.reposition(region.start);
      return;
    }
    this.reanchor(position);
    this.sources.forEach((source) => {
      applyLoop(source, region);
    });
    this.reposition(position);
  }

  private moveTo(offset: number): void {
    if (this.playState === 'playing') {
      this.halt();
      this.startAll(offset);
    } else {
      this.pausedAt = offset;
    }
  }

  /** Re-bases both anchor queues at now (loop change) so the new region wraps from the current position. */
  private reanchor(position: number): void {
    const now = this.options.context.currentTime;
    const heard = this.heard.at(now);
    const played = this.played.at(now);
    const playedNow = Math.max(now, played.when);
    const buffer = this.offsetAt(this.played, playedNow);
    this.rebase(this.heard, anchorOf(Math.max(now, heard.when), position, rateOf(heard)));
    this.rebase(this.played, anchorOf(playedNow, buffer, rateOf(played)));
  }

  private rebase(queue: AnchorQueue, first: TransportAnchor): void {
    const later = queue.all.filter((anchor) => anchor.when > first.when);
    queue.reset(first);
    later.forEach((anchor) => {
      queue.push(anchor);
    });
  }

  private startOffset(position: number): number {
    return this.region.enabled && !isInside(position, this.region) ? this.region.start : position;
  }

  private startAll(offset: number, at?: number): void {
    const { rate, latency } = this.current;
    const earliest = this.options.context.currentTime + this.options.startDelaySeconds + latency;
    const when = Math.max(at ?? earliest, earliest);
    this.restartHook();
    this.sourceStart = when - latency;
    this.heard.reset(anchorOf(when, offset, rate));
    this.played.reset(anchorOf(this.sourceStart, offset, rate));
    this.pending = null;
    const start = { when: this.sourceStart, offset };
    const started = [...this.buffers].map(([name, buffer]) => ({
      source: this.startSource(name, buffer, start),
      duration: buffer.duration,
    }));
    this.sources = started.map((s) => s.source);
    this.watchEnd(started.reduce((a, b) => (b.duration > a.duration ? b : a)).source);
  }

  private offsetAt(queue: AnchorQueue, time: number): number {
    return Math.min(wrapPosition(queue.positionAt(time), this.region), this.length);
  }

  private startSource(name: ChannelId, buffer: AudioBuffer, start: { when: number; offset: number }): AudioBufferSourceNode {
    const source = this.options.context.createBufferSource();
    source.buffer = buffer;
    applyLoop(source, this.region);
    this.applyRate(source, start.when);
    source.connect(this.options.output(name));
    source.start(start.when, start.offset);
    return source;
  }

  /** Rate in effect at the source's start, plus every later automation step of the running sources. */
  private applyRate(source: AudioBufferSourceNode, at: number): void {
    source.playbackRate.value = rateOf(this.played.at(at));
    this.played.all
      .filter((anchor) => anchor.when > at)
      .forEach((anchor) => source.playbackRate.setValueAtTime(rateOf(anchor), anchor.when));
  }

  private dropPending(): void {
    const pending = this.pending;
    if (pending === null || pending.at <= this.options.context.currentTime) {
      return;
    }
    this.heard.remove(pending.heard);
    this.played.remove(pending.played);
    this.sources.forEach((source) => source.playbackRate.cancelScheduledValues(pending.at));
  }

  private prune(): void {
    const before = this.options.context.currentTime - ANCHOR_HISTORY_SECONDS;
    this.heard.prune(before);
    this.played.prune(before);
  }

  private watchEnd(longest: AudioBufferSourceNode): void {
    longest.onended = () => {
      if (this.sources.includes(longest)) {
        this.stop();
      }
    };
  }

  private halt(): void {
    for (const source of this.sources) {
      source.onended = null;
      source.stop();
      source.disconnect();
    }
    this.sources = [];
  }

  private setState(state: PlaybackState): void {
    if (state !== this.playState) {
      this.playState = state;
      this.listeners.forEach((l) => {
        l.onStateChanged(state);
      });
    }
  }

  private reposition(position: number): void {
    this.listeners.forEach((l) => {
      l.onReposition(position);
    });
  }
}
