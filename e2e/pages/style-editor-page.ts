import { expect, type Locator } from '@playwright/test';
import { applySteps, TestIdPage } from './test-id-page';

/** Values typed into the style editor; unset fields are left as they are. */
export interface StyleEdits {
  readonly name?: string;
  readonly genre?: string;
  readonly subGenre?: string;
  readonly description?: string;
  readonly moods?: readonly string[];
  readonly instruments?: readonly string[];
}

/** The style editor `/styles/new`, `/styles/{id}/edit` (ui-test-ids 1.8.0). */
export class StyleEditorPage extends TestIdPage {
  /** Readable error (`data-field` names the field). */
  get error(): Locator {
    return this.byId('style-error');
  }

  /** Opens the editor of a new style. */
  async gotoNew(): Promise<void> {
    await this.page.goto('/styles/new');
    await expect(this.byId('style-name-input')).toBeVisible();
  }

  /** Applies `edits`. Tag inputs take one tag per Enter. */
  async fill(edits: StyleEdits): Promise<void> {
    const e = edits;
    await applySteps([
      [e.name, () => this.byId('style-name-input').fill(e.name ?? '')],
      [e.genre, () => this.byId('style-genre-select').selectOption({ label: e.genre ?? '' })],
      [e.subGenre, () => this.byId('style-subgenre-input').fill(e.subGenre ?? '')],
      [e.description, () => this.byId('style-description-input').fill(e.description ?? '')],
    ]);
    await this.addTags('style-mood-tags', edits.moods ?? []);
    await this.addTags('style-instrument-tags', edits.instruments ?? []);
  }

  /** Clicks "Save". */
  async save(): Promise<void> {
    await this.byId('style-save').click();
  }

  /** Waits for the error of `field` and returns its text. */
  async expectFieldError(field: string): Promise<string> {
    await expect(this.error).toHaveAttribute('data-field', field);
    return (await this.error.textContent()) ?? '';
  }

  private async addTags(testId: string, tags: readonly string[]): Promise<void> {
    for (const tag of tags) {
      await this.byId(testId).fill(tag);
      await this.byId(testId).press('Enter');
    }
  }
}
