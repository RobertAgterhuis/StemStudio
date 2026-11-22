import type { StemStudioApi, TrackDto } from '../support/api';
import type { AssetsApi } from '../support/assets-api';
import { ensureCancelledPhase, ensureDetailCompleted } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';
import { clefs, crossNoteheads, expectedFakeBars, fifths, isMidi, isPdf, measureCount, noteTypes, staves, timeSignature, unpitchedNotes, voices, type GridDto } from '../support/musicxml';
import type { ScoreApi } from '../support/score-api';

// Feature 008 US1-US5 (008:T045, T050, T056, T065, T070; FR-001..FR-010; SC-003; quickstart
// M3-M8). Fake stack: the fake transcriber plays one note per beat of the track's real grid
// (data-model 2.6) and notation + engraving are real, so the files are checked on real output.
const scoreMs = 90_000;
let track: TrackDto;

async function gridOf(api: StemStudioApi, trackId: string): Promise<GridDto> {
  const grid = (await api.getTrack(trackId)).beatGrid as unknown as GridDto | null;
  if (grid === null) {
    throw new Error('the fake stack analyses a beat grid; this track has none');
  }
  return grid;
}

async function readyScore(assetsApi: AssetsApi, scoreApi: ScoreApi, id: string): Promise<string> {
  await assetsApi.waitForReady(id, scoreMs);
  return scoreApi.musicXml(id);
}

test.beforeEach(async ({ api, scoreApi }) => {
  test.setTimeout(fakeJobTimeoutMs + 3 * scoreMs);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
  await scoreApi.requireFeature(track.id);
});

test.describe('sheet music for the bass', { tag: '@sheet-music' }, () => {
  test('generate from the track page; PDF preview, MusicXML bars on the grid, MIDI (US1, SC-003)', async ({ api, assetsApi, library, scoreApi, scoreDialog, scoreItems, trackPage }) => {
    await trackPage.goto(track.id);
    await scoreDialog.openForStem('bass');
    await scoreDialog.generate();
    const id = await scoreDialog.savedAssetId();
    await scoreDialog.savedOpen.click();
    await library.waitForStatus(id, 'ready', scoreMs);
    await expect(scoreItems.preview(id)).toBeVisible();
    await expect(scoreItems.preview(id)).toHaveAttribute('src', /inline=true/u);
    expect(isPdf((await scoreApi.file(id, 'pdf')).bytes)).toBe(true);
    expect(isMidi((await scoreApi.file(id, 'midi')).bytes)).toBe(true);
    const grid = await gridOf(api, track.id);
    const xml = await readyScore(assetsApi, scoreApi, id);
    expect(measureCount(xml)).toBe(expectedFakeBars(grid));
    expect(fifths(xml)).not.toBeNull();
    expect(timeSignature(xml)).toBe(`${String(grid.beatsPerBar)}/4`);
    expect(clefs(xml)).toContainEqual({ sign: 'F', line: 4, octaveChange: -1 });
  });

  test('downloads are attachments named after the item', async ({ assetsApi, scoreApi }) => {
    const created = await scoreApi.create({ source: { trackId: track.id, channel: 'bass' } });
    await assetsApi.waitForReady(created.id, scoreMs);
    for (const role of ['pdf', 'musicxml', 'midi'] as const) {
      expect((await scoreApi.file(created.id, role)).disposition).toMatch(/^attachment/u);
    }
  });
});

test.describe('key override and simplified rhythm', { tag: '@sheet-music' }, () => {
  test('E minor and "simplified" give one sharp and no sixteenths (US2)', async ({ assetsApi, scoreApi, scoreDialog, trackPage }) => {
    await trackPage.goto(track.id);
    await scoreDialog.openForStem('bass');
    await scoreDialog.generate({ keyOverride: 'E minor', simplified: true });
    const xml = await readyScore(assetsApi, scoreApi, await scoreDialog.savedAssetId());
    expect(fifths(xml)).toBe(1);
    expect(noteTypes(xml).has('16th')).toBe(false);
  });
});

test.describe('piano', { tag: '@sheet-music' }, () => {
  test('the fake piano score is a grand staff with treble and bass clefs (US3)', async ({ assetsApi, scoreApi, scoreDialog, trackPage }) => {
    await trackPage.goto(track.id);
    await scoreDialog.openForStem('piano');
    await scoreDialog.generate({ profile: 'piano' });
    const xml = await readyScore(assetsApi, scoreApi, await scoreDialog.savedAssetId());
    expect(staves(xml)).toBe(2);
    expect(clefs(xml).map((c) => c.sign)).toEqual(expect.arrayContaining(['G', 'F']));
  });
});

test.describe('drums', { tag: ['@sheet-music', '@detail'] }, () => {
  // The fake stack runs the detail phase by itself, so the "not split" track is a band copy
  // whose detail phase is cancelled (not the shared click track: other specs let its phase finish).
  test('before the split the dialog asks to split the drums first (US4-2)', async ({ api, scoreApi, scoreDialog, trackPage }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + scoreMs);
    const plain = await ensureCancelledPhase(api, `score-unsplit-${testInfo.project.name}`);
    expect((await scoreApi.options(plain.id, 'drums')).drumsRequireSplit).toBe(true);
    await trackPage.goto(plain.id);
    await scoreDialog.openForStem('drums');
    await expect(scoreDialog.byId('score-drums-split-required')).toBeVisible();
    await expect(scoreDialog.byId('score-drums-split-start')).toBeVisible();
  });

  // The fake detail phase splits the drums into kick, snare, toms, hi-hat and rest (research
  // R-11), so the fake score draws the hi-hat with x noteheads; cymbals are not in the fake
  // split and stay covered by the percussion builder's unit tests (beat-analysis 008).
  test('after the split: percussion clef, two voices of unpitched notes (US4)', async ({ api, assetsApi, scoreApi, scoreDialog, trackPage }) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 2 * scoreMs);
    const band = await ensureDetailCompleted(api);
    await trackPage.goto(band.id);
    await scoreDialog.openForStem('drums');
    await scoreDialog.generate({ profile: 'drums' });
    const xml = await readyScore(assetsApi, scoreApi, await scoreDialog.savedAssetId());
    expect(clefs(xml).map((c) => c.sign)).toContain('percussion');
    expect([...voices(xml)].sort()).toEqual([1, 2]);
    expect(unpitchedNotes(xml)).toBeGreaterThan(0);
    expect(crossNoteheads(xml)).toBeGreaterThan(0);
  });
});

test.describe('sheet music from a library item', { tag: '@sheet-music' }, () => {
  test('a loop-region export becomes sheet music linked to the item and the track (US5)', async ({ assetsApi, library, scoreApi, scoreDialog }) => {
    const exported = await assetsApi.createExport({ trackId: track.id, mode: 'raw', channels: ['bass'], range: { startSeconds: 4, endSeconds: 12 }, format: { format: 'flac' } });
    await assetsApi.waitForReady(exported.id, scoreMs);
    await library.goto();
    await scoreDialog.openForAsset(exported.id);
    await scoreDialog.generate({ profile: 'bass' });
    const id = await scoreDialog.savedAssetId();
    await assetsApi.waitForReady(id, scoreMs);
    const score = await scoreApi.get(id);
    expect([score.provenance.trackId, score.provenance.sourceAssetIds]).toEqual([track.id, [exported.id]]);
    // ui-test-ids 1.7.0: `asset-source-<id>` shows the track title; the audio item is linked in
    // `provenance.sourceAssetIds` only (frontend 008 US5 hand-off).
    await library.goto();
    await expect(library.source(id)).toContainText(track.title);
  });
});
