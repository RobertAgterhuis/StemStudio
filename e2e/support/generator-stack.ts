/**
 * Control of the generative compose stack for the opt-in cases (`@restart`): restarting the
 * generator (009 edge case "the generative service restarts during a job") and the SC-006 disk
 * walk after a voice profile is deleted (010 quickstart V7). Needs Docker on the host and
 * `E2E_GENERATIVE_COMPOSE_FILES` (the compose files of the stack, separated by the platform's
 * path delimiter); every other case treats the stack as a black box.
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import path from 'node:path';

/** Compose files of the generative stack under test (empty when not configured). */
export function generativeComposeFiles(): string[] {
  const raw = process.env.E2E_GENERATIVE_COMPOSE_FILES ?? '';
  return raw.split(path.delimiter).filter((file) => file !== '');
}

function compose(args: readonly string[], timeoutMs: number): string {
  const files = generativeComposeFiles().flatMap((file) => ['-f', file]);
  return execFileSync('docker', ['compose', ...files, ...args], { stdio: 'pipe', timeout: timeoutMs, encoding: 'utf-8' });
}

/** `docker compose ... restart <service>` (argument list, no shell). */
export function restartService(service: 'generator' | 'singer', timeoutMs = 120_000): void {
  compose(['restart', service], timeoutMs);
}

/**
 * Files below `/data` of `service` whose path contains one of `needles` (case-insensitive), via
 * `docker compose exec -T <service> find /data -type f`.
 */
export function dataFilesContaining(service: 'api' | 'generator' | 'singer', needles: readonly string[], timeoutMs = 60_000): string[] {
  const lower = needles.map((needle) => needle.toLowerCase());
  const files = compose(['exec', '-T', service, 'find', '/data', '-type', 'f'], timeoutMs).split('\n');
  return files.filter((file) => lower.some((needle) => file.toLowerCase().includes(needle)));
}
