import type { StemName, TrackDto } from '../support/api';
import { fillAndCommit, isOn, numericValue } from '../support/controls';
import { installEngineProbe, measureActionToEngineMs, sliderAction } from '../support/engine-probe';
import { fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';

// US2 mixer in a real browser (T097; FR-011, FR-013, SC-004 per ADR-008 #5).
const silentDb = -60;
const sc004LimitMs = 50;

let track: TrackDto;
let stems: StemName[];

test.beforeEach(async ({ api, page, mixer }) => {
  track = await api.ensureCompleted('music_100bpm_20s.mp3', fakeJobTimeoutMs);
  stems = track.stems.map((stem) => stem.name);
  await installEngineProbe(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(stems);
});

test('one strip per stem with fader 80 %, pan 0, mute and solo off, plus a master fader', async ({
  mixer,
}) => {
  expect(stems).toHaveLength(6);
  for (const stem of stems) {
    await expect.poll(() => numericValue(mixer.fader(stem))).toBeCloseTo(80, 0);
    await expect.poll(() => numericValue(mixer.pan(stem))).toBeCloseTo(0, 0);
    expect(await isOn(mixer.mute(stem))).toBe(false);
    expect(await isOn(mixer.solo(stem))).toBe(false);
    await expect(mixer.meter(stem)).toBeAttached();
  }
  await expect(mixer.byId('master-fader')).toBeVisible();
});

test('play after a user gesture drives the master meter and the position', async ({ mixer }) => {
  await mixer.play();
  await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  await expect(mixer.byId('transport-position')).not.toHaveText(/^0?0:00(\.0+)?$/, { timeout: 5_000 });
});

test('mute silences a channel meter and solo silences the others', async ({ mixer }) => {
  await mixer.play();
  await expect.poll(() => mixer.peak('vocals'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  await mixer.mute('vocals').click();
  await expect.poll(() => mixer.peak('vocals')).toBeLessThanOrEqual(silentDb);
  await mixer.solo('drums').click();
  await expect.poll(() => mixer.peak('bass')).toBeLessThanOrEqual(silentDb);
  await expect.poll(() => mixer.peak('drums')).toBeGreaterThan(silentDb);
});

test('a loop from 10 s to 20 s is accepted', async ({ mixer }) => {
  await mixer.setLoop(10, 20, true);
  await expect(mixer.byId('loop-error')).toBeHidden();
  expect(await isOn(mixer.byId('loop-toggle'))).toBe(true);
});

test('a loop with B <= A is refused with a reason', async ({ mixer }) => {
  await fillAndCommit(mixer.byId('loop-start'), '20');
  await fillAndCommit(mixer.byId('loop-end'), '10');
  await mixer.byId('loop-toggle').click();
  await expect(mixer.byId('loop-error')).toBeVisible();
  await expect(mixer.byId('loop-error')).not.toBeEmpty();
  expect(await isOn(mixer.byId('loop-toggle'))).toBe(false);
});

test('mixer controls reach the audio engine within 50 ms (SC-004)', async ({ mixer }) => {
  await mixer.play();
  await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  const fader = mixer.fader('vocals');
  const timings = {
    fader: await measureActionToEngineMs(fader, await sliderAction(fader, 50)),
    mute: await measureActionToEngineMs(mixer.mute('guitar'), { kind: 'click' }),
    solo: await measureActionToEngineMs(mixer.solo('drums'), { kind: 'click' }),
  };
  test.info().annotations.push({ type: 'sc-004 ms', description: JSON.stringify(timings) });
  expect(timings.fader).toBeLessThan(sc004LimitMs);
  expect(timings.mute).toBeLessThan(sc004LimitMs);
  expect(timings.solo).toBeLessThan(sc004LimitMs);
});
