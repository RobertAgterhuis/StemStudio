import type { Locator, Page } from '@playwright/test';
import { PracticeLogPanel, PracticePage } from '../pages/practice-log-panel';
import { SetlistsPage } from '../pages/setlists-page';
import { ShortcutSettings } from '../pages/shortcut-settings';
import {
  fontsReady,
  openScoreAtBar,
  openSectionLane,
  openSetlistEditor,
  openSetlistPlay,
  seedPracticeUx,
  type PracticeUxData,
} from '../support/practice-flow-ux';
import type { UxData } from '../support/ux-data';
import { expect, test } from '../support/ux-test';
import { cellState, dynamicMasks, matrixThemes, requireVisualContainer, shotName } from '../support/visual';

// Feature 014 T103 visual baselines (FR-031, SC-006): the section lane, the Score panel paused at a fixed bar, the setlists
// page, the setlist editor, the setlist play bar, the Practice log panel, the Practice page and the shortcut settings, in
// both themes at desktop width and at 320 px, Advanced. Seeded data (support/practice-flow-ux.ts); container only, fresh
// stack (the setlists page and the Practice page list what other specs left). The CTO generates and approves the
// baselines in the integration wave (TEST-STRATEGY 9 / 21).
requireVisualContainer();

const widths = [{ name: 'desktop', width: 1280, height: 900 }, { name: '320', width: 320, height: 800 }] as const;

/** What a surface masks beyond {@link dynamicMasks}; an element surface also names the element it captures. */
interface Capture {
  readonly mask?: readonly Locator[];
}

/** A surface captured as one element. */
interface ElementCapture extends Capture {
  readonly target: Locator;
}

/** One surface of the matrix: opens it with the seeded data and returns what to capture. */
interface Surface<T extends Capture> {
  readonly id: string;
  readonly open: (page: Page, data: UxData, seed: PracticeUxData) => Promise<T>;
}

/** Charts of the Practice log name the last seven days, which change every day. */
function chartMasks(page: Page): Locator[] {
  return [page.getByTestId(/^practice-log-chart-(days|tempo)(-table)?$/u)];
}

/** Surfaces inside the mixer, captured as their element. */
const elementSurfaces: readonly Surface<ElementCapture>[] = [
  { id: 'section-lane', open: async (page, data, seed) => ({ target: (await openSectionLane(page, data, seed)).lane }) },
  { id: 'score-panel', open: async (page, data, seed) => ({ target: (await openScoreAtBar(page, data, seed)).panel }) },
  { id: 'setlist-bar', open: async (page, data, seed) => ({ target: (await openSetlistPlay(page, data, seed)).bar }) },
  {
    id: 'practice-log-panel',
    open: async (page, data, seed) => {
      await openSectionLane(page, data, seed);
      const log = new PracticeLogPanel(page);
      await log.open();
      await expect(log.byId('practice-log-today')).toBeVisible();
      return { target: log.panel, mask: chartMasks(page) };
    },
  },
];

/** The new pages, captured full page (shell included, so the 320 px layout shows). */
const pageSurfaces: readonly Surface<Capture>[] = [
  {
    id: 'setlists',
    open: async (page, _data, seed) => {
      await new SetlistsPage(page).goto();
      await expect(page.getByTestId(`setlist-row-${seed.setlist.id}`)).toBeVisible();
      return { mask: [page.getByTestId(/^setlist-row-/u).filter({ hasNot: page.getByTestId(`setlist-open-${seed.setlist.id}`) })] };
    },
  },
  {
    id: 'setlist-editor',
    open: async (page, _data, seed) => {
      await openSetlistEditor(page, seed);
      return {};
    },
  },
  {
    id: 'practice-page',
    open: async (page) => {
      await new PracticePage(page).goto();
      await expect(page.getByTestId('practice-enabled')).toHaveAttribute('aria-checked', 'true');
      return { mask: [page.getByTestId('practice-overview-table'), page.getByTestId('practice-sessions')] };
    },
  },
  {
    id: 'shortcut-settings',
    open: async (page) => {
      await new ShortcutSettings(page).goto();
      await expect(page.getByTestId('pedal-setup')).toBeVisible();
      return {};
    },
  },
];

/** Seeds, opens the surface, waits for the fonts, moves the pointer away (no help tooltip) and returns its masks. */
async function prepare<T extends Capture>(page: Page, data: UxData, surface: Surface<T>): Promise<{ readonly capture: T; readonly mask: Locator[] }> {
  test.setTimeout(240_000);
  const capture = await surface.open(page, data, await seedPracticeUx(data));
  await fontsReady(page);
  await page.mouse.move(0, 0);
  return { capture, mask: [...dynamicMasks(page), ...(capture.mask ?? [])] };
}

for (const theme of matrixThemes) {
  for (const size of widths) {
    test.describe(`practice flow / ${size.name} / ${theme}`, { tag: '@practice-flow' }, () => {
      test.use({ storageState: cellState('advanced', theme), viewport: { width: size.width, height: size.height } });

      for (const surface of elementSurfaces) {
        test(`${surface.id} matches its baseline`, async ({ page, uxData }) => {
          const { capture, mask } = await prepare(page, uxData, surface);
          await expect(capture.target).toHaveScreenshot(shotName([surface.id, size.name, theme]), { mask });
        });
      }

      for (const surface of pageSurfaces) {
        test(`${surface.id} matches its full-page baseline`, async ({ page, uxData }) => {
          const { mask } = await prepare(page, uxData, surface);
          await expect(page).toHaveScreenshot(shotName([surface.id, size.name, theme]), { fullPage: true, mask });
        });
      }
    });
  }
}
