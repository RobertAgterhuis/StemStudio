import type { Page } from '@playwright/test';
import { HelpPanel } from '../pages/help-panel';
import { axeViolations } from '../support/axe';
import { createFixtureSheet, requireModelBackend } from '../support/lyric-assist-fixtures';
import { runAssist } from '../support/lyric-assist-flow';
import { expect, test } from '../support/lyric-assist-test';
import { horizontalOverflow, reflowViewport } from '../support/robustness';
import { cellState, matrixThemes } from '../support/visual';

// Feature 023 (SC-008, quickstart Q9; WCAG 2.2 AA as in 004): axe on the lyrics editor with the
// assistant pane open and a finished check, in both themes at Expert (every pane control) and
// Beginner (fewest). Fake stack with the lyric scripts (`@lyric-assist`). The NVDA pass of Q9 is
// manual (docs/testing/023-lyric-assist.md section 6).
//
// Small screens (LESSONS frontend: 320 px width and 720 px height; 2.4.11 Focus Not Obscured):
// `/lyrics/{id}` reflows at 320 px without horizontal page scroll, and every control of the pane
// reached with Tab ends inside the viewport and is not covered by another element, at 320 px, at a
// 720 px high desktop window and with the help panel open.

/** A 720 px high desktop window (the shortest supported height). */
const shortViewport = { width: 1280, height: 720 } as const;

/** Tab presses per walk; the pane has fewer stops, so focus leaves it before this. */
const maxTabs = 80;

/** Runs in the page: '' when the element under (x, y) is the focused control or draws it (a label), else what covers it. */
function coverOf(el: Element, [x, y]: readonly [number, number]): string {
  const top = document.elementFromPoint(x, y) ?? document.body;
  const owner = el.closest('label') ?? el;
  const drawn = [owner === top, owner.contains(top), top.contains(owner)].includes(true);
  return drawn ? '' : top.outerHTML.slice(0, 80);
}

/** Runs in the page: a readable name of a focus stop. */
function stopName(el: Element): string {
  return el.getAttribute('data-testid') ?? `${el.tagName.toLowerCase()} "${el.textContent.trim().slice(0, 30)}"`;
}

/** Whether a point lies inside the viewport. */
function insideViewport([x, y]: readonly [number, number], size: { readonly width: number; readonly height: number }): boolean {
  return x >= 0 && y >= 0 && x <= size.width && y <= size.height;
}

/** '' when the focused control's centre is inside the viewport and not covered, else why it is hidden. */
async function focusedCover(page: Page): Promise<string> {
  const focused = page.locator(':focus');
  const box = await focused.boundingBox();
  const size = page.viewportSize();
  if (box === null || size === null) {
    return 'no box';
  }
  const centre = [box.x + box.width / 2, box.y + box.height / 2] as const;
  return insideViewport(centre, size) ? focused.evaluate(coverOf, centre) : 'outside the viewport';
}

/** Tabs through the pane from its first assistant button; returns the stops the user cannot see and what hides them. */
async function hiddenPaneStops(page: Page): Promise<string[]> {
  const pane = page.getByTestId('lyric-assist-pane');
  await page.getByTestId('lyric-assist-check').focus();
  const hidden: string[] = [];
  for (let i = 0; i < maxTabs && (await pane.locator(':focus').count()) > 0; i += 1) {
    const cover = await focusedCover(page);
    if (cover !== '') {
      hidden.push(`${await page.locator(':focus').evaluate(stopName)} under ${cover}`);
    }
    await page.keyboard.press('Tab');
  }
  return hidden;
}

for (const theme of matrixThemes) {
  for (const level of ['beginner', 'expert'] as const) {
    test.describe(`lyric assistant / ${level} / ${theme}`, { tag: '@lyric-assist' }, () => {
      test.use({ storageState: cellState(level, theme) });

      test('the editor with the pane open and a finished check', async ({ lyricAssistApi, assistPane, page }) => {
        await requireModelBackend(lyricAssistApi, 'fake');
        const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
        await assistPane.goto(sheet.id);
        expect(await axeViolations(page), 'pane idle').toEqual([]);
        await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
        await expect(assistPane.list).toHaveAttribute('role', 'listbox');
        expect(await axeViolations(page), 'pane with suggestions').toEqual([]);
      });

      test('the pane at 320 px wide reflows and keeps every control visible', async ({ lyricAssistApi, assistPane, page }) => {
        await page.setViewportSize(reflowViewport);
        await requireModelBackend(lyricAssistApi, 'fake');
        const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
        await assistPane.goto(sheet.id);
        await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
        expect(await horizontalOverflow(page), 'horizontal page scroll').toBe(0);
        // axe first (soft, so the walk still reports): the walk scrolls targets under the sticky top bar.
        expect.soft(await axeViolations(page), 'axe at 320 px').toEqual([]);
        expect(await hiddenPaneStops(page), 'pane controls hidden at 320 px').toEqual([]);
      });

      test('the pane at 720 px high keeps every control visible', async ({ lyricAssistApi, assistPane, page }) => {
        await page.setViewportSize(shortViewport);
        await requireModelBackend(lyricAssistApi, 'fake');
        const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
        await assistPane.goto(sheet.id);
        await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
        expect(await hiddenPaneStops(page), 'pane controls hidden at 720 px high').toEqual([]);
      });

      test('the pane with the help panel open', async ({ lyricAssistApi, assistPane, page }) => {
        await page.setViewportSize(shortViewport);
        await requireModelBackend(lyricAssistApi, 'fake');
        const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
        await assistPane.goto(sheet.id);
        await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
        const help = new HelpPanel(page);
        await help.open('button');
        // The panel moves focus to its heading after it opened; walk the pane only after that.
        await expect(help.panel.locator(':focus')).toHaveCount(1);
        await expect(assistPane.pane).toBeVisible();
        expect(await horizontalOverflow(page), 'horizontal page scroll with help').toBe(0);
        expect.soft(await axeViolations(page), 'axe with help').toEqual([]);
        expect(await hiddenPaneStops(page), 'pane controls hidden with help').toEqual([]);
      });
    });
  }
}
