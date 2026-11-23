/**
 * Compose control of the opt-in `@claude-key` specs (011 follow-up claude-key): start the api with
 * or without `ANTHROPIC_API_KEY`, lose the Data Protection key ring, and read what the stack wrote
 * (container logs, the SQLite files) for the sentinel scan. Every command is an argument list
 * (`execFileSync`, no shell on the host); the sentinel travels only in the child's environment,
 * never on a command line.
 *
 * Run the specs from the shell that started the stack: `docker compose up` re-reads the
 * compose variables (`STEMSTUDIO_AGENT_BACKEND`, ...) from this process's environment.
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import { expect, request } from '@playwright/test';
import { claudeKeyVariable } from './claude-key';
import { apiContextOptions, composeFile } from './env';

/** Time for the api to be recreated or restarted and report healthy. */
export const apiRestartTimeoutMs = 180_000;

/** The child environment: this process's, without the Claude key unless `key` is given, no MSYS path rewriting. */
function composeEnvironment(key: string | undefined): NodeJS.ProcessEnv {
  const env: NodeJS.ProcessEnv = { ...process.env, MSYS_NO_PATHCONV: '1' };
  if (key === undefined) {
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete -- the variable name is assembled (secret scan)
    delete env[claudeKeyVariable];
  } else {
    env[claudeKeyVariable] = key;
  }
  return env;
}

/** Runs `docker compose -f <E2E_COMPOSE_FILE> <args>` and returns its standard output. */
function compose(args: readonly string[], key?: string): Buffer {
  return execFileSync('docker', ['compose', '-f', composeFile, ...args], {
    env: composeEnvironment(key),
    stdio: ['ignore', 'pipe', 'pipe'],
    timeout: apiRestartTimeoutMs,
    maxBuffer: 512 * 1024 * 1024,
  });
}

/**
 * Recreates only the api (`up -d --no-deps --wait api`) with `ANTHROPIC_API_KEY` = `key`, or
 * without the variable when `key` is undefined, and waits until it is healthy.
 */
export async function startApiWithEnvironmentKey(key: string | undefined): Promise<void> {
  compose(['up', '-d', '--no-deps', '--wait', '--wait-timeout', '170', 'api'], key);
  await waitForApi();
}

/**
 * Removes the key ring files from the api's key volume and restarts the api, so the stored
 * ciphertext can no longer be decrypted (quickstart Q9, CK-SC-004). The api keeps its environment.
 */
export async function loseKeyRing(): Promise<void> {
  compose(['exec', '-T', 'api', 'sh', '-c', 'rm -f /keys/*.xml']);
  compose(['restart', 'api']);
  await waitForApi();
}

/** Polls `GET /api/agent/status` until it answers 200. */
export async function waitForApi(): Promise<void> {
  const context = await request.newContext(apiContextOptions);
  try {
    await expect
      .poll(async () => (await context.get('/api/agent/status').catch(() => null))?.status() ?? 0, {
        timeout: apiRestartTimeoutMs,
        intervals: [1_000, 2_000],
      })
      .toBe(200);
  } finally {
    await context.dispose();
  }
}

/** Logs of the api and web containers since `since` (an ISO time), stdout and stderr. */
export function containerLogs(since: string): Buffer {
  return compose(['logs', '--no-color', '--since', since, 'api', 'web']);
}

/** The bytes of the api's `stemstudio.db` and its `-wal`/`-shm` files (missing files are skipped). */
export function databaseFiles(): Buffer {
  const script = 'find /data -maxdepth 2 -type f -name "stemstudio.db*" -exec cat {} +; true';
  return compose(['exec', '-T', 'api', 'sh', '-c', script]);
}
