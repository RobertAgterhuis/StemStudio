import { expect, type Locator, type Page } from '@playwright/test';
import type { LyricsStatus } from '../support/lyrics';

/** The lyrics card on the track detail page `/tracks/{id}` (ui-test-ids 1.6.0, feature 006). */
export class LyricsSection {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the page. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The card. */
  get section(): Locator {
    return this.byId('lyrics-section');
  }

  /** Status (`data-status` = `<lyricsStatus>`). */
  get status(): Locator {
    return this.byId('lyrics-status');
  }

  /** Credit text. */
  get credit(): Locator {
    return this.byId('lyrics-credit');
  }

  /** "Align to the vocals" button. */
  get align(): Locator {
    return this.byId('lyrics-align');
  }

  /** Expected alignment time text. */
  get alignEstimate(): Locator {
    return this.byId('lyrics-align-estimate');
  }

  /** Alignment progress (`aria-valuenow` 0-100). */
  get alignProgress(): Locator {
    return this.byId('lyrics-align-progress');
  }

  /** Device badge of the alignment (Advanced and Expert). */
  get alignDevice(): Locator {
    return this.byId('lyrics-align-device');
  }

  /** Readable reason of a failed lookup or alignment. */
  get error(): Locator {
    return this.byId('lyrics-error');
  }

  /** Static, unsynced text. */
  get preview(): Locator {
    return this.byId('lyrics-preview');
  }

  /** "Search again". */
  get search(): Locator {
    return this.byId('lyrics-search');
  }

  /** The personal-practice notice. */
  get copyrightNotice(): Locator {
    return this.byId('lyrics-copyright-notice');
  }

  /** Opens `/tracks/{id}` and waits for the lyrics card. */
  async goto(trackId: string): Promise<void> {
    await this.page.goto(`/tracks/${trackId}`);
    await this.section.waitFor();
  }

  /** Waits (without reloading) until the status is `status`. */
  async waitForStatus(status: LyricsStatus, timeoutMs = 60_000): Promise<void> {
    await expect(this.status).toHaveAttribute('data-status', status, { timeout: timeoutMs });
  }

  /** Enters lyrics through the paste dialog. */
  async paste(text: string): Promise<void> {
    await this.byId('lyrics-paste-open').click();
    await this.byId('lyrics-paste-input').fill(text);
    await this.byId('lyrics-paste-submit').click();
  }

  /** Removes the lyrics and confirms in the shared confirm dialog (Advanced and Expert). */
  async remove(): Promise<void> {
    await this.byId('lyrics-remove').click();
    await this.byId('confirm-dialog-ok').click();
    await this.waitForStatus('removed');
  }
}
