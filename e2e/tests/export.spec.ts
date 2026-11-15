import type { StemName, TrackDto } from '../support/api';
import type { Page } from '@playwright/test';
import { dbDifference, mixRms, samplesEqual, wavFacts, type MixInput } from '../support/audio-check';
import type { AssetDto, AssetsApi } from '../support/assets-api';
import { setNumericValue, setToggle } from '../support/controls';
import { ensureDetailCompleted, fakeStems } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { expectHelpMarker } from '../support/help';
import { expect, test } from '../support/test';
import { panMatrix } from '../support/render-vectors';

// Feature 007 US1, US2, US4, US5 (007:T041, T047, T058, T063; FR-001..FR-006, FR-010, FR-013;
// SC-003, SC-004; quickstart X2, X5, X7). Fake stack: the fake separator copies the mix into
// every stem, so expected levels are computed from the served stems and the shared pan vectors.
const renderMs = 60_000;
let track: TrackDto;

async function stemBytes(assetsApi: AssetsApi, stem: StemName): Promise<Buffer> {
  const url = track.stems.find((s) => s.name === stem)?.url ?? `/api/tracks/${track.id}/stems/${stem}`;
  return assetsApi.bytes(url);
}

/** A stem of an expected mix at its volume (0..1) and pan (-1..1). */
interface ExpectedChannel {
  readonly stem: StemName;
  readonly gain: number;
  readonly pan: number;
}

/** What a mixed export must contain: these channels, summed and scaled by `master`. */
interface ExpectedMix {
  readonly asset: AssetDto;
  readonly channels: readonly ExpectedChannel[];
  readonly master: number;
}

/**
 * Downloads the 16-bit WAV of `expected.asset` and asserts that its RMS per channel matches the
 * served stems mixed with the shared pan vectors (+/- 0.1 dB, after the applied clip protection).
 */
async function expectVectorLaw(scratch: Page, assetsApi: AssetsApi, expected: ExpectedMix): Promise<void> {
  const file = (await assetsApi.download(expected.asset.id)).bytes;
  expect(wavFacts(file)).toMatchObject({ sampleRate: 44_100, channels: 2, bitsPerSample: 16 });
  const lowered = 10 ** ((expected.asset.audio?.appliedGainDb ?? 0) / 20);
  const inputs: MixInput[] = [];
  for (const channel of expected.channels) {
    const base64 = (await stemBytes(assetsApi, channel.stem)).toString('base64');
    inputs.push({ base64, gain: channel.gain, matrix: panMatrix(channel.pan) });
  }
  const reference = await mixRms(scratch, inputs, expected.master * lowered);
  const actual = await mixRms(scratch, [{ base64: file.toString('base64'), gain: 1, matrix: panMatrix(0) }], 1);
  expect(Math.abs(dbDifference(actual[0], reference[0]))).toBeLessThanOrEqual(0.1);
  expect(Math.abs(dbDifference(actual[1], reference[1]))).toBeLessThanOrEqual(0.1);
}

async function readyFromConfirmation(id: string, assetsApi: AssetsApi): Promise<AssetDto> {
  return assetsApi.waitForReady(id, renderMs);
}

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + 3 * renderMs);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.describe('raw stem', { tag: '@export' }, () => {
  test('bass as FLAC 16 is bit-identical to the stored stem (SC-004)', async ({ trackPage, exportDialog, library, assetsApi, scratch }) => {
    await trackPage.goto(track.id);
    await exportDialog.openForStem('bass');
    await exportDialog.save({ format: 'flac', bitDepth: 16 });
    const id = await exportDialog.savedAssetId();
    await exportDialog.savedOpen.click();
    await library.waitForStatus(id, 'ready', renderMs);
    // Names are unique across the library (a score of the bass shares the default name), so a
    // stack that already holds one adds " (n)".
    await expect(library.name(id)).toHaveText(new RegExp(`^${track.title} - bass( \\(\\d+\\))?$`, 'u'));
    const asset = await readyFromConfirmation(id, assetsApi);
    expect([asset.audio?.format, asset.audio?.bitDepth]).toEqual(['flac', 16]);
    const file = await assetsApi.download(id);
    expect(await samplesEqual(scratch, file.bytes, await stemBytes(assetsApi, 'bass'))).toBe(true);
  });
});

test.describe('raw detail stem', { tag: ['@export', '@detail'] }, () => {
  test('drums.snare as MP3 plays and downloads as .mp3; unsplit parents offer nothing', async ({ api, trackPage, exportDialog, library, assetsApi, page }) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 2 * renderMs);
    const band = await ensureDetailCompleted(api);
    await trackPage.goto(band.id);
    await expect(page.locator('[data-testid^="export-detail-bass-"]')).toHaveCount(0);
    await expect(page.locator('[data-testid^="export-detail-guitar-"]')).toHaveCount(0);
    await exportDialog.openForDetail('drums', 'snare');
    await exportDialog.save({ format: 'mp3', mp3Bitrate: 320 });
    const id = await exportDialog.savedAssetId();
    await readyFromConfirmation(id, assetsApi);
    await library.goto();
    expect(await library.playerDuration(id)).toBeGreaterThan(0);
    const file = await assetsApi.download(id);
    expect(file.contentType).toBe('audio/mpeg');
    expect(file.disposition).toMatch(/filename="[^"]+\.mp3"/);
  });
});

