import { readFileSync } from 'node:fs';
import { expect, type Locator } from '@playwright/test';
import type { LibraryJobKind, LibraryJobStatus } from '../support/library-care-api';
import { TestIdPage } from './test-id-page';

/** Profile radio of the backup dialog (ui-test-ids 1.12.0). */
export type BackupProfile = 'complete' | 'compact' | 'custom';

/** Optional categories of the backup dialog (`backup-option-<category>`). */
export type BackupOption = 'detail-stems' | 'exports' | 'scores' | 'generations' | 'recordings' | 'voice-profiles';

/** Clean-up kinds of the Storage tab (`cleanup-<kind>`). */
export type CleanupButton = 'detail-stems' | 'worker-cache' | 'leftovers';

/** Sortable columns of the per-track storage table (`storage-sort-<column>`). */
export type StorageColumn =
  | 'total'
  | 'source'
  | 'stems'
  | 'detail-stems'
  | 'artwork'
  | 'exports'
  | 'scores'
  | 'generations'
  | 'recordings'
  | 'worker-cache';

/** Library care page `/care` (feature 013): Backups and Storage tabs, every element by test id. */
export class LibraryCarePage extends TestIdPage {
  /** Page root (`data-gate`). */
  get root(): Locator {
    return this.byId('care-page');
  }

  /** Opens `/care` on a tab and waits for the page root. */
  async goto(tab: 'backups' | 'storage' = 'backups'): Promise<void> {
    await this.page.goto(tab === 'storage' ? '/care?tab=storage' : '/care');
    await expect(this.root).toBeVisible();
    await expect(this.byId(`care-tab-${tab}`)).toHaveAttribute('aria-selected', 'true');
  }

  /** Selects a tab by clicking it. */
  async selectTab(tab: 'backups' | 'storage'): Promise<void> {
    await this.byId(`care-tab-${tab}`).click();
    await expect(this.byId(`care-tab-${tab}`)).toHaveAttribute('aria-selected', 'true');
  }

  // ---- Backups ------------------------------------------------------------------------

  /** Opens the new-backup dialog and waits for its estimate. */
  async openBackupDialog(): Promise<Locator> {
    await this.byId('backup-new').click();
    const dialog = this.byId('backup-dialog');
    await expect(dialog).toBeVisible();
    await expect(this.byId('backup-estimate')).toHaveAttribute('data-total-bytes', /^\d+$/);
    return dialog;
  }

  /** Chooses a profile radio of the open dialog. */
  async chooseProfile(profile: BackupProfile): Promise<void> {
    await this.byId(`backup-profile-${profile}`).check();
  }

  /** Sets one optional category checkbox (Advanced and Expert). */
  async setOption(option: BackupOption, on: boolean): Promise<void> {
    await this.byId(`backup-option-${option}`).setChecked(on);
  }

  /** Starts the backup from the open dialog. */
  async startBackup(): Promise<void> {
    await expect(this.byId('backup-start')).toBeEnabled();
    await this.byId('backup-start').click();
  }

  /** The library job panel. */
  get job(): Locator {
    return this.byId('library-job');
  }

  /** Waits until the job panel shows a job of `kind` with `status`. */
  async waitForJob(kind: LibraryJobKind, status: LibraryJobStatus, timeoutMs: number): Promise<void> {
    await expect(this.job).toHaveAttribute('data-kind', kind, { timeout: timeoutMs });
    await expect(this.job).toHaveAttribute('data-status', status, { timeout: timeoutMs });
  }

  /** Row of an archive. */
  row(fileName: string): Locator {
    return this.byId(`backup-row-${fileName}`);
  }

  /** Downloads an archive through `backup-download-<fileName>`; returns its bytes. */
  async download(fileName: string): Promise<Buffer> {
    const [download] = await Promise.all([this.page.waitForEvent('download'), this.byId(`backup-download-${fileName}`).click()]);
    expect(download.suggestedFilename()).toBe(fileName);
    return readFileSync(await download.path());
  }

  /** Deletes an archive through its confirm dialog. */
  async deleteArchive(fileName: string): Promise<void> {
    await this.byId(`backup-delete-${fileName}`).click();
    await this.byId('backup-delete-confirm').click();
    await expect(this.row(fileName)).toHaveCount(0);
  }

