/**
 * Chord lane controller (contract 2.6.0 `attachChordLane` / `detachChordLane`): validates the chart, builds a
 * {@link LaneView} in the host and draws it on the shared frame loop against the heard position.
 *
 * @packageDocumentation
 */
import { SecondScale, scaleFor, type TimeScale } from '../timeline/bars';
import { ViewController, type ViewControllerDeps } from '../timeline/view-controller';
import type { BeatGrid, ChordChartData, ChordLaneOptions } from '../types';
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
export interface ChordLaneDeps extends ViewControllerDeps {
  /** Beat grid of the loaded track. */
  readonly grid: () => BeatGrid | null;
  /** Shared chord display state. */
  readonly display: ChordDisplayState;
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
export class ChordLane extends ViewController<LaneView> {
  private readonly writer = new DomWriter();

  /** @param deps - Loop, clock, transport state, grid, display state and document. */
  public constructor(private readonly deps: ChordLaneDeps) {
    super(deps);
    deps.display.onChange(() => {
      this.redraw();
    });
  }

  /** Draw counters. */
  public get stats(): ViewStats {
    return { frames: this.framesDrawn, writes: this.writer.writes };
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
    const host = this.host(elementId, 'Chord lane');
    this.detach();
    const scale = scaleFor(this.deps.grid());
    const spelled = new SpelledChart(prepared, this.deps.display);
    const requestFrame = (): void => {
      this.redraw();
    };
    const window = laneWindow(scale, options);
    this.show(new LaneView({ host, chart: spelled, scale, window, reducedMotion: options.reducedMotion, writer: this.writer, requestFrame }));
  }
}
