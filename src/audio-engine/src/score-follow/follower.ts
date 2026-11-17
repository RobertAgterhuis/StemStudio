/**
 * Score follower (contract 2.4.0 "Score follower", ADR-023 D3): highlights the bar of the heard position on the
 * engine's shared frame loop - continuously while playing, plus one frame after attach, seek, pause, stop, loop
 * change, timing change and option change. The clock is the karaoke clock without an offset (A-012-1), so the
 * highlight is right at every speed.
 *
 * @packageDocumentation
 */
import { FrameSubscription, type FrameLoop } from '../frame-loop';
import type { TransportListener } from '../transport';
import type { PlaybackState, ScoreFollowOptions, ScoreViewMap } from '../types';
import { BarCursor } from './cursor';
import { ScoreDom } from './dom';
import { AutoScroll, boxVisible, type VisibilityCheck } from './scroll';
import { resolveFollowOptions, validateScoreView } from './validate';

/** Construction parameters of {@link ScoreFollower}. */
export interface ScoreFollowerDeps {
  /** The engine's shared frame loop. */
  readonly loop: FrameLoop;
  /** Heard track position (s): the transport at the heard output time. */
  readonly position: () => number;
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** Wall clock (ms) for the scroll pause; default `performance.now()`. */
  readonly now?: () => number;
  /** Document holding the host; default `globalThis.document`. */
  readonly document?: Document;
  /** Reduced-motion visibility check; default the bounding boxes. */
  readonly isVisible?: VisibilityCheck;
}

/** Draw counters (SC-004 diagnostics; the tests check that frames without a bar change write no styles). */
export interface FollowStats {
  /** Frames drawn since attach. */
  readonly frames: number;
  /** Bar changes (highlight moves) since attach. */
  readonly barChanges: number;
}

/** One attached follower. */
interface Attached {
  readonly view: ScoreViewMap;
  readonly cursor: BarCursor;
  readonly dom: ScoreDom;
  readonly scroll: AutoScroll;
  current: number;
  frames: number;
  barChanges: number;
}

/** The engine's score follower; listens to the transport. */
export class ScoreFollower implements TransportListener {
  private attached: Attached | null = null;
  private readonly frames: FrameSubscription;

  /** @param deps - Frame loop, heard clock, transport state, wall clock and document. */
  public constructor(private readonly deps: ScoreFollowerDeps) {
    this.frames = new FrameSubscription(deps.loop, this.draw);
  }

  /** Draw counters of the attached follower (zeros when none is attached). */
  public get stats(): FollowStats {
    return { frames: this.attached?.frames ?? 0, barChanges: this.attached?.barChanges ?? 0 };
  }

  /**
   * Validates everything first, then replaces an attached follower.
   *
   * @param elementId - Host element id.
   * @param view - Score view.
   * @param options - Follow options.
   * @throws Error `Score host not found: <id>`, `Invalid score view: <reason>`, `Invalid score follow options: ...`.
   */
  public attach(elementId: string, view: ScoreViewMap, options: ScoreFollowOptions): void {
    const host = (this.deps.document ?? globalThis.document).getElementById(elementId);
    if (host === null) {
      throw new Error(`Score host not found: ${elementId}`);
    }
    validateScoreView(view);
    const settings = resolveFollowOptions(options);
    this.detach();
    const now = this.deps.now ?? ((): number => performance.now());
    const scroll = new AutoScroll({ host, now, isVisible: this.deps.isVisible ?? boxVisible }, settings);
    this.attached = { view, cursor: new BarCursor(view.bars), dom: new ScoreDom(host, view), scroll, current: -1, frames: 0, barChanges: 0 };
    this.frames.follow(this.deps.playing());
  }

  /** @param options - New options (validated); drawn on the next frame. No-op without an attached follower. */
  public setOptions(options: ScoreFollowOptions): void {
    const settings = resolveFollowOptions(options);
    this.attached?.scroll.configure(settings);
    this.redraw();
  }

  /** Draws one frame when attached (seek, loop or timing change). */
  public redraw(): void {
    if (this.attached !== null) {
      this.frames.redraw();
    }
  }

  /** Removes the highlights, host attributes and listeners; no-op when none is attached. */
  public detach(): void {
    this.frames.stop();
    this.attached?.dom.dispose();
    this.attached?.scroll.dispose();
    this.attached = null;
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    if (this.attached !== null) {
      this.frames.follow(state === 'playing');
    }
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    this.redraw();
  }

  /** {@inheritDoc TransportListener.onTimingChanged} */
  public onTimingChanged(): void {
    this.redraw();
  }

  private readonly draw = (): void => {
    const attached = this.attached;
    if (attached === null) {
      return;
    }
    const t = this.deps.position();
    const index = attached.cursor.locate(t);
    attached.frames += 1;
    attached.dom.setPosition(Math.round(t * 1000));
    if (index !== attached.current) {
      attached.current = index;
      attached.barChanges += 1;
      attached.dom.show(attached.view.bars[index] ?? null);
    }
    const bar = attached.view.bars[index];
    if (bar !== undefined) {
      attached.scroll.update(bar, attached.dom.highlightOf(bar.page));
    }
  };
}
