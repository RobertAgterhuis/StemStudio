/**
 * Drum hit lanes controller (contract 2.6.0 `attachHitLanes`, `setHitLaneOptions`, `detachHitLanes`): validates the
 * hits, builds a {@link HitView} in the host and draws it on the shared frame loop against the heard position.
 *
 * @packageDocumentation
 */
import { scaleFor } from '../timeline/bars';
import { ViewController, type ViewControllerDeps } from '../timeline/view-controller';
import type { BeatGrid, DrumHitEvent, HitLaneOptions } from '../types';
import { HitView } from './hit-view';
import { DEFAULT_HIT_SETTINGS, resolveHitOptions, validateHits, type HitLaneSettings } from './validate';

export { LANE_ORDER } from './validate';

/** Construction parameters of {@link HitLanes}. */
export interface HitLanesDeps extends ViewControllerDeps {
  /** Beat grid of the loaded track (reduced-motion pages). */
  readonly grid: () => BeatGrid | null;
}

/** Draw counters (frame budget diagnostics). */
export interface HitLaneStats {
  /** Frames drawn. */
  readonly frames: number;
  /** Canvas drawing calls of the attached view. */
  readonly primitives: number;
}

/** The engine's hit lanes; listens to the transport. */
export class HitLanes extends ViewController<HitView> {
  private settings: HitLaneSettings = DEFAULT_HIT_SETTINGS;

  /** @param deps - Loop, clock, transport state, grid and document. */
  public constructor(private readonly deps: HitLanesDeps) {
    super(deps);
  }

  /** Draw counters. */
  public get stats(): HitLaneStats {
    return { frames: this.framesDrawn, primitives: this.view?.primitives ?? 0 };
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
    const host = this.host(elementId, 'Hit lane');
    this.detach();
    this.settings = settings;
    const requestFrame = (): void => {
      this.redraw();
    };
    this.show(new HitView({ host, hits: checked, scale: scaleFor(this.deps.grid()), settings, requestFrame }));
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
      this.redraw();
    }
  }
}
