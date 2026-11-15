import { expect, type Locator, type Page } from '@playwright/test';
import type { AssetStatus } from '../support/assets-api';

/** The library page `/library` (ui-test-ids 1.5.0, feature 007). */
export class LibraryPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the page. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Row of one item (`data-status`). */
  row(assetId: string): Locator {
    return this.byId(`asset-row-${assetId}`);
  }

  /** Name of one item. */
  name(assetId: string): Locator {
    return this.byId(`asset-name-${assetId}`);
  }

  /** Source track of one item (`data-track-available`). */
  source(assetId: string): Locator {
    return this.byId(`asset-source-${assetId}`);
  }

  /** The `<audio>` player of a ready item. */
  player(assetId: string): Locator {
    return this.byId(`asset-play-${assetId}`);
  }

  /** The download link of a ready item. */
  download(assetId: string): Locator {
    return this.byId(`asset-download-${assetId}`);
  }

  /** "Lowered by X dB" note (only when the export was scaled down). */
  gainNote(assetId: string): Locator {
    return this.byId(`asset-gain-note-${assetId}`);
  }

  /** Opens `/library`. */
  async goto(): Promise<void> {
    await this.page.goto('/library');
    await this.byId('library-total-size').waitFor();
  }

  /** Waits (without reloading) until the row shows `status`. */
  async waitForStatus(assetId: string, status: AssetStatus, timeoutMs = 60_000): Promise<void> {
    await expect(this.row(assetId)).toHaveAttribute('data-status', status, { timeout: timeoutMs });
  }

  /** Filters by source track (Advanced/Expert). */
  async filterByTrack(trackId: string): Promise<void> {
    await this.byId('library-filter-track').selectOption(trackId);
  }

  /** Renames one item through its rename button, input and confirm. */
  async rename(assetId: string, name: string): Promise<void> {
    await this.byId(`asset-rename-${assetId}`).click();
    await this.byId('asset-rename-input').fill(name);
    await this.byId('asset-rename-confirm').click();
    await expect(this.name(assetId)).toHaveText(name);
  }

  /** Deletes one item and confirms in the shared confirm dialog. */
  async delete(assetId: string): Promise<void> {
    await this.byId(`asset-delete-${assetId}`).click();
    await this.byId('confirm-dialog-ok').click();
    await expect(this.row(assetId)).toHaveCount(0);
  }

  /**
   * Duration in seconds the item's `<audio>` element reports once its metadata is loaded. The
   * players use `preload="none"` (a long list loads nothing up front), so the metadata is
   * requested here the way pressing play would.
   */
  async playerDuration(assetId: string): Promise<number> {
    const player = this.player(assetId);
    await player.evaluate((el) => {
      const audio = el as HTMLAudioElement;
      audio.preload = 'metadata';
      audio.load();
    });
    await expect.poll(() => player.evaluate((el) => (el as HTMLAudioElement).readyState)).toBeGreaterThanOrEqual(1);
    return player.evaluate((el) => (el as HTMLAudioElement).duration);
  }
}
