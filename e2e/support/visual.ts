/**
 * Rules of the visual regression suite (TEST-STRATEGY.md 10.2, SC-005): where it may run, what is
 * masked, and the storage state of each matrix cell.
 *
 * @packageDocumentation
 */
import { existsSync } from 'node:fs';
import { test, type Locator, type Page } from '@playwright/test';
import { levelState, type Level, type StorageState } from './preferences';

/** Themes of the matrix (explicit choices, so the OS setting does not matter). */
export type MatrixTheme = 'light' | 'dark';

/** Both themes, in report order. */
export const matrixThemes: readonly MatrixTheme[] = ['dark', 'light'];

/** The three levels, in report order. */
export const matrixLevels: readonly Level[] = ['beginner', 'advanced', 'expert'];

/**
 * True inside the Playwright Linux image (`mcr.microsoft.com/playwright:v1.65.1-noble`, browsers
 * in `/ms-playwright`, as started by `scripts/playwright-container.sh`) or when
 * `E2E_VISUAL_CONTAINER=1` is set explicitly.
 */
export function inVisualContainer(): boolean {
  const flagged = process.env.E2E_VISUAL_CONTAINER === '1';
  return flagged || (process.platform === 'linux' && existsSync('/ms-playwright'));
}

/** Skips the current file's tests outside the container, with the reason as annotation. */
export function requireVisualContainer(): void {
  test.skip(!inVisualContainer(), 'visual baselines render only in mcr.microsoft.com/playwright:v1.65.1-noble');
}

/** Storage state of one matrix cell: level, explicit theme, reduced motion, no welcome or offers. */
export function cellState(level: Level, theme: MatrixTheme): StorageState {
  return levelState(level, { theme, motion: 'reduced' });
}

/** Regions whose pixels change on their own: meters, clock, transport state, toasts, players, PDF previews, dates. */
export function dynamicMasks(page: Page): Locator[] {
  return [
    page.getByTestId(/^meter-/),
    page.getByTestId('transport-position'),
    page.getByTestId('transport-state'),
    page.getByTestId('toast'),
    // Native <audio> controls of library items show the duration once metadata has loaded (007).
    page.getByTestId(/^asset-play-/),
    // The browser's PDF viewer of a score item (008) renders asynchronously, inside its own frame.
    page.getByTestId(/^asset-preview-/),
    // The only non-test-id selector of the suite: dates are rendered in <time> (cosmetic mask).
    page.locator('time'),
  ];
}

/** Screenshot file name of a matrix cell, e.g. `mixer-expanded--advanced--dark.png`. */
export function shotName(parts: readonly string[]): string {
  return `${parts.join('--')}.png`;
}
