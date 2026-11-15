import type { TrackDto } from '../support/api';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import {
  advancedScoreDialogIds,
  beginnerHiddenKaraokeIds,
  beginnerHiddenScoreDialogIds,
  everyLevelKaraokeIds,
  everyLevelLyricsIds,
  everyLevelScoreDialogIds,
  everyLevelScoreTrackIds,
  expectNotRendered,
  expectRendered,
} from '../support/levels';
import { requireLyricsMode } from '../support/lyrics';
import type { LyricsApi } from '../support/lyrics-api';
import { levelState, type Level } from '../support/preferences';
import { expect, test } from '../support/test';

// Level rules of the 006/008 additions (ui-test-ids 1.6.0 and 1.7.0 "Beginner" columns, R7;
// 006 FR-011, 008 FR-012). Karaoke and lyrics cases need STEMSTUDIO_FAKE_LYRICS=word (the
// default); the sheet music cases run in every lyrics mode. `lyrics-align-device` exists only
// while aligning and `score-detail-*` only with pitched detail stems, so they are not listed.
const settleMs = 60_000;
let track: TrackDto;

/** Ids rendered (`shown`) and not rendered (`hidden`) at one level. */
interface LevelCase {
  readonly level: Level;
  readonly karaoke: { readonly shown: readonly string[]; readonly hidden: readonly string[] };
  readonly lyrics: { readonly shown: readonly string[]; readonly hidden: readonly string[] };
  readonly dialog: { readonly shown: readonly string[]; readonly hidden: readonly string[] };
}

const noTempoNoQuality = ['score-quality', 'score-tempo-required'];

const cases: readonly LevelCase[] = [
  {
    level: 'beginner',
    karaoke: { shown: everyLevelKaraokeIds, hidden: beginnerHiddenKaraokeIds },
    lyrics: { shown: everyLevelLyricsIds, hidden: ['lyrics-remove', 'lyrics-align-device'] },
    dialog: { shown: everyLevelScoreDialogIds, hidden: [...beginnerHiddenScoreDialogIds, 'score-tempo-required'] },
  },
  {
    level: 'advanced',
    karaoke: { shown: [...everyLevelKaraokeIds, ...beginnerHiddenKaraokeIds], hidden: [] },
    lyrics: { shown: [...everyLevelLyricsIds, 'lyrics-remove'], hidden: ['lyrics-align-device'] },
    dialog: { shown: [...everyLevelScoreDialogIds, ...advancedScoreDialogIds], hidden: noTempoNoQuality },
  },
];

/** Word mode only: the shared track's lyrics are found (a karaoke case may have removed them). */
async function wordLyricsFound(lyricsApi: LyricsApi): Promise<void> {
  requireLyricsMode('word');
  await lyricsApi.requireFeature(track.id);
  const current = await lyricsApi.waitForStatus(track.id, ['found', 'removed'], settleMs);
  if (current.status === 'removed') {
    expect(await lyricsApi.search(track.id)).toBe(202);
  }
  await lyricsApi.waitForStatus(track.id, ['found'], settleMs);
}

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + 2 * settleMs);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

for (const { level, karaoke, lyrics: card, dialog } of cases) {
  test.describe(`at ${level}`, () => {
    test.use({ storageState: levelState(level) });

    test('the karaoke banner keeps toggle and credit; size and offset follow the level', { tag: '@karaoke' }, async ({ lyricsApi, mixer, page }) => {
      await wordLyricsFound(lyricsApi);
      await mixer.goto(track.id);
      await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', level);
      await expectRendered(page, karaoke.shown);
      await expectNotRendered(page, karaoke.hidden);
    });

    test('the lyrics card keeps status, search and paste; remove follows the level', { tag: '@lyrics' }, async ({ lyrics, lyricsApi, page }) => {
      await wordLyricsFound(lyricsApi);
      await lyrics.goto(track.id);
      await lyrics.waitForStatus('found');
      await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', level);
      await expectRendered(page, card.shown);
      await expectNotRendered(page, card.hidden);
    });

    test('the sheet music dialog keeps profile and rhythm; key and range follow the level', { tag: '@sheet-music' }, async ({ page, scoreApi, scoreDialog, trackPage }) => {
      await scoreApi.requireFeature(track.id);
      await trackPage.goto(track.id);
      await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', level);
      await expectRendered(page, everyLevelScoreTrackIds(fakeStems));
      await scoreDialog.openForStem('bass');
      await expectRendered(page, dialog.shown);
      await expectNotRendered(page, dialog.hidden);
    });
  });
}
