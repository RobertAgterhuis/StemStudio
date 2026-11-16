import type { StemName, TrackDto } from '../support/api';
import { setToggle } from '../support/controls';
import { fakeStems, silentDb } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { installOutputProbe, outputRmsDb } from '../support/output-probe';
import { positionRate, recordPositions } from '../support/practice';
import { expect, test } from '../support/test';

// Feature 012 SC-008 (T047, quickstart P14): every instrument family counts equally. For each of
// vocals, guitar, bass, keys (stem `piano`), drums and other: solo that stem, set 80 % and -2 st,
// play 4 s; the soloed stem is heard, the others are silent, the output carries signal, the
// position advances at 0.8x (+/- 2 %) and the page reports no error. On the fake stack every stem
// is the mix, so each family has the same material and only the routing differs.
const families: readonly { readonly family: string; readonly stem: StemName }[] = [
  { family: 'vocals', stem: 'vocals' },
  { family: 'guitar', stem: 'guitar' },
  { family: 'bass', stem: 'bass' },
  { family: 'keys', stem: 'piano' },
  { family: 'drums', stem: 'drums' },
  { family: 'other', stem: 'other' },
];
let track: TrackDto;

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + 90_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.describe('every instrument family', { tag: '@practice' }, () => {
  for (const { family, stem } of families) {
    test(`${family}: soloed ${stem} at 80 % and -2 st plays at 0.8x with signal (P14, SC-008)`, async ({ mixer, page, practice }) => {
      const errors: string[] = [];
      page.on('pageerror', (error) => errors.push(error.message));
      await installOutputProbe(page);
      await mixer.goto(track.id);
      await mixer.waitForReady(fakeStems);
      await setToggle(mixer.solo(stem), true);
      await practice.set(80, -2);
      await practice.waitForStretch('active');
      await mixer.play();
      await expect.poll(() => mixer.peak(stem), { timeout: 10_000 }).toBeGreaterThan(silentDb);
      const other = fakeStems.find((s) => s !== stem) ?? 'other';
      await expect.poll(() => mixer.peak(other)).toBeLessThanOrEqual(silentDb);
      await expect.poll(() => outputRmsDb(page)).toBeGreaterThan(-50);
      const rate = positionRate(await recordPositions(mixer.byId('transport-position'), 4_000));
      expect(Math.abs(rate / 0.8 - 1), `position rate ${rate.toFixed(3)}`).toBeLessThanOrEqual(0.02);
      await expect(practice.panel).toHaveAttribute('data-speed', '80');
      await expect(practice.panel).toHaveAttribute('data-transpose', '-2');
      expect(errors).toEqual([]);
    });
  }
});
