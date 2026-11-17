/**
 * Playback of the loaded take (research R-09, contract 2.5.0): the take buffer covers song time
 * `[origin, origin + duration)` and is scheduled against the transport's heard anchors, segment by segment
 * (`segmentsFor`, the metronome's mapping), so it stays sample-locked at every speed, in every loop pass and across
 * count-in gaps. The take source is never looped: each loop pass gets its own source. Sources start `latency` before
 * the heard time, like the stems, and connect to the `take` channel (fader x gate -> panner -> meter -> master, so the
 * 012 tempo stage processes it like the stems).
 *
 * Events: start/seek/pause/stop re-plan from now; a live rate change or a new explicit pass splices at the newest
 * anchor; a wake-up timer extends the plan of a long native loop. Timers never decide when audio starts.
 *
 * @packageDocumentation
 */
import { segmentsFor, type Segment } from '../metronome/timeline';
import type { Transport, TransportListener } from '../transport';
import type { PlaybackState } from '../types';

/** Delay of a re-plan after a change (s, contract 2.5.0 "re-scheduled at ctx.currentTime + 0.01"). */
export const TAKE_LEAD_SECONDS = 0.01;
/** The plan is extended when less than this is planned ahead (s). */
const PLAN_AHEAD_SECONDS = 2;
/** Wake-up interval of the plan extension (ms). */
const WAKE_MS = 250;

/** The take's place in the song. */
export interface TakeSpan {
  /** Song time of the buffer's first frame (s). */
  readonly origin: number;
  /** Buffer length (s). */
  readonly duration: number;
}

/** One source to start. */
export interface TakeChunk {
  /** Heard context time of its first frame. */
  readonly heardStart: number;
  /** Buffer time of its first frame (s). */
  readonly bufferOffset: number;
  /** Buffer seconds to play. */
  readonly duration: number;
  /** Playback rate. */
  readonly rate: number;
}

/**
 * @param segments - Heard track segments (`segmentsFor`).
 * @param span - Song span of the take.
 * @returns One chunk per segment that overlaps the take.
 */
export function planTake(segments: readonly Segment[], span: TakeSpan): TakeChunk[] {
  return segments.flatMap((segment) => {
    const from = Math.max(segment.trackFrom, span.origin);
    const to = Math.min(segment.trackTo, span.origin + span.duration);
    if (to - from <= 1e-9) {
      return [];
    }
    const heardStart = segment.contextStart + (from - segment.trackFrom) / segment.rate;
    return [{ heardStart, bufferOffset: from - span.origin, duration: to - from, rate: segment.rate }];
  });
}

/** The part of a saved take file that is the take. */
export interface TakeSlice {
  readonly startSample: number;
  readonly lengthSamples: number;
  readonly channels: 1 | 2;
}

/**
 * @param context - Context creating the buffer.
 * @param full - Decoded (padded) take file.
 * @param slice - Span and channels of the take.
 * @returns A new buffer of `slice.channels` channels with `[start, start + length)`; a stored stereo copy of a mono
 *   take is reduced back to channel 0, a mono file is duplicated for a stereo take.
 */
export function sliceTake(context: BaseAudioContext, full: AudioBuffer, slice: TakeSlice): AudioBuffer {
  const out = context.createBuffer(slice.channels, Math.max(1, slice.lengthSamples), full.sampleRate);
  for (let c = 0; c < slice.channels; c++) {
    const source = full.getChannelData(Math.min(c, full.numberOfChannels - 1));
    out.getChannelData(c).set(source.subarray(slice.startSample, slice.startSample + slice.lengthSamples));
  }
  return out;
}

/** A started take source. */
interface Scheduled {
  readonly source: AudioBufferSourceNode;
  readonly heardStart: number;
  readonly heardEnd: number;
}

/** Construction parameters of {@link TakeChannel}. */
export interface TakeChannelOptions {
  readonly context: AudioContext;
  readonly transport: Transport;
  /** Input node of the `take` channel. */
  readonly output: () => AudioNode;
}

