import type { HelpOpener } from '../pages/help-panel';
import { blurActiveElement, wrongFirstResults } from '../support/help';
import { levelState } from '../support/preferences';
import { openScreen, screenTopics, type ScreenId } from '../support/screens';
import { expect, test } from '../support/ux-test';

// T052 / FR-022..FR-024, quickstart V-H1..V-H5. Default level Advanced (config); V-H4 at Beginner.
const helpScreens: readonly ScreenId[] = ['import', 'tracks', 'track', 'mixer', 'mixes'];
const keyOpeners: readonly HelpOpener[] = ['question-key', 'f1-key'];

for (const screen of helpScreens) {
  test(`help on ${screen}: button, rail item, ? and F1 open the screen topic and Escape closes`, { tag: '@ux' }, async ({
    page,
    help,
    shell,
    uxData,
  }) => {
    await openScreen(page, screen, uxData);
    for (const [opener, returnTo] of [['button', 'help-toggle'], ['nav', 'nav-help']] as const) {
      await help.open(opener);
      await help.expectTopic(screenTopics[screen]);
      expect(await shell.focusIsInside(help.panel), `${opener}: focus moves into the panel`).toBe(true);
      await help.closeWithEscape();
      await expect(page.getByTestId(returnTo)).toBeFocused();
    }
    for (const opener of keyOpeners) {
      await blurActiveElement(page);
      await help.open(opener);
      await help.expectTopic(screenTopics[screen]);
      expect(await shell.focusIsInside(help.panel), `${opener}: focus moves into the panel`).toBe(true);
      await help.closeWithEscape();
    }
  });
}

test.describe('in the mixer', () => {
  test.beforeEach(async ({ page, uxData }) => {
    await openScreen(page, 'mixer', uxData);
  });

  test('F1 on a focused marked control opens its topic and Escape returns focus to it', { tag: '@ux' }, async ({ page, help }) => {
    await page.getByTestId('mute-vocals').focus();
    await help.open('f1-key');
    await help.expectTopic('mixer-mute-solo');
    await help.closeWithEscape();
    await expect(page.getByTestId('mute-vocals')).toBeFocused();
  });

  test('hover and keyboard focus show the tooltip; "Learn more" opens the topic', { tag: '@ux' }, async ({ page, help }) => {
    const tooltip = page.getByTestId('help-tooltip-mixer-pan');
    await page.getByTestId('pan-vocals').hover();
    await expect(tooltip).toBeVisible();
    await expect(tooltip).not.toBeEmpty();
    await blurActiveElement(page);
    await page.mouse.move(0, 0);
    await page.getByTestId('pan-vocals').focus();
    await expect(tooltip).toBeVisible();
    await tooltip.getByTestId('help-learn-more').click();
    await help.expectTopic('mixer-pan');
  });

  test('the 20 reference queries return their topic first', { tag: '@ux' }, async ({ help }) => {
    await help.open('button');
    expect(await wrongFirstResults(help)).toEqual({});
  });

  test('a related link opens that topic and Back returns', { tag: '@ux' }, async ({ help }) => {
    await help.open('button');
    await help.openTopic('pan', 'mixer-pan');
    const first = help.relatedLinks.first();
    const related = ((await first.getAttribute('data-testid')) ?? '').replace('help-related-', '');
    await first.click();
    await help.expectTopic(related);
    await help.byId('help-back').click();
    await expect(help.panel).toHaveAttribute('data-topic-id', 'mixer-pan');
  });

  test('at 1280 px the docked panel leaves transport and strips usable', { tag: '@ux' }, async ({ page, help, mixer }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await help.open('button');
    await expect(mixer.byId('transport-play')).toBeInViewport();
    await expect(mixer.strip('vocals')).toBeInViewport();
    await mixer.play();
    await expect(mixer.byId('transport-position')).not.toHaveText(/^\s*0?0:00(\.0+)?\b/, { timeout: 5_000 });
    await expect(help.panel).toBeVisible();
  });
});

test.describe('at Beginner', () => {
  test.use({ storageState: levelState('beginner') });

  test('a gated topic shows the level note, and its switch raises the level', { tag: '@ux' }, async ({ page, help, shell, uxData }) => {
    await openScreen(page, 'mixer', uxData);
    await help.open('button');
    await help.openTopic('pan', 'mixer-pan');
    await expect(help.byId('help-level-note')).toBeVisible();
    await help.byId('help-level-switch').click();
    await shell.expectLevel('advanced');
    await expect(help.byId('help-level-note')).toBeHidden();
    await expect(page.getByTestId('pan-vocals')).toBeVisible();
  });
});
