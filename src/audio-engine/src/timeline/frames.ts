/**
 * Drawing of one attached view on the shared frame loop (contract 2.6.0): every frame while playing, plus one frame
 * after every attach, seek, pause, stop, loop change, timing change and every explicit {@link TimelineFrames.redraw}
 * (offset, capo, transpose, options, resize).
 *
 * @packageDocumentation
 */
import { FrameSubscription, type FrameCallback, type FrameLoop } from '../frame-loop';
import type { TransportListener } from '../transport';
import type { PlaybackState } from '../types';

/** Construction parameters of {@link TimelineFrames}. */
export interface TimelineFramesOptions {
  /** The engine's shared frame loop. */
  readonly loop: FrameLoop;
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** The view's frame. */
  readonly draw: FrameCallback;
}

/** Transport listener that schedules a view's frames while the view is active. */
export class TimelineFrames implements TransportListener {
  private readonly subscription: FrameSubscription;
  private isActive = false;

  /** @param options - Loop, transport state and drawing. */
  public constructor(private readonly options: TimelineFramesOptions) {
    this.subscription = new FrameSubscription(options.loop, options.draw);
  }

  /** Whether a view is attached (frames are scheduled). */
  public get active(): boolean {
    return this.isActive;
  }

  /** Activates the drawing: one frame now, every frame while playing. */
  public start(): void {
    this.isActive = true;
    this.subscription.follow(this.options.playing());
  }

  /** Stops drawing; later triggers are ignored until {@link TimelineFrames.start}. */
  public stop(): void {
    this.isActive = false;
    this.subscription.stop();
  }

  /** Draws one frame when active. */
  public redraw(): void {
    if (this.isActive) {
      this.subscription.redraw();
    }
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    if (this.isActive) {
      this.subscription.follow(state === 'playing');
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
}
