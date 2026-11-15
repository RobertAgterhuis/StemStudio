import { vi, type MockInstance } from 'vitest';

/** Recording `ResizeObserver` double; tests fire its callback with {@link FakeResizeObserver.trigger}. */
export class FakeResizeObserver {
  public static readonly instances: FakeResizeObserver[] = [];
  public readonly observed = new Set<Element>();
  public disconnected = false;

  public constructor(private readonly callback: ResizeObserverCallback) {
    FakeResizeObserver.instances.push(this);
  }

  /** The most recently created observer. */
  public static latest(): FakeResizeObserver {
    const latest = FakeResizeObserver.instances.at(-1);
    if (latest === undefined) {
      throw new Error('no ResizeObserver was created');
    }
    return latest;
  }

  public observe(target: Element): void {
    this.observed.add(target);
  }

  public unobserve(target: Element): void {
    this.observed.delete(target);
  }

  public disconnect(): void {
    this.disconnected = true;
    this.observed.clear();
  }

  /** Delivers one callback for every observed element. */
  public trigger(): void {
    const entries = [...this.observed].map((target) => ({ target }) as ResizeObserverEntry);
    this.callback(entries, this);
  }
}

/** Geometry of the stubbed layout (px). */
export const LAYOUT = {
  hostLeft: 100,
  hostTop: 50,
  rowHeight: 40,
  wordPitch: 60,
  wordWidth: 50,
  wordHeight: 30,
} as const;

const ROWS: Readonly<Record<string, number>> = { previous: 0, current: 1, next: 2 };

/** Mutable shift applied to every word (simulates a reflow after a resize). */
export const layoutShift = { x: 0 };

function rect(left: number, top: number, width: number, height: number): DOMRect {
  return { left, top, width, height, right: left + width, bottom: top + height, x: left, y: top } as DOMRect;
}

function rowTop(element: Element): number {
  const slot = element.closest('.karaoke-line')?.getAttribute('data-slot') ?? '';
  return LAYOUT.hostTop + (ROWS[slot] ?? 0) * LAYOUT.rowHeight;
}

function wordRect(element: Element): DOMRect {
  const index = [...(element.parentElement?.children ?? [])].indexOf(element);
  const left = LAYOUT.hostLeft + layoutShift.x + index * LAYOUT.wordPitch;
  return rect(left, rowTop(element), LAYOUT.wordWidth, LAYOUT.wordHeight);
}

function rectOf(element: Element): DOMRect {
  if (element.classList.contains('karaoke-word')) {
    return wordRect(element);
  }
  return element.classList.contains('karaoke-line')
    ? rect(LAYOUT.hostLeft, rowTop(element), 600, LAYOUT.rowHeight)
    : rect(LAYOUT.hostLeft, LAYOUT.hostTop, 600, 3 * LAYOUT.rowHeight);
}

/**
 * Stubs `getBoundingClientRect` with the grid in {@link LAYOUT}: word `i` of a slot is at
 * `hostLeft + i * wordPitch`, rows previous/current/next are `rowHeight` apart.
 *
 * @returns The spy, to count layout reads.
 */
export function stubLayout(): MockInstance<() => DOMRect> {
  layoutShift.x = 0;
  return vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    return rectOf(this);
  });
}

/** Centre x of word `index` relative to the host (px). */
export function wordX(index: number): number {
  return layoutShift.x + index * LAYOUT.wordPitch + LAYOUT.wordWidth / 2;
}

/** Top of a slot row relative to the host (px). */
export function rowY(slot: 'previous' | 'current' | 'next'): number {
  return (ROWS[slot] ?? 0) * LAYOUT.rowHeight;
}

/** Installs {@link FakeResizeObserver} as the global `ResizeObserver`. */
export function stubResizeObserver(): void {
  FakeResizeObserver.instances.length = 0;
  vi.stubGlobal('ResizeObserver', FakeResizeObserver);
}
