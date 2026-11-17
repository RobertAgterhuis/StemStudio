import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** One level of the library info (`library-info-<level>`). */
export type LibraryInfoField = 'artist' | 'album' | 'song';

/** Values to set: a string sets an own name, `'auto'` ticks "Automatic", absent leaves the field. */
export type LibraryInfoEdit = Partial<Readonly<Record<LibraryInfoField, string>>>;

/** The library info dialog (ui-test-ids 1.22.0, feature 022 US4; every level). */
export class LibraryInfoDialog extends TestIdPage {
  /** The dialog (`data-scope` = `track` / `item`). */
  get dialog(): Locator {
    return this.byId('library-info-dialog');
  }

  /** Readable reason (400 field messages, 404, 409). */
  get error(): Locator {
    return this.byId('library-info-error');
  }

  /** "Look up band info again" (track scope). */
  get lookup(): Locator {
    return this.byId('library-info-lookup');
  }

  /** Text input of one level. */
  input(field: LibraryInfoField): Locator {
    return this.byId(`library-info-${field}`);
  }

  /** "Automatic" checkbox of one level. */
  automatic(field: LibraryInfoField): Locator {
    return this.byId(`library-info-auto-${field}`);
  }

  /** Opens the dialog for the song folder's track (`library-info-edit`). */
  async openForTrack(): Promise<void> {
    await this.byId('library-info-edit').click();
    await expect(this.dialog).toHaveAttribute('data-scope', 'track');
  }

  /** Opens the dialog for one item (`asset-library-info-<assetId>`). */
  async openForItem(assetId: string): Promise<void> {
    await this.byId(`asset-library-info-${assetId}`).click();
    await expect(this.dialog).toHaveAttribute('data-scope', 'item');
  }

  /** Fills the given fields (`'auto'` ticks Automatic) without saving. */
  async fill(edit: LibraryInfoEdit): Promise<void> {
    for (const field of ['artist', 'album', 'song'] as const) {
      const value = edit[field];
      if (value === 'auto') {
        await this.automatic(field).check();
      } else if (value !== undefined) {
        await this.automatic(field).uncheck();
        await this.input(field).fill(value);
      }
    }
  }

  /** Fills, saves and waits until the dialog closes. */
  async save(edit: LibraryInfoEdit): Promise<void> {
    await this.fill(edit);
    await this.byId('library-info-save').click();
    await expect(this.dialog).toBeHidden();
  }

  /**
   * "Reset" (all automatic). The contract does not say whether Reset saves at once, so a dialog
   * that stays open is saved explicitly; either way it ends closed.
   */
  async reset(): Promise<void> {
    await this.byId('library-info-reset').click();
    if (await this.dialog.isVisible()) {
      await this.byId('library-info-save').click();
    }
    await expect(this.dialog).toBeHidden();
  }
}
