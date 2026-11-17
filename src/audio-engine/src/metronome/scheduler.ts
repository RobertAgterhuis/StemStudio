/**
 * Look-ahead metronome scheduler. A timer only wakes it; every click is started with
 * `source.start(exactContextTime)`, computed from `AudioContext.currentTime`, the transport
 * anchor and the beat grid (never from the timer).
 *
 * @packageDocumentation
 */
import type { TransportClock, TransportListener } from '../transport';
import type { BeatGrid, MetronomeSettings, PlaybackState } from '../types';
import type { ClickOutput } from './clicks';
import { SILENT_GRID, createClickGrid, type ClickGrid } from './grid';
import { segmentsFor, type Segment } from './timeline';

/** Construction parameters of {@link MetronomeScheduler}. */
export interface SchedulerOptions {
  /** The engine's AudioContext (the clock). */
  readonly context: BaseAudioContext;
  /** Transport timing view. */
  readonly clock: TransportClock;
  /** Click output. */
  readonly output: ClickOutput;
  /** Look-ahead window (s). */
  readonly aheadSeconds: number;
  /** Wake-up interval (ms); wakes the scheduler only. */
  readonly intervalMs: number;
  /** Called on every wake-up while playing, metronome on or off (loop-pass observation, contract 2.4.0). */
  readonly onTick?: () => void;
}

interface Scheduled {
  readonly node: AudioScheduledSourceNode;
  readonly time: number;
}

/** Keeps the clicks of the next `aheadSeconds` scheduled while the transport plays. */
export class MetronomeScheduler implements TransportListener {
  private grid: ClickGrid = SILENT_GRID;
  private enabled = false;
  private timer: ReturnType<typeof setInterval> | undefined;
  private scheduledUntil = 0;
  private scheduled: Scheduled[] = [];

  /** @param options - Clock, transport, output and timing. */
  public constructor(private readonly options: SchedulerOptions) {}

  /**
   * Applies settings and the beat grid; volume changes immediately, clicks are rescheduled.
   *
   * @param settings - Validated metronome settings.
   * @param beatGrid - Beat grid of the loaded track, or null.
   */
  public configure(settings: MetronomeSettings, beatGrid: BeatGrid | null): void {
    this.options.output.setVolume(settings.volume);
    this.grid = createClickGrid(beatGrid, settings);
    this.enabled = settings.enabled;
    this.reschedule();
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    this.stopTimer();
    if (state === 'playing') {
      this.timer = setInterval(this.wake, this.options.intervalMs);
    }
    this.reschedule();
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    this.reschedule();
  }

  /** {@inheritDoc TransportListener.onTimingChanged} */
  public onTimingChanged(): void {
    this.reschedule();
  }

  /** Stops the wake-up timer and cancels future clicks. */
  public dispose(): void {
    this.stopTimer();
    this.cancelFuture();
  }

  private readonly wake = (): void => {
    this.options.onTick?.();
    this.tick();
  };

  private readonly tick = (): void => {
    if (!this.enabled) {
      return;
    }
    const now = this.options.context.currentTime;
    const window = { from: Math.max(now, this.scheduledUntil), to: now + this.options.aheadSeconds };
    segmentsFor(this.options.clock, window).forEach((segment) => {
      this.scheduleSegment(segment);
    });
    this.scheduledUntil = window.to;
    this.scheduled = this.scheduled.filter((s) => s.time >= now - 1);
  };

  private scheduleSegment(segment: Segment): void {
    for (const click of this.grid.between(segment.trackFrom, segment.trackTo)) {
      const time = segment.contextStart + (click.time - segment.trackFrom) / segment.rate;
      this.scheduled.push({ node: this.options.output.play(time, click.accent), time });
    }
  }

  private reschedule(): void {
    this.cancelFuture();
    this.scheduledUntil = 0;
    this.tick();
  }

  private cancelFuture(): void {
    const now = this.options.context.currentTime;
    const [future, past] = partition(this.scheduled, (s) => s.time >= now);
    future.forEach((s) => {
      s.node.stop();
    });
    this.scheduled = past;
  }

  private stopTimer(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }
}

function partition<T>(items: readonly T[], predicate: (item: T) => boolean): [T[], T[]] {
  return [items.filter(predicate), items.filter((item) => !predicate(item))];
}
