import type { APIRequestContext } from '@playwright/test';
import { InstrumentViews } from '../pages/instrument-views';
import type { StemStudioApi, TrackDto } from '../support/api';
import { apiBaseUrl, fakeJobTimeoutMs } from '../support/env';
import { musicXmlOf } from '../support/instrument-scores';
import { InstrumentViewsApi, required, sharpSymbol, type ChordChartDto, type ChordSegmentDto } from '../support/instrument-views';
import { expect, test } from '../support/test';

// Feature 016 US1 (T076; FR-010, FR-013..FR-017, SC-006): chords for the harmony family (keys, guitar, singers). Fake
// stack: the fake chord recognizer writes one chord per bar cycling C, G, Am, F (data-model 2.7, research R-11); the
// backend detects the chart on its own after the separation, so the spec waits on the track's own chord analysis
// (LESSONS qa). The "Practise as" picker of keys and guitar is covered by instrument-practise-as.spec.ts.
const fixture = 'music_100bpm_20s.flac';
const chordsMs = 60_000;
const scoreMs = 90_000;
const expectedCycle = ['C', 'G', 'Am', 'F'];

let context: APIRequestContext;
let views: InstrumentViewsApi;

test.beforeEach(async ({ playwright }) => {
  test.setTimeout(fakeJobTimeoutMs + chordsMs + scoreMs);
  context = await playwright.request.newContext({ baseURL: apiBaseUrl });
  views = new InstrumentViewsApi(context);
  await views.requireFeature();
});

test.afterEach(async () => {
  await context.dispose();
});

/** The first four chords after the leading "no chord" segment. */
function firstChords(chart: ChordChartDto): ChordSegmentDto[] {
  return chart.segments.filter((s) => s.root !== null).slice(0, 4);
}

async function readyTrack(api: StemStudioApi): Promise<{ readonly track: TrackDto; readonly chart: ChordChartDto }> {
  const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
  return { track, chart: await views.waitForChords(track.id, chordsMs) };
}

test.describe('chords in the mixer (016 US1)', { tag: '@instrument' }, () => {
  test('the chord lane follows the fake bars C, G, Am, F', async ({ api, mixer, page }) => {
    const { track, chart } = await readyTrack(api);
    const ui = new InstrumentViews(page);
    const chords = firstChords(chart);
    expect(chords.map(sharpSymbol)).toEqual(expectedCycle);

    await mixer.goto(track.id);
    await mixer.waitForReady(['bass']);
    await expect(ui.chordsStatus).toHaveAttribute('data-status', 'ready');
    for (const chord of chords) {
      await mixer.seek((chord.startMs + chord.endMs) / 2000);
      await expect(ui.chordLane).toHaveAttribute('data-current-symbol', sharpSymbol(chord));
    }
  });

  test('banner chords sit above the karaoke words', async ({ api, mixer, lyricsApi }) => {
    const { track } = await readyTrack(api);
    await lyricsApi.requireFeature(track.id);
    await lyricsApi.waitForStatus(track.id, ['found'], chordsMs);

    await mixer.goto(track.id);
    await mixer.waitForReady(['vocals']);

    await expect(mixer.karaokeBanner).toHaveAttribute('data-chords', 'true');
    await mixer.seek(2);
    await expect(mixer.karaokeBanner.locator('.karaoke-chord').first()).toBeVisible();
    // The engine anchors a chord right before its word (audio-engine 2.6.0, L1 hand-off).
    await expect(mixer.karaokeBanner.locator('.karaoke-chord + .karaoke-word').first()).toBeAttached();
  });
});

test.describe('chord sheet on the track page (016 US1)', { tag: '@instrument' }, () => {
  test('the chord sheet lists the chords above the lines', async ({ api, trackPage, page }) => {
    const { track, chart } = await readyTrack(api);
    const ui = new InstrumentViews(page);

    await trackPage.goto(track.id);

    await expect(ui.chordSheet).toBeVisible();
    await expect(ui.sheetRow(0)).toBeVisible();
    const sheet = await views.sheet(track.id);
    const first = sheet.rows.findIndex((r) => r.chords.length > 0);
    expect(first).toBeGreaterThanOrEqual(0);
    await expect(ui.sheetChord(first, 0)).toHaveText(required(sheet.rows[first]?.chords[0], 'first chord').symbol);
    expect(expectedCycle).toContain(sharpSymbol(required(firstChords(chart)[0], 'first segment')));
  });

  test('Advanced: a corrected chord persists', async ({ api, trackPage, page }) => {
    const { track, chart } = await readyTrack(api);
    const ui = new InstrumentViews(page);
    const index = chart.segments.findIndex((s) => s.root !== null);
    const original = required(chart.segments[index], 'segment').label;

    try {
      await trackPage.goto(track.id);
      await ui.byId(`chord-edit-${String(index)}`).click();
      await expect(ui.byId('chord-edit-dialog')).toBeVisible();
      await ui.byId('chord-edit-root').selectOption('2');
      await ui.byId('chord-edit-quality').selectOption('Min');
      await ui.byId('chord-edit-save').click();
      await expect(ui.byId('chord-edit-dialog')).toBeHidden();

      await page.reload();
      await expect(ui.chordSheet).toBeVisible();
      const edited = required((await views.chords(track.id)).document, 'chart');
      expect(edited.segments[index]).toMatchObject({ label: 'D:min', edited: true });
      expect(edited.hasEdits).toBe(true);
    } finally {
      await views.editSegment(track.id, index, original);
    }
  });

  test('Advanced: the ChordPro download starts with the title', async ({ api, trackPage, page }) => {
    const { track } = await readyTrack(api);
    const ui = new InstrumentViews(page);

    await trackPage.goto(track.id);
    const href = required(await ui.byId('chord-sheet-download').getAttribute('href'), 'ChordPro link');

    expect(href).toContain(`/api/tracks/${track.id}/chords/chordpro`);
    expect((await views.text(href)).startsWith('{title:')).toBe(true);
  });

  test('a lead sheet with chords has <harmony> elements', async ({ api, scoreApi, assetsApi }) => {
    const { track } = await readyTrack(api);

    const xml = await musicXmlOf(scoreApi, assetsApi, { source: { trackId: track.id, channel: 'vocals' }, profile: 'vocals', includeChords: true }, scoreMs);

    expect(xml).toContain('<harmony');
  });
});
