/**
 * Control of the compose stack for the opt-in `@worker-restart` spec (feature 002, FR-019).
 * Everything else in the suite treats the stack as a black box (TEST-STRATEGY.md section 5).
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import { composeFile } from './env';

/**
 * Runs `docker compose -f <E2E_COMPOSE_FILE> restart worker` (argument list, no shell) and
 * returns when the command has finished.
 * @throws Error with docker's output when the command fails (for example no Docker on the host).
 */
export function restartWorker(timeoutMs = 120_000): void {
  execFileSync('docker', ['compose', '-f', composeFile, 'restart', 'worker'], {
    stdio: 'pipe',
    timeout: timeoutMs,
  });
}
