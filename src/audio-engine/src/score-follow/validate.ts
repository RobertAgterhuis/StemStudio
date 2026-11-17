/**
 * Validation of the score view and the follow options (contract 2.4.0 "Score follower", "Validation").
 *
 * @packageDocumentation
 */
import type { ScoreFollowOptions, ScoreViewBar, ScoreViewMap } from '../types';

/** Largest number of pages. */
export const MAX_PAGES = 200;
/** Largest number of bars. */
export const MAX_BARS = 2000;
/** Default pause of auto-scroll after a manual scroll (ms). */
export const DEFAULT_SCROLL_PAUSE_MS = 4000;
/** Largest scroll pause (ms). */
export const MAX_SCROLL_PAUSE_MS = 60000;
/** Bars must follow each other within this many seconds. */
const CONTIGUITY_SECONDS = 0.001;

function invalid(reason: string): Error {
  return new Error(`Invalid score view: ${reason}`);
}

function inRange(value: number, min: number, max: number): boolean {
  return Number.isFinite(value) && value >= min && value <= max;
}

function checkPages(view: ScoreViewMap): Set<number> {
  if (view.pages.length < 1 || view.pages.length > MAX_PAGES) {
    throw invalid(`pages must be 1..${String(MAX_PAGES)}`);
  }
  const numbers = new Set<number>();
  view.pages.forEach((page) => {
    if (numbers.has(page.page)) {
      throw invalid(`duplicate page ${String(page.page)}`);
    }
    numbers.add(page.page);
  });
  return numbers;
}

function checkBox(bar: ScoreViewBar): void {
  const inside = [bar.x, bar.y, bar.width, bar.height].every((v) => inRange(v, 0, 1));
  if (!inside || bar.width <= 0 || bar.height <= 0) {
    throw invalid(`bar ${String(bar.index)} box outside 0..1`);
  }
}

function checkBar(bar: ScoreViewBar, pages: ReadonlySet<number>, previous: ScoreViewBar | undefined): void {
  if (!pages.has(bar.page)) {
    throw invalid(`bar ${String(bar.index)} is on unknown page ${String(bar.page)}`);
  }
  checkBox(bar);
  if (!(bar.startSeconds < bar.endSeconds)) {
    throw invalid(`bar ${String(bar.index)} ends before it starts`);
  }
  if (previous !== undefined && !(Math.abs(bar.startSeconds - previous.endSeconds) <= CONTIGUITY_SECONDS)) {
    throw invalid(`bar ${String(bar.index)} does not follow bar ${String(previous.index)}`);
  }
}

/**
 * @param view - Score view from the backend.
 * @throws Error `Invalid score view: <reason>` for bad pages, bars, boxes, times or gaps.
 */
export function validateScoreView(view: ScoreViewMap): void {
  const pages = checkPages(view);
  if (view.bars.length < 1 || view.bars.length > MAX_BARS) {
    throw invalid(`bars must be 1..${String(MAX_BARS)}`);
  }
  view.bars.forEach((bar, i) => {
    checkBar(bar, pages, view.bars[i - 1]);
  });
}

/** Options with the defaults resolved. */
export interface FollowSettings {
  readonly follow: boolean;
  readonly reducedMotion: boolean;
  readonly scrollPauseMs: number;
}

/** @returns `matchMedia('(prefers-reduced-motion: reduce)').matches`, false without `matchMedia`. */
export function prefersReducedMotion(): boolean {
  const media = (globalThis as { matchMedia?: (query: string) => { matches: boolean } }).matchMedia;
  return media?.('(prefers-reduced-motion: reduce)').matches === true;
}

/**
 * @param options - Requested options.
 * @returns The options with `reducedMotion` (prefers-reduced-motion) and `scrollPauseMs` (4000) defaulted.
 * @throws Error `Invalid score follow options: scrollPauseMs must be 0..60000`.
 */
export function resolveFollowOptions(options: ScoreFollowOptions): FollowSettings {
  const scrollPauseMs = options.scrollPauseMs ?? DEFAULT_SCROLL_PAUSE_MS;
  if (!inRange(scrollPauseMs, 0, MAX_SCROLL_PAUSE_MS)) {
    throw new Error(`Invalid score follow options: scrollPauseMs must be 0..${String(MAX_SCROLL_PAUSE_MS)}`);
  }
  return { follow: options.follow, reducedMotion: options.reducedMotion ?? prefersReducedMotion(), scrollPauseMs };
}
