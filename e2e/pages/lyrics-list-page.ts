import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** The lyrics list `/lyrics` and its editor (ui-test-ids 1.9.0 `lyrics-*`). */
export class LyricsListPage extends TestIdPage {
  /** Opens `/lyrics`. */
  async goto(): Promise<void> {
    await this.page.goto('/lyrics');
    await expect(this.byId('lyrics-new')).toBeVisible();
  }

  /** The row of a lyrics sheet. */
  row(lyricsId: string): Locator {
    return this.byId(`lyrics-row-${lyricsId}`);
  }

  /** A row action: `edit`, `delete`, `song` ("New song from these lyrics"). */
  action(action: 'edit' | 'delete' | 'song', lyricsId: string): Locator {
    return this.byId(`lyrics-${action}-${lyricsId}`);
  }

  /** Readable error of the editor. */
  get error(): Locator {
    return this.byId('lyrics-error');
  }

  /** Writes title and text into the open editor and saves. */
  async saveEditor(title: string, text: string): Promise<void> {
    await this.byId('lyrics-title-input').fill(title);
    await this.byId('lyrics-text-input').fill(text);
    await this.byId('lyrics-save').click();
  }

  /** Creates a sheet through "New" + the editor. */
  async create(title: string, text: string): Promise<void> {
    await this.byId('lyrics-new').click();
    await this.saveEditor(title, text);
  }

  /** Deletes a sheet through its confirm dialog. */
  async delete(lyricsId: string): Promise<void> {
    await this.action('delete', lyricsId).click();
    await this.byId('lyrics-delete-confirm').click();
    await expect(this.row(lyricsId)).toHaveCount(0);
  }
}
