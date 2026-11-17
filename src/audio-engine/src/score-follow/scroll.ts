/**
 * Auto-scroll of the score follower (contract 2.4.0 "Scrolling", FR-010, FR-011): one `scrollIntoView` per system
 * change (a new page or `y` moved by more than 0.02), paused for `scrollPauseMs` after a manual scroll inside the
 * host. With reduced motion it jumps, and only when the bar left the visible part of the host. The pause only gates
 * scrolling, never timing.
 *
 * @packageDocumentation
 */
import type { ScoreViewBar } from '../types';
import type { FollowSettings } from './validate';

/** Keys that scroll the host. */
const SCROLL_KEYS = new Set(['PageUp', 'PageDown', 'Home', 'End', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);
/** A `y` change above this (page fraction) is a new system. */
const SYSTEM_STEP = 0.02;

/** Whether `element` lies inside the visible part of `host` (a layout read, once per system change). */
export type VisibilityCheck = (element: HTMLElement, host: HTMLElement) => boolean;

/**
 * @param element - Highlight.
 * @param host - Scrolling host.
 * @returns True when the element's box is vertically inside the host's box (`getBoundingClientRect`).
 */
export const boxVisible: VisibilityCheck = (element, host) => {
  const box = element.getBoundingClientRect();
  const view = host.getBoundingClientRect();
  return box.top >= view.top && box.bottom <= view.bottom;
};

/** Construction parameters of {@link AutoScroll}. */
export interface AutoScrollOptions {
  readonly host: HTMLElement;
  /** Wall clock in ms. */
  readonly now: () => number;
  readonly isVisible: VisibilityCheck;
}

/** Decides when to scroll the current bar into view. */
export class AutoScroll {
  private pausedUntil = -Infinity;
  private lastPage: number | null = null;
  private lastY = Number.NaN;
  private forced = false;

  /**
   * @param options - Host, clock and visibility check.
   * @param settings - Follow options.
   */
  public constructor(
    private readonly options: AutoScrollOptions,
    private settings: FollowSettings,
  ) {
    options.host.addEventListener('wheel', this.manual, { passive: true });
    options.host.addEventListener('touchmove', this.manual, { passive: true });
    options.host.addEventListener('keydown', this.key);
  }

  /** @param settings - New options; turning follow on (again) ends a pause and scrolls on the next draw. */
  public configure(settings: FollowSettings): void {
    this.settings = settings;
    if (settings.follow) {
      this.pausedUntil = -Infinity;
      this.forced = true;
    }
  }

  /**
   * @param bar - The current bar.
   * @param highlight - Its highlight element (absent when the page has no container).
   */
  public update(bar: ScoreViewBar, highlight: HTMLElement | undefined): void {
    const wanted = this.forced || this.isNewSystem(bar);
    if (wanted && this.allowed() && highlight !== undefined) {
      this.forced = false;
      this.lastPage = bar.page;
      this.lastY = bar.y;
      this.scroll(highlight);
    }
  }

  /** Stops listening to the host. */
  public dispose(): void {
    const { host } = this.options;
    host.removeEventListener('wheel', this.manual);
    host.removeEventListener('touchmove', this.manual);
    host.removeEventListener('keydown', this.key);
  }

  private isNewSystem(bar: ScoreViewBar): boolean {
    return bar.page !== this.lastPage || Math.abs(bar.y - this.lastY) > SYSTEM_STEP;
  }

  private allowed(): boolean {
    return this.settings.follow && this.options.now() >= this.pausedUntil;
  }

  private scroll(highlight: HTMLElement): void {
    const { reducedMotion } = this.settings;
    if (reducedMotion && this.options.isVisible(highlight, this.options.host)) {
      return;
    }
    highlight.scrollIntoView({ block: 'center', inline: 'nearest', behavior: reducedMotion ? 'auto' : 'smooth' });
  }

  private readonly manual = (): void => {
    this.pausedUntil = this.options.now() + this.settings.scrollPauseMs;
  };

  private readonly key = (event: Event): void => {
    if (SCROLL_KEYS.has((event as KeyboardEvent).key)) {
      this.manual();
    }
  };
}
