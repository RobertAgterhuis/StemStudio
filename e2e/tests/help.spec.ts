import { navPaths } from '../pages/app-shell';
import type { HelpOpener, HelpPanel } from '../pages/help-panel';
import {
  artworkReferenceQueries,
  blurActiveElement,
  expectHelpMarker,
  exportReferenceQueries,
  generativePageMarkers,
  generativeReferenceQueries,
  generativeScreenTopics,
  lyricsReferenceQueries,
  scoreReferenceQueries,
  wrongFirstResults,
} from '../support/help';
import { expectRendered, generativeNavIds } from '../support/levels';
import { requireLyricsMode } from '../support/lyrics';
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
      await expect.poll(() => shell.focusIsInside(help.panel), `${opener}: focus moves into the panel`).toBe(true);
      await help.closeWithEscape();
      await expect(page.getByTestId(returnTo)).toBeFocused();
    }
    for (const opener of keyOpeners) {
      await blurActiveElement(page);
      await help.open(opener);
      await help.expectTopic(screenTopics[screen]);
      await expect.poll(() => shell.focusIsInside(help.panel), `${opener}: focus moves into the panel`).toBe(true);
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

// Help topics of features 005 and 007 (ui-test-ids 1.4.0/1.5.0; 007 FR-014, quickstart X10:
// "every new area opens its help topic"). Not tagged @ux, so the 004 suite keeps its scope.
async function expectTopicsReachable(help: HelpPanel, queries: Readonly<Record<string, string>>): Promise<void> {
  await help.open('button');
  expect(await wrongFirstResults(help, queries)).toEqual({});
  for (const [query, topicId] of Object.entries(queries)) {
    // Click only once the list shows this query's results: a result clicked while the previous
    // list is still being replaced can open another topic.
    await help.search(query);
    await expect.poll(() => help.firstResultTopic(), query).toBe(topicId);
    await help.openTopic(query, topicId);
    await expect(help.byId('help-topic-body')).not.toBeEmpty();
  }
}

test('the 005 reference queries return their topic first, and each topic opens', { tag: '@artwork' }, async ({ page, help, uxData }) => {
  await openScreen(page, 'mixer', uxData);
  await expect(help.byId('help-toggle')).toBeVisible();
  await expectTopicsReachable(help, artworkReferenceQueries);
});

test('the 007 reference queries return their topic first, and each topic opens', { tag: '@export' }, async ({ page, help, uxData }) => {
  await openScreen(page, 'mixer', uxData);
  await expect(help.byId('help-toggle')).toBeVisible();
  await expectTopicsReachable(help, exportReferenceQueries);
});

test('covers and the band info panel carry their help markers', { tag: '@artwork' }, async ({ page, artwork, bandInfo, importPage, mixer, uxData }) => {
  await importPage.goto();
  await expectHelpMarker(importPage.legalNotice, 'online-lookups');
  await openScreen(page, 'track', uxData);
  await expect(artwork.trackCover).toBeVisible();
  await expectHelpMarker(artwork.trackCover, 'artwork');
  await bandInfo.show();
  await expectHelpMarker(bandInfo.panel, 'band-info');
  await mixer.goto(uxData.detailTrack.id);
  await expectHelpMarker(artwork.mixerCover, 'artwork');
});

test('the export dialog and the library carry their help markers', { tag: '@export' }, async ({ page, exportDialog, library, shell, uxData }) => {
  await openScreen(page, 'mixer', uxData);
  await exportDialog.openForMix();
  await expect(exportDialog.dialog).toBeVisible();
  await expectHelpMarker(exportDialog.dialog, 'export-dialog');
  await expectHelpMarker(exportDialog.byId('export-format'), 'export-formats');
  await expectHelpMarker(exportDialog.byId('export-range-start'), 'export-range');
  await library.goto();
  await expectHelpMarker(shell.root, 'library');
});

// Help topics of features 006 and 008 (ui-test-ids 1.6.0/1.7.0 "Help topics").
test('the 006 reference queries return their topic first, and each topic opens', { tag: '@karaoke' }, async ({ page, help, uxData }) => {
  await openScreen(page, 'mixer', uxData);
  await expect(help.byId('help-toggle')).toBeVisible();
  await expectTopicsReachable(help, lyricsReferenceQueries);
});

test('the 008 reference queries return their topic first, and each topic opens', { tag: '@sheet-music' }, async ({ page, help, uxData }) => {
  await openScreen(page, 'track', uxData);
  await expect(help.byId('help-toggle')).toBeVisible();
  await expectTopicsReachable(help, scoreReferenceQueries);
});

test('the karaoke banner, its offset and the lyrics card carry their help markers', { tag: '@karaoke' }, async ({ page, importPage, karaoke, lyrics, uxData }) => {
  requireLyricsMode('word');
  await importPage.goto();
  await expectHelpMarker(importPage.legalNotice, 'lyrics-sources');
  await openScreen(page, 'mixer', uxData);
  await expect(karaoke.host).toBeVisible();
  await expectHelpMarker(karaoke.host, 'karaoke');
  await expectHelpMarker(karaoke.offset, 'karaoke-offset');
  await lyrics.goto(uxData.detailTrack.id);
  await expectHelpMarker(lyrics.section, 'lyrics-sources');
});

test('the sheet music dialog carries its help markers', { tag: '@sheet-music' }, async ({ page, scoreDialog, uxData }) => {
  await openScreen(page, 'track', uxData);
  await scoreDialog.openForStem('bass');
  await expect(scoreDialog.dialog).toBeVisible();
  await expectHelpMarker(scoreDialog.dialog, 'sheet-music');
  await expectHelpMarker(scoreDialog.byId('score-profile'), 'sheet-music-profiles');
  await expectHelpMarker(scoreDialog.byId('score-key-override'), 'sheet-music-key-tempo');
});

// Help topics of features 009 and 010 (009 FR-015, 010 FR-014). None needs a generator: tagged
// @styles, so `npm run test:styles` covers them on every stack.
test('the 009/010 reference queries return their topic first, and each topic opens', { tag: '@styles' }, async ({ help, page }) => {
  await page.goto('/styles');
  // Wait for the page to finish loading: its screen topic would otherwise replace a topic opened early.
  await expect(page.locator('[data-testid^="style-row-"]').first()).toBeVisible();
  await expect(help.byId('help-toggle')).toBeVisible();
  await expectTopicsReachable(help, generativeReferenceQueries);
});

test('the style, generation, song and voice pages open their screen topic', { tag: '@styles' }, async ({ help, page, shell }) => {
  for (const [path, topicId] of Object.entries(generativeScreenTopics)) {
    await page.goto(path);
    await shell.waitForShell();
    await help.open('button');
    await help.expectTopic(topicId);
    // Escape closes the panel once focus has moved into it (as in the 004 help cases).
    await expect.poll(() => shell.focusIsInside(help.panel), `${path}: focus moves into the panel`).toBe(true);
    await help.closeWithEscape();
  }
});

test('the rail links reach the generation, style, song, lyrics and voice pages (ui-test-ids 1.8.0, 1.9.0)', { tag: '@styles' }, async ({ page, shell }) => {
  await page.goto('/');
  await expectRendered(page, generativeNavIds);
  for (const target of ['generations', 'styles', 'songs', 'lyrics', 'voices'] as const) {
    await shell.navigate(target);
    await expect(page).toHaveURL(navPaths[target]);
  }
});

test('the style, generation, song and voice pages carry their help markers', { tag: '@styles' }, async ({ page, shell }) => {
  for (const [path, topicIds] of Object.entries(generativePageMarkers)) {
    await page.goto(path);
    for (const topicId of topicIds) {
      await expectHelpMarker(shell.root, topicId);
    }
  }
  await page.goto('/voices');
  await expectHelpMarker(shell.root, 'voice-profiles');
  await page.getByTestId('voice-new').click();
  await expect(page.getByTestId('voice-consent-text')).toBeVisible();
  await expectHelpMarker(shell.root, 'voice-consent');
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
