import { isOn } from '../support/controls';
import { fakeJobTimeoutMs } from '../support/env';
import { expertFlow, loopWithKeys, paletteToMixer, reloadMixWithKeys, saveWithKeys } from '../support/expert-flow';
import { uniqueMixName } from '../support/main-flow';
import { expertState, levelState } from '../support/preferences';
import { expect, test } from '../support/ux-test';

// T062 / FR-017..FR-019, SC-008, quickstart V-E1..V-E4.
const keyBudget = 25;

test.describe('Expert', () => {
  test.use({ storageState: expertState });

  test('keyboard only: palette -> mixer -> loop -> save -> load in at most 25 key presses', { tag: '@ux' }, async ({
    page,
    api,
    mixer,
  }, testInfo) => {
    // The fake stack cannot import from a URL (no network): the flow starts after the import,
    // with the 20 s fixture completed through the API (TEST-STRATEGY.md 10.6).
    const track = await api.ensureCompleted('music_100bpm_20s.mp3', fakeJobTimeoutMs);
    const flow = expertFlow(page, api, mixer);
    const name = uniqueMixName('Keys', testInfo.project.name);
    await page.goto('/');
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'expert');

    await test.step('palette -> track -> mixer', () => paletteToMixer(flow, track));

    await test.step('[ ] L', () => loopWithKeys(flow));

    const mixId = await test.step('Mod+S, name, Enter', () => saveWithKeys(flow, track.id, name));

    await test.step('G M, palette -> load', () => reloadMixWithKeys(flow, mixId, name));

    test.info().annotations.push({ type: 'sc-008 key presses', description: String(flow.keys.total) });
    expect(flow.keys.total).toBeLessThanOrEqual(keyBudget);
  });

  test('Ctrl+K "mixes" Enter opens the mix library', { tag: '@ux' }, async ({ page, api, mixer }) => {
    const flow = expertFlow(page, api, mixer);
    await page.goto('/tracks');
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'expert');
    await flow.palette.run('mixes', 'go-mixes');
    await expect(page).toHaveURL(/\/mixes$/);
  });

  test('mixer shortcuts do not fire while typing in a text field', { tag: '@ux' }, async ({ page, mixer, uxData }) => {
    await mixer.goto(uxData.detailTrack.id);
    await mixer.waitForReady(['vocals']);
    // The metronome starts on when the beat grid is reliable (MixerState), so every toggle is compared
    // with its state before typing instead of with "off".
    const toggles = [mixer.byId('loop-toggle'), mixer.byId('metronome-toggle'), mixer.mute('vocals'), mixer.solo('vocals')];
    const before = await Promise.all(toggles.map((toggle) => isOn(toggle)));
    await mixer.byId('mix-save-as').click();
    await expect(mixer.byId('mix-name-input')).toBeFocused();
    await page.keyboard.type('lk[]ms1gm');
    await expect(mixer.byId('mix-name-input')).toHaveValue('lk[]ms1gm');
    expect(await Promise.all(toggles.map((toggle) => isOn(toggle)))).toEqual(before);
    await expect(page).toHaveURL(/\/mixer/);

    await page.goto('/tracks');
    await page.getByTestId('tracks-search').fill('');
    await page.getByTestId('tracks-search').pressSequentially('gm');
    await expect(page).toHaveURL(/\/tracks$/);
  });

  test('Ctrl+/ shows the shortcut overlay of the current screen', { tag: '@ux' }, async ({ page, mixer, uxData }) => {
    await mixer.goto(uxData.detailTrack.id);
    await mixer.waitForReady(['vocals']);
    await page.keyboard.press('ControlOrMeta+Slash');
    const overlay = page.getByTestId('shortcut-overlay');
    await expect(overlay).toBeVisible();
    await expect(overlay).toContainText(/loop/i);
    await page.getByTestId('shortcut-overlay-close').click();
    await expect(overlay).toBeHidden();
  });
});

test.describe('Advanced', () => {
  test.use({ storageState: levelState('advanced') });

  // 014 FR-024 (CTO-DECISIONS "Shortcut overlay level"): Ctrl+/ opens the overlay at every level; 004 had it Expert-only.
  test('Ctrl+/ opens the overlay, and so does the help panel', { tag: '@ux' }, async ({ page, help, mixer, uxData }) => {
    await mixer.goto(uxData.detailTrack.id);
    await mixer.waitForReady(['vocals']);
    await page.keyboard.press('ControlOrMeta+Slash');
    await expect(page.getByTestId('shortcut-overlay')).toBeVisible();
    await page.getByTestId('shortcut-overlay-close').click();
    await expect(page.getByTestId('shortcut-overlay')).toBeHidden();
    await help.open('button');
    await help.byId('help-shortcuts').click();
    await expect(page.getByTestId('shortcut-overlay')).toBeVisible();
  });
});
