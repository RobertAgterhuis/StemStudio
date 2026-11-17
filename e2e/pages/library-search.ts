import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** A result group of the live search (`search-group-<group>`, ui-test-ids 1.22.0). */
export type SearchGroup = 'artists' | 'albums' | 'songs' | 'items';

/** The live library search box and its results (ui-test-ids 1.22.0, feature 022 US3). */
export class LibrarySearch extends TestIdPage {
  /** The `<input type="search">` (every level). */
  get input(): Locator {
    return this.byId('library-search');
  }

  /** Clear button; exists only while the box has text. */
  get clear(): Locator {
    return this.byId('library-search-clear');
  }

  /** Polite live region with the result count ("12 results"). */
  get status(): Locator {
    return this.byId('library-search-status');
  }

  /** "Only in this folder" (Advanced+, inside a folder). */
  get scope(): Locator {
    return this.byId('library-search-scope');
  }

  /** Results container while the query is not blank. */
  get results(): Locator {
    return this.byId('search-results');
  }

  /** "No matches for '...'" state. */
  get empty(): Locator {
    return this.byId('library-search-empty');
  }

  /** "Clear search" inside the empty state. */
  get emptyClear(): Locator {
    return this.byId('library-search-empty-clear');
  }

  /** Readable reason when the search failed. */
  get error(): Locator {
    return this.byId('library-search-error');
  }

  /** One result group (exists only with hits). */
  group(group: SearchGroup): Locator {
    return this.byId(`search-group-${group}`);
  }

  /** "Show all N" of a group. */
  showAll(group: SearchGroup): Locator {
    return this.byId(`search-show-all-${group}`);
  }

  /** A folder hit. */
  folderHit(folderId: string): Locator {
    return this.byId(`search-hit-${folderId}`);
  }

  /** An item hit (the 007 row inside the results). */
  itemHit(assetId: string): Locator {
    return this.results.getByTestId(`asset-row-${assetId}`);
  }

  /** Item rows inside the results. */
  get itemRows(): Locator {
    return this.results.getByTestId(/^asset-row-[0-9a-f-]{36}$/u);
  }

  /** Highlighted parts (`<mark>`) inside `scope`. */
  marks(scope: Locator): Locator {
    return scope.getByTestId('search-mark');
  }

  /** Types `text` one key at a time (the live search reacts to each key). */
  async type(text: string, delayMs = 60): Promise<void> {
    await this.input.focus();
    await this.input.pressSequentially(text, { delay: delayMs });
  }

  /** Replaces the query and waits until results or the empty state show for it. */
  async search(text: string): Promise<void> {
    await this.input.fill(text);
    // The empty state is shown inside the results section, so both may match.
    await expect(this.results.or(this.empty).first()).toBeVisible();
  }

  /** Ids of the item hits currently shown, in order. */
  async itemIds(): Promise<string[]> {
    const ids = await this.itemRows.evaluateAll((rows) => rows.map((row) => row.getAttribute('data-testid') ?? ''));
    return ids.map((id) => id.replace('asset-row-', ''));
  }
}
