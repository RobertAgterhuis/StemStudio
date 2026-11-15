import { detailKey } from '../pages/mixer-page';
import type { MixSettingsDto, TrackDto } from '../support/api';
import { isOn, numericValue } from '../support/controls';
import { ensureDetailCompleted, fakeDetailOutcomes, fakeStems, splitParents } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { uniqueMixName } from '../support/main-flow';
import { expect, test } from '../support/test';

// Feature 002 "mixes" (`@detail`, T081; US5 AS1-AS3, FR-015, SC-005, quickstart D10).
const snare = detailKey('drums', 'snare');
const kick = detailKey('drums', 'kick');

/** A mix body in the 1.1.0 shape: no `expanded`, no `detailChannels` (saved before feature 002). */
function oldStyleMix(name: string): MixSettingsDto {
  return {
    name,
    masterVolume: 1,
    channels: [{ stem: 'vocals', volume: 0.35, pan: 0, mute: false, solo: false }],
    metronome: { enabled: false, volume: 0.7, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 },
    loop: null,
  };
}

let track: TrackDto;

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 90_000);
  track = await ensureDetailCompleted(api);
});

test('a saved mix restores the expanded group and the muted snare after a reload', { tag: '@detail' }, async ({
  api,
  mixer,
  mixLibrary,
  page,
}, testInfo) => {
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  await mixer.setExpanded('drums', true, fakeDetailOutcomes.drums.stems);
  await mixer.mute(snare).click();
  await expect(mixer.byId('unsaved-indicator')).toBeVisible();
  const name = uniqueMixName('No snare', testInfo.project.name);
  await mixer.saveNew(name);
  await expect.poll(async () => (await api.findMix(track.id, name))?.id).toBeTruthy();
  const mixId = (await api.findMix(track.id, name))?.id ?? '';

  const saved = (await api.getMix(mixId)).settings;
  expect(saved.expanded).toEqual(['drums']);
  expect(saved.detailChannels).toContainEqual(expect.objectContaining({ parent: 'drums', stem: 'snare', mute: true }));

  await page.reload();
  await mixLibrary.goto();
  await mixLibrary.load(mixId);
  await mixer.waitForReady(fakeStems);
  await expect(mixer.expand('drums')).toHaveAttribute('aria-expanded', 'true', { timeout: 30_000 });
  await expect.poll(() => isOn(mixer.mute(snare))).toBe(true);
  await expect.poll(() => isOn(mixer.mute(kick))).toBe(false);
  for (const parent of splitParents.filter((p) => p !== 'drums')) {
    await expect(mixer.expand(parent)).toHaveAttribute('aria-expanded', 'false');
  }

  const copy = await api.duplicateMix(mixId);
  expect(copy.settings.expanded).toEqual(saved.expanded);
  expect(copy.settings.detailChannels).toEqual(saved.detailChannels);
});

test('a mix saved before detail stems existed loads collapsed with its settings', { tag: '@detail' }, async ({
  api,
  mixer,
}, testInfo) => {
  const mix = await api.createMix(track.id, oldStyleMix(uniqueMixName('Old style', testInfo.project.name)));
  expect(mix.settings.expanded ?? []).toEqual([]);
  expect(mix.settings.detailChannels ?? []).toEqual([]);

  await mixer.goto(track.id, mix.id);
  await mixer.waitForReady(fakeStems);
  await expect.poll(() => numericValue(mixer.fader('vocals'))).toBeCloseTo(35, 0);
  for (const parent of splitParents) {
    await expect(mixer.expand(parent)).toHaveAttribute('aria-expanded', 'false');
    await expect(mixer.strip(parent)).not.toHaveAttribute('data-expanded', 'true');
  }
});
