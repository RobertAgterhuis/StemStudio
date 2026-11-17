/**
 * Helpers of the feature 013 restore and merge specs: the fixture catalogues of qa T012, a
 * fingerprint of the library (to prove a refused restore changed nothing, SC-004) and the outcome
 * of a restore attempt that may be refused synchronously (`409`/`422`) or by the job.
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, test, type APIResponse } from '@playwright/test';
import type { StemStudioApi } from './api';
import { fixtureArchiveDir } from './backup-archive';
import { libraryJobTimeoutMs } from './library-care';
import { problemCode, type LibraryCareApi, type LibraryJobDto, type ProblemDto, type RestoreOptionsDto, type SkippedItemDto } from './library-care-api';

/** One archive of `hostile-manifest.json` (vector, expected outcome, data-model 3.4 step). */
export interface HostileArchive {
  readonly file: string;
  readonly vector: string;
  readonly outcome: string;
  readonly step: number;
  readonly summary: string;
}

/** One archive of `schema-manifest.json` (expected row state and restore outcome). */
export interface StateArchive {
  readonly file: string;
  readonly state: 'restorable' | 'migration' | 'incompatible' | 'unrecognised';
  readonly outcome: string;
  readonly summary: string;
}

function catalogue<T>(name: string): T[] {
  return (JSON.parse(readFileSync(path.join(fixtureArchiveDir, '..', name), 'utf8')) as { archives: T[] }).archives;
}

/** The hostile corpus (H1-H17) and the valid control. */
export const hostileArchives: readonly HostileArchive[] = catalogue<HostileArchive>('hostile-manifest.json');

/** The schema / state archives. */
export const stateArchives: readonly StateArchive[] = catalogue<StateArchive>('schema-manifest.json');

/**
 * Archives of the corpus that verify and restore must both refuse with their code. H16
 * (`insufficient-disk-space`) is a restore-only vector: verify extracts nothing, so it does not
 * check free space (CTO decision 2026-11-17).
 */
export const refusedArchives: readonly HostileArchive[] = hostileArchives.filter((item) => item.outcome.startsWith('archive-'));

/** What identifies the library's content for an unchanged check (ids and update times). */
export async function libraryFingerprint(api: StemStudioApi): Promise<string> {
  const tracks = (await api.listTracks()).map((track) => `${track.id}@${track.updatedAt}`).sort();
  const mixes = (await api.listMixes()).map((mix) => `${mix.id}@${mix.updatedAt}`).sort();
  return JSON.stringify({ tracks, mixes });
}

/** Problem code of a refused restore or verify: synchronous problem or the job's error. */
export async function refusalCode(careApi: LibraryCareApi, response: APIResponse): Promise<string> {
  if (response.status() !== 202) return problemCode((await response.json()) as ProblemDto);
  const job = await careApi.waitForJob(((await response.json()) as { jobId: string }).jobId, libraryJobTimeoutMs);
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  return jobProblemCode(job);
}

/** Code of a finished job's problem: the verify report's problem, else the job error, else its status. */
function jobProblemCode(job: LibraryJobDto): string {
  return verifyProblemCode(job) ?? job.error?.code ?? `job ${job.status}`;
}

/** Code of the problem in a verify report, if any. */
function verifyProblemCode(job: LibraryJobDto): string | undefined {
  return job.report?.type === 'verify' ? job.report.problem?.code : undefined;
}

/** Replace-restore options with the typed confirmation. */
export function replaceOptions(voiceProfiles = true): RestoreOptionsDto {
  return { mode: 'replace', voiceProfiles, confirmation: 'REPLACE' };
}

/** Asserts that no previous library exists (a refused restore must not move anything aside). */
export async function expectNoPreviousLibrary(careApi: LibraryCareApi): Promise<void> {
  expect(await careApi.previous()).toBeNull();
}

/** State archives whose restore button must be disabled (`incompatible`, `unrecognised`). */
export const notRestorableArchives: readonly StateArchive[] = stateArchives.filter((item) => item.state === 'incompatible' || item.state === 'unrecognised');

/** The restore report of a job, or a report with nothing skipped and no migration. */
export function restoreReportOf(job: LibraryJobDto): { readonly skipped: readonly SkippedItemDto[]; readonly migratedFrom: string | null } {
  return job.report?.type === 'restore' ? job.report : { skipped: [], migratedFrom: null };
}

/** The corpus archive of a research R-06 vector. */
export function hostileArchive(vector: string): HostileArchive {
  const found = hostileArchives.find((item) => item.vector === vector);
  if (found === undefined) throw new Error(`no fixture archive for ${vector}`);
  return found;
}

/** Skips unless the library is empty (`@fresh-library`, the first spec on a fresh project). */
export async function requireEmptyLibrary(careApi: LibraryCareApi): Promise<void> {
  test.skip(!(await careApi.status()).libraryEmpty, 'needs an empty library (run first on a fresh project: npm run test:library-care-fresh)');
}
