/**
 * Sample-locked transport: every stem (standard and decoded detail stems) gets a fresh source
 * started with the same `when` and the same offset; the position is derived from
 * `AudioContext.currentTime` only.
 *
 * @packageDocumentation
 */
import { NO_LOOP, applyLoop, isInside, wrapPosition } from './loop';
import { trackDuration, type ChannelBuffers } from './stem-loader';
import type { ChannelId, LoopRegion, PlaybackState } from './types';

/** Shared start of the running sources: context time `when` plays track time `offset`. */
export interface TransportAnchor {
  /** Context time (s) at which `offset` is heard. */
  readonly when: number;
  /** Track time (s) heard at `when`. */
  readonly offset: number;
}

/** Observer of transport events (callbacks to .NET, metronome). */
export interface TransportListener {
  /** Called on every state change. */
  onStateChanged(state: PlaybackState): void;
  /** Called after a seek, pause, stop, end of track or loop change with the new position. */
  onReposition(position: number): void;
}

/** Read-only timing view of the transport used by the metronome. */
export interface TransportClock {
  /** Anchor of the running sources, or null when not playing. */
  anchor(): TransportAnchor | null;
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

/** Play/pause/stop/seek/loop over the loaded stems. */
export class Transport implements TransportClock {
  private buffers: ChannelBuffers = new Map();
  private sources: AudioBufferSourceNode[] = [];
  private current: TransportAnchor = { when: 0, offset: 0 };
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

  /** @param listener - Receives state and reposition events. */
  public addListener(listener: TransportListener): void {
    this.listeners.push(listener);
  }

  /** {@inheritDoc TransportClock.anchor} */
  public anchor(): TransportAnchor | null {
    return this.playState === 'playing' ? this.current : null;
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

  /** Resumes the context and starts all stems at the stored position. */
  public async play(): Promise<void> {
    await this.options.context.resume();
    if (this.playState === 'playing' || this.buffers.size === 0) {
      return;
    }
    this.startAll(this.startOffset(this.pausedAt));
    this.setState('playing');
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

  /** @returns Current position (s), derived from `currentTime` and wrapped into an active loop. */
  public position(): number {
    return this.playState === 'playing' ? this.offsetAt(this.options.context.currentTime) : this.pausedAt;
  }

  /**
   * Adds decoded buffers (a detail group) to the track; while playing they are started at once
   * on the shared anchor with `start(at, offsetAt(at))` and the loop properties.
   *
   * @param buffers - Buffers keyed by channel id; the duration is unchanged.
   * @param at - Context time of the start, not before the shared `when` (see {@link Transport.startTimeAfter}).
   */
  public addBuffers(buffers: ChannelBuffers, at: number): void {
    this.buffers = new Map([...this.buffers, ...buffers]);
    if (this.playState !== 'playing') {
      return;
    }
    const anchor = { when: at, offset: this.offsetAt(at) };
    buffers.forEach((buffer, id) => {
      this.sources.push(this.startSource(id, buffer, anchor));
    });
  }

  /**
   * @param delay - Minimum delay from now (s).
   * @returns `currentTime + delay`, or the shared `when` while it is still in the future.
   */
  public startTimeAfter(delay: number): number {
    const soon = this.options.context.currentTime + delay;
    return this.playState === 'playing' ? Math.max(soon, this.current.when) : soon;
  }

  /** @param region - Validated loop region applied to the running sources. */
  public setLoop(region: LoopRegion): void {
    const position = this.position();
    this.region = region;
    if (this.playState === 'playing') {
      this.applyRunningLoop(position, region);
    }
  }

  /** Stops all sources without notifying anyone (engine disposal). */
  public dispose(): void {
    this.listeners.length = 0;
    this.halt();
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

  private reanchor(position: number): void {
    const when = Math.max(this.options.context.currentTime, this.current.when);
    this.current = { when, offset: position };
  }

  private startOffset(position: number): number {
    return this.region.enabled && !isInside(position, this.region) ? this.region.start : position;
  }

  private startAll(offset: number): void {
    const anchor = { when: this.options.context.currentTime + this.options.startDelaySeconds, offset };
    const started = [...this.buffers].map(([name, buffer]) => ({
      source: this.startSource(name, buffer, anchor),
      duration: buffer.duration,
    }));
    this.sources = started.map((s) => s.source);
    this.current = anchor;
    this.watchEnd(started.reduce((a, b) => (b.duration > a.duration ? b : a)).source);
  }

  private offsetAt(time: number): number {
    const { when, offset } = this.current;
    return Math.min(wrapPosition(offset + Math.max(0, time - when), this.region), this.length);
  }

  private startSource(name: ChannelId, buffer: AudioBuffer, anchor: TransportAnchor): AudioBufferSourceNode {
    const source = this.options.context.createBufferSource();
    source.buffer = buffer;
    applyLoop(source, this.region);
    source.connect(this.options.output(name));
    source.start(anchor.when, anchor.offset);
    return source;
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
