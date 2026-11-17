import { expect, type Locator, type Page } from '@playwright/test';

/** The shortcut settings page (`/settings/shortcuts`, ui-test-ids 1.13.0, Advanced and Expert) incl. the pedal set-up. */
export class ShortcutSettings {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Opens the page. */
  async goto(): Promise<void> {
    await this.page.goto('/settings/shortcuts');
    await expect(this.byId('shortcut-settings')).toBeVisible();
  }

  /** The test-id form of a command id (`mixer.loop.toggle` -> `mixer-loop-toggle`). */
  static idOf(command: string): string {
    return command.replace(/[.:]/gu, '-');
  }

  /** Starts capturing a new key for `command`, presses `key` and returns (the capture may end in a conflict). */
  async capture(command: string, key: string): Promise<void> {
    await this.byId(`shortcut-edit-${ShortcutSettings.idOf(command)}`).click();
    await expect(this.byId('shortcut-capture')).toBeVisible();
    await this.page.keyboard.press(key);
  }

  /** Resets every shortcut. */
  async resetAll(): Promise<void> {
    await this.byId('shortcut-reset-all').click();
  }

  /** Turns pedal mode on with a preset. */
  async usePedals(preset: string): Promise<void> {
    const mode = this.byId('pedal-mode');
    if ((await mode.getAttribute('aria-checked')) !== 'true') {
      await mode.click();
    }
    await this.byId('pedal-preset').selectOption(preset);
  }
}
