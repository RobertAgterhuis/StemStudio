import type { APIRequestContext } from '@playwright/test';
import { ScorePanel } from '../pages/score-panel';
import type { AssetsApi } from '../support/assets-api';
import { ensureDetailCompleted, fakeStems } from '../support/detail';
import { apiBaseUrl, fakeJobTimeoutMs } from '../support/env';
import { chromaticTranspose, readyScoreId, type InstrumentScoreRequest, type ScorePartDto } from '../support/instrument-scores';
import { InstrumentViewsApi, required } from '../support/instrument-views';
import { clefs, fifths } from '../support/musicxml';
import { PracticeFlowApi } from '../support/practice-flow';
import type { ScoreApi } from '../support/score-api';
import { expect, test } from '../support/test';

// Feature 016 US5 (T112; FR-048..FR-056, SC-005, SC-006, quickstart V11): written parts for the orchestral families on
// the fake stack. The fake detail split gives `other.strings` and `other.brass`; there is no woodwinds detail stem, so
// the woodwinds case uses the `other` stem with the `woodwinds` profile. The fake notes are in C major (concert), so the
// written keys are A major for the alto sax (M6 up, 3 sharps) and D major for the clarinet (M2 up, 2 sharps).
const scoreMs = 90_000;
const viewMs = 30_000;

let context: APIRequestContext;

test.beforeEach(async ({ playwright }) => {
  test.setTimeout(fakeJobTimeoutMs + 4 * scoreMs);
  context = await playwright.request.newContext({ baseURL: apiBaseUrl });
  await new InstrumentViewsApi(context).requireFeature();
});

test.afterEach(async () => {
  await context.dispose();
});

/** The pitches of every note-on with an explicit status byte on any channel (same reading as `hasNoteOn`). */
function noteOnPitches(bytes: Buffer): number[] {
  const at = (i: number): number => bytes[i] ?? 0;
  const isNoteOn = (i: number): boolean => at(i) >> 4 === 0x9 && at(i + 1) < 128 && at(i + 2) > 0;
  return [...bytes.keys()].filter(isNoteOn).map((i) => at(i + 1)).sort((a, b) => a - b);
}

/** A concert and a written score of one channel, both ready. */
async function scorePair(scoreApi: ScoreApi, assetsApi: AssetsApi, concert: InstrumentScoreRequest, part: ScorePartDto): Promise<{ readonly concertId: string; readonly partId: string }> {
  const concertId = await readyScoreId(scoreApi, assetsApi, concert, scoreMs);
  const partId = await readyScoreId(scoreApi, assetsApi, { ...concert, part }, scoreMs);
  return { concertId, partId };
}

test.describe('transposed parts (016 US5)', { tag: '@instrument' }, () => {
  test('brass as alto sax in E-flat: written a major sixth up, MIDI at concert pitch', async ({ api, scoreApi, assetsApi }) => {
    const track = await ensureDetailCompleted(api);
    const source = { trackId: track.id, channel: 'other.brass' };

    const { concertId, partId } = await scorePair(scoreApi, assetsApi, { source, profile: 'brass' }, { instrument: 'alto-sax-eb', clef: 'auto' });
    const concert = await scoreApi.musicXml(concertId);
    const written = await scoreApi.musicXml(partId);

    expect(fifths(concert), 'the fake notes are in concert C').toBe(0);
    expect(chromaticTranspose(written)).toBe(-9);
    expect(fifths(written)).toBe(3);
    expect(clefs(written)[0]?.sign).toBe('G');
    const [concertMidi, writtenMidi] = await Promise.all([scoreApi.file(concertId, 'midi'), scoreApi.file(partId, 'midi')]);
    expect(noteOnPitches(writtenMidi.bytes)).toEqual(noteOnPitches(concertMidi.bytes));
  });

  test('woodwinds as clarinet in B-flat: written a major second up', async ({ api, scoreApi, assetsApi }) => {
    const track = await ensureDetailCompleted(api);

    const id = await readyScoreId(scoreApi, assetsApi, { source: { trackId: track.id, channel: 'other' }, profile: 'woodwinds', part: { instrument: 'clarinet-bb', clef: 'auto' } }, scoreMs);
    const written = await scoreApi.musicXml(id);

    expect(chromaticTranspose(written)).toBe(-2);
    expect(fifths(written)).toBe(2);
  });

  test('strings as viola: alto clef, no transposition', async ({ api, scoreApi, assetsApi }) => {
    const track = await ensureDetailCompleted(api);

    const id = await readyScoreId(scoreApi, assetsApi, { source: { trackId: track.id, channel: 'other.strings' }, profile: 'strings', part: { instrument: 'viola', clef: 'auto' } }, scoreMs);
    const written = await scoreApi.musicXml(id);

    expect(clefs(written)[0]).toMatchObject({ sign: 'C', line: 3 });
    expect(chromaticTranspose(written)).toBeNull();
  });
});

test.describe('parts in the 014 score view (016 US5)', { tag: '@instrument' }, () => {
  test('the part and the concert part show the same bar at a fixed position', async ({ api, scoreApi, assetsApi, mixer, page }) => {
    const track = await ensureDetailCompleted(api);
    const source = { trackId: track.id, channel: 'other.brass' };
    const { concertId, partId } = await scorePair(scoreApi, assetsApi, { source, profile: 'brass' }, { instrument: 'alto-sax-eb', clef: 'auto' });
    const flow = new PracticeFlowApi(context);
    const view = await flow.waitForScoreView(partId, viewMs);
    await flow.waitForScoreView(concertId, viewMs);
    const bar = required(view.bars[2], 'bar 3 of the part');
    const panel = new ScorePanel(page);

    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await panel.open();
    const bars: number[] = [];
    for (const id of [partId, concertId]) {
      await panel.choose(`Brass - ${(await scoreApi.get(id)).name}`, id, viewMs);
      await mixer.seek(bar.startSeconds + 0.2);
      await expect.poll(() => panel.barNumber()).toBe(bar.number);
      bars.push(await panel.barNumber());
    }

    expect(bars).toEqual([bar.number, bar.number]);
  });
});
