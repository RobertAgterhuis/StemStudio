import type { Locator, Page } from '@playwright/test';

/** Import screen `/` (ui-test-ids.md, "Import"). */
export class ImportPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Personal-practice / copyright notice (FR-005). */
  get legalNotice(): Locator {
    return this.page.getByTestId('legal-notice');
  }

  /** Help text about separating instruments beyond the model's stems (FR-010). */
  get stemLimitationHelp(): Locator {
    return this.page.getByTestId('stem-limitation-help');
  }

  /** The readable rejection reason; exists only while an error is shown. */
  get error(): Locator {
    return this.page.getByTestId('import-error');
  }

  /** Opens the import screen. */
  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.legalNotice.waitFor();
  }

  /** Selects a local file; the import starts on selection (there is no separate submit id). */
  async uploadFile(filePath: string): Promise<void> {
    await this.page.getByTestId('import-file-input').setInputFiles(filePath);
  }

  /** Enters a YouTube/Spotify URL and submits it. */
  async importUrl(url: string): Promise<void> {
    await this.page.getByTestId('import-url-input').fill(url);
    await this.page.getByTestId('import-url-submit').click();
  }
}
