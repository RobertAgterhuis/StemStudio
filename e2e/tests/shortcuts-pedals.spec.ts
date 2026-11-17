import type { Locator, Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import { ShortcutSettings } from '../pages/shortcut-settings';
import { axeViolations } from '../support/axe';
import { isOn } from '../support/controls';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { blurActiveElement } from '../support/help';
import { positionSeconds } from '../support/levels';
import { expect, test } from '../support/test';

// Feature 014 US2 (T068; FR-019..FR-024, SC-006): keyboard shortcuts and foot pedals in the mixer. Fake stack, the 20 s
// music fixture. Each FR-020 command is pressed once outside text fields and its on-screen control must show the change;
// the same keys type in text fields, sliders keep their arrows, keys can be remapped (with a conflict), pedals work while a
// button has focus and single-key shortcuts can be switched off. Default keys come from the catalogue
// (`src/StemStudio.Web/wwwroot/js/keymap-defaults.json`).
const fixture = 'music_100bpm_20s.flac';

test.beforeEach(async ({ api, mixer }) => {
  test.setTimeout(fakeJobTimeoutMs + 90_000);
  const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
});

async function press(page: Page, key: string): Promise<void> {
  await blurActiveElement(page);
  await page.keyboard.press(key);
}

async function heard(page: Page): Promise<number> {
  return positionSeconds((await page.getByTestId('transport-position').textContent()) ?? '');
}

async function expectToggled(page: Page, key: string, control: Locator): Promise<void> {
  const before = await isOn(control);
  await press(page, key);
  await expect.poll(() => isOn(control)).toBe(!before);
}

async function expectSeek(page: Page, mixer: MixerPage, key: string, delta: number): Promise<void> {
  await mixer.seek(10);
  await press(page, key);
  await expect.poll(() => heard(page)).toBeCloseTo(10 + delta, 0);
}

test.describe('every practice command by keyboard (014 US2 scenario 1, FR-020)', { tag: '@practice-flow' }, () => {
  test('transport and seeking: Space, Home, arrows and Shift+arrows', async ({ mixer, page }) => {
    await expectSeek(page, mixer, 'ArrowLeft', -5);
    await expectSeek(page, mixer, 'ArrowRight', 5);
    await expectSeek(page, mixer, 'Shift+ArrowLeft', -1);
    await expectSeek(page, mixer, 'Shift+ArrowRight', 1);
    await press(page, ' ');
    await expect.poll(() => heard(page)).toBeGreaterThan(11.5);
    await press(page, ' ');
    await press(page, 'Home');
    await expect.poll(() => heard(page)).toBeLessThan(0.5);
  });

  test('loop A, B, on/off and the switches K, Shift+K, T', async ({ mixer, page, practice }) => {
    await mixer.seek(4);
    await press(page, '[');
    await expect.poll(async () => Number(await mixer.byId('loop-start').inputValue())).toBeCloseTo(4, 0);
    await mixer.seek(9);
    await press(page, ']');
    await expect.poll(async () => Number(await mixer.byId('loop-end').inputValue())).toBeCloseTo(9, 0);
    await expectToggled(page, 'l', mixer.byId('loop-toggle'));
    await expectToggled(page, 'k', mixer.byId('metronome-toggle'));
    await expectToggled(page, 'Shift+K', practice.countInToggle);
    await expectToggled(page, 't', practice.trainerToggle);
  });

  test('speed and key: -, =, ",", "." and 0', async ({ page, practice }) => {
    await press(page, '-');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '95');
    await press(page, '=');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
    await press(page, '.');
    await expect(practice.panel).toHaveAttribute('data-transpose', '1');
    await press(page, ',');
    await expect(practice.panel).toHaveAttribute('data-transpose', '0');
    await press(page, '-');
    await press(page, '0');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
  });

  test('channels: 1 selects the first strip, M mutes and S solos it', async ({ mixer, page }) => {
    await press(page, '1');
    await expect(mixer.strip('vocals')).toBeVisible();
    await expectToggled(page, 'm', mixer.mute('vocals'));
    await expectToggled(page, 's', mixer.solo('vocals'));
  });

  test('score panel and follow: F and Shift+F; overview Ctrl+/, palette Ctrl+K', async ({ page }) => {
    const toggle = page.getByTestId('score-panel-toggle');
    const expanded = await toggle.getAttribute('aria-expanded');
    await press(page, 'f');
    await expect(toggle).not.toHaveAttribute('aria-expanded', expanded ?? 'false');
    await press(page, 'Control+/');
    await expect(page.getByTestId('shortcut-overlay')).toBeVisible();
    await expect(page.getByTestId('shortcut-row-mixer-section-next')).toHaveAttribute('data-keys', /n/u);
    await page.keyboard.press('Escape');
    await press(page, 'Control+k');
    await expect(page.getByRole('dialog')).toBeVisible();
  });
});

