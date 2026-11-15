import { isOn, numericValue, setNumericValue } from '../support/controls';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import {
  beginnerHiddenMixerIds,
  everyLevelMixerIds,
  expectNotRendered,
  expectRendered,
  expertMixerIds,
  positionSeconds,
  timedLevelSwitch,
} from '../support/levels';
import { levelState, readStoredPreferences } from '../support/preferences';
import { expect, test } from '../support/ux-test';

// T047 / FR-012..FR-014, FR-020, SC-010, ui-test-ids 1.3.0 R7-R9 (quickstart V-J3..V-J7).
const drumDetails = ['drums-kick', 'drums-snare', 'drums-toms'];
const switchBudgetMs = 200;

test.describe('Beginner', () => {
  test.use({ storageState: levelState('beginner') });

  test('the mixer hides exactly the R8 ids and renders every other control', { tag: '@ux' }, async ({ page, mixer, uxData }) => {
    await mixer.goto(uxData.detailTrack.id);
    await mixer.waitForReady(fakeStems);
    await expectNotRendered(page, beginnerHiddenMixerIds(fakeStems));
    await expectRendered(page, everyLevelMixerIds(fakeStems));
    await expectNotRendered(page, expertMixerIds(fakeStems));
    await mixer.setExpanded('drums', true, ['kick', 'snare', 'toms']);
    await expectNotRendered(page, beginnerHiddenMixerIds(drumDetails).filter((id) => id.startsWith('pan-')));
    await expectRendered(page, drumDetails.map((key) => `fader-${key}`));
    await expect(page.getByTestId('journey-rail')).toBeVisible();
  });

  test('library search, sort and job details are not shown', { tag: '@ux' }, async ({ page, uxData }) => {
    await page.goto('/tracks');
    await expect(page.getByTestId(`track-row-${uxData.detailTrack.id}`)).toBeVisible();
    await expectNotRendered(page, ['tracks-search', 'tracks-sort']);
    await page.goto('/mixes');
    await expect(page.getByTestId(`mix-row-${uxData.mixId}`)).toBeVisible();
    await expectNotRendered(page, ['mixes-search']);
    await page.goto(`/tracks/${uxData.detailTrack.id}`);
    await expect(page.getByTestId('track-status')).toHaveText(/completed/i);
    await expectNotRendered(page, ['job-details']);
  });
});

test.describe('Advanced', () => {
  test.use({ storageState: levelState('advanced') });

  test('the mixer renders every 1.2.0 control and no Expert extras (R7)', { tag: '@ux' }, async ({ page, mixer, uxData }) => {
    await mixer.goto(uxData.detailTrack.id);
    await mixer.waitForReady(fakeStems);
    await expectRendered(page, [...everyLevelMixerIds(fakeStems), ...beginnerHiddenMixerIds(fakeStems)]);
    await expectNotRendered(page, [...expertMixerIds(fakeStems), 'journey-rail']);
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-density', 'comfortable');
  });

  test('a pan set at Advanced shows as a read-only marker at Beginner', { tag: '@ux' }, async ({ page, mixer, shell, uxData }) => {
    await mixer.goto(uxData.plainTrack.id);
    await mixer.waitForReady(fakeStems);
    await setNumericValue(mixer.pan('bass'), -30);
    await shell.setLevel('beginner');
    await expect(page.getByTestId('pan-marker-bass')).toContainText(/Panned/);
    await expect(page.getByTestId('pan-marker-vocals')).toHaveCount(0);
    await expect(mixer.byId('unsaved-indicator')).toBeVisible();
    await shell.setLevel('advanced');
    await expect.poll(() => numericValue(mixer.pan('bass'))).toBeCloseTo(-30, 0);
  });

  test('a level choice persists after a reload', { tag: '@ux' }, async ({ page, shell }) => {
    await page.goto('/');
    await shell.setLevel('expert');
    await page.reload();
    await shell.expectLevel('expert');
    expect((await readStoredPreferences(page))?.level).toBe('expert');
  });

  test('a second tab follows a level change without a reload', { tag: '@ux' }, async ({ page, shell }) => {
    await page.goto('/');
    const other = await page.context().newPage();
    await other.goto('/');
    await expect(other.getByTestId('app-shell')).toHaveAttribute('data-level', 'advanced');
    await shell.setLevel('beginner');
    await expect(other.getByTestId('app-shell')).toHaveAttribute('data-level', 'beginner');
    await other.close();
  });

  test('switching level keeps playback, position and unsaved changes, within 200 ms', { tag: '@ux' }, async ({ page, api, mixer, shell }) => {
    const track = await api.ensureCompleted('music_100bpm_20s.mp3', fakeJobTimeoutMs);
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await setNumericValue(mixer.fader('vocals'), 50);
    await mixer.play();
    await expect(mixer.byId('transport-position')).not.toHaveText(/^\s*0?0:00(\.0+)?\b/);
    const stateBefore = await mixer.byId('transport-state').textContent();
    const before = positionSeconds((await mixer.byId('transport-position').textContent()) ?? '');

    const timings = [await timedLevelSwitch(page, 'beginner'), await timedLevelSwitch(page, 'advanced')];
    test.info().annotations.push({ type: 'sc-010 ms', description: JSON.stringify(timings) });

    await shell.expectLevel('advanced');
    expect(Math.max(...timings)).toBeLessThan(switchBudgetMs);
    await expect(mixer.byId('transport-state')).toHaveText(stateBefore ?? '');
    await expect.poll(async () => positionSeconds((await mixer.byId('transport-position').textContent()) ?? '')).toBeGreaterThan(before);
    await expect.poll(() => numericValue(mixer.fader('vocals'))).toBeCloseTo(50, 0);
    await expect(mixer.byId('unsaved-indicator')).toBeVisible();
  });
});

test.describe('Expert', () => {
  test.use({ storageState: levelState('expert') });

  test('the mixer adds numeric entry and dB readouts; the track page adds job details (R9)', { tag: '@ux' }, async ({ page, mixer, shell, uxData }) => {
    await mixer.goto(uxData.detailTrack.id);
    await mixer.waitForReady(fakeStems);
    await expectRendered(page, [...everyLevelMixerIds(fakeStems), ...beginnerHiddenMixerIds(fakeStems), ...expertMixerIds(fakeStems)]);
    await expect(shell.root).toHaveAttribute('data-density', 'compact');
    await setNumericValue(mixer.byId('fader-value-vocals'), 40);
    await expect.poll(() => numericValue(mixer.fader('vocals'))).toBeCloseTo(40, 0);
    expect(await isOn(mixer.mute('vocals'))).toBe(false);
    await page.goto(`/tracks/${uxData.detailTrack.id}`);
    await expect(page.getByTestId('job-details')).toBeVisible();
    await page.goto('/tracks');
    await expect(page.getByTestId('tracks-search')).toBeVisible();
  });
});
