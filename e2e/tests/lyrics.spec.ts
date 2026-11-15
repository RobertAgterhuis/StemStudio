import type { LyricsSection } from '../pages/lyrics-section';
import type { StemStudioApi, TrackDto } from '../support/api';
import { fakeDeviceMode, fakeJobTimeoutMs, runId } from '../support/env';
import { fixturePath } from '../support/fixtures';
import { fakeAlignedLines, readFakeLyrics, requireFakeDevice, requireLyricsMode } from '../support/lyrics';
import type { LyricsApi } from '../support/lyrics-api';
import { expect, test } from '../support/test';
import { uniqueWavCopy } from '../support/unique-audio';

// Feature 006 US2 + lookup outcomes (006:T048; FR-001, FR-004, FR-010, FR-012; SC-005;
// quickstart L3). Fake stack; each case runs only in the STEMSTUDIO_FAKE_LYRICS mode (and fake
// device) it asserts. Every case imports its own copy of a fixture, so the lyrics state of one
// case never leaks into another. Expectations: tests/fixtures/lyrics/fake-lyrics.json.
const fake = readFakeLyrics();
const settleMs = 60_000;
const alignMs = 90_000;

async function freshTrack(api: StemStudioApi, lyricsApi: LyricsApi, salt: string): Promise<TrackDto> {
  const copy = uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `lyrics-${salt}-${runId()}`);
  const track = await api.ensureFileCompleted(copy, fakeJobTimeoutMs);
  await lyricsApi.requireFeature(track.id);
  return track;
}

/** On a CPU worker pasted text waits for a click (US2-2); a GPU worker aligns by itself. */
async function alignOnCpu(lyrics: LyricsSection): Promise<void> {
  if (fakeDeviceMode === 'cpu') {
    await lyrics.waitForStatus('alignPending', settleMs);
    await lyrics.align.click();
  }
}

test.beforeEach(() => {
  test.setTimeout(fakeJobTimeoutMs + settleMs + alignMs);
});

test.describe('plain lyrics', { tag: '@lyrics' }, () => {
  test.beforeEach(() => {
    requireLyricsMode('plain');
  });

  test('on a GPU worker the alignment starts by itself and the banner sings aligned words (US2-1)', async ({ api, karaoke, lyrics, lyricsApi, mixer }, info) => {
    requireFakeDevice('gpu');
    const track = await freshTrack(api, lyricsApi, `gpu-${info.project.name}`);
    const aligned = await lyricsApi.waitForStatus(track.id, ['found', 'failed'], settleMs + alignMs);
    expect([aligned.status, aligned.origin, aligned.credit]).toEqual(['found', 'aligned', fake.modes.plain.alignedCredit]);
    expect(aligned.lines.map((l) => l.words.map((w) => w.startMs))).toEqual(fakeAlignedLines(fake.text.plainText, fake.fakeAlignment).map((l) => l.words.map((w) => w.startMs)));
    await lyrics.goto(track.id);
    await expect(lyrics.credit).toHaveText(fake.modes.plain.alignedCredit ?? '');
    await mixer.goto(track.id);
    await expect(karaoke.host).toHaveAttribute('data-sync', 'aligned');
  });

  test('on a CPU worker alignment waits for a click and shows the estimate (US2-2, SC-005)', async ({ api, karaoke, lyrics, lyricsApi, mixer }, info) => {
    requireFakeDevice('cpu');
    const track = await freshTrack(api, lyricsApi, `cpu-${info.project.name}`);
    await lyricsApi.waitForStatus(track.id, ['alignPending'], settleMs);
    await lyrics.goto(track.id);
    await expect(lyrics.status).toHaveAttribute('data-status', 'alignPending');
    await expect(lyrics.preview).toContainText(fake.lines[0]?.text ?? '');
    await expect(lyrics.alignEstimate).toContainText(/CPU/iu);
    await lyrics.align.click();
    await lyrics.waitForStatus('found', alignMs);
    await mixer.goto(track.id);
    await expect(karaoke.host).toHaveAttribute('data-sync', 'aligned');
  });

  test('pasted lyrics are aligned and credited to the user (US2-3)', async ({ api, lyrics, lyricsApi }, info) => {
    const track = await freshTrack(api, lyricsApi, `paste-${info.project.name}`);
    await lyricsApi.waitForStatus(track.id, ['alignPending', 'found'], settleMs + alignMs);
    await lyrics.goto(track.id);
    await lyrics.paste(fake.text.pastedText);
    await alignOnCpu(lyrics);
    await lyrics.waitForStatus('found', alignMs);
    const pasted = await lyricsApi.get(track.id);
    expect([pasted.source, pasted.origin]).toEqual(['user', 'aligned']);
    expect(pasted.credit).toBe('Lyrics: entered by you; timing: automatic alignment');
    expect(pasted.lines.map((l) => l.text)).toEqual(fake.text.pastedText.split('\n'));
  });
});

test.describe('lyrics not found', { tag: '@lyrics' }, () => {
  test('the track page offers search again and the mixer explains why (US1-4)', async ({ api, karaoke, lyrics, lyricsApi, mixer }, info) => {
    requireLyricsMode('not-found');
    const track = await freshTrack(api, lyricsApi, `nf-${info.project.name}`);
    await lyricsApi.waitForStatus(track.id, ['notFound'], settleMs);
    await lyrics.goto(track.id);
    await expect(lyrics.search).toBeVisible();
    await expect(lyrics.byId('lyrics-paste-open')).toBeVisible();
    await mixer.goto(track.id);
    await karaoke.expectUnavailable('notFound');
  });
});

test.describe('lyrics lookup failed', { tag: '@lyrics' }, () => {
  test('the reason is shown and search again retries (FR-012)', async ({ api, lyrics, lyricsApi }, info) => {
    requireLyricsMode('failed');
    const track = await freshTrack(api, lyricsApi, `failed-${info.project.name}`);
    await lyricsApi.waitForStatus(track.id, ['failed'], settleMs);
    await lyrics.goto(track.id);
    await expect(lyrics.error).toHaveText(fake.modes.failed.error ?? '');
    await lyrics.search.click();
    await lyrics.waitForStatus('failed', settleMs);
    await expect(lyrics.error).toHaveText(fake.modes.failed.error ?? '');
  });
});

test.describe('lyrics stay in the app', { tag: '@lyrics' }, () => {
  test('lyrics responses are never cached and the track list carries no text (FR-010)', async ({ api, lyricsApi }, info) => {
    requireLyricsMode('word');
    const track = await freshTrack(api, lyricsApi, `privacy-${info.project.name}`);
    await lyricsApi.waitForStatus(track.id, ['found'], settleMs);
    const response = await lyricsApi.getResponse(track.id);
    expect(response.headers()['cache-control']).toContain('no-store');
    const listed = JSON.stringify(await api.listTracks());
    for (const word of ['Second fake line for the ball', 'Last fake line']) {
      expect(listed).not.toContain(word);
    }
  });
});
