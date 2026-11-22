/**
 * Word geometry of the banner, read only outside the frame callback: after the DOM is built
 * and in `ResizeObserver` callbacks (which run after layout, so the reads are free).
 *
 * @packageDocumentation
 */
import type { Point } from './ball';

/** A slot element and the line it shows (-1 or past the end when empty). */
export interface SlotView {
  /** Line index shown in the slot. */
  readonly line: number;
  /** `.karaoke-line` element. */
  readonly element: HTMLElement;
}

/** Measured word positions of the lines in the slots. */
export interface Geometry {
  /** Height of the current slot (px). */
  readonly lineHeight: number;
  /** Word points (centre x, top y relative to the host) per line index. */
  readonly lines: ReadonlyMap<number, readonly Point[]>;
}

/** Geometry before anything was measured. */
export const NO_GEOMETRY: Geometry = { lineHeight: 0, lines: new Map() };

/**
 * Reads the word rectangles of the slots relative to the host.
 *
 * @param host - Banner host.
 * @param slots - Slots in previous/current/next order.
 * @returns The geometry.
 */
export function measureSlots(host: HTMLElement, slots: readonly [SlotView, SlotView, SlotView]): Geometry {
  const origin = host.getBoundingClientRect();
  const lines = new Map<number, readonly Point[]>();
  slots.forEach((slot) => {
    const points = [...slot.element.querySelectorAll(':scope > .karaoke-word')].map((word) => {
      const rect = word.getBoundingClientRect();
      return { x: rect.left + rect.width / 2 - origin.left, y: rect.top - origin.top };
    });
    lines.set(slot.line, points);
  });
  return { lineHeight: slots[1].element.getBoundingClientRect().height, lines };
}

/** Host attributes whose change means a new text size (re-measure). */
export const SIZE_ATTRIBUTES: readonly string[] = ['class', 'data-size'];

/**
 * Observes the host and the current word elements with a `ResizeObserver` (when available)
 * and the host's size class / `data-size` with a `MutationObserver`.
 */
export class LayoutWatcher {
  private readonly observer: ResizeObserver | null;
  private readonly sizeClass: MutationObserver;
  private words: readonly HTMLElement[] = [];

  /**
   * @param host - Banner host (observed for its whole lifetime).
   * @param onResize - Called from the observer callbacks (after a resize or a size-class change).
   */
  public constructor(host: HTMLElement, onResize: () => void) {
    const Observer = (globalThis as { ResizeObserver?: typeof ResizeObserver }).ResizeObserver;
    this.observer = Observer === undefined ? null : new Observer(onResize);
    this.observer?.observe(host);
    this.sizeClass = new MutationObserver(onResize);
    this.sizeClass.observe(host, { attributes: true, attributeFilter: [...SIZE_ATTRIBUTES] });
  }

  /** Whether resize callbacks deliver measurements (false: measure right after building). */
  public get active(): boolean {
    return this.observer !== null;
  }

  /** @param words - New word elements to observe instead of the previous ones. */
  public watch(words: readonly HTMLElement[]): void {
    this.words.forEach((word) => {
      this.observer?.unobserve(word);
    });
    this.words = words;
    words.forEach((word) => {
      this.observer?.observe(word);
    });
  }

  /** Stops observing. */
  public dispose(): void {
    this.observer?.disconnect();
    this.sizeClass.disconnect();
    this.words = [];
  }
}
