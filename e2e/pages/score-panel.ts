import { expect, type Locator, type Page } from '@playwright/test';

/**
 * The Score panel of the mixer (ui-test-ids 1.13.0, feature 014): score select, the follower host `score-view` (the engine
 * writes `data-bar-index`, `data-bar-number`, `data-page`, `data-position-ms`), pages, the follow switch and the notes.
 */
export class ScorePanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The panel region (`data-asset-id`, `data-view-status`). */
  get panel(): Locator {
    return this.byId('score-panel');
  }

  /** The follower host. */
  get view(): Locator {
    return this.byId('score-view');
  }

  /** Auto-scroll on/off (`aria-pressed`). */
  get follow(): Locator {
    return this.byId('score-follow-toggle');
  }

  /** The score select. */
  get select(): Locator {
    return this.byId('score-select');
  }

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** One page container (1-based). */
  pageOf(n: number): Locator {
    return this.byId(`score-page-${String(n)}`);
  }

  /** Opens the panel when it is closed. */
  async open(): Promise<void> {
    const toggle = this.byId('score-panel-toggle');
    if ((await toggle.getAttribute('aria-expanded')) !== 'true') {
      await toggle.click();
    }
    await expect(this.panel).toBeVisible();
  }

  /** Chooses a score by its option text ("Bass - <name>") and waits until its view is ready and page 1 is shown. */
  async choose(label: string, assetId: string, timeoutMs: number): Promise<void> {
    await this.select.selectOption({ label });
    await expect(this.panel).toHaveAttribute('data-asset-id', assetId);
    await expect(this.panel).toHaveAttribute('data-view-status', 'ready', { timeout: timeoutMs });
    await expect(this.pageOf(1).locator('img')).toBeVisible();
  }

  /** The bar number the follower shows (`data-bar-number`), or NaN without one. */
  async barNumber(): Promise<number> {
    return Number((await this.view.getAttribute('data-bar-number')) ?? Number.NaN);
  }

  /** The bar index the follower shows (`data-bar-index`), or NaN without one. */
  async barIndex(): Promise<number> {
    return Number((await this.view.getAttribute('data-bar-index')) ?? Number.NaN);
  }
}
