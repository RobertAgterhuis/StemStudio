import { HelpPanel } from '../pages/help-panel';
import type { TrackDto } from '../support/api';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { expect, test } from '../support/test';
import { requireOnlineMode } from '../support/feature-skips';
import { beginnerHiddenBandInfoIds, expectNotRendered } from '../support/levels';
import { fakeOnlineMode, readFakeProvider } from '../support/online';
import { fixturePath } from '../support/fixtures';
import { uniqueWavCopy } from '../support/unique-audio';
import { levelState } from '../support/preferences';

// Feature 005 US2 (005:T043; FR-003, FR-004, FR-008, FR-009, FR-011, FR-014, FR-015; quickstart
// A3). Fake stack; each describe runs only in the STEMSTUDIO_FAKE_ONLINE mode it asserts
// (support/online.ts). Expectations come from tests/fixtures/metadata/fake-provider.json.
const fake = readFakeProvider();
const settleMs = 60_000;
let track: TrackDto;

// A track of its own per run and mode: a lookup result is stored, so a track looked up while the
// stack ran in another STEMSTUDIO_FAKE_ONLINE mode (only the worker restarts) keeps that result.
const ownTrack = (): string => uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `band-info-${fakeOnlineMode}-${runId()}`);

test.beforeEach(async ({ api, metadataApi }) => {
  test.setTimeout(fakeJobTimeoutMs + 2 * settleMs);
  track = await api.ensureFileCompleted(ownTrack(), fakeJobTimeoutMs);
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

  test('a correction is stored and the panel and cover follow it', async ({ artwork, bandInfo, metadataApi, page }, testInfo) => {
    // A title of its own per run, so a correction stored by an earlier run cannot satisfy the wait.
    const title = `Corrected ${runId()} ${testInfo.project.name}`;
    await page.goto(`/tracks/${track.id}`);
    await bandInfo.show();
    await bandInfo.submitCorrection(title, fake.found.artist.name);
    const corrected = async (): Promise<[string, string | undefined]> => {
      const info = await metadataApi.getBandInfo(track.id);
      return [info.status, info.recording?.title];
    };
    await expect.poll(corrected, { timeout: settleMs }).toEqual(['found', title]);
    expect((await metadataApi.getTrack(track.id)).metadata.correctedByUser).toBe(true);
    await expect(bandInfo.status).toHaveAttribute('data-status', 'found', { timeout: settleMs });
    await expect(artwork.trackCover).toHaveAttribute('data-artwork-source', fake.modes.found.artworkSource ?? 'none');
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
