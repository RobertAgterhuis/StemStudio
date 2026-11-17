/**
 * Swaps the api's SQLite database for a 022 fixture (`tests/fixtures/library/db/`, written by the
 * backend seeder T044) and back, for the opt-in `@library-db` specs (SC-003 migration, SC-004
 * search speed). Uses `docker compose cp` on the stopped api of the stack under test
 * (`E2E_COMPOSE_FILE`, `E2E_COMPOSE_PROJECT`); never any other compose project. The original
 * database is copied out first and put back by {@link LibraryDatabaseSwap.restore}.
 *
 * @packageDocumentation
 */
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from '@playwright/test';
import { compose, requireCompose, startService, stopService } from './compose';
import { libraryFixtureDir } from './library-seed';

/** The api's database inside the container (`StemStudio__DataDir=/data`). */
export const apiDatabasePath = '/data/stemstudio.db';

const stateDir = path.resolve(__dirname, '..', '.state', 'library-db');

/** Absolute path of a database fixture, skipping the test when it has not been generated. */
export function requireDatabaseFixture(relative: string): string {
  const file = path.join(libraryFixtureDir, relative);
  test.skip(!existsSync(file), `${relative} is missing; generate it with the backend seeder (tests/fixtures/library/README.md)`);
  return file;
}

/**
 * Empties the SQLite side files of the stopped api (an empty `-wal` is ignored and an empty
 * `-shm` is rebuilt), so the copied database is read exactly as copied. `docker compose cp`
 * cannot delete, so empty files are copied over them.
 */
function emptySideFiles(): void {
  const empty = path.join(stateDir, 'empty');
  writeFileSync(empty, '');
  for (const side of ['-wal', '-shm']) {
    compose(['cp', empty, `api:${apiDatabasePath}${side}`]);
  }
}

/** Copies `-wal` of the stopped api next to `target` when there is one (best effort). */
function saveWal(target: string): void {
  try {
    compose(['cp', `api:${apiDatabasePath}-wal`, `${target}-wal`]);
  } catch {
    // No WAL: the clean shutdown checkpointed it.
  }
}

/** A database swap that can be undone. */
export class LibraryDatabaseSwap {
  private constructor(private readonly backup: string) {}

  /**
   * Stops the api, saves its database, copies `fixture` in and starts the api again (the start-up
   * migration and filing backfill then run on the fixture). Skips unless `E2E_COMPOSE=1`.
   */
  static start(fixture: string): LibraryDatabaseSwap {
    requireCompose();
    mkdirSync(stateDir, { recursive: true });
    const backup = path.join(stateDir, `original-${String(Date.now())}.db`);
    stopService('api');
    compose(['cp', `api:${apiDatabasePath}`, backup]);
    saveWal(backup);
    compose(['cp', fixture, `api:${apiDatabasePath}`]);
    emptySideFiles();
    startService('api');
    return new LibraryDatabaseSwap(backup);
  }

  /** Puts the original database back and restarts the api. */
  restore(): void {
    stopService('api');
    compose(['cp', this.backup, `api:${apiDatabasePath}`]);
    emptySideFiles();
    if (existsSync(`${this.backup}-wal`)) {
      compose(['cp', `${this.backup}-wal`, `api:${apiDatabasePath}-wal`]);
    }
    startService('api');
  }
}
