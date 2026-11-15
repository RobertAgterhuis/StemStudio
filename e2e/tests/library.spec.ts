import type { TrackDto } from '../support/api';
import type { AssetDto, AssetsApi, AudioFileFormat } from '../support/assets-api';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from '../support/env';
import { expect, test } from '../support/test';
import { fixturePath } from '../support/fixtures';
import { uniqueWavCopy } from '../support/unique-audio';

// Feature 007 US3 (007:T053; FR-007..FR-009, FR-012; quickstart X8). Items are created through
// the API (the dialog is covered by export.spec.ts); the library page is driven through the UI.
const renderMs = 60_000;

async function saveRaw(assetsApi: AssetsApi, track: TrackDto, stem: string, format: AudioFileFormat): Promise<AssetDto> {
  const name = `Library ${stem} ${runId()}-${String(Date.now())}`;
  const created = await assetsApi.createExport({ trackId: track.id, mode: 'raw', channels: [stem], format: { format }, name });
  return assetsApi.waitForReady(created.id, renderMs);
}

test.describe('library management', { tag: '@library' }, () => {
  test('filter, rename (download name follows) and delete (gone after reload)', async ({ api, assetsApi, library, page }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs + 4 * renderMs);
    const track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
    const other = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
    const [bass, drums, vocals] = [
      await saveRaw(assetsApi, track, 'bass', 'flac'),
      await saveRaw(assetsApi, track, 'drums', 'wav'),
      await saveRaw(assetsApi, track, 'vocals', 'mp3'),
    ];
    const elsewhere = await saveRaw(assetsApi, other, 'bass', 'flac');

    await library.goto();
    await expect(library.byId('library-total-size')).toHaveText(/\d+(\.\d+)?\s?(B|KB|MB|GB)/);
    await library.filterByTrack(track.id);
    for (const item of [bass, drums, vocals]) {
      await expect(library.row(item.id)).toBeVisible();
    }
    await expect(library.row(elsewhere.id)).toHaveCount(0);

    const renamed = `Practice bass ${runId()}-${testInfo.project.name}`;
    await library.rename(bass.id, renamed);
    const download = page.waitForEvent('download');
    await library.download(bass.id).click();
    expect((await download).suggestedFilename()).toBe(`${renamed}.flac`);

    await library.delete(drums.id);
    await page.reload();
    await library.byId('library-total-size').waitFor();
    await expect(library.row(drums.id)).toHaveCount(0);
    expect(await assetsApi.find(drums.id)).toBeUndefined();
  });

  test('items of a deleted track stay, are marked and still play', async ({ api, assetsApi, library }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs + 2 * renderMs);
    const copy = uniqueWavCopy(fixturePath('click_90bpm_3-4_12s.wav'), `${runId()}-library-${testInfo.project.name}`);
    const track = await api.ensureFileCompleted(copy, fakeJobTimeoutMs);
    const item = await saveRaw(assetsApi, track, 'drums', 'flac');
    // A new import starts its detail phase (002); a track cannot be deleted while it is active.
    await api.stopDetail(track.id, detailPhaseTimeoutMs);
    expect(await api.deleteTrack(track.id)).toBe(204);

    await library.goto();
    await expect(library.source(item.id)).toHaveAttribute('data-track-available', 'false');
    await expect(library.row(item.id)).toHaveAttribute('data-status', 'ready');
    expect(await library.playerDuration(item.id)).toBeGreaterThan(0);
    expect((await assetsApi.get(item.id)).provenance.trackAvailable).toBe(false);
  });
});
