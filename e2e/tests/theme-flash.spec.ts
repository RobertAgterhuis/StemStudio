import type { ResolvedTheme } from '../pages/app-shell';
import { levelState, type ThemePreference } from '../support/preferences';
import { distance, firstFrame, nearerTheme, themeBackgrounds } from '../support/theme-flash';
import { expect, test } from '../support/ux-test';

// T037 / SC-004, quickstart V-D1: the first painted frame has the right theme in 20 of 20 reloads.
// The stored choice is set against the OS setting, so an explicit choice must win over the OS.
const reloads = 20;

interface FlashCase {
  readonly stored: ThemePreference;
  readonly os: ResolvedTheme;
  readonly expected: ResolvedTheme;
}

const cases: readonly FlashCase[] = [
  { stored: 'dark', os: 'light', expected: 'dark' },
  { stored: 'light', os: 'dark', expected: 'light' },
  { stored: 'system', os: 'dark', expected: 'dark' },
  { stored: 'system', os: 'light', expected: 'light' },
];

for (const { stored, os, expected } of cases) {
  test.describe(`stored theme ${stored}, OS ${os}`, () => {
    test.use({ storageState: levelState('advanced', { theme: stored }), colorScheme: os });

    test(`the first frame is ${expected} in ${String(reloads)} of ${String(reloads)} reloads`, { tag: '@ux' }, async ({ page }) => {
      await page.goto('/');
      await expect(page.getByTestId('app-shell')).toHaveAttribute('data-theme', expected);
      const backgrounds = await themeBackgrounds(page);
      expect(distance(backgrounds.dark, backgrounds.light), 'the two themes need different page backgrounds').toBeGreaterThan(30);

      const seen: string[] = [];
      for (let i = 0; i < reloads; i++) {
        const frame = await firstFrame(page);
        seen.push(`${frame.attribute ?? 'none'}/${nearerTheme(frame.pixel, backgrounds)}`);
      }
      expect(seen).toEqual(Array.from({ length: reloads }, () => `${expected}/${expected}`));
    });
  });
}
