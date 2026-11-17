import type { Locator, Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import type { TrackDto } from '../support/api';
import { fillAndCommit, isOn, setToggle } from '../support/controls';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { blurActiveElement, expectHelpMarker } from '../support/help';
import {
  advancedPracticeIds,
  everyLevelPracticeIds,
  expectNoIdWithPrefix,
  expectNotRendered,
  expectRendered,
  expertPracticeIds,
} from '../support/levels';
import { expertState, levelState } from '../support/preferences';
import { expect, test } from '../support/test';

// Feature 012 polish (T066; FR-016, FR-017; quickstart P13): level visibility of the practice
// panel (ui-test-ids 1.11.0), keyboard operation incl. the shortcuts `-`, `=`, `,`, `.`, `r`, `c`,
// `t`, spoken values, live regions and help markers. Fake stack, the 12 s click fixture (reliable
// grid, so the count-in is available). The axe scans and the visual baselines of the panel are in
// a11y/ and visual/ (screen `mixer-practice`).
let track: TrackDto;

test.beforeEach(async ({ api, mixer }) => {
  test.setTimeout(fakeJobTimeoutMs + 60_000);
  track = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
});

/** The practice controls a keyboard user must reach with Tab (with the speed changed). */
const tabTargets: readonly string[] = [
  'practice-panel-toggle',
  'practice-speed',
  'practice-speed-up',
  'practice-transpose-down',
  'practice-transpose-up',
  'practice-reset',
  'countin-toggle',
];

/** One focus stop: the focused element's test id and whether it matches `:focus-visible`. */
interface TabStop {
  readonly id: string;
  readonly focusVisible: boolean;
}

/** Presses Tab `count` times and records every focus stop. */
async function tabStops(page: Page, count: number): Promise<TabStop[]> {
  const stops: TabStop[] = [];
  for (let tab = 0; tab < count; tab++) {
    await page.keyboard.press('Tab');
    stops.push(await page.evaluate(() => {
      const el = document.activeElement;
      return { id: el?.getAttribute('data-testid') ?? '', focusVisible: el?.matches(':focus-visible') ?? false };
    }));
  }
  return stops;
}

async function pressOutsideFields(page: Page, key: string): Promise<void> {
  await blurActiveElement(page);
  await page.keyboard.press(key);
}

test.describe('practice panel disclosure (012 integration, ui-test-ids 1.11.0)', { tag: '@practice' }, () => {
  // Nothing stored: the panel starts closed at neutral settings (CTO decision 2026-11-17).
  test.use({ storageState: levelState('advanced', {}, 'unset') });

  test('starts closed at neutral settings, opens with its toggle and the browser remembers the choice', async ({ mixer, page, practice }) => {
    const toggle = practice.byId('practice-panel-toggle');
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await expect(toggle).toHaveAttribute('aria-controls', 'practice-panel-body');
    await expect(practice.byId('practice-panel-body')).toBeHidden();
    await expect(practice.speed).toBeHidden();
    await toggle.focus();
    await page.keyboard.press('Enter');
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(practice.speed).toBeVisible();

    await page.reload();
    await mixer.waitForReady(fakeStems);
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  });

  test('without a stored choice a practice setting opens the panel', async ({ page, practice }) => {
    await pressOutsideFields(page, '.');
    await expect(practice.panel).toHaveAttribute('data-transpose', '1');
    await expect(practice.byId('practice-panel-toggle')).toHaveAttribute('aria-expanded', 'true');
    await expect(practice.byId('practice-transpose')).toBeVisible();
  });

  test.describe('closed by choice', () => {
    test.use({ storageState: levelState('advanced', {}, 'collapsed') });

    test('a shortcut changes the speed, the panel stays closed and its title says so', async ({ page, practice }) => {
      await pressOutsideFields(page, '-');
      await expect(practice.panel).toHaveAttribute('data-speed', '95');
      await expect(practice.byId('practice-panel-toggle')).toHaveAttribute('aria-expanded', 'false');
      await expect(practice.byId('practice-panel-summary')).toHaveText('95 %');
    });
  });
});

test.describe('Beginner', { tag: '@practice' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('renders speed, transpose, reset and the count-in switch; no trainer, bars or Expert ids (P13, FR-016)', async ({ page, practice }) => {
    await expectRendered(page, everyLevelPracticeIds);
    await expectNotRendered(page, [...advancedPracticeIds, ...expertPracticeIds]);
    await expectNoIdWithPrefix(page, 'trainer-');
    await practice.setSpeed(65);
    await expect(practice.byId('practice-quality-hint')).toBeVisible();
    await expect(practice.byId('practice-speed-reset')).toHaveCount(0);
    await expect(practice.byId('practice-effective-bpm')).toBeVisible();
  });

  test('the count-in switch gives one bar; `t` is ignored below Advanced (FR-016, FR-017)', async ({ mixer, page, practice }) => {
    await setToggle(practice.countInToggle, true);
    await pressOutsideFields(page, 't');
    await expectNoIdWithPrefix(page, 'trainer-');
    await mixer.play();
    await expect(practice.countInIndicator).toHaveAttribute('data-beats-total', '4');
  });
});

test.describe('Advanced', { tag: '@practice' }, () => {
  test('adds the trainer, count-in bars and every pass, and "100 %" while the speed is changed (P13)', async ({ page, practice }) => {
    await expectRendered(page, [...everyLevelPracticeIds, ...advancedPracticeIds]);
    await expectNotRendered(page, expertPracticeIds);
    await expect(practice.byId('practice-speed-reset')).toHaveCount(0);
    await practice.setSpeed(95);
    await expect(practice.byId('practice-speed-reset')).toBeVisible();
    await practice.byId('practice-speed-reset').click();
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
    await expect(practice.byId('practice-speed-reset')).toHaveCount(0);
  });

  test('controls speak their values and use the slider keys (FR-017)', async ({ practice }) => {
    await expect(practice.panel).toHaveAttribute('role', 'group');
    await expect(practice.panel).toHaveAttribute('aria-labelledby', /\S/u);
    await practice.setSpeed(75);
    await expect(practice.speed).toHaveAttribute('aria-valuetext', '75 percent');
    for (const [key, value] of [['ArrowRight', '76'], ['PageDown', '66'], ['Home', '50'], ['End', '120']] as const) {
      await practice.speed.press(key);
      await expect(practice.speed).toHaveAttribute('aria-valuenow', value);
    }
    for (const [semitones, spoken] of [[2, '+2 semitones'], [0, '0 semitones'], [-3, '-3 semitones']] as const) {
      await practice.setTranspose(semitones);
      await expect(practice.transpose).toHaveAttribute('aria-valuetext', spoken);
    }
    await expect(practice.countInToggle).toHaveAttribute('aria-checked', /^(true|false)$/u);
    await expect(practice.trainerToggle).toHaveAttribute('aria-checked', /^(true|false)$/u);
    await expect(practice.trainerStatus).toHaveAttribute('aria-live', 'polite');
  });

  test('Tab reaches every practice control with a visible focus ring (FR-017)', async ({ page, practice }) => {
    await practice.setSpeed(95);
    await practice.byId('practice-speed-down').focus();
    const stops = await tabStops(page, 25);
    const reached = stops.filter((stop) => tabTargets.includes(stop.id));
    expect([...new Set(reached.map((stop) => stop.id))].sort()).toEqual([...tabTargets].sort());
    expect(reached.filter((stop) => !stop.focusVisible)).toEqual([]);
  });
});

test.describe('Advanced keyboard and help', { tag: '@practice' }, () => {
  test('the shortcuts -, =, ",", ".", r, c and t work outside text fields and not inside them (US1-8, FR-017)', async ({ mixer, page, practice }) => {
    await pressOutsideFields(page, '-');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '95');
    await pressOutsideFields(page, '=');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
    await pressOutsideFields(page, '.');
    await expect(practice.panel).toHaveAttribute('data-transpose', '1');
    await pressOutsideFields(page, ',');
    await expect(practice.panel).toHaveAttribute('data-transpose', '0');
    await practice.set(90, 2);
    await pressOutsideFields(page, 'r');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
    await expect(practice.panel).toHaveAttribute('data-transpose', '0');
    await pressOutsideFields(page, 'c');
    await expect.poll(() => isOn(practice.countInToggle)).toBe(true);
    await expectTrainerShortcut(mixer, page, practice.trainerToggle);
    await mixer.byId('loop-start').focus();
    await page.keyboard.press('=');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
  });

  test('the practice controls carry their help markers (FR-016)', async ({ practice }) => {
    await practice.setSpeed(65);
    await expectHelpMarker(practice.speed, 'practice-speed');
    await expectHelpMarker(practice.transpose, 'practice-transpose');
    await expectHelpMarker(practice.countInToggle, 'practice-count-in');
    await expectHelpMarker(practice.byId('trainer-panel'), 'practice-speed-trainer');
    await expectHelpMarker(practice.byId('practice-quality-hint'), 'practice-sound-quality');
    await expect(practice.byId('practice-quality-hint')).toBeVisible();
  });
});

