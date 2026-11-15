import type { FrameScheduler } from '../../src/frame-loop';

/** Manually stepped `requestAnimationFrame`; records how many frames were requested. */
export class FakeFrames implements FrameScheduler {
  public requested = 0;
  private readonly pending = new Map<number, FrameRequestCallback>();
  private next = 1;

  public request(callback: FrameRequestCallback): number {
    const id = this.next++;
    this.requested += 1;
    this.pending.set(id, callback);
    return id;
  }

  public cancel(handle: number): void {
    this.pending.delete(handle);
  }

  /** Number of frames currently scheduled. */
  public get size(): number {
    return this.pending.size;
  }

  /** Runs every scheduled callback with `timestamp`. */
  public step(timestamp: number): void {
    const callbacks = [...this.pending.values()];
    this.pending.clear();
    callbacks.forEach((cb) => {
      cb(timestamp);
    });
  }
}

/** Sets `document.visibilityState` and fires `visibilitychange`. */
export function setVisibility(doc: Document, state: DocumentVisibilityState): void {
  Object.defineProperty(doc, 'visibilityState', { configurable: true, get: () => state });
  doc.dispatchEvent(new Event('visibilitychange'));
}
