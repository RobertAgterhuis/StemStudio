import { expect, type Locator, type Page } from '@playwright/test';

/** Mix library `/mixes` (ui-test-ids.md, "Mix library"). Mix ids come from the API. */
export class MixLibraryPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Row of one mix. */
  row(mixId: string): Locator {
    return this.page.getByTestId(`mix-row-${mixId}`);
  }

  /** Opens `/mixes`. */
  async goto(): Promise<void> {
    await this.page.goto('/mixes');
  }

  /** Loads a mix; the mixer opens with its settings. */
  async load(mixId: string): Promise<void> {
    await this.page.getByTestId(`mix-load-${mixId}`).click();
    await this.page.waitForURL(/\/tracks\/[0-9a-f-]{36}\/mixer/);
  }

  /** Duplicates a mix ("<name> (copy)"). */
  async duplicate(mixId: string): Promise<void> {
    await this.page.getByTestId(`mix-duplicate-${mixId}`).click();
  }

  /** Deletes a mix and confirms the dialog; the row disappears. */
  async delete(mixId: string): Promise<void> {
    await this.page.getByTestId(`mix-delete-${mixId}`).click();
    await this.page.getByTestId('confirm-dialog-ok').click();
    await expect(this.row(mixId)).toBeHidden();
  }
}
