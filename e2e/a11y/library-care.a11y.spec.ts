import type { Locator, Page } from '@playwright/test';
import { axeViolations } from '../support/axe';
import { fakeJobTimeoutMs } from '../support/env';
import { backupNow, deleteArchives, expect, libraryJobTimeoutMs, test } from '../support/library-care';
import { cellState, matrixThemes } from '../support/visual';

// Feature 013 polish (T066; FR-032, WCAG 2.2 AA as in 004): every new modal dialog of library
// care is scanned with axe in both themes at Expert (every control rendered), traps the focus
// while open (Tab and Shift+Tab stay inside), closes with Escape and returns the focus to the
// control that opened it. The pages themselves are scanned in library-care-backup.spec.ts.

/** Test id of the element that has the focus, or of its closest ancestor with one. */
async function focusedInside(page: Page, dialogId: string): Promise<boolean> {
  return page.evaluate((id) => document.activeElement?.closest(`[data-testid="${id}"]`) !== null, dialogId);
}

/** Tabs forwards and backwards through the open dialog; the focus never leaves it. */
async function expectFocusTrapped(page: Page, dialogId: string): Promise<void> {
  await expect.poll(() => focusedInside(page, dialogId), { message: `focus moved into ${dialogId}` }).toBe(true);
  for (const key of [...Array<string>(12).fill('Tab'), ...Array<string>(12).fill('Shift+Tab')]) {
    await page.keyboard.press(key);
    expect(await focusedInside(page, dialogId), `${key} kept the focus in ${dialogId}`).toBe(true);
  }
}

/** Escape closes the dialog and the focus returns to its opener. */
async function expectEscapeReturnsFocus(page: Page, dialog: Locator, opener: Locator): Promise<void> {
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(opener).toBeFocused();
}

/** Opens a dialog with the keyboard, scans it, checks the trap and Escape. */
async function checkDialog(page: Page, opener: Locator, dialogId: string): Promise<void> {
  await opener.focus();
  await page.keyboard.press('Enter');
  const dialog = page.getByTestId(dialogId);
  await expect(dialog).toBeVisible();
  await expect(dialog).toHaveAttribute('aria-modal', 'true');
  expect(await axeViolations(page)).toEqual([]);
  await expectFocusTrapped(page, dialogId);
  await expectEscapeReturnsFocus(page, dialog, opener);
}

for (const theme of matrixThemes) {
  test.describe(`library care dialogs / ${theme}`, { tag: ['@library-care'] }, () => {
    test.use({ storageState: cellState('expert', theme) });

    test('the backup dialog', async ({ care, page }) => {
      await care.goto('backups');
      await expect(care.byId('backup-new')).toBeEnabled();
      await checkDialog(page, care.byId('backup-new'), 'backup-dialog');
    });

    test('the restore dialog and the delete confirmation of an archive', async ({ api, care, careApi, page }) => {
      test.setTimeout(fakeJobTimeoutMs + libraryJobTimeoutMs * 2);
      await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
      const archive = await backupNow(careApi);
      try {
        await care.goto('backups');
        await checkDialog(page, care.byId(`backup-restore-${archive}`), 'restore-dialog');
        await care.byId(`backup-delete-${archive}`).focus();
        await page.keyboard.press('Enter');
        await expect(care.byId('backup-delete-confirm')).toBeVisible();
        expect(await axeViolations(page)).toEqual([]);
        await page.keyboard.press('Escape');
        await expect(care.byId('backup-delete-confirm')).toHaveCount(0);
      } finally {
        await deleteArchives(careApi, [archive]);
      }
    });

    test('the clean-up preview', async ({ care, page }) => {
      await care.goto('storage');
      await expect(care.byId('cleanup-leftovers')).toBeEnabled();
      await checkDialog(page, care.byId('cleanup-leftovers'), 'cleanup-dialog');
    });
  });
}
