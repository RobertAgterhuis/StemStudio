import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** The style library `/styles` (ui-test-ids 1.8.0 `style-*`). */
export class StyleLibraryPage extends TestIdPage {
  /** Opens `/styles` and waits for the first row or the empty state. */
  async goto(): Promise<void> {
    await this.page.goto('/styles');
    await expect(this.rows.first().or(this.empty)).toBeVisible();
  }

  /** Every style row (`data-kind` = `preset` / `user`). */
  get rows(): Locator {
    return this.page.locator('[data-testid^="style-row-"]');
  }

  /** Empty state when the filters match nothing. */
  get empty(): Locator {
    return this.byId('style-empty');
  }

  /** The row of one style. */
  row(styleId: string): Locator {
    return this.byId(`style-row-${styleId}`);
  }

  /** A row action: `duplicate` (every style), `edit` and `delete` (user styles only). */
  action(action: 'duplicate' | 'edit' | 'delete', styleId: string): Locator {
    return this.byId(`style-${action}-${styleId}`);
  }

  /** Filters: `genre`, `mood`, `instrument` or `kind` (all / presets / mine). */
  async filter(kind: 'genre' | 'mood' | 'instrument' | 'kind', label: string): Promise<void> {
    await this.byId(`style-filter-${kind}`).selectOption({ label });
  }

  /** Types into the search box. */
  async search(text: string): Promise<void> {
    await this.byId('style-search').fill(text);
  }

  /** Deletes a user style through the confirm dialog. */
  async delete(styleId: string): Promise<void> {
    await this.action('delete', styleId).click();
    await this.byId('style-delete-confirm').click();
    await expect(this.row(styleId)).toHaveCount(0);
  }

  /** Style ids of the visible rows, in order. */
  async visibleIds(): Promise<string[]> {
    const ids = await this.rows.evaluateAll((rows) => rows.map((row) => row.getAttribute('data-testid') ?? ''));
    return ids.map((id) => id.replace('style-row-', ''));
  }
}
