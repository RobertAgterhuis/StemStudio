/**
 * Docker compose access for the feature 013 cases that look inside the containers (`@compose`):
 * byte sums of library folders (SC-006), stopping the worker (US3-2), resetting the worker's
 * cache (US2-7 rehydration) and placing archives in the backups folder when the host folder is
 * not known. Commands are argument lists (no shell on the host) against `E2E_COMPOSE_FILE` and,
 * when set, the compose project `E2E_COMPOSE_PROJECT` (the CTO's separate integration project).
 * The suite never touches any other compose project.
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import { test } from '@playwright/test';
import { composeFile } from './env';

/** Compose project of the stack under test (`-p`); empty = compose's default for the file. */
export const composeProject: string = process.env.E2E_COMPOSE_PROJECT ?? '';

/** True when the run may call `docker compose` (`E2E_COMPOSE=1`). */
export const composeAllowed: boolean = process.env.E2E_COMPOSE === '1';

/** Skips the test unless `E2E_COMPOSE=1` (the case runs `docker compose` against the stack). */
export function requireCompose(): void {
  test.skip(!composeAllowed, 'needs E2E_COMPOSE=1 (and E2E_COMPOSE_PROJECT for a separate project): the case runs docker compose');
}

/** Runs `docker compose -f <file> [-p <project>] <args>` and returns its standard output. */
export function compose(args: readonly string[], timeoutMs = 120_000): string {
  const project = composeProject === '' ? [] : ['-p', composeProject];
  return execFileSync('docker', ['compose', '-f', composeFile, ...project, ...args], { encoding: 'utf8', stdio: 'pipe', timeout: timeoutMs });
}

/** `docker compose exec -T <service> <command...>`. */
export function composeExec(service: string, command: readonly string[], timeoutMs = 60_000): string {
  return compose(['exec', '-T', service, ...command], timeoutMs);
}

/**
 * Sum of the sizes of the regular files below each folder inside `service` (missing folders
 * count 0). Uses `find -printf %s` (GNU findutils in both images), because `du -sb` also counts
 * directory entries and SC-006 compares file bytes with zero deviation.
 * @param folders Absolute container paths, constants of the specs (never user input).
 */
export function fileBytes(service: string, folders: readonly string[]): Map<string, number> {
  const sums = new Map<string, number>();
  for (const folder of folders) {
    const output = composeExec(service, ['sh', '-c', `find '${folder}' -type f -printf '%s\\n' 2>/dev/null || true`]);
    sums.set(folder, output.split('\n').filter((line) => line.trim() !== '').reduce((sum, line) => sum + Number(line), 0));
  }
  return sums;
}

/** Folders (with a glob) inside `service` whose files are summed per match, e.g. `/data/tracks/<id>/stems`. */
export function fileBytesOf(service: string, folder: string): number {
  return fileBytes(service, [folder]).get(folder) ?? 0;
}

/** `docker compose stop <service>`. */
export function stopService(service: string): void {
  compose(['stop', service], 120_000);
}

/** `docker compose start <service>` and wait for its health check (`up --wait` would rebuild). */
export function startService(service: string): void {
  compose(['start', service], 120_000);
  compose(['up', '-d', '--no-build', '--wait', service], 180_000);
}

/** Removes every worker cache entry (`/data/cache`), as a lost worker volume would. */
export function resetWorkerCache(): void {
  composeExec('worker', ['sh', '-c', 'rm -rf /data/cache/by-audio /data/cache/by-source']);
}
