/**
 * One `requestAnimationFrame` loop per engine, shared by the meters and the karaoke banner
 * (contract 2.1.0). It runs only while it has work and the document is visible; drawing never
 * uses `setTimeout`/`setInterval`.
 *
 * @packageDocumentation
 */

/** Animation-frame scheduling, injectable for tests. */
export interface FrameScheduler {
  /** Schedules `callback` for the next frame; returns a handle. */
  request(callback: FrameRequestCallback): number;
  /** Cancels a scheduled frame. */
  cancel(handle: number): void;
}

/** The browser's `requestAnimationFrame` / `cancelAnimationFrame`. */
export const browserFrames: FrameScheduler = {
  request(callback) {
    return requestAnimationFrame(callback);
  },
  cancel(handle) {
    cancelAnimationFrame(handle);
  },
};

/** Work done on an animation frame; receives the rAF timestamp (ms). */
export type FrameCallback = (timestamp: number) => void;

/** Construction parameters of {@link FrameLoop}. */
export interface FrameLoopOptions {
  /** Frame scheduler; defaults to {@link browserFrames}. */
  readonly frames?: FrameScheduler;
  /** Document whose `visibilityState` pauses the loop; defaults to `globalThis.document`. */
  readonly document?: Document;
}

/** Shared animation-frame loop: continuous subscribers plus one-shot frame requests. */
export class FrameLoop {
  private readonly frames: FrameScheduler;
  private readonly doc: Document;
  private readonly continuous = new Set<FrameCallback>();
  private pending = new Set<FrameCallback>();
  private handle: number | undefined;

  /** @param options - Frame scheduler and document. */
  public constructor(options: FrameLoopOptions = {}) {
    this.frames = options.frames ?? browserFrames;
    this.doc = options.document ?? globalThis.document;
    this.doc.addEventListener('visibilitychange', this.onVisibility);
  }

  /** Whether an animation frame is currently scheduled. */
  public get running(): boolean {
    return this.handle !== undefined;
  }

  /** @param callback - Called on every frame until {@link FrameLoop.remove}; starts the loop. */
  public add(callback: FrameCallback): void {
    this.continuous.add(callback);
    this.schedule();
  }

  /** @param callback - Continuous subscriber to drop; the loop stops when no work is left. */
  public remove(callback: FrameCallback): void {
    this.continuous.delete(callback);
    if (this.continuous.size === 0 && this.pending.size === 0) {
      this.cancel();
    }
  }

  /** @param callback - Called on the next frame only (deduplicated with continuous subscribers). */
  public once(callback: FrameCallback): void {
    this.pending.add(callback);
    this.schedule();
  }

  /** Cancels the frame, forgets all work and stops following the document visibility. */
  public dispose(): void {
    this.doc.removeEventListener('visibilitychange', this.onVisibility);
    this.continuous.clear();
    this.pending.clear();
    this.cancel();
  }

  private readonly frame = (timestamp: number): void => {
    this.handle = undefined;
    const due = new Set([...this.continuous, ...this.pending]);
    this.pending = new Set();
    due.forEach((callback) => {
      callback(timestamp);
    });
    this.schedule();
  };

  private readonly onVisibility = (): void => {
    if (this.doc.visibilityState === 'visible') {
      this.schedule();
    } else {
      this.cancel();
    }
  };

  private schedule(): void {
    const hasWork = this.continuous.size > 0 || this.pending.size > 0;
    if (hasWork && this.handle === undefined && this.doc.visibilityState === 'visible') {
      this.handle = this.frames.request(this.frame);
    }
  }

  private cancel(): void {
    if (this.handle !== undefined) {
      this.frames.cancel(this.handle);
      this.handle = undefined;
    }
  }
}

/**
 * A drawing that follows the transport on the shared loop (karaoke banner, score follower): every frame while
 * playing, plus one frame after each event.
 */
export class FrameSubscription {
  /**
   * @param loop - The shared frame loop.
   * @param draw - The drawing.
   */
  public constructor(
    private readonly loop: FrameLoop,
    private readonly draw: FrameCallback,
  ) {}

  /** @param playing - Draw every frame (true) or stop; one frame is drawn either way. */
  public follow(playing: boolean): void {
    if (playing) {
      this.loop.add(this.draw);
    } else {
      this.loop.remove(this.draw);
    }
    this.loop.once(this.draw);
  }

  /** Draws one frame. */
  public redraw(): void {
    this.loop.once(this.draw);
  }

  /** Stops drawing every frame. */
  public stop(): void {
    this.loop.remove(this.draw);
  }
}
