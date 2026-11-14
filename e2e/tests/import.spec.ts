import type { ImportPage } from '../pages/import-page';
import type { StemName } from '../support/api';
import { fakeJobTimeoutMs } from '../support/env';
import { fixtureFacts, fixturePath, type FixtureName } from '../support/fixtures';
import { expect, test } from '../support/test';

// US1 import against the fake-separator stack (T081; SC-006, SC-007, G1, G6).
const sixStems: readonly StemName[] = ['vocals', 'drums', 'bass', 'guitar', 'piano', 'other'];
const music: FixtureName = 'music_100bpm_20s.mp3';

test('shows the personal-practice notice and the stem limitation help', async ({ importPage }) => {
  await importPage.goto();
  await expect(importPage.legalNotice).toContainText(/personal practice/i);
  await expect(importPage.legalNotice).toContainText(/copyright/i);
  await expect(importPage.stemLimitationHelp).toBeVisible();
});

test('upload reaches completed with six fake stems and the fixture tempo', async ({
  importPage,
  trackPage,
}) => {
  await importPage.goto();
  await importPage.uploadFile(fixturePath(music));
  await trackPage.waitForTrackId();
  await trackPage.waitForStatus('completed', fakeJobTimeoutMs);
  await expect(trackPage.progress).toHaveAttribute('aria-valuenow', '100');
  await expect(trackPage.model).toContainText('fake');
  for (const stem of sixStems) {
    await expect(trackPage.stem(stem)).toBeVisible();
  }
  const bpmError = Math.abs((await trackPage.bpmValue()) - (fixtureFacts(music).bpm ?? 0));
  expect(bpmError, 'detected BPM within 3 of the fixture tempo').toBeLessThanOrEqual(3);
});

test('re-uploading a processed file shows the same track ready within 2 s (SC-006)', async ({
  api,
  importPage,
  trackPage,
}) => {
  const track = await api.ensureCompleted(music, fakeJobTimeoutMs);
  await importPage.goto();
  const started = Date.now();
  await importPage.uploadFile(fixturePath(music));
  expect(await trackPage.waitForTrackId()).toBe(track.id);
  await trackPage.waitForStatus('completed', 2_000);
  expect(Date.now() - started).toBeLessThan(2_000);
});

test('streams a multi-megabyte WAV upload to completion (G1)', async ({ api, importPage, trackPage }) => {
  await importPage.goto();
  await importPage.uploadFile(fixturePath('upload_4mb.wav'));
  const trackId = await trackPage.waitForTrackId(60_000);
  await trackPage.waitForStatus('completed', fakeJobTimeoutMs);
  const track = await api.getTrack(trackId);
  expect(track.durationSeconds).toBeCloseTo(fixtureFacts('upload_4mb.wav').durationSeconds ?? 0, 0);
});

test('a corrupt MP3 ends failed with a readable reason and can be retried', async ({
  api,
  importPage,
  trackPage,
}) => {
  await importPage.goto();
  await importPage.uploadFile(fixturePath('corrupt.mp3'));
  const trackId = await trackPage.waitForTrackId();
  await trackPage.waitForStatus('failed', fakeJobTimeoutMs);
  await expect(trackPage.error).not.toBeEmpty();
  const before = (await api.getTrack(trackId)).updatedAt;
  await trackPage.retry.click();
  await expect.poll(async () => (await api.getTrack(trackId)).updatedAt).not.toBe(before);
  await trackPage.waitForStatus('failed', fakeJobTimeoutMs);
  await expect(trackPage.error).not.toBeEmpty();
});

test('track list opens the track and the mixer (FR-018)', async ({ api, page, trackList, trackPage }) => {
  const track = await api.ensureCompleted(music, fakeJobTimeoutMs);
  await trackList.goto();
  await expect(trackList.rowStatus(track.id)).toContainText(/completed/i);
  await trackList.open(track.id);
  expect(await trackPage.waitForTrackId()).toBe(track.id);
  await trackPage.openMixer.click();
  await page.waitForURL(`**/tracks/${track.id}/mixer`);
  await expect(page.getByTestId('strip-vocals')).toBeVisible();
});

interface Reject {
  readonly name: string;
  readonly submit: (importPage: ImportPage) => Promise<void>;
}

const rejects: readonly Reject[] = [
  { name: 'an unsupported .ogg file', submit: (p) => p.uploadFile(fixturePath('unsupported.ogg')) },
  { name: 'a vimeo.com URL', submit: (p) => p.importUrl('https://vimeo.com/76979871') },
  {
    name: 'a YouTube playlist URL',
    submit: (p) => p.importUrl('https://www.youtube.com/playlist?list=PL590L5WQmH8fJ54F369BLDSqIwcs-TCfs'),
  },
];

for (const reject of rejects) {
  test(`rejects ${reject.name} with a reason and creates no track (SC-007)`, async ({
    api,
    importPage,
    page,
  }) => {
    const tracksBefore = (await api.listTracks()).length;
    await importPage.goto();
    await reject.submit(importPage);
    await expect(importPage.error).toBeVisible();
    await expect(importPage.error).not.toBeEmpty();
    await expect(page).toHaveURL(/\/$/);
    expect(await api.listTracks()).toHaveLength(tracksBefore);
  });
}
