import type { Locator, Page } from '@playwright/test';

/** Facts of the band info panel (ui-test-ids 1.4.0). */
export type BandFact = 'name' | 'type' | 'years' | 'summary' | 'country' | 'genres' | 'members' | 'website' | 'sources';

/** Band info panel and the correction dialog (track detail page and mixer, feature 005). */
export class BandInfoPanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Button that opens the panel. */
  get open(): Locator {
    return this.page.getByTestId('band-info-open');
  }

  /** The panel. */
  get panel(): Locator {
    return this.page.getByTestId('band-info-panel');
  }

  /** Status element (`data-status` = metadata status). */
  get status(): Locator {
    return this.page.getByTestId('band-info-status');
  }

  /** One fact of the panel. */
  fact(name: BandFact): Locator {
    return this.page.getByTestId(`band-info-${name}`);
  }

  /** Wikipedia attribution block (three links + "Excerpt, unmodified"). */
  get attribution(): Locator {
    return this.page.getByTestId('band-info-attribution');
  }

  /** Retry (statuses failed, notFound). */
  get retry(): Locator {
    return this.page.getByTestId('band-info-retry');
  }

  /** Opens the correction dialog. */
  get correct(): Locator {
    return this.page.getByTestId('band-info-correct');
  }

  /** Notice shown when online lookups are off. */
  get disabled(): Locator {
    return this.page.getByTestId('band-info-disabled');
  }

  /** Opens the panel and waits for it. */
  async show(): Promise<void> {
    await this.open.click();
    await this.panel.waitFor();
  }

  /** Corrects title and artist in the dialog and submits. */
  async submitCorrection(title: string, artist: string): Promise<void> {
    await this.correct.click();
    await this.page.getByTestId('metadata-correct-title').fill(title);
    await this.page.getByTestId('metadata-correct-artist').fill(artist);
    await this.page.getByTestId('metadata-correct-submit').click();
  }
}
