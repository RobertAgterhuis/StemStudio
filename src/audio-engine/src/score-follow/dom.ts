/**
 * DOM of the score follower (contract 2.4.0 "DOM"): one absolutely positioned `.score-highlight` per page container
 * (`[data-score-page="<n>"]`), owned by the engine, and the host attributes `data-bar-index`, `data-bar-number`,
 * `data-page`, `data-position-ms`. Writes only; no layout reads.
 *
 * @packageDocumentation
 */
import type { ScoreViewBar, ScoreViewMap } from '../types';

const HOST_ATTRIBUTES = ['data-bar-index', 'data-bar-number', 'data-page', 'data-position-ms'] as const;

function percent(fraction: number): string {
  return `${String(Math.round(fraction * 100000) / 1000)}%`;
}

/** Highlights and host attributes of one attached follower. */
export class ScoreDom {
  private readonly highlights = new Map<number, HTMLElement>();
  private shownPage: number | null = null;

  /**
   * @param host - Host element with the page containers.
   * @param view - Validated score view.
   */
  public constructor(
    private readonly host: HTMLElement,
    view: ScoreViewMap,
  ) {
    view.pages.forEach(({ page }) => {
      const container = host.querySelector<HTMLElement>(`[data-score-page="${String(page)}"]`);
      if (container !== null) {
        this.highlights.set(page, this.createHighlight(container));
      }
    });
    this.show(null);
    this.setPosition(0);
  }

  /**
   * @param page - Page number.
   * @returns Its highlight element, when the page container exists.
   */
  public highlightOf(page: number): HTMLElement | undefined {
    return this.highlights.get(page);
  }

  /** @param bar - The current bar, or null for none. */
  public show(bar: ScoreViewBar | null): void {
    const page = bar?.page ?? null;
    if (this.shownPage !== page) {
      this.idle(this.shownPage);
    }
    this.shownPage = page;
    if (bar === null) {
      this.writeHost(['-1', '', '']);
    } else {
      this.writeHost([String(bar.index), String(bar.number), String(bar.page)]);
      this.place(bar);
    }
  }

  private idle(page: number | null): void {
    if (page !== null) {
      this.highlights.get(page)?.setAttribute('data-state', 'idle');
    }
  }

  private writeHost([index, number, page]: readonly [string, string, string]): void {
    this.host.setAttribute('data-bar-index', index);
    this.host.setAttribute('data-bar-number', number);
    this.host.setAttribute('data-page', page);
  }

  /** @param ms - Heard position in integer milliseconds. */
  public setPosition(ms: number): void {
    this.host.setAttribute('data-position-ms', String(ms));
  }

  /** Removes the highlights and the host attributes. */
  public dispose(): void {
    this.highlights.forEach((element) => {
      element.remove();
    });
    this.highlights.clear();
    HOST_ATTRIBUTES.forEach((name) => {
      this.host.removeAttribute(name);
    });
  }

  private place(bar: ScoreViewBar): void {
    const element = this.highlights.get(bar.page);
    if (element === undefined) {
      return;
    }
    const { style } = element;
    style.left = percent(bar.x);
    style.top = percent(bar.y);
    style.width = percent(bar.width);
    style.height = percent(bar.height);
    element.setAttribute('data-state', 'current');
  }

  private createHighlight(container: HTMLElement): HTMLElement {
    const element = container.ownerDocument.createElement('div');
    element.className = 'score-highlight';
    element.setAttribute('aria-hidden', 'true');
    element.setAttribute('data-state', 'idle');
    element.style.position = 'absolute';
    element.style.pointerEvents = 'none';
    container.appendChild(element);
    return element;
  }
}
