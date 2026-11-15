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

  /**
   * The worker's processing device (ui-test-ids 1.2.0): `data-device` = `gpu`, `cpu` or
   * `unknown`; text "GPU (<name>)", "CPU" or "Unknown".
   */
  get workerDevice(): Locator {
    return this.page.getByTestId('worker-device');
  }

  /** Why the CPU is used although a GPU was requested; exists only while such a reason is set. */
  get workerDeviceReason(): Locator {
    return this.page.getByTestId('worker-device-reason');
  }

  /** Opens the import screen. */
  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.legalNotice.waitFor();
  }

  /**
   * Selects a local file; the import starts on selection (there is no separate submit id).
   * Blazor's `InputFile` attaches its `change` listener in a JS `init` call after the first
   * render; a selection made before that is lost, so wait for the marker `init` sets.
   */
  async uploadFile(filePath: string): Promise<void> {
    const input = this.page.getByTestId('import-file-input');
    await input.evaluate(
      (el) =>
        new Promise<void>((resolve) => {
          const ready = (): void => {
            if ('_blazorInputFileNextFileId' in el) resolve();
            else requestAnimationFrame(ready);
          };
          ready();
        }),
    );
    await input.setInputFiles(filePath);
  }

  /** Enters a YouTube/Spotify URL and submits it. */
  async importUrl(url: string): Promise<void> {
    await this.page.getByTestId('import-url-input').fill(url);
    await this.page.getByTestId('import-url-submit').click();
  }
}
