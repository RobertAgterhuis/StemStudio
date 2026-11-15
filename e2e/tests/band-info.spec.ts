import { HelpPanel } from '../pages/help-panel';
import type { TrackDto } from '../support/api';
import { fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';
import { requireOnlineMode } from '../support/feature-skips';
import { beginnerHiddenBandInfoIds, expectNotRendered } from '../support/levels';
import { readFakeProvider } from '../support/online';
import { levelState } from '../support/preferences';

// Feature 005 US2 (005:T043; FR-003, FR-004, FR-008, FR-009, FR-011, FR-014, FR-015; quickstart
// A3). Fake stack; each describe runs only in the STEMSTUDIO_FAKE_ONLINE mode it asserts
// (support/online.ts). Expectations come from tests/fixtures/metadata/fake-provider.json.
const fake = readFakeProvider();
const settleMs = 60_000;
let track: TrackDto;

test.beforeEach(async ({ api, metadataApi }) => {
  test.setTimeout(fakeJobTimeoutMs + 2 * settleMs);
  track = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
  await metadataApi.waitForMetadata(track.id, settleMs);
});

test.describe('band info found', { tag: '@band-info' }, () => {
  test.beforeEach(() => {
    requireOnlineMode('found');
  });

  test('the panel shows the facts with their sources and the Wikipedia attribution', async ({ bandInfo, page }) => {
    const artist = fake.found.artist;
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await expect(bandInfo.status).toHaveAttribute('data-status', 'found');
    await expect(bandInfo.fact('name')).toHaveText(artist.name);
    await expect(bandInfo.fact('type')).toContainText(new RegExp(artist.type, 'i'));
    await expect(bandInfo.fact('years')).toContainText(String(artist.beginYear));
    await expect(bandInfo.fact('summary')).toContainText(artist.summary.extract);
    await expect(bandInfo.fact('country')).toBeVisible();
    for (const member of artist.members) {
      await expect(bandInfo.fact('members')).toContainText(member.name);
    }
    for (const source of fake.found.sources) {
      await expect(bandInfo.fact('sources')).toContainText(source);
    }
    const attribution = bandInfo.attribution;
    await expect(attribution.getByRole('link', { name: artist.summary.title })).toHaveAttribute('href', artist.summary.pageUrl);
    await expect(attribution.getByRole('link', { name: 'Wikipedia contributors' })).toHaveAttribute('href', artist.summary.historyUrl);
    await expect(attribution.getByRole('link', { name: 'CC BY-SA 4.0' })).toHaveAttribute('href', artist.summary.licenceUrl);
    await expect(attribution).toContainText('Excerpt, unmodified');
  });

  test('the mixer shows album and year and opens the same panel', async ({ artwork, bandInfo, mixer }) => {
    await mixer.goto(track.id);
    await expect(artwork.mixerAlbum).toHaveText(fake.found.mixerAlbumText);
    await bandInfo.show();
    await expect(bandInfo.fact('name')).toHaveText(fake.found.artist.name);
  });

  test('a correction is stored and the panel and cover follow it', async ({ artwork, bandInfo, metadataApi, page }) => {
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await bandInfo.submitCorrection('Corrected Title', fake.found.artist.name);
    await expect(bandInfo.status).toHaveAttribute('data-status', 'found', { timeout: settleMs });
    const corrected = await metadataApi.waitForMetadata(track.id, settleMs);
    expect([corrected.metadata.status, corrected.metadata.correctedByUser]).toEqual(['found', true]);
    await expect(artwork.trackCover).toHaveAttribute('data-artwork-source', fake.modes.found.artworkSource ?? 'none');
    expect((await metadataApi.getBandInfo(track.id)).recording?.title).toBe('Corrected Title');
  });
});

test.describe('band info found at Beginner', { tag: '@band-info' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('only name, type, years and summary with attribution are shown (FR-015)', async ({ bandInfo, page }) => {
    requireOnlineMode('found');
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await expect(bandInfo.fact('name')).toBeVisible();
    await expect(bandInfo.fact('summary')).toBeVisible();
    await expect(bandInfo.attribution).toBeVisible();
    await expectNotRendered(page, beginnerHiddenBandInfoIds);
  });
});

test.describe('band info not found or failed', { tag: '@band-info' }, () => {
  test('not found offers retry and correction; a retry settles again without guessing', async ({ bandInfo, metadataApi, page }) => {
    requireOnlineMode('not-found');
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await expect(bandInfo.status).toHaveAttribute('data-status', 'notFound');
    await expect(bandInfo.fact('name')).toHaveCount(0);
    await expect(bandInfo.correct).toBeVisible();
    await bandInfo.retry.click();
    await expect(bandInfo.status).toHaveAttribute('data-status', 'notFound', { timeout: settleMs });
    expect((await metadataApi.waitForMetadata(track.id, settleMs)).metadata.status).toBe('notFound');
  });

  test('failed shows the reason and a retry', async ({ bandInfo, page }) => {
    requireOnlineMode('failed');
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await expect(bandInfo.status).toHaveAttribute('data-status', 'failed');
    await expect(bandInfo.status).toContainText(fake.modes.failed.error ?? '');
    await expect(bandInfo.retry).toBeVisible();
    await expect(bandInfo.correct).toBeVisible();
  });
});

test.describe('online lookups off', { tag: '@band-info' }, () => {
  test('the panel says so and links to the help topic', async ({ bandInfo, metadataApi, page }) => {
    requireOnlineMode('off');
    expect((await metadataApi.getTrack(track.id)).metadata.status).toBe('disabled');
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await expect(bandInfo.disabled).toBeVisible();
    await expect(bandInfo.retry).toHaveCount(0);
    await bandInfo.disabled.getByRole('link').first().click();
    await new HelpPanel(page).expectTopic('online-lookups');
  });
});
