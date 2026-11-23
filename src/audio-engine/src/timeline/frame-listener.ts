/**
 * Transport listener that forwards the transport events to a {@link TimelineFrames}; the base of the view controllers
 * and the live singing session.
 *
 * @packageDocumentation
 */
import type { TransportListener } from '../transport';
import type { PlaybackState } from '../types';
import type { TimelineFrames } from './frames';

/** Forwards state, reposition and timing changes to the frames of a view. */
export abstract class FrameListener implements TransportListener {
  /** The frames the events go to. */
  protected abstract readonly frames: TimelineFrames;

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
}
