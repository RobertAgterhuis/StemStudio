/**
 * Environment of the stack under test (tasks.md Appendix B: `E2E_BASE_URL`, `E2E_API_URL`).
 *
 * @packageDocumentation
 */
import path from 'node:path';

/** Blazor web front end (default: compose port 8080). */
export const webBaseUrl: string = process.env.E2E_BASE_URL ?? 'http://localhost:8080';

/** Backend API, used for test set-up and for asserting server state (default: port 8081). */
export const apiBaseUrl: string = process.env.E2E_API_URL ?? 'http://localhost:8081';

/**
 * Identifier of this suite run, shared by all workers (set once in `playwright.config.ts`).
 * Mix names include it so reruns against the same data volume never collide.
 */
export function runId(): string {
  return process.env.E2E_RUN_ID ?? 'local';
}

/** Timeout for a fake-separator job to reach a terminal state (upload + ffmpeg + librosa). */
export const fakeJobTimeoutMs = 120_000;

/** Timeout for a real-Demucs job on CPU (20 s clip; `@real` only). */
export const realJobTimeoutMs = 15 * 60_000;

/**
 * Seconds the fake detail models wait per parent step (feature 002). Must equal the stack's
 * `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS`; the `@detail` specs expect 5, so a phase takes ~25 s.
 */
export const fakeDetailStepSeconds = Number(process.env.E2E_FAKE_DETAIL_STEP_SECONDS ?? '5');

/** Timeout for a fake detail phase to reach a terminal state (plan, three groups, finalise). */
export const detailPhaseTimeoutMs = Math.max(60_000, 20 * fakeDetailStepSeconds * 1000);

/** Compose file of the stack under test; used only by the opt-in `@worker-restart` spec. */
export const composeFile: string =
  process.env.E2E_COMPOSE_FILE ?? path.resolve(__dirname, '..', '..', 'docker-compose.yml');
