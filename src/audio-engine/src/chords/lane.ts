/**
 * Chord lane controller (contract 2.6.0 `attachChordLane` / `detachChordLane`): validates the chart, builds a
 * {@link LaneView} in the host and draws it on the shared frame loop against the heard position.
 *
 * @packageDocumentation
 */
import type { FrameLoop } from '../frame-loop';
import { SecondScale, scaleFor, type TimeScale } from '../timeline/bars';
import { TimelineFrames } from '../timeline/frames';
import type { TransportListener } from '../transport';
import type { BeatGrid, ChordChartData, ChordLaneOptions, PlaybackState } from '../types';
import { DomWriter } from '../views/dom-writer';
import { prepareChart } from './chart';
import { SpelledChart, type ChordDisplayState } from './display';
import { LaneView, type LaneWindow } from './lane-view';

/** Default bars before the current one. */
export const DEFAULT_BARS_BEHIND = 1;
/** Default bars after the current one. */
export const DEFAULT_BARS_AHEAD = 6;
/** Seconds shown before / after the position without a beat grid. */
const SECONDS_WINDOW: LaneWindow = { behind: 4, ahead: 12 };

/** Construction parameters of {@link ChordLane}. */
export interface ChordLaneDeps {
  /** The engine's shared frame loop. */
  readonly loop: FrameLoop;
  /** Heard position in ms (the timeline clock). */
  readonly clock: { positionMs(): number };
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** Beat grid of the loaded track. */
  readonly grid: () => BeatGrid | null;
  /** Shared chord display state. */
  readonly display: ChordDisplayState;
  /** Document holding the host; default `globalThis.document`. */
  readonly document?: Document;
}

/** Draw counters (frame budget diagnostics). */
export interface ViewStats {
  /** Frames drawn. */
  readonly frames: number;
  /** Counted DOM writes. */
  readonly writes: number;
}

function clamp(value: number | undefined, fallback: number, max: number, min: number): number {
  return Math.min(Math.max(Math.round(value ?? fallback), min), max);
}

/**
 * @param scale - Time scale of the track.
 * @param options - Lane options.
 * @returns The window: bars (clamped to 0..8 behind, 1..16 ahead; the current bar counts as ahead) or 4 s / 12 s.
 */
export function laneWindow(scale: TimeScale, options: ChordLaneOptions): LaneWindow {
  if (scale instanceof SecondScale) {
    return SECONDS_WINDOW;
  }
  return { behind: clamp(options.barsBehind, DEFAULT_BARS_BEHIND, 8, 0), ahead: clamp(options.barsAhead, DEFAULT_BARS_AHEAD, 16, 1) + 1 };
}

/** The engine's chord lane; listens to the transport. */
export class ChordLane implements TransportListener {
  private readonly frames: TimelineFrames;
  private readonly writer = new DomWriter();
  private view: LaneView | null = null;
  private drawn = 0;

  /** @param deps - Loop, clock, transport state, grid, display state and document. */
  public constructor(private readonly deps: ChordLaneDeps) {
    this.frames = new TimelineFrames({ loop: deps.loop, playing: deps.playing, draw: this.draw });
    deps.display.onChange(() => {
      this.redraw();
    });
  }

  /** Draw counters. */
  public get stats(): ViewStats {
    return { frames: this.drawn, writes: this.writer.writes };
  }

  /**
   * Validates first, then replaces an attached lane.
   *
   * @param elementId - Host element id.
   * @param chart - Chord chart.
   * @param options - Window and motion options.
   * @throws Error `Chord lane host not found: <id>` or `Invalid chord chart: <reason>`.
   */
  public attach(elementId: string, chart: ChordChartData, options: ChordLaneOptions = {}): void {
    const prepared = prepareChart(chart);
    const host = (this.deps.document ?? globalThis.document).getElementById(elementId);
    if (host === null) {
      throw new Error(`Chord lane host not found: ${elementId}`);
    }
    this.detach();
    const scale = scaleFor(this.deps.grid());
    const spelled = new SpelledChart(prepared, this.deps.display);
    const requestFrame = (): void => {
      this.redraw();
    };
    const window = laneWindow(scale, options);
    this.view = new LaneView({ host, chart: spelled, scale, window, reducedMotion: options.reducedMotion, writer: this.writer, requestFrame });
    this.frames.start();
  }

  /** Removes the lane; no-op when none is attached. */
  public detach(): void {
    this.frames.stop();
    this.view?.dispose();
    this.view = null;
  }

  /** Draws one frame when attached. */
  public redraw(): void {
    this.frames.redraw();
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    this.frames.onStateChanged(state);
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    this.frames.onReposition();
  }

  /** {@inheritDoc TransportListener.onTimingChanged} */
  public onTimingChanged(): void {
    this.frames.onTimingChanged();
  }

  private readonly draw = (): void => {
    if (this.view !== null) {
      this.drawn += 1;
      this.view.draw(this.deps.clock.positionMs());
    }
  };
}
