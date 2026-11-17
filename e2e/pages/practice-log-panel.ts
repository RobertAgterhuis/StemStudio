import { expect, type Locator, type Page } from '@playwright/test';

/**
 * The Practice log panel of the mixer and the Practice page (`/practice`) of feature 014 (ui-test-ids 1.13.0). 012's
 * `practice-panel` is the speed and key panel (`pages/practice-panel.ts`).
 */
export class PracticeLogPanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The panel region. */
  get panel(): Locator {
    return this.byId('practice-log-panel');
  }

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Opens the panel when it is closed and acknowledges the first-use notice when it is shown. */
  async open(): Promise<void> {
    const toggle = this.byId('practice-log-panel-toggle');
    if ((await toggle.getAttribute('aria-expanded')) !== 'true') {
      await toggle.click();
    }
    await expect(this.panel).toBeVisible();
    const notice = this.byId('practice-log-notice-ok');
    if (await notice.isVisible()) {
      await notice.click();
    }
  }

  /** Deletes this song's log with its confirmation. */
  async deleteTrackLog(): Promise<void> {
    await this.byId('practice-log-delete-track').click();
    await this.byId('practice-log-delete-confirm').click();
  }
}

/** The Practice page (`/practice`). */
export class PracticePage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Opens the page. */
  async goto(): Promise<void> {
    await this.page.goto('/practice');
    await expect(this.byId('practice-page')).toBeVisible();
  }

  /** The log switch. */
  get enabled(): Locator {
    return this.byId('practice-enabled');
  }
}
