/**
 * Puts committed fixture archives (tests/fixtures/backup/archives, qa T012) into the stack's
 * backups folder, as a user copies an archive from another machine (FR-010). The folder is the
 * host directory bind-mounted at `/backups` (devops T011): `E2E_BACKUP_DIR` names it for this run
 * (the e2e compose override uses a temporary folder owned by uid 1654). Without it the archive is
 * copied with `docker compose cp` when `E2E_COMPOSE=1`; otherwise the case is skipped.
 *
 * @packageDocumentation
 */
import { copyFileSync, existsSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { test } from '@playwright/test';
import { fixtureArchiveDir } from './backup-archive';
import { compose, composeAllowed } from './compose';

/** Host path of the backups folder of the stack under test, or empty when unknown. */
export const backupDir: string = process.env.E2E_BACKUP_DIR ?? '';

/** Skips unless an archive can be placed into the backups folder. */
export function requireBackupFolderAccess(): void {
  test.skip(backupDir === '' && !composeAllowed, 'set E2E_BACKUP_DIR (host folder mounted at /backups) or E2E_COMPOSE=1');
}

/** Copies the fixture archive `name` into the backups folder (under the same name). */
export function placeFixtureArchive(name: string): void {
  const source = path.join(fixtureArchiveDir, name);
  if (backupDir !== '') {
    copyFileSync(source, path.join(backupDir, name));
    return;
  }
  compose(['cp', source, `api:/backups/${name}`]);
}

/** Removes a placed archive from the host folder (when known; the API delete covers the rest). */
export function removeFromBackupDir(name: string): void {
  if (backupDir !== '') rmSync(path.join(backupDir, name), { force: true });
}

/** File names in the host backups folder (empty when the folder is unknown). */
export function backupDirFiles(): string[] {
  return backupDir !== '' && existsSync(backupDir) ? readdirSync(backupDir) : [];
}
