/**
 * Probes for the robustness checks of feature 004 (T063 follow-up): reflow at 320 CSS px
 * (WCAG 1.4.10) and filled controls that keep a visible label in forced-colours mode (WCAG 1.4.11).
 *
 * @packageDocumentation
 */
import type { Locator, Page } from '@playwright/test';
import type { ScreenId } from './screens';

/** Narrowest viewport of WCAG 1.4.10 (320 CSS px wide). */
export const reflowViewport = { width: 320, height: 720 } as const;

/** Screens that must reflow without horizontal page scrolling at 320 px. */
export const reflowScreens: readonly ScreenId[] = ['import', 'tracks', 'track', 'track-no-detail', 'mixes', 'mixer', 'library'];

/** Pixels the document is wider than the viewport (0 when nothing scrolls horizontally). */
export async function horizontalOverflow(page: Page): Promise<number> {
  return page.evaluate(() => {
    const root = document.documentElement;
    return Math.max(0, root.scrollWidth - root.clientWidth);
  });
}

/** Text colour and background colour of an element as computed by the browser. */
export interface ComputedColours {
  readonly color: string;
  readonly background: string;
}

/** Reads the computed text and background colour of the element behind a locator. */
export async function computedColours(locator: Locator): Promise<ComputedColours> {
  return locator.evaluate((element) => {
    const style = getComputedStyle(element);
    return { color: style.color, background: style.backgroundColor };
  });
}