/** Schedules the loaded take. */
export class TakeChannel implements TransportListener {
  private buffer: AudioBuffer | null = null;
  private origin = 0;
  private scheduled: Scheduled[] = [];
  private plannedUntil = -Infinity;
  private timer: ReturnType<typeof setInterval> | undefined;

  /** @param options - Context, transport and channel input. */
  public constructor(private readonly options: TakeChannelOptions) {}

  /** Whether a take is loaded. */
  public get loaded(): boolean {
    return this.buffer !== null;
  }

  /**
   * Loads (or replaces) the take and schedules it at once while playing.
   *
   * @param buffer - Take audio.
   * @param startSample - Song sample of its first frame (adjustment included).
   */
  public load(buffer: AudioBuffer, startSample: number): void {
    this.buffer = buffer;
    this.origin = startSample / buffer.sampleRate;
    this.restart();
  }

  /** @param startSample - New song sample of the first frame (timing adjustment); re-planned at once. */
  public moveTo(startSample: number): void {
    if (this.buffer !== null) {
      this.origin = startSample / this.buffer.sampleRate;
      this.replan(this.soon());
    }
  }

  /** Removes the take (its sources stop now). */
  public unload(): void {
    this.buffer = null;
    this.restart();
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    clearInterval(this.timer);
    this.timer = state === 'playing' ? setInterval(this.extend, WAKE_MS) : undefined;
    this.restart();
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    this.restart();
  }

  /** {@inheritDoc TransportListener.onTimingChanged} */
  public onTimingChanged(): void {
    const newest = this.options.transport.anchors().at(-1)?.when ?? -Infinity;
    this.replan(Math.max(this.soon(), newest));
  }

  /** Stops every take source and the wake-up timer. */
  public dispose(): void {
    clearInterval(this.timer);
    this.buffer = null;
    this.cut(-Infinity);
  }

  private soon(): number {
    return this.options.context.currentTime + TAKE_LEAD_SECONDS + this.options.transport.timing.latency;
  }

  /** Stops every take source now and plans the take from now + {@link TAKE_LEAD_SECONDS}. */
  private restart(): void {
    this.cut(-Infinity);
    this.plan(this.soon());
  }

  /** Cuts every take source at heard time `from` and plans the take from there. */
  private replan(from: number): void {
    this.cut(from);
    this.plan(from);
  }

  private readonly extend = (): void => {
    const now = this.options.context.currentTime;
    if (Number.isFinite(this.plannedUntil) && this.plannedUntil - now < PLAN_AHEAD_SECONDS) {
      this.plan(this.plannedUntil);
    }
  };

  /** Stops sources that start at or after `from`, and ends the running ones at `from`. */
  private cut(from: number): void {
    const latency = this.options.transport.timing.latency;
    const now = this.options.context.currentTime;
    this.scheduled = this.scheduled.filter((item) => {
      if (item.heardStart >= from || from <= now) {
        item.source.stop();
        return false;
      }
      if (item.heardEnd > from) {
        item.source.stop(from - latency);
      }
      return item.heardEnd > now;
    });
    this.plannedUntil = -Infinity;
  }

  private plan(from: number): void {
    const { transport } = this.options;
    const buffer = this.buffer;
    if (buffer === null || transport.state !== 'playing') {
      return;
    }
    const segments = segmentsFor(transport, { from, to: Infinity });
    planTake(segments, { origin: this.origin, duration: buffer.duration }).forEach((chunk) => {
      this.start(buffer, chunk);
    });
    const last = segments.at(-1);
    this.plannedUntil = last === undefined ? -Infinity : last.contextStart + (last.trackTo - last.trackFrom) / last.rate;
  }

  private start(buffer: AudioBuffer, chunk: TakeChunk): void {
    const { context, transport, output } = this.options;
    const source = context.createBufferSource();
    source.buffer = buffer;
    source.playbackRate.value = chunk.rate;
    source.connect(output());
    source.start(chunk.heardStart - transport.timing.latency, chunk.bufferOffset, chunk.duration);
    this.scheduled.push({ source, heardStart: chunk.heardStart, heardEnd: chunk.heardStart + chunk.duration / chunk.rate });
  }
}
