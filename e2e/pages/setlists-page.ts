import { expect, type Download, type Locator, type Page } from '@playwright/test';
import { fillAndCommit } from '../support/controls';

/**
 * The setlists page (`/setlists`) and the setlist editor (`/setlists/{id}`) of feature 014 (ui-test-ids 1.13.0): create,
 * open, play, delete; add songs with an optional mix, reorder with the up/down buttons, notes, remove, PDF export.
 */
export class SetlistsPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Opens the list. */
  async goto(): Promise<void> {
    await this.page.goto('/setlists');
    await expect(this.byId('setlists-page')).toBeVisible();
  }

  /** Creates a setlist through the UI and returns its id (from the editor URL it opens, or the new row). */
  async create(name: string): Promise<string> {
    await this.byId('setlist-create').click();
    await fillAndCommit(this.byId('setlist-create-name'), name);
    await this.byId('setlist-create-confirm').click();
    await expect(this.page).toHaveURL(/\/setlists\/[0-9a-f-]{36}$/u);
    return new URL(this.page.url()).pathname.split('/').pop() ?? '';
  }

  /** Opens the editor of a setlist. */
  async open(setlistId: string): Promise<void> {
    await this.page.goto(`/setlists/${setlistId}`);
    await expect(this.byId('setlist-name')).toBeVisible();
  }

  /** Adds a song (and optionally a mix of it) with the track picker. */
  async add(trackId: string, mixName?: string): Promise<void> {
    await this.byId('setlist-add').click();
    await expect(this.byId('setlist-add-dialog')).toBeVisible();
    await this.byId(`setlist-add-track-${trackId}`).click();
    if (mixName !== undefined) {
      await this.byId('setlist-add-mix').selectOption({ label: mixName });
    }
    await this.byId('setlist-add-confirm').click();
    await expect(this.byId('setlist-add-dialog')).toBeHidden();
  }

  /** The entry rows in play order (`data-position`). */
  get entries(): Locator {
    return this.page.locator('[data-testid^="setlist-entry-"][data-position]');
  }

  /** Entry ids in play order. */
  async entryIds(): Promise<string[]> {
    const ids = await this.entries.evaluateAll((rows) => rows.map((row) => row.getAttribute('data-testid') ?? ''));
    return ids.map((id) => id.replace(/^setlist-entry-/u, ''));
  }

  /** Opens the PDF dialog and downloads with the default options. */
  async downloadPdf(): Promise<Download> {
    await this.byId('setlist-pdf').click();
    const download = this.page.waitForEvent('download');
    await this.byId('setlist-pdf-download').click();
    return download;
  }
}

/** The setlist bar in the mixer during play mode (ui-test-ids 1.13.0). */
export class SetlistBar {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The bar (`data-setlist-id`, `data-position`, `data-count`). */
  get bar(): Locator {
    return this.page.getByTestId('setlist-bar');
  }

  /** "Next song". */
  get next(): Locator {
    return this.page.getByTestId('setlist-bar-next');
  }

  /** "Previous song". */
  get previous(): Locator {
    return this.page.getByTestId('setlist-bar-previous');
  }

  /** The entry note. */
  get note(): Locator {
    return this.page.getByTestId('setlist-bar-note');
  }

  /** Waits until play mode shows the mixer of `trackId` with the bar. */
  async expectOn(trackId: string): Promise<void> {
    await expect(this.page).toHaveURL(new RegExp(`/tracks/${trackId}/mixer`, 'u'));
    await expect(this.bar).toBeVisible();
  }

  /** Leaves play mode. */
  async exit(): Promise<void> {
    await this.page.getByTestId('setlist-bar-exit').click();
    await expect(this.bar).toBeHidden();
  }
}
