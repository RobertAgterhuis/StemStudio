import { expect, type Locator, type Page } from '@playwright/test';
import type { KeyCounter } from '../support/keyboard';

/** Command palette (ui-test-ids 1.3.0 "Palette"), driven by the keyboard only. */
export class CommandPalette {
  /**
   * @param page The Playwright page.
   * @param keys Counter through which every key press and text entry is sent (SC-008).
   */
  constructor(
    private readonly page: Page,
    private readonly keys: KeyCounter,
  ) {}

  /** The palette dialog. */
  get dialog(): Locator {
    return this.page.getByTestId('command-palette');
  }

  /** Result item of a command id (`aria-selected` on the active one). */
  command(commandId: string): Locator {
    return this.page.getByTestId(`command-${commandId}`);
  }

  /** Ctrl+K / Cmd+K, then waits for the input to have focus. */
  async open(): Promise<void> {
    await this.keys.press('ControlOrMeta+K');
    await expect(this.dialog).toBeVisible();
    await expect(this.page.getByTestId('command-palette-input')).toBeFocused();
  }

  /** Opens the palette, types `query`, waits until `commandId` is the active result, presses Enter. */
  async run(query: string, commandId: string): Promise<void> {
    await this.open();
    await this.keys.type(query);
    await expect(this.command(commandId)).toHaveAttribute('aria-selected', 'true');
    await this.keys.press('Enter');
    await expect(this.dialog).toBeHidden();
  }

  /** Opens the palette, types `query` and presses Enter on whatever result is active first. */
  async runFirst(query: string): Promise<void> {
    await this.open();
    await this.keys.type(query);
    await expect(this.page.getByTestId(/^command-(?!palette)/).first()).toBeVisible();
    await this.keys.press('Enter');
    await expect(this.dialog).toBeHidden();
  }
}
