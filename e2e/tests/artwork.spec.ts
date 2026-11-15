import type { Locator } from '@playwright/test';
import type { ShownArtworkSource } from '../pages/artwork-view';
import { artworkFacts, artworkFixturePath, type ArtworkFixtureName } from '../support/artwork-fixtures';
import { fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';
import { requireEnv, requireNetwork, requireOnlineMode, requirePerf } from '../support/feature-skips';
import type { ArtworkDto, TrackWithArtworkDto } from '../support/metadata-api';
import { levelState } from '../support/preferences';
import { seedArtworkTracks } from '../support/seed-artwork';

// Feature 005 US1 + US3 (005:T030, T050; FR-001, FR-002, FR-005..FR-007, FR-013, FR-015;
// SC-001, SC-003; quickstart A2, A9). Fake stack. Embedded covers outrank every automatic
// picture (data-model 3.1), so those cases run in every STEMSTUDIO_FAKE_ONLINE mode; the
// placeholder case needs a mode in which the fake provider offers no Cover Art Archive picture.
const embedded: readonly ArtworkFixtureName[] = ['cover_embedded_10s.mp3', 'cover_embedded_10s.flac'];

function artworkOf(track: TrackWithArtworkDto): ArtworkDto {
  if (track.artwork == null) throw new Error(`track ${track.id} has no artwork`);
  return track.artwork;
}

async function expectSource(element: Locator, source: ShownArtworkSource): Promise<void> {
  await expect(element).toHaveAttribute('data-artwork-source', source, { timeout: 30_000 });
}

test.describe('artwork from the import', { tag: '@artwork' }, () => {
  for (const name of embedded) {
    test(`an uploaded ${artworkFacts(name).format.toUpperCase()} shows its embedded cover everywhere`, async ({
      importPage, trackPage, trackList, mixer, artwork, metadataApi, page,
    }) => {
      test.setTimeout(fakeJobTimeoutMs + 60_000);
      await importPage.goto();
      await importPage.uploadFile(artworkFixturePath(name));
      const trackId = await trackPage.waitForTrackId();
      await expectSource(artwork.trackCover, 'embedded');
      const track = await metadataApi.getTrack(trackId);
      await expect(artwork.trackCover).toHaveAttribute('alt', `Cover of ${track.title}`);
      await trackPage.waitForStatus('completed', fakeJobTimeoutMs);

      await trackList.goto();
      await expectSource(artwork.thumb(trackId), 'embedded');
      await expect(artwork.thumb(trackId)).toHaveAttribute('loading', 'lazy');
      await expect(artwork.thumb(trackId)).toHaveAttribute('decoding', 'async');

      await mixer.goto(trackId);
      await expectSource(artwork.mixerCover, 'embedded');
      await page.goto(`/tracks/${trackId}`);
      await expectSource(artwork.trackCover, 'embedded');
    });
  }

  test('a WAV without tags shows the placeholder on all three screens', async ({ api, trackList, mixer, artwork, page }) => {
    requireOnlineMode('not-found', 'failed', 'off');
    test.setTimeout(fakeJobTimeoutMs + 60_000);
    const track = await api.ensureCompleted('click_90bpm_3-4_12s.wav', fakeJobTimeoutMs);
    expect(track.status).toBe('completed');
    await page.goto(`/tracks/${track.id}`);
    await expectSource(artwork.trackCover, 'none');
    await trackList.goto();
    await expectSource(artwork.thumb(track.id), 'none');
    await mixer.goto(track.id);
    await expectSource(artwork.mixerCover, 'none');
  });

  test('pictures are small, versioned and cached', async ({ api, metadataApi }) => {
    test.setTimeout(fakeJobTimeoutMs + 30_000);
    const track = await metadataApi.getTrack((await api.ensureFileCompleted(artworkFixturePath('cover_embedded_10s.mp3'), fakeJobTimeoutMs)).id);
    const art = artworkOf(track);
    expect(art.source).toBe('embedded');
    const cover = await metadataApi.fetchArtwork(art.coverUrl);
    const thumb = await metadataApi.fetchArtwork(art.thumbUrl);
    expect([cover.status, cover.contentType, thumb.contentType]).toEqual([200, 'image/jpeg', 'image/jpeg']);
    expect(cover.bytes).toBeLessThanOrEqual(200 * 1024);
    expect(thumb.bytes).toBeLessThanOrEqual(20 * 1024);
    expect(cover.cacheControl).toContain('immutable');
    expect((await metadataApi.fetchArtwork(art.coverUrl, cover.etag)).status).toBe(304);
    expect((await metadataApi.fetchArtwork(`/api/tracks/${track.id}/artwork/cover`)).cacheControl).toContain('no-cache');
  });
});

test.describe('artwork of real YouTube and Spotify imports', { tag: ['@artwork', '@online'] }, () => {
  const links = [
    { name: 'YouTube', variable: 'E2E_YOUTUBE_URL', source: 'youtube' },
    { name: 'Spotify', variable: 'E2E_SPOTIFY_URL', source: 'spotify' },
  ] as const;
  for (const link of links) {
    test(`a ${link.name} import shows the ${link.source} picture as soon as the download is done`, async ({ importPage, trackPage, artwork }) => {
      requireNetwork();
      const url = requireEnv(link.variable);
      test.setTimeout(10 * 60_000);
      await importPage.goto();
      await importPage.importUrl(url);
      expect(await trackPage.waitForTrackId()).not.toBe('');
      await expectSource(artwork.trackCover, link.source);
    });
  }
});

test.describe('own picture', { tag: '@artwork' }, () => {
  let trackId = '';

  test.beforeEach(async ({ api, metadataApi }) => {
    test.setTimeout(fakeJobTimeoutMs + 60_000);
    trackId = (await api.ensureFileCompleted(artworkFixturePath('cover_embedded_10s.mp3'), fakeJobTimeoutMs)).id;
    await metadataApi.resetArtwork(trackId);
  });

  test('upload a picture, see it in the library, then go back to the automatic one', async ({ artwork, trackList, page, metadataApi }) => {
    await page.goto(`/tracks/${trackId}`);
    await expectSource(artwork.trackCover, 'embedded');
    await expect(artwork.reset).toHaveCount(0);
    await artwork.uploadPicture(artworkFixturePath('own_picture_800x600.png'));
    await expectSource(artwork.trackCover, 'user');
    expect((await metadataApi.getTrack(trackId)).artwork?.source).toBe('user');

    await trackList.goto();
    await expectSource(artwork.thumb(trackId), 'user');

    await page.goto(`/tracks/${trackId}`);
    await artwork.reset.click();
    await expectSource(artwork.trackCover, 'embedded');
    await expect(artwork.reset).toHaveCount(0);
  });

  for (const bad of ['not_a_picture.png', 'unsupported_picture.gif'] as const) {
    test(`${bad} is refused with a readable reason (HTTP ${String(artworkFacts(bad).uploadStatus)})`, async ({ artwork, page, metadataApi }) => {
      expect((await metadataApi.setArtwork(trackId, artworkFixturePath(bad))).status).toBe(artworkFacts(bad).uploadStatus);
      await page.goto(`/tracks/${trackId}`);
      await artwork.uploadPicture(artworkFixturePath(bad));
      await expect(artwork.error).toBeVisible();
      await expect(artwork.error).not.toBeEmpty();
      await expectSource(artwork.trackCover, 'embedded');
    });
  }
});

test.describe('own picture at Beginner', { tag: '@artwork' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('the picture actions are not rendered (FR-015)', async ({ api, artwork, page }) => {
    test.setTimeout(fakeJobTimeoutMs + 30_000);
    const track = await api.ensureFileCompleted(artworkFixturePath('cover_embedded_10s.mp3'), fakeJobTimeoutMs);
    await page.goto(`/tracks/${track.id}`);
    await expectSource(artwork.trackCover, 'embedded');
    await expect(artwork.uploadInput).toHaveCount(0);
    await expect(artwork.reset).toHaveCount(0);
  });
});

test.describe('library with 100 pictures', { tag: ['@artwork', '@perf'] }, () => {
  test('first screen within 1 s, lazy thumbnails of at most 20 KB (SC-003)', async ({ api, metadataApi, trackList, artwork, page }) => {
    requirePerf();
    test.setTimeout(100 * fakeJobTimeoutMs);
    const ids = await seedArtworkTracks({ api, metadataApi }, 100);
    const started = Date.now();
    await trackList.goto();
    await expect(page.locator('[data-testid^="track-thumb-"]').first()).toBeVisible();
    expect(Date.now() - started).toBeLessThan(1000);
    for (const id of ids.slice(0, 100)) {
      const thumbUrl = (await metadataApi.getTrack(id)).artwork?.thumbUrl ?? '';
      expect((await metadataApi.fetchArtwork(thumbUrl)).bytes).toBeLessThanOrEqual(20 * 1024);
    }
    await expect(artwork.thumb(ids[0] ?? '')).toHaveAttribute('loading', 'lazy');
  });
});