async function expectTrainerShortcut(mixer: MixerPage, page: Page, toggle: Locator): Promise<void> {
  await mixer.setLoop(2, 6, true);
  await pressOutsideFields(page, 't');
  await expect.poll(() => isOn(toggle)).toBe(true);
  await pressOutsideFields(page, 't');
  await expect.poll(() => isOn(toggle)).toBe(false);
}

test.describe('Expert', { tag: '@practice' }, () => {
  test.use({ storageState: expertState });

  test('adds number entry and the stretch diagnostics (P13, FR-016)', async ({ page, practice }) => {
    await expectRendered(page, [...everyLevelPracticeIds, ...advancedPracticeIds, ...expertPracticeIds]);
    await fillAndCommit(practice.byId('practice-speed-input'), '85');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '85');
    await fillAndCommit(practice.byId('practice-transpose-input'), '-4');
    await expect(practice.panel).toHaveAttribute('data-transpose', '-4');
    await practice.waitForStretch('active');
    await expect(practice.byId('practice-stretch-status')).toContainText('Stretch: active');
    await expect(practice.byId('practice-stretch-status')).toHaveAttribute('data-underruns', '0');
    await expect(practice.byId('practice-stretch-status')).toHaveAttribute('data-latency-ms', /^\d+$/u);
    await expectHelpMarker(practice.byId('practice-stretch-status'), 'practice-sound-quality');
  });
});