test.describe('what I hear', { tag: '@export' }, () => {
  test('solo drums + bass, bass pan -40 %, master 70 %: the WAV matches the vector law (+/- 0.1 dB)', async ({ mixer, exportDialog, assetsApi, scratch }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await setToggle(mixer.solo('drums'), true);
    await setToggle(mixer.solo('bass'), true);
    await setNumericValue(mixer.pan('bass'), -40);
    await setNumericValue(mixer.byId('master-fader'), 70);
    await setToggle(mixer.byId('metronome-toggle'), true);
    await expect(mixer.byId('unsaved-indicator')).toBeVisible();

    await exportDialog.openForMix();
    await expect(exportDialog.byId('export-source-audible')).toBeChecked();
    await exportDialog.save({ format: 'wav', bitDepth: 16 });
    const asset = await readyFromConfirmation(await exportDialog.savedAssetId(), assetsApi);
    expect(asset.provenance.contents).toMatch(/^drums \+ bass, mixed/);
    const channels = [{ stem: 'drums', gain: 0.8, pan: 0 }, { stem: 'bass', gain: 0.8, pan: -0.4 }] as const;
    await expectVectorLaw(scratch, assetsApi, { asset, channels, master: 0.7 });
  });

  test('"Selected channels" drums + bass: only those two, at fader and pan, mute and solo ignored (US2-3)', async ({ mixer, exportDialog, assetsApi, scratch }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await setNumericValue(mixer.fader('drums'), 60);
    await setToggle(mixer.mute('drums'), true);
    await setNumericValue(mixer.pan('bass'), -40);
    await setToggle(mixer.solo('vocals'), true);
    await setNumericValue(mixer.byId('master-fader'), 70);

    await exportDialog.openForMix();
    await exportDialog.save({ selectedChannels: ['drums', 'bass'], format: 'wav', bitDepth: 16 });
    const asset = await readyFromConfirmation(await exportDialog.savedAssetId(), assetsApi);
    expect(asset.provenance.contents).toMatch(/^drums \+ bass, mixed/);
    const channels = [{ stem: 'drums', gain: 0.6, pan: 0 }, { stem: 'bass', gain: 0.8, pan: -0.4 }] as const;
    await expectVectorLaw(scratch, assetsApi, { asset, channels, master: 0.7 });
  });

  test('a clipping mix is lowered and the library says by how much (FR-005)', async ({ mixer, exportDialog, library, assetsApi }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    for (const stem of fakeStems) {
      await setNumericValue(mixer.fader(stem), 100);
      await setNumericValue(mixer.pan(stem), -100);
    }
    await exportDialog.openForMix();
    await exportDialog.save({ format: 'wav', bitDepth: 24 });
    const asset = await readyFromConfirmation(await exportDialog.savedAssetId(), assetsApi);
    expect(asset.audio?.appliedGainDb).toBeLessThan(0);
    expect(asset.audio?.peakDbfs).toBeCloseTo(-0.1, 2);
    await library.goto();
    await expect(library.gainNote(asset.id)).toContainText(/Lowered by .* dB/);
    await expectHelpMarker(library.gainNote(asset.id), 'export-clipping');
  });
});

test.describe('loop region', { tag: '@export' }, () => {
  test('"Only the loop region" 2.0-4.5 s gives a 2.500 s file (+/- 1 ms)', async ({ mixer, exportDialog, assetsApi }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await mixer.setLoop(2, 4.5, true);
    await exportDialog.openForMix();
    await exportDialog.save({ format: 'wav', bitDepth: 16, loopOnly: true });
    const asset = await readyFromConfirmation(await exportDialog.savedAssetId(), assetsApi);
    const facts = wavFacts((await assetsApi.download(asset.id)).bytes);
    expect(Math.abs(facts.durationSeconds - 2.5)).toBeLessThanOrEqual(0.001);
  });
});

// US4-2 (FR-006; ExportRange: 0 <= start < end <= track duration, length >= 0.5 s). The fixture
// is 20 s long. The dialog may refuse on input (submit disabled) or show the API's 400/422.
const invalidRanges = [
  { name: 'shorter than 0.5 s', range: { start: 3, end: 3.2 } },
  { name: 'ending before it starts', range: { start: 6, end: 4 } },
  { name: 'reaching past the end of the song', range: { start: 18, end: 25 } },
] as const;

test.describe('range validation', { tag: '@export' }, () => {
  for (const { name, range } of invalidRanges) {
    test(`a range ${name} shows a readable reason and saves nothing (US4-2)`, async ({ mixer, exportDialog, assetsApi }) => {
      const before = (await assetsApi.list({ trackId: track.id })).total;
      await mixer.goto(track.id);
      await mixer.waitForReady(fakeStems);
      await exportDialog.openForMix();
      await exportDialog.attempt({ format: 'wav', range });
      await expect(exportDialog.error).toBeVisible();
      await expect(exportDialog.error).not.toBeEmpty();
      await expect(exportDialog.saved).toHaveCount(0);
      expect((await assetsApi.list({ trackId: track.id })).total).toBe(before);
    });
  }
});

test.describe('stem pack', { tag: '@export' }, () => {
  test('six raw items appear and all become ready (fake track without detail)', async ({ trackPage, exportDialog, assetsApi }) => {
    const before = (await assetsApi.list({ trackId: track.id })).total;
    await trackPage.goto(track.id);
    await exportDialog.openForStemPack();
    await exportDialog.save({ format: 'flac', includeDetail: false });
    await expect.poll(async () => (await assetsApi.list({ trackId: track.id })).total).toBe(before + 6);
    const newest = (await assetsApi.list({ trackId: track.id })).items.slice(0, 6);
    for (const item of newest) {
      await assetsApi.waitForReady(item.id, renderMs);
    }
    expect(newest.map((item) => item.provenance.contents).sort()).toEqual(fakeStems.map((s) => `${s}, raw`).sort());
  });
});
