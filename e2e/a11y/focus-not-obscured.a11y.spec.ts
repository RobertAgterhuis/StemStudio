import type { Page } from '@playwright/test';
import { practicePanelEntries, preferencesRecord, storageStateFor, type Level, type StorageState } from '../support/preferences';
import { openScreen } from '../support/screens';
import { expect, test } from '../support/ux-test';

// 012 integration (WCAG 2.4.11 Focus Not Obscured, CTO decision 2026-11-17): on the mixer the
// karaoke banner, the count-in slot and the transport stick to the bottom of the viewport together
// (`.ss-mixer-dock`, 240-370 px tall depending on the banner text size and the width). Every
// control reached with Tab must end above that dock, so `scroll-padding-bottom` has to follow the
// dock's real height, and on small screens the page must end above the bottom navigation bar,
// under which the dock sticks. Worst cases: the largest banner text, the help panel docked at Beginner, the
// practice panel open, and a phone-sized viewport.

/** Tab presses per case; the mixer has fewer stops, so focus wraps before this. */
const maxTabs = 160;

/** A rectangle in CSS pixels of the viewport. */
interface Box {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

interface Obscured {
  readonly id: string;
  readonly bottom: number;
  readonly dockTop: number;
}

/** Storage state: a level, the banner at its largest text size and the practice panel open. */
function worstCaseState(level: Level): StorageState {
  const entries = { ...practicePanelEntries('expanded'), 'stemstudio.karaoke': JSON.stringify({ visible: true, size: 'l' }) };
  return storageStateFor(preferencesRecord({ level, theme: 'light', motion: 'reduced' }), undefined, entries);
}

/** What the page reports for the focused element. */
interface RawStop {
  readonly id: string;
  readonly box: Box;
  readonly dock: Box;
}

/**
 * Runs in the page: the focused element with its box and the dock's box, or null for the body and
 * for elements inside the dock, the navigation rail (fixed beside the dock, or below it on small
 * screens) or the help panel: the dock does not cover those. One evaluation,
 * so focus cannot move between the reads.
 */
function activeStop(): RawStop | null {
  const el = document.activeElement;
  const dock = document.querySelector('.ss-mixer-dock');
  if (el === null || dock === null || el.matches('body, .ss-mixer-dock *, .ss-rail *, [data-testid="help-panel"] *')) {
    return null;
  }
  const { x, y, width, height } = el.getBoundingClientRect();
  const area = dock.getBoundingClientRect();
  return { id: el.getAttribute('data-testid') ?? el.tagName, box: { x, y, width, height }, dock: { x: area.x, y: area.y, width: area.width, height: area.height } };
}

/** The focus stop when the dock covers part of it, else null. */
function coveredStop(stop: RawStop | null): Obscured | null {
  if (stop === null) {
    return null;
  }
  const bottom = Math.round(stop.box.y + stop.box.height);
  const dockTop = Math.round(stop.dock.y);
  return bottom > dockTop + 1 ? { id: stop.id, bottom, dockTop } : null;
}

/** Tabs through the page and returns every focused control (outside the dock and the help panel) that ends under the dock. */
async function obscuredFocusStops(page: Page): Promise<Obscured[]> {
  const obscured: Obscured[] = [];
  for (let tab = 0; tab < maxTabs; tab++) {
    await page.keyboard.press('Tab');
    const stop = coveredStop(await page.evaluate(activeStop));
    if (stop !== null) {
      obscured.push(stop);
    }
  }
  return obscured;
}

test.describe('focused controls stay above the mixer dock (WCAG 2.4.11)', { tag: '@practice' }, () => {
  test.describe('Beginner with help docked, 1440 x 900', () => {
    test.use({ storageState: worstCaseState('beginner'), viewport: { width: 1440, height: 900 } });

    test('no focus stop ends under the karaoke banner and transport', async ({ page, uxData, help }) => {
      await openScreen(page, 'mixer', uxData);
      await expect(page.getByTestId('practice-panel-toggle')).toHaveAttribute('aria-expanded', 'true');
      await help.open('button');
      await page.getByTestId('practice-panel-toggle').focus();
      expect(await obscuredFocusStops(page)).toEqual([]);
    });
  });

  test.describe('Advanced on a phone, 390 x 844', () => {
    test.use({ storageState: worstCaseState('advanced'), viewport: { width: 390, height: 844 } });

    test('no focus stop ends under the karaoke banner and transport', async ({ page, uxData }) => {
      await openScreen(page, 'mixer', uxData);
      await page.getByTestId('practice-panel-toggle').focus();
      expect(await obscuredFocusStops(page)).toEqual([]);
    });
  });
});
