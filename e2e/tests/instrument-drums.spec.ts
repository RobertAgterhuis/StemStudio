import { InstrumentViews } from '../pages/instrument-views';
import type { MixerPage } from '../pages/mixer-page';
import type { TrackDto } from '../support/api';
import { isOn } from '../support/controls';
import { ensureDetailCompleted, fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { drumDetailKeys, expectSilentMeters, expectSomeMeterMoves, setAllExpanded } from '../support/instrument-mixer';
import { required, type DrumHitsDto, useInstrumentApi } from '../support/instrument-views';
import { PracticeFlowApi, barAt } from '../support/practice-flow';
import { expect, test } from '../support/test';

// Feature 016 US3 (T092; FR-024..FR-034, SC-003, SC-006, quickstart V7/V8): the drum trainer for the drums family on
// the fake stack. The fake drum hits follow the 008 pattern (kick on 1 and 3, snare on 2 and 4, hi-hat on every eighth,
// research R-11) and are detected by the backend after the drum split, so the spec waits on the track's own drum-hits
// analysis. The fake detail split carries kick, snare and hi-hat stems (research R-11), so the guide-piece case listens
// to the hi-hat as T092 names it.
const hitsMs = 60_000;
const scoreMs = 90_000;
const meterWindowMs = 1500;
const hitToleranceMs = 30;

const suite = useInstrumentApi(fakeJobTimeoutMs + 2 * hitsMs + scoreMs);

/** The first hit strictly after `ms` and every piece that sounds at that time. */
function nextHitAfter(hits: DrumHitsDto, ms: number): { readonly ms: number; readonly pieces: string[] } {
  const next = required(hits.hits.find((h) => h.timeMs > ms), `a hit after ${String(ms)} ms`);
  return { ms: next.timeMs, pieces: hits.hits.filter((h) => Math.abs(h.timeMs - next.timeMs) <= 1).map((h) => h.piece) };
}

/** The four beats of bar 2 of the fake grid, in ms. */
function barTwoBeatsMs(track: TrackDto): number[] {
  const grid = required(track.beatGrid, 'beat grid');
  const beats = required(grid.beats, 'beat times');
  return beats.slice(grid.beatsPerBar, 2 * grid.beatsPerBar).map((s) => Math.round(s * 1000));
}

async function openDrumTrainer(mixer: MixerPage, ui: InstrumentViews, track: TrackDto): Promise<void> {
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  await ui.practiseAsFamily('drums');
  await expect(ui.view).toHaveAttribute('data-view', 'drum-trainer');
  await expect(ui.byId('drum-hits-status')).toHaveAttribute('data-status', 'ready');
}

test.describe('drum trainer (016 US3)', { tag: '@instrument' }, () => {
  test('the hit lanes name the next fake hit at the beat times', async ({ api, mixer, page }) => {
    const track = await ensureDetailCompleted(api);
    const hits = await suite.views().waitForDrumHits(track.id, hitsMs);
    expect(hits.pieces).toEqual(expect.arrayContaining(['kick', 'snare', 'hihat']));
    const ui = new InstrumentViews(page);
    await openDrumTrainer(mixer, ui, track);

    for (const beatMs of barTwoBeatsMs(track)) {
      const expected = nextHitAfter(hits, beatMs - 100);
      await mixer.seek((beatMs - 100) / 1000);
      await expect.poll(async () => Math.abs((await ui.nextHit()).ms - expected.ms)).toBeLessThanOrEqual(hitToleranceMs);
      expect(expected.pieces).toContain((await ui.nextHit()).piece);
    }
    await expect(ui.byId('drum-bar-text')).not.toBeEmpty();
  });

  test('Drumless silences every drum meter, keeps the band and restores the mix', async ({ api, mixer, page }) => {
    const track = await ensureDetailCompleted(api);
    await suite.views().waitForDrumHits(track.id, hitsMs);
    const ui = new InstrumentViews(page);
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await setAllExpanded(mixer, track, true);
    const drums = drumDetailKeys(track);

    const metronomeBefore = await isOn(mixer.byId('metronome-toggle'));
    await ui.drumless.click();
    await expect(ui.drumless).toHaveAttribute('aria-pressed', 'true');
    await expect(ui.drumTrainer).toBeVisible();
    await expect.poll(() => isOn(mixer.byId('metronome-toggle'))).toBe(true);
    await mixer.play();
    await expectSilentMeters(mixer, drums, meterWindowMs);
    await expectSomeMeterMoves(mixer, ['bass', 'guitar', 'piano'], meterWindowMs);

    await ui.drumless.click();
    await expect(ui.drumless).toHaveAttribute('aria-pressed', 'false');
    for (const key of drums) {
      await expect.poll(() => isOn(mixer.mute(key)), { message: `mute-${key}` }).toBe(false);
    }
    await expect.poll(() => isOn(mixer.byId('metronome-toggle'))).toBe(metronomeBefore);
  });

  test('Advanced: a guide piece is the only audible drum channel', async ({ api, mixer, page }) => {
    const track = await ensureDetailCompleted(api);
    const ui = new InstrumentViews(page);
    await openDrumTrainer(mixer, ui, track);
    await setAllExpanded(mixer, track, true);
    const others = drumDetailKeys(track).filter((key) => key !== 'drums-hihat');

    await ui.drumLaneLabel('hihat').click();
    await expect(ui.drumLaneLabel('hihat')).toHaveAttribute('aria-pressed', 'true');
    await mixer.play();

    await expectSomeMeterMoves(mixer, ['drums-hihat'], meterWindowMs);
    await expectSilentMeters(mixer, others, meterWindowMs);
  });
});

test.describe('drum score in the trainer (016 US3)', { tag: '@instrument' }, () => {
  test('the drum score follows the bar under the transport', async ({ api, mixer, page, scoreApi, assetsApi }) => {
    const track = await ensureDetailCompleted(api);
    const created = await scoreApi.create({ source: { trackId: track.id, channel: 'drums' }, profile: 'drums' });
    await assetsApi.waitForReady(created.id, scoreMs);
    const view = await new PracticeFlowApi(suite.context()).waitForScoreView(created.id, scoreMs);
    const ui = new InstrumentViews(page);
    await openDrumTrainer(mixer, ui, track);
    await expect(ui.drumScore).toBeVisible();

    for (const bar of view.bars.slice(1, 4)) {
      await mixer.seek(bar.startSeconds + 0.2);
      await expect(ui.drumScore).toHaveAttribute('data-bar-number', String(required(barAt(view, bar.startSeconds + 0.2), 'bar').number));
    }
  });
});
