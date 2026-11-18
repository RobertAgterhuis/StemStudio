/**
 * Drum hit lanes controller (contract 2.6.0 `attachHitLanes`, `setHitLaneOptions`, `detachHitLanes`): validates the
 * hits, builds a {@link HitView} in the host and draws it on the shared frame loop against the heard position.
 *
 * @packageDocumentation
 */
import type { FrameLoop } from '../frame-loop';
import { scaleFor } from '../timeline/bars';
import { TimelineFrames } from '../timeline/frames';
import type { TransportListener } from '../transport';
import type { BeatGrid, DrumHitEvent, HitLaneOptions, PlaybackState } from '../types';
import { HitView } from './hit-view';
import { DEFAULT_HIT_SETTINGS, resolveHitOptions, validateHits, type HitLaneSettings } from './validate';

export { LANE_ORDER } from './validate';

/** Construction parameters of {@link HitLanes}. */
export interface HitLanesDeps {
  /** The engine's shared frame loop. */
  readonly loop: FrameLoop;
  /** Heard position in ms (the timeline clock). */
  readonly clock: { positionMs(): number };
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** Beat grid of the loaded track (reduced-motion pages). */
  readonly grid: () => BeatGrid | null;
  /** Document holding the host; default `globalThis.document`. */
  readonly document?: Document;
}

/** Draw counters (frame budget diagnostics). */
export interface HitLaneStats {
  /** Frames drawn. */
  readonly frames: number;
  /** Canvas drawing calls of the attached view. */
  readonly primitives: number;
}

/** The engine's hit lanes; listens to the transport. */
export class HitLanes implements TransportListener {
  private readonly frames: TimelineFrames;
  private view: HitView | null = null;
  private settings: HitLaneSettings = DEFAULT_HIT_SETTINGS;
  private drawn = 0;

  /** @param deps - Loop, clock, transport state, grid and document. */
  public constructor(private readonly deps: HitLanesDeps) {
    this.frames = new TimelineFrames({ loop: deps.loop, playing: deps.playing, draw: this.draw });
  }

  /** Draw counters. */
  public get stats(): HitLaneStats {
    return { frames: this.drawn, primitives: this.view?.primitives ?? 0 };
  }

  /**
   * Validates first, then replaces an attached view.
   *
   * @param elementId - Host element id.
   * @param hits - Hits in time order.
   * @param options - Look-ahead, pieces and motion.
   * @throws Error `Hit lane host not found: <id>` or `Invalid drum hits: <reason>`.
   */
  public attach(elementId: string, hits: readonly DrumHitEvent[], options: HitLaneOptions = {}): void {
    const checked = validateHits(hits);
    const settings = resolveHitOptions(options, DEFAULT_HIT_SETTINGS);
    const host = (this.deps.document ?? globalThis.document).getElementById(elementId);
    if (host === null) {
      throw new Error(`Hit lane host not found: ${elementId}`);
    }
    this.detach();
    this.settings = settings;
    const requestFrame = (): void => {
      this.frames.redraw();
    };
    this.view = new HitView({ host, hits: checked, scale: scaleFor(this.deps.grid()), settings, requestFrame });
    this.frames.start();
  }

  /**
   * @param options - New look-ahead, pieces or motion; ignored without an attached view.
   * @throws Error `Invalid drum hits: unknown piece <p>` (nothing changes).
   */
  public setOptions(options: HitLaneOptions): void {
    const settings = resolveHitOptions(options, this.settings);
    if (this.view !== null) {
      this.settings = settings;
      this.view.configure(settings);
      this.frames.redraw();
    }
  }

  /** Removes the canvas and the host attributes; no-op when none is attached. */
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
