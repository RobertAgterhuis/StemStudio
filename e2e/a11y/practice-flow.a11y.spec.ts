import type { Locator, Page } from '@playwright/test';
import { PracticeLogPanel, PracticePage } from '../pages/practice-log-panel';
import { SetlistsPage } from '../pages/setlists-page';
import { ShortcutSettings } from '../pages/shortcut-settings';
import { axeViolations } from '../support/axe';
import { apiContextOptions } from '../support/env';
import { blurActiveElement } from '../support/help';
import { PracticeFlowApi } from '../support/practice-flow';
import { openScoreAtBar, openSectionLane, openSetlistEditor, openSetlistPlay, seedPracticeUx } from '../support/practice-flow-ux';
import { expect, test } from '../support/ux-test';
import { cellState, matrixThemes } from '../support/visual';

// Feature 014 T103 (FR-031, SC-006): axe-core (WCAG 2.0-2.2 A/AA) on the section lane, the Score panel paused at a bar,
// the Practice log panel, the setlists page, the setlist editor with its dialogs, setlist play mode, the Practice page and
// the shortcut settings, in both themes at Expert (every control rendered); a keyboard-only walk of every new page that
// reaches its main control by Tab and activates it; and FR-031 "not colour alone": the current score bar and the current
// section carry an outline, border or inset shadow plus a text or ARIA state. Seeded data: support/practice-flow-ux.ts.
// Nothing is saved by the walks (the create form, the add dialog and a shortcut capture are cancelled; the log switch is
// switched back), so the seeded surfaces of the visual suite stay as they are.
test.beforeEach(() => {
  test.setTimeout(240_000);
});

/** Tabs from the top of the page until the focused element's test id matches `id` and returns that id. */
async function tabTo(page: Page, id: string | RegExp, maxTabs = 120): Promise<string> {
  await blurActiveElement(page);
  for (let tab = 0; tab < maxTabs; tab++) {
    await page.keyboard.press('Tab');
    const focused = await page.evaluate(() => document.activeElement?.getAttribute('data-testid') ?? '');
    if (typeof id === 'string' ? focused === id : id.test(focused)) {
      await expect(page.getByTestId(focused)).toBeFocused();
      return focused;
    }
  }
  throw new Error(`${String(id)} was not reached with ${String(maxTabs)} Tab presses`);
}

for (const theme of matrixThemes) {
  test.describe(`practice flow / ${theme}`, { tag: '@practice-flow' }, () => {
    test.use({ storageState: cellState('expert', theme) });

    test('mixer: section lane, Score panel at a bar and Practice log panel have no violations', async ({ page, uxData }) => {
      const seed = await seedPracticeUx(uxData);
      await openScoreAtBar(page, uxData, seed);
      expect(await axeViolations(page)).toEqual([]);
      await new PracticeLogPanel(page).open();
      expect(await axeViolations(page)).toEqual([]);
    });

    test('setlists page has no violations', async ({ page, uxData }) => {
      const seed = await seedPracticeUx(uxData);
      await new SetlistsPage(page).goto();
      await expect(page.getByTestId(`setlist-row-${seed.setlist.id}`)).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
    });

    test('setlist editor, its add dialog and its PDF dialog have no violations', async ({ page, uxData }) => {
      const setlists = await openSetlistEditor(page, await seedPracticeUx(uxData));
      expect(await axeViolations(page)).toEqual([]);
      await setlists.byId('setlist-add').click();
      await expect(setlists.byId('setlist-add-dialog')).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
      await page.keyboard.press('Escape');
      await expect(setlists.byId('setlist-add-dialog')).toBeHidden();
      await setlists.byId('setlist-pdf').click();
      await expect(setlists.byId('setlist-pdf-dialog')).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
    });

    test('setlist play mode has no violations', async ({ page, uxData }) => {
      await openSetlistPlay(page, uxData, await seedPracticeUx(uxData));
      expect(await axeViolations(page)).toEqual([]);
    });

    test('Practice page and shortcut settings have no violations', async ({ page, uxData }) => {
      await seedPracticeUx(uxData);
      await new PracticePage(page).goto();
      expect(await axeViolations(page)).toEqual([]);
      await new ShortcutSettings(page).goto();
      await expect(page.getByTestId('pedal-setup')).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
    });
  });
}