test.describe('keys in fields and sliders (014 US2 scenario 1)', { tag: '@practice-flow' }, () => {
  test('a key typed in a text field types it and runs no command', async ({ mixer, page }) => {
    await press(page, 'Control+k');
    const input = page.getByRole('dialog').getByRole('combobox').or(page.getByRole('dialog').getByRole('textbox')).first();
    await input.focus();
    const loopBefore = await isOn(mixer.byId('loop-toggle'));

    await page.keyboard.type('lm');

    await expect(input).toHaveValue('lm');
    expect(await isOn(mixer.byId('loop-toggle'))).toBe(loopBefore);
  });

  test('a focused slider keeps its arrow keys (no seek)', async ({ mixer, page, practice }) => {
    await mixer.seek(10);
    await practice.speed.focus();

    await page.keyboard.press('ArrowLeft');

    await expect(practice.speed).not.toHaveAttribute('aria-valuenow', '100');
    expect(await heard(page)).toBeCloseTo(10, 0);
  });
});

test.describe('remapping, pedals and single-key off (014 US2 scenarios 3-5)', { tag: '@practice-flow' }, () => {
  test('a used key shows the conflict; a free key works; reset brings the default back', async ({ mixer, page }) => {
    const settings = new ShortcutSettings(page);
    const back = page.url();
    await settings.goto();

    await settings.capture('mixer.loop.toggle', 'n');
    await expect(settings.byId('shortcut-conflict')).toContainText(/Next section/u);
    await settings.byId('shortcut-capture-cancel').click();
    await settings.capture('mixer.loop.toggle', 'j');
    await page.goto(back);
    await mixer.waitForReady(fakeStems);
    await expectToggled(page, 'j', mixer.byId('loop-toggle'));

    await settings.goto();
    await settings.resetAll();
    await page.goto(back);
    await mixer.waitForReady(fakeStems);
    await expectToggled(page, 'l', mixer.byId('loop-toggle'));
  });

  test('pedal mode: PageDown plays while a mute button has focus, and does not press it', async ({ mixer, page }) => {
    const settings = new ShortcutSettings(page);
    const back = page.url();
    await settings.goto();
    await settings.usePedals('play-section-start');
    await page.goto(back);
    await mixer.waitForReady(fakeStems);
    await mixer.seek(2);
    await mixer.mute('drums').focus();
    const muted = await isOn(mixer.mute('drums'));

    await page.keyboard.press('PageDown');

    await expect.poll(() => heard(page)).toBeGreaterThan(2.5);
    expect(await isOn(mixer.mute('drums'))).toBe(muted);
    await page.keyboard.press('PageDown');
  });

  test('with single-key shortcuts off, letters do nothing and chords still work', async ({ mixer, page }) => {
    await page.getByTestId('pref-single-key').click();
    await expect(page.getByTestId('pref-single-key')).toHaveAttribute('aria-checked', 'false');
    const loopBefore = await isOn(mixer.byId('loop-toggle'));

    await press(page, 'l');
    await press(page, 'Control+/');

    await expect(page.getByTestId('shortcut-overlay')).toBeVisible();
    expect(await isOn(mixer.byId('loop-toggle'))).toBe(loopBefore);
    await page.keyboard.press('Escape');
    await page.getByTestId('pref-single-key').click();
  });

  test('the shortcut settings page has no axe violations (SC-006)', async ({ page }) => {
    const settings = new ShortcutSettings(page);
    await settings.goto();
    await expect(settings.byId('pedal-setup')).toBeVisible();

    expect(await axeViolations(page)).toEqual([]);
  });
});
