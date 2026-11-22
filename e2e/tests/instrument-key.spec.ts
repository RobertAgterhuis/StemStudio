import { InstrumentViews } from '../pages/instrument-views';
import type { MixerPage } from '../pages/mixer-page';
import { PracticePanel } from '../pages/practice-panel';
import type { StemStudioApi, TrackDto } from '../support/api';
import { fakeJobTimeoutMs } from '../support/env';
import { practiceFlowFixturePath } from '../support/fixtures';
import { families } from '../support/instrument-family';
import { chordTrack, required, sharpSymbol, type ChordChartDto, type ChordSegmentDto, useInstrumentApi } from '../support/instrument-views';
import { expect, test } from '../support/test';

// Feature 016 US7 (T122; FR-065..FR-071, SC-006, SC-010, quickstart V12): the song key for every family. Fake chords
// (data-model 2.7): C major (confidence 0.9) for bars 1-8, D major from bar 9 (D, A, Bm, G). The 82 s practice-flow
// song (120 BPM) is long enough for both regions; the spec takes the region start from the track's own key view and
// waits on the chords analysis the backend started after the separation.
const chordsMs = 60_000;
const changeMs = 8_000;

const suite = useInstrumentApi(fakeJobTimeoutMs + 2 * chordsMs);

/** The fixture track and its ready chord chart. */
function keyTrack(api: StemStudioApi): ReturnType<typeof chordTrack> {
  return chordTrack(api, suite.views(), practiceFlowFixturePath('practice-song.wav'), fakeJobTimeoutMs);
}

/** Start (ms) of the second key region (the fake change to D major). */
async function changeStartMs(trackId: string): Promise<number> {
  const regions = (await suite.views().key(trackId)).regions;
  expect(regions).toHaveLength(2);
  const change = required(regions[1], 'second key region');
  expect(change.display).toContain('D major');
  return change.startMs;
}

/** The chord in the D major region whose transposed symbol needs a sharp (B minor -> C#m at +2). */
function bMinorAfter(chart: ChordChartDto, startMs: number): ChordSegmentDto {
  return required(chart.segments.find((s) => s.startMs >= startMs && s.root === 11 && s.quality === 'min'), 'B:min in the D region');
}

async function openMixer(mixer: MixerPage, track: TrackDto): Promise<InstrumentViews> {
  await mixer.goto(track.id);
  await mixer.waitForReady(['bass']);
  return new InstrumentViews(mixer.byId('mixer-key').page());
}

test.describe('song key for every family (016 US7)', { tag: '@instrument' }, () => {
  for (const family of families) {
    test(`${family}: "C major" next to the tempo`, async ({ api, mixer }) => {
      const { track } = await keyTrack(api);
      const ui = await openMixer(mixer, track);

      await ui.practiseAsFamily(family);

      await expect(ui.mixerKey).toHaveText('C major');
      await expect(ui.mixerKey).toHaveAttribute('data-tonic', '0');
      await expect(ui.mixerKey).toHaveAttribute('data-mode', 'major');
    });
  }

  test('the track page shows the key and its regions', async ({ api, trackPage, page }) => {
    const { track } = await keyTrack(api);
    const ui = new InstrumentViews(page);

    await trackPage.goto(track.id);

    await expect(ui.trackKey).toHaveText('C major');
    await expect(ui.byId('track-key-region-1')).toContainText('D major');
  });
});

test.describe('key changes and transposition (016 US7)', { tag: '@instrument' }, () => {
  test('playing past the change shows D major as the current key', async ({ api, mixer }) => {
    const { track } = await keyTrack(api);
    const startMs = await changeStartMs(track.id);
    const ui = await openMixer(mixer, track);

    await mixer.seek(Math.max(0, startMs - 1500) / 1000);
    await mixer.play();

    await expect(ui.mixerKeyCurrent).toHaveText('D major', { timeout: changeMs });
  });

  test('transpose +2 names the resulting and the original key, and the D region spells with sharps', async ({ api, mixer, page }) => {
    const { track, chart } = await keyTrack(api);
    const chord = bMinorAfter(chart, await changeStartMs(track.id));
    const ui = await openMixer(mixer, track);

    await new PracticePanel(page).setTranspose(2);

    await expect(ui.mixerKey).toHaveText('D major (original C major, +2)');
    await mixer.seek((chord.startMs + chord.endMs) / 2000);
    await expect(ui.chordLane).toHaveAttribute('data-current-symbol', sharpSymbol({ root: 1, quality: chord.quality, bass: null }));
  });

  test('orchestral with an alto sax shows the written key', async ({ api, mixer }) => {
    const { track } = await keyTrack(api);
    const ui = await openMixer(mixer, track);

    await ui.practiseAsFamily('orchestral');
    await ui.view.getByTestId('score-part-instrument').selectOption('alto-sax-eb');

    await expect(ui.mixerKeyWritten).toHaveText('Your part: A major');
    await expect(ui.mixerKey).toHaveText('C major');
  });
});
