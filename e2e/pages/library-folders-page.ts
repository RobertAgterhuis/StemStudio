import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** Level of a folder view (`library-folders[data-level]`, ui-test-ids 1.22.0). */
export type FolderViewLevel = 'root' | 'artist' | 'album' | 'song';

/** Art kind of a tile or header (`data-art`). */
export type FolderArtKind = 'cover' | 'mosaic' | 'placeholder';

/** Child order of the Advanced `library-sort` select. */
export type FolderSortChoice = 'name' | 'year' | 'recent' | 'size' | 'items';

/** The folder view `/library` and `/library/{folderId}` (ui-test-ids 1.22.0, feature 022). */
export class LibraryFoldersPage extends TestIdPage {
  /** The folder view container (`data-level`). */
  get view(): Locator {
    return this.byId('library-folders');
  }

  /** The breadcrumb `<nav>` landmark. */
  get breadcrumb(): Locator {
    return this.byId('library-breadcrumb');
  }

  /** `h1` of the open folder ("Library" at the root). */
  get title(): Locator {
    return this.byId('library-folder-title');
  }

  /** "Sorting your library (n items left)"; exists only while items wait for filing. */
  get filingPending(): Locator {
    return this.byId('library-filing-pending');
  }

  /** "This folder is empty now" for an unknown or emptied folder id. */
  get folderMissing(): Locator {
    return this.byId('library-folder-missing');
  }

  /** Opens the library root and waits for the folder view. */
  async gotoRoot(): Promise<void> {
    await this.page.goto('/library');
    await expect(this.view).toHaveAttribute('data-level', 'root');
  }

  /** Opens one folder by id (deep link); does not assert that it exists. */
  async gotoFolder(folderId: string): Promise<void> {
    await this.page.goto(`/library/${folderId}`);
    // The empty-folder state is shown inside the folder view, so both may match.
    await expect(this.view.or(this.folderMissing).first()).toBeVisible();
  }

  /** The tile (link) of one child folder (`data-level`, `data-art`). */
  tile(folderId: string): Locator {
    return this.byId(`folder-tile-${folderId}`);
  }

  /** The art of one tile (1 `<img>` for cover, 2-4 for mosaic, or the placeholder). */
  tileArt(folderId: string): Locator {
    return this.byId(`folder-art-${folderId}`);
  }

  /** Name text of one tile. */
  tileName(folderId: string): Locator {
    return this.byId(`folder-name-${folderId}`);
  }

  /** Counts text of one tile. */
  tileCounts(folderId: string): Locator {
    return this.byId(`folder-counts-${folderId}`);
  }

  /** Every child tile in display order. */
  get tiles(): Locator {
    return this.page.getByTestId(/^folder-tile-(ar|al|so)[0-9a-f]{16}$/u);
  }

  /** The crumb of a parent folder. */
  crumb(folderId: string): Locator {
    return this.byId(`library-crumb-${folderId}`);
  }

  /** An item row inside a song folder (007 component). */
  row(assetId: string): Locator {
    return this.byId(`asset-row-${assetId}`);
  }

  /** The "Artist > Album > Song" path of an item (three links). */
  itemPath(assetId: string): Locator {
    return this.byId(`asset-path-${assetId}`);
  }

  /** Opens a child folder by clicking its tile and waits for the level. */
  async openTile(folderId: string, level: Exclude<FolderViewLevel, 'root'>): Promise<void> {
    await this.tile(folderId).click();
    await expect(this.view).toHaveAttribute('data-level', level);
  }

  /** Walks root > artist > album > song through the tiles. */
  async openSongFolder(path: { readonly artistId: string; readonly albumId: string; readonly songId: string }): Promise<void> {
    await this.gotoRoot();
    await this.openTile(path.artistId, 'artist');
    await this.openTile(path.albumId, 'album');
    await this.openTile(path.songId, 'song');
  }

  /** Switches grid/list and checks `aria-pressed`. */
  async setLayout(layout: 'grid' | 'list'): Promise<void> {
    await this.byId(`library-layout-${layout}`).click();
    await expect(this.byId(`library-layout-${layout}`)).toHaveAttribute('aria-pressed', 'true');
  }

  /** Chooses the child order (Advanced/Expert only). */
  async sortBy(sort: FolderSortChoice): Promise<void> {
    await this.byId('library-sort').selectOption(sort);
  }

  /** Names of the child tiles in display order. */
  async tileNames(): Promise<string[]> {
    return this.page.locator('[data-testid^="folder-name-"]').allInnerTexts();
  }
}
