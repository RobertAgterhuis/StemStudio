import { expect, type Locator, type Page } from '@playwright/test';
import { fillAndCommit } from '../support/controls';
import type { SectionKind } from '../support/practice-flow';

/**
 * The section editor of the mixer (ui-test-ids 1.13.0, feature 014, Advanced and Expert): one row per section
 * (`section-editor-row-<n>`, 1-based), split at the playhead, add, delete, merge with the next, Done / Cancel.
 */
export class SectionEditor {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The editor panel. */
  get panel(): Locator {
    return this.byId('section-editor');
  }

  /** Every row, in order. */
  get rows(): Locator {
    return this.panel.locator('[data-testid^="section-editor-row-"]');
  }

  /** The error text (409/422), present only while an error is shown. */
  get error(): Locator {
    return this.byId('section-editor-error');
  }

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Opens the editor from the mixer (`section-edit`). */
  async open(): Promise<void> {
    await this.byId('section-edit').click();
    await expect(this.panel).toBeVisible();
  }

  /** Saves with Done. */
  async done(): Promise<void> {
    await this.byId('section-editor-done').click();
  }

  /** Splits the section under the playhead. */
  async split(): Promise<void> {
    await this.byId('section-editor-split').click();
  }

  /** Merges row `n` with the next one. */
  async merge(n: number): Promise<void> {
    await this.byId(`section-editor-merge-${String(n)}`).click();
  }

  /** Deletes row `n`. */
  async remove(n: number): Promise<void> {
    await this.byId(`section-editor-delete-${String(n)}`).click();
  }

  /** Renames row `n`. */
  async rename(n: number, label: string): Promise<void> {
    await fillAndCommit(this.byId(`section-editor-label-${String(n)}`), label);
  }

  /** Changes the kind of row `n`. */
  async setKind(n: number, kind: SectionKind): Promise<void> {
    await this.byId(`section-editor-kind-${String(n)}`).selectOption(kind);
  }

  /** Moves the start of row `n` (bar number or time, as the field takes it). */
  async setStart(n: number, value: string): Promise<void> {
    await fillAndCommit(this.byId(`section-editor-start-${String(n)}`), value);
  }
}
