/**
 * Bridges transport events to the Blazor `[JSInvokable]` callbacks.
 *
 * @packageDocumentation
 */
import type { TransportListener } from './transport';
import type { DotNetCallbacks, DotNetObjectReference, PlaybackState } from './types';

/** Interval of position reports while playing (10 Hz). UI feedback only, never audible timing. */
export const POSITION_REPORT_INTERVAL_MS = 100;

/**
 * Sends `OnPlaybackStateChanged` on every state change, `OnPositionChanged` after every
 * reposition and at most every {@link POSITION_REPORT_INTERVAL_MS} ms while playing.
 */
export class DotNetNotifier implements TransportListener {
  private timer: ReturnType<typeof setInterval> | undefined;

  /**
   * @param ref - .NET object implementing the callbacks.
   * @param position - Reads the current transport position.
   */
  public constructor(
    private readonly ref: DotNetObjectReference,
    private readonly position: () => number,
  ) {}

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    this.invoke('OnPlaybackStateChanged', state);
    if (state === 'playing') {
      this.startReports();
    } else {
      this.dispose();
    }
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(position: number): void {
    this.invoke('OnPositionChanged', position);
  }

  /** Stops the periodic position reports. */
  public dispose(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }

  private startReports(): void {
    this.dispose();
    this.timer = setInterval(() => {
      this.invoke('OnPositionChanged', this.position());
    }, POSITION_REPORT_INTERVAL_MS);
  }

  private invoke(method: DotNetCallbacks, value: unknown): void {
    // A disconnected circuit rejects; the engine must keep working regardless.
    this.ref.invokeMethodAsync(method, value).catch(() => undefined);
  }
}
