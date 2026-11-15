import { detailKey } from '../pages/mixer-page';
import { alignmentSnapshot, installAlignmentProbe } from '../support/alignment-probe';
import { isOn, numericValue, setNumericValue } from '../support/controls';
import { ensureDetailCompleted, fakeDetailOutcomes, fakeStems, silentDb, splitParents } from '../support/detail';
import { installEngineProbe, measureSc004 } from '../support/engine-probe';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';

// Feature 002 "mixer" (`@detail`) against the fake stack (docs/testing/TEST-STRATEGY.md section 5).
const drumDetails = fakeDetailOutcomes.drums.stems;
const sc004LimitMs = 50;
const alignmentToleranceMs = 1;
const loopRepetitions = 50;

// T064: US2 AS1-AS5, FR-012..FR-014, SC-004. Rest stems of the fake models are silent
// (sub-stems sum to 1.0x the parent), so they are not expected to move a meter.
const snare = detailKey('drums', 'snare');
const kick = detailKey('drums', 'kick');
const toms = detailKey('drums', 'toms');

test.beforeEach(async ({ api, page, mixer }) => {
  test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 60_000);
  const track = await ensureDetailCompleted(api);
  await installEngineProbe(page);
  await installAlignmentProbe(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
});

test('only split parents expand, and expanding swaps the parent audio for its detail strips', { tag: '@detail' }, async ({
  mixer,
}) => {
  for (const parent of splitParents) {
    await expect(mixer.expand(parent)).toHaveAttribute('aria-expanded', 'false');
  }
  await expect(mixer.expand('guitar')).toHaveCount(0);
  await expect(mixer.expand('piano')).toHaveCount(0);
  await mixer.setLoop(2, 3, true);
  await mixer.play();
  await expect.poll(() => mixer.peak('drums'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  const masterBefore = await mixer.maxPeakOver('master', 1_500);

  await mixer.setExpanded('drums', true, drumDetails);
  await expect(mixer.strip('drums')).toHaveAttribute('data-expanded', 'true');
  await expect(mixer.group('drums')).toBeVisible();
  for (const detail of drumDetails) {
    const key = detailKey('drums', detail);
    await expect.poll(() => numericValue(mixer.fader(key))).toBeCloseTo(80, 0);
    await expect.poll(() => numericValue(mixer.pan(key))).toBeCloseTo(0, 0);
    expect(await isOn(mixer.mute(key))).toBe(false);
    expect(await isOn(mixer.solo(key))).toBe(false);
    await expect(mixer.meter(key)).toBeAttached();
  }
  await expect.poll(() => mixer.peak(kick), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  await expect.poll(() => mixer.peak('drums'), { timeout: 10_000 }).toBeLessThanOrEqual(silentDb);
  const masterAfter = await mixer.maxPeakOver('master', 1_500);
  expect(Math.abs(masterAfter - masterBefore), 'expanding at defaults keeps the mix level').toBeLessThanOrEqual(1.5);
});

test('muting the snare silences only the snare; solo kick silences every other channel', { tag: '@detail' }, async ({ mixer }) => {
  await mixer.play();
  await mixer.setExpanded('drums', true, drumDetails);
  await expect.poll(() => mixer.peak(snare), { timeout: 10_000 }).toBeGreaterThan(silentDb);

  await mixer.mute(snare).click();
  await expect.poll(() => mixer.peak(snare)).toBeLessThanOrEqual(silentDb);
  await expect.poll(() => mixer.peak(kick)).toBeGreaterThan(silentDb);
  await expect.poll(() => mixer.peak(toms)).toBeGreaterThan(silentDb);
  await expect.poll(() => mixer.peak('vocals')).toBeGreaterThan(silentDb);

  await mixer.solo(kick).click();
  await expect.poll(() => mixer.peak(kick)).toBeGreaterThan(silentDb);
  await expect.poll(() => mixer.peak(toms)).toBeLessThanOrEqual(silentDb);
  await expect.poll(() => mixer.peak('vocals')).toBeLessThanOrEqual(silentDb);
  await expect.poll(() => mixer.peak('bass')).toBeLessThanOrEqual(silentDb);
});

test('detail controls reach the audio engine within 50 ms (SC-004)', { tag: '@detail' }, async ({ mixer }) => {
  await mixer.play();
  await mixer.setExpanded('drums', true, drumDetails);
  await expect.poll(() => mixer.peak(kick), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  const timings = await measureSc004({ fader: mixer.fader(snare), mute: mixer.mute(toms), solo: mixer.solo(kick) });
  test.info().annotations.push({ type: 'sc-004 detail ms', description: JSON.stringify(timings) });
  expect(Math.max(timings.fader, timings.mute, timings.solo), JSON.stringify(timings)).toBeLessThan(sc004LimitMs);
});

test('collapsing brings the parent back and the next expansion keeps the detail settings', { tag: '@detail' }, async ({ mixer }) => {
  await mixer.play();
  await mixer.setExpanded('drums', true, drumDetails);
  await mixer.mute(snare).click();
  await setNumericValue(mixer.fader(kick), 50);

  await mixer.setExpanded('drums', false);
  await expect(mixer.strip('drums')).not.toHaveAttribute('data-expanded', 'true');
  await expect.poll(() => mixer.peak('drums'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  expect(await isOn(mixer.mute('drums'))).toBe(false);

  await mixer.setExpanded('drums', true, drumDetails);
  await expect.poll(() => isOn(mixer.mute(snare))).toBe(true);
  await expect.poll(() => numericValue(mixer.fader(kick))).toBeCloseTo(50, 0);
  await expect.poll(() => mixer.peak(snare)).toBeLessThanOrEqual(silentDb);
  await expect.poll(() => mixer.peak('drums'), { timeout: 10_000 }).toBeLessThanOrEqual(silentDb);
});

test('50 loop repetitions with drums expanded mid-playback stay sample-aligned (SC-004)', { tag: '@detail' }, async ({
  mixer,
  page,
}) => {
  test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 3 * 60_000);
  await mixer.setLoop(2, 3, true);
  await mixer.play();
  await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  await mixer.setExpanded('drums', true, drumDetails);

  await expect
    .poll(async () => (await alignmentSnapshot(page)).loops, { timeout: 120_000, intervals: [1_000] })
    .toBeGreaterThanOrEqual(loopRepetitions);
  const snapshot = await alignmentSnapshot(page);
  test.info().annotations.push({ type: 'alignment', description: JSON.stringify(snapshot) });
  expect(snapshot.sources, 'six standard plus four drum sources').toBeGreaterThanOrEqual(10);
  expect(snapshot.maxErrorMs).toBeLessThanOrEqual(alignmentToleranceMs);
  await expect.poll(() => mixer.peak(kick)).toBeGreaterThan(silentDb);
});
