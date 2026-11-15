import type { Locator, Page } from '@playwright/test';

/** Track list `/tracks` (ui-test-ids.md, "Track list"). */
export class TrackListPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Row of one track. */
  row(trackId: string): Locator {
    return this.page.getByTestId(`track-row-${trackId}`);
  }

  /** Status badge of one track. */
  rowStatus(trackId: string): Locator {
    return this.page.getByTestId(`track-row-status-${trackId}`);
  }

  /** "AI" text badge of a generated track's row (ui-test-ids 1.8.0). */
  aiBadge(trackId: string): Locator {
    return this.page.getByTestId(`track-ai-badge-${trackId}`);
  }

  /** Opens `/tracks`. */
  async goto(): Promise<void> {
    await this.page.goto('/tracks');
  }

  /** Opens a track from its row: its first link if it has one, else the row itself. */
  async open(trackId: string): Promise<void> {
    const link = this.row(trackId).getByRole('link').first();
    await ((await link.count()) > 0 ? link.click() : this.row(trackId).click());
  }
}
