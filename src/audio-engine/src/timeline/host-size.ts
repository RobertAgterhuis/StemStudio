/**
 * Size of a view host, measured only outside the frame callback (contract 2.6.0 frame budget): once when the view is
 * built and again in `ResizeObserver` callbacks (which run after layout, so the read is free).
 *
 * @packageDocumentation
 */

/** Measured host size plus resize notifications. */
export class HostSize {
  private readonly observer: ResizeObserver | null;
  private measuredWidth = 0;
  private measuredHeight = 0;

  /**
   * @param host - Host element.
   * @param onResize - Called after a re-measure in a resize callback.
   */
  public constructor(
    private readonly host: HTMLElement,
    private readonly onResize: () => void,
  ) {
    this.measure();
    const Observer = (globalThis as { ResizeObserver?: typeof ResizeObserver }).ResizeObserver;
    this.observer = Observer === undefined ? null : new Observer(this.resized);
    this.observer?.observe(host);
  }

  /** Host width (px). */
  public get width(): number {
    return this.measuredWidth;
  }

  /** Host height (px). */
  public get height(): number {
    return this.measuredHeight;
  }

  /** Stops observing. */
  public dispose(): void {
    this.observer?.disconnect();
  }

  private measure(): void {
    const rect = this.host.getBoundingClientRect();
    this.measuredWidth = rect.width;
    this.measuredHeight = rect.height;
  }

  private readonly resized = (): void => {
    this.measure();
    this.onResize();
  };
}