  /** Opens the restore dialog of an archive. */
  async openRestore(fileName: string): Promise<Locator> {
    await this.byId(`backup-restore-${fileName}`).click();
    const dialog = this.byId('restore-dialog');
    await expect(dialog).toBeVisible();
    return dialog;
  }

  /** Replace restore from the open dialog: mode, typed `REPLACE`, start. */
  async confirmReplace(voiceProfiles = true): Promise<void> {
    await this.byId('restore-mode-replace').check();
    const voices = this.byId('restore-voice-profiles');
    if ((await voices.count()) > 0) await voices.setChecked(voiceProfiles);
    await this.byId('restore-confirm-input').fill('REPLACE');
    await expect(this.byId('restore-start')).toBeEnabled();
    await this.byId('restore-start').click();
  }

  /** Merge restore from the open dialog (Advanced and Expert). */
  async confirmMerge(): Promise<void> {
    await this.byId('restore-mode-merge').check();
    await expect(this.byId('restore-start')).toBeEnabled();
    await this.byId('restore-start').click();
  }

  /** "Undo restore" of the previous-library panel. */
  async undoRestore(): Promise<void> {
    await this.byId('previous-library-undo').click();
  }

  /** "Discard previous library" with the typed `DISCARD` confirmation. */
  async discardPrevious(): Promise<void> {
    await this.byId('previous-library-discard').click();
    await this.byId('previous-library-discard-input').fill('DISCARD');
    await this.byId('previous-library-discard-confirm').click();
    await expect(this.byId('previous-library')).toHaveCount(0);
  }

  // ---- Storage ------------------------------------------------------------------------

  /** Bytes shown for a storage category (`storage-category-<category>` `data-bytes`). */
  async categoryBytes(category: string): Promise<number> {
    return Number(await this.byId(`storage-category-${category}`).getAttribute('data-bytes'));
  }

  /** Track row of the per-track table. */
  trackRow(trackId: string): Locator {
    return this.byId(`storage-track-row-${trackId}`);
  }

  /** Track ids of the per-track table in display order. */
  async trackOrder(): Promise<string[]> {
    const ids = await this.byId('storage-tracks-table').locator('[data-testid^="storage-track-row-"]').evaluateAll((rows) => rows.map((row) => row.getAttribute('data-testid') ?? ''));
    return ids.map((id) => id.replace('storage-track-row-', ''));
  }

  /** Clicks a sortable column header until it reports `direction`. */
  async sortBy(column: StorageColumn, direction: 'ascending' | 'descending'): Promise<void> {
    // `storage-sort-<column>` is the sort button inside the column header; `aria-sort` belongs on the `th` (ARIA).
    const button = this.byId(`storage-sort-${column}`);
    const header = button.locator('xpath=ancestor::th[1]');
    // Wait for each click's render before deciding on the next one (a quick second click would reverse the first).
    for (let click = 0; click < 2; click++) {
      const current = (await header.getAttribute('aria-sort')) ?? 'none';
      if (current === direction) break;
      await button.click();
      await expect(header).not.toHaveAttribute('aria-sort', current);
    }
    await expect(header).toHaveAttribute('aria-sort', direction);
  }

  /** Opens the clean-up preview of a kind and waits for its total. */
  async openCleanup(kind: CleanupButton): Promise<number> {
    await this.byId(`cleanup-${kind}`).click();
    await expect(this.byId('cleanup-dialog')).toBeVisible();
    await expect(this.byId('cleanup-plan-total')).toHaveAttribute('data-bytes', /^\d+$/);
    return Number(await this.byId('cleanup-plan-total').getAttribute('data-bytes'));
  }

  /** Confirms the open plan; returns the freed bytes of `cleanup-result`. */
  async confirmCleanup(): Promise<number> {
    await this.byId('cleanup-confirm').click();
    await expect(this.byId('cleanup-result')).toHaveAttribute('data-freed-bytes', /^\d+$/);
    return Number(await this.byId('cleanup-result').getAttribute('data-freed-bytes'));
  }
}
