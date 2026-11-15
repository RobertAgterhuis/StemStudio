import type { Locator, Page } from '@playwright/test';
import type { ScoreFileRole } from '../support/score-api';

/** The score-specific parts of a library row (ui-test-ids 1.7.0, feature 008). */
export class ScoreItems {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the page. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** PDF viewer of a ready score item (`src` = the pdf URL with `inline=true`). */
  preview(assetId: string): Locator {
    return this.byId(`asset-preview-${assetId}`);
  }

  /** Download link of one file role. */
  download(assetId: string, role: ScoreFileRole): Locator {
    return this.byId(`asset-download-${role}-${assetId}`);
  }

  /** Key text with confidence (absent for drums). */
  key(assetId: string): Locator {
    return this.byId(`asset-score-key-${assetId}`);
  }

  /** Tempo and time signature text. */
  tempo(assetId: string): Locator {
    return this.byId(`asset-score-tempo-${assetId}`);
  }

  /** Warnings list (exists only with warnings). */
  warnings(assetId: string): Locator {
    return this.byId(`asset-score-warnings-${assetId}`);
  }

  /** The profile's quality note. */
  limits(assetId: string): Locator {
    return this.byId(`asset-score-limits-${assetId}`);
  }

  /** Piano model credit (piano items only). */
  attribution(assetId: string): Locator {
    return this.byId(`asset-attribution-${assetId}`);
  }
}
