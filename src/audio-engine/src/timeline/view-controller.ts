/**
 * Shared controller of an attachable engine view (chord lane, hit lanes, pitch view; contract 2.6.0): holds the
 * attached drawing, schedules its frames through {@link TimelineFrames} and draws it against the heard position.
 *
 * @packageDocumentation
 */
import type { FrameLoop } from '../frame-loop';
import { FrameListener } from './frame-listener';
import { TimelineFrames } from './frames';

/** The drawing side of one attached view. */
export interface AttachedView {
  /** Draws the view for heard track time `t` (ms). */
  draw(t: number): void;
  /** Removes the view's DOM and listeners. */
  dispose(): void;
}

/** Construction parameters shared by the view controllers. */
export interface ViewControllerDeps {
  /** The engine's shared frame loop. */
  readonly loop: FrameLoop;
  /** Heard position in ms (the timeline clock). */
  readonly clock: { positionMs(): number };
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** Document holding the hosts; default `globalThis.document`. */
  readonly document?: Document;
}

/** Base of the view controllers; listens to the transport. */
export abstract class ViewController<V extends AttachedView> extends FrameListener {
  /** The attached view, or null. */
  protected view: V | null = null;
  protected readonly frames: TimelineFrames;
  private drawn = 0;

  /** @param base - Loop, clock, transport state and document. */
  protected constructor(private readonly base: ViewControllerDeps) {
    super();
    this.frames = new TimelineFrames({ loop: base.loop, playing: base.playing, draw: this.draw });
  }

  /** Frames drawn so far. */
  protected get framesDrawn(): number {
    return this.drawn;
  }

  /** Removes the view; no-op when none is attached. */
  public detach(): void {
    this.frames.stop();
    this.view?.dispose();
    this.view = null;
  }

  /** Draws one frame when attached. */
  public redraw(): void {
    this.frames.redraw();
  }

  /**
   * @param elementId - Host element id.
   * @param label - Start of the not-found message (e.g. `Chord lane`).
   * @returns The host element.
   * @throws Error `<label> host not found: <id>`.
   */
  protected host(elementId: string, label: string): HTMLElement {
    const host = (this.base.document ?? globalThis.document).getElementById(elementId);
    if (host === null) {
      throw new Error(`${label} host not found: ${elementId}`);
    }
    return host;
  }

  /** Replaces the attached view and starts drawing it. */
  protected show(view: V): void {
    this.view = view;
    this.frames.start();
  }

  private readonly draw = (): void => {
    if (this.view !== null) {
      this.drawn += 1;
      this.view.draw(this.base.clock.positionMs());
    }
  };
}