test.describe('practice flow by keyboard only', { tag: '@practice-flow' }, () => {
  test.use({ storageState: cellState('advanced', 'dark') });

  test.afterAll(async ({ playwright }) => {
    // The Practice page walk switches the log back; this restores it if that test failed half-way.
    const context = await playwright.request.newContext(apiContextOptions);
    await new PracticeFlowApi(context).setPracticeLog(true);
    await context.dispose();
  });

  test('setlists: New setlist opens the name field, the seeded setlist is reachable', async ({ page, uxData }) => {
    const seed = await seedPracticeUx(uxData);
    await new SetlistsPage(page).goto();
    await tabTo(page, 'setlist-create');
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('setlist-create')).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('setlist-create-name')).toBeFocused();
    await tabTo(page, `setlist-open-${seed.setlist.id}`);
  });

  test('setlist editor: Add songs opens the track picker, Escape closes it; the entry buttons are reachable', async ({ page, uxData }) => {
    const seed = await seedPracticeUx(uxData);
    const setlists = await openSetlistEditor(page, seed);
    await tabTo(page, 'setlist-add');
    await page.keyboard.press('Enter');
    await expect(setlists.byId('setlist-add-dialog')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(setlists.byId('setlist-add-dialog')).toBeHidden();
    const [first] = await setlists.entryIds();
    await tabTo(page, `setlist-entry-down-${first ?? ''}`);
  });

  test('setlist play: Next song moves play mode to song 2', async ({ page, uxData }) => {
    const seed = await seedPracticeUx(uxData);
    const bar = await openSetlistPlay(page, uxData, seed);
    await tabTo(page, 'setlist-bar-next');
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(new RegExp(`/setlists/${seed.setlist.id}/play/2$`, 'u'));
    await bar.expectOn(uxData.plainTrack.id, 2);
  });

  test('Practice page: the log switch toggles with Space and back', async ({ page, uxData }) => {
    await seedPracticeUx(uxData);
    const practice = new PracticePage(page);
    await practice.goto();
    await expect(practice.enabled).toHaveAttribute('aria-checked', 'true');
    await tabTo(page, 'practice-enabled');
    await page.keyboard.press('Space');
    await expect(practice.enabled).toHaveAttribute('aria-checked', 'false');
    await page.keyboard.press('Space');
    await expect(practice.enabled).toHaveAttribute('aria-checked', 'true');
  });

  test('shortcut settings: Change starts a key capture, Escape cancels it and keeps the keys', async ({ page }) => {
    const settings = new ShortcutSettings(page);
    await settings.goto();
    const editId = await tabTo(page, /^shortcut-edit-/u);
    const row = page.getByTestId(editId).locator('xpath=ancestor::tr');
    const keys = (await row.getAttribute('data-keys')) ?? '';
    await page.keyboard.press('Space');
    await expect(settings.byId('shortcut-capture')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(settings.byId('shortcut-capture')).toHaveCount(0);
    await expect(row).toHaveAttribute('data-keys', keys);
  });
});

/** The non-colour marks of an element: border, outline and box shadow (colours removed). */
interface Marks {
  readonly border: number;
  readonly outline: number;
  readonly shadow: string;
}

async function marksOf(element: Locator): Promise<Marks> {
  return element.evaluate((el) => {
    const style = getComputedStyle(el);
    const widths = [style.borderTopWidth, style.borderRightWidth, style.borderBottomWidth, style.borderLeftWidth].map((w) => Number.parseFloat(w));
    const outline = style.outlineStyle === 'none' ? 0 : Number.parseFloat(style.outlineWidth);
    return { border: Math.max(...widths), outline, shadow: style.boxShadow.replace(/rgba?\([^)]*\)/gu, '').trim() };
  });
}

/** True when the marks alone (no colour) make an element stand out: a thick border or outline, or an inset shadow. */
function marked(marks: Marks): boolean {
  return marks.border >= 2 || marks.outline >= 2 || marks.shadow.includes('inset');
}

test.describe('FR-031: the current bar and the current section are not shown by colour alone', { tag: '@practice-flow' }, () => {
  test.use({ storageState: cellState('advanced', 'light') });

  test('the highlighted score bar has a border and the panel names the bar in text', async ({ page, uxData }) => {
    const seed = await seedPracticeUx(uxData);
    const panel = await openScoreAtBar(page, uxData, seed);
    // The engine's highlight has no test id (web-interop score follow): it is the element of the view in state "current".
    const highlight = panel.view.locator('[data-state="current"]');
    await expect(highlight).toHaveCount(1);
    await expect(highlight).toBeVisible();
    expect(marked(await marksOf(highlight)), 'the current bar has a border, outline or inset shadow').toBe(true);
    const number = String(seed.score.view.bars[1]?.number ?? '');
    await expect(panel.byId('score-bar-label')).toHaveText(new RegExp(`^Bar ${number} of \\d+$`, 'u'));
  });

  test('the current section has an ARIA state, a text and a mark the other sections do not have', async ({ page, uxData }) => {
    // The click fixture (plain track) has at least two sections (014 sections.spec), so a non-current one exists.
    const lane = await openSectionLane(page, uxData, await seedPracticeUx(uxData), 'plain');
    const current = lane.lane.locator('[data-current="true"]');
    await expect(current).toHaveCount(1);
    await expect(current).toHaveAttribute('aria-current', 'true');
    await expect(current).toContainText('playing now');
    const marks = await marksOf(current);
    expect(marked(marks), 'the current section has a border, outline or inset shadow').toBe(true);
    const other = lane.lane.locator('[data-current="false"]').first();
    await expect(other).toBeVisible();
    await expect(other).not.toHaveAttribute('aria-current', 'true');
    await expect(other).not.toContainText('playing now');
    expect(await marksOf(other), 'a section that is not current lacks the mark').not.toEqual(marks);
  });
});
