/**
 * Client of the feature 013 library-care and storage routes (backend-api 1.12.0, tags
 * `library-care` and `storage`), with the DTO shapes the specs read. Used for set-up and for
 * asserting server state; the UI itself is driven through `pages/library-care-page.ts`.
 *
 * @packageDocumentation
 */
import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { okJson } from './http';

/** Contract `Category`. */
export type BackupCategory =
  | 'database'
  | 'stems'
  | 'artwork'
  | 'pendingUploads'
  | 'detailStems'
  | 'exports'
  | 'scores'
  | 'generations'
  | 'recordings'
  | 'voiceProfiles';

/** Contract `StorageCategory`. */
export type StorageCategory =
  | BackupCategory
  | 'leftovers'
  | 'restoreStaging'
  | 'previousLibrary'
  | 'workerSeparationCache'
  | 'workerDetailCache'
  | 'workerJobFiles'
  | 'workerScratch';

/** Contract `GateMode`. */
export type GateMode = 'open' | 'backupRunning' | 'maintenance';

/** Contract `LibraryJobKind`. */
export type LibraryJobKind = 'backup' | 'verify' | 'restore' | 'undoRestore';

/** Contract `LibraryJobStatus`. */
export type LibraryJobStatus = 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';

/** Contract `FolderState`. */
export interface FolderStateDto {
  readonly available: boolean;
  readonly writable: boolean;
  readonly hostPathHint: string | null;
  readonly freeBytes: number | null;
  readonly totalBytes: number | null;
  readonly reason: string | null;
}

/** Contract `ActiveWork`. */
export interface ActiveWorkDto {
  readonly kind: 'import' | 'detail' | 'export' | 'score' | 'lyricsAlignment' | 'generation' | 'agentRun';
  readonly id: string;
  readonly title: string;
}

/** Contract `ArchiveCounts`. */
export interface ArchiveCountsDto {
  readonly tracks: number;
  readonly mixes: number;
  readonly assets: number;
  readonly generations: number;
  readonly styles: number;
  readonly lyricsSheets: number;
  readonly voiceProfiles: number;
}

/** Contract `PreviousLibrary`. */
export interface PreviousLibraryDto {
  readonly restoreId: string;
  readonly movedAsideAt: string;
  readonly sizeBytes: number;
  readonly counts: ArchiveCountsDto;
  readonly restoredArchive: string | null;
}

/** Contract `JobError`. */
export interface JobErrorDto {
  readonly code: string;
  readonly message: string;
}

/** Contract `CategoryCount`. */
export interface CategoryCountDto {
  readonly category: BackupCategory;
  readonly items: number;
  readonly files: number;
  readonly bytes: number;
}

/** Contract `SkippedItem`. */
export interface SkippedItemDto {
  readonly category: string;
  readonly id: string;
  readonly name: string | null;
  readonly reason: 'same-id' | 'same-source' | 'dependency-skipped' | 'consent-invalid' | 'option' | 'missing-file';
  readonly renamedTo?: string | null;
}

/** Contract `BackupReport` / `VerifyReport` / `RestoreReport` (discriminated by `type`). */
export type LibraryJobReportDto =
  | { readonly type: 'backup'; readonly fileName: string; readonly included: readonly CategoryCountDto[]; readonly entries: number; readonly bytes: number; readonly warnings: readonly unknown[] }
  | { readonly type: 'verify'; readonly ok: boolean; readonly entriesChecked: number; readonly bytesChecked: number; readonly problem: JobErrorDto | null }
  | {
      readonly type: 'restore';
      readonly mode: 'empty' | 'replace' | 'merge' | 'undo';
      readonly restored: readonly CategoryCountDto[];
      readonly skipped: readonly SkippedItemDto[];
      readonly migratedFrom: string | null;
      readonly warnings: readonly string[];
      readonly previousLibraryKept: boolean;
    };

/** Contract `LibraryJob`. */
export interface LibraryJobDto {
  readonly jobId: string;
  readonly kind: LibraryJobKind;
  readonly status: LibraryJobStatus;
  readonly stage: string | null;
  readonly stageText: string | null;
  readonly progress: number;
  readonly bytesDone: number;
  readonly bytesTotal: number;
  readonly currentItem: string | null;
  readonly etaSeconds: number | null;
  readonly archiveFileName: string | null;
  readonly cancellable: boolean;
  readonly report: LibraryJobReportDto | null;
  readonly error: JobErrorDto | null;
  readonly createdAt: string;
  readonly startedAt: string | null;
  readonly finishedAt: string | null;
}

/** Contract `LibraryCareStatus`. */
export interface LibraryCareStatusDto {
  readonly gate: GateMode;
  readonly activeJob: LibraryJobDto | null;
  readonly backupFolder: FolderStateDto;
  readonly previousLibrary: PreviousLibraryDto | null;
  readonly activeWork: readonly ActiveWorkDto[];
  readonly libraryEmpty: boolean;
}

/** Contract `Compatibility`. */
export interface CompatibilityDto {
  readonly restorable: boolean;
  readonly reason: string | null;
  readonly migrationNeeded: boolean;
}

/** Contract `BackupSummary`. */
export interface BackupSummaryDto {
  readonly fileName: string;
  readonly sizeBytes: number;
  readonly modifiedAt: string;
  readonly recognised: boolean;
  readonly reason: string | null;
  readonly backupId: string | null;
  readonly createdAt: string | null;
  readonly appVersion: string | null;
  readonly backendApi: string | null;
  readonly formatVersion: string | null;
  readonly included: readonly BackupCategory[];
  readonly counts: ArchiveCountsDto | null;
  readonly compatibility: CompatibilityDto;
}

/** Contract `BackupList`. */
export interface BackupListDto {
  readonly folder: FolderStateDto;
  readonly archives: readonly BackupSummaryDto[];
}

/** Contract `BackupOptions` (every member defaults to true). */
export interface BackupOptionsDto {
  readonly detailStems?: boolean;
  readonly exports?: boolean;
  readonly scores?: boolean;
  readonly generations?: boolean;
  readonly recordings?: boolean;
  readonly voiceProfiles?: boolean;
}

/** Contract `CategorySize`. */
export interface CategorySizeDto {
  readonly category: BackupCategory;
  readonly included: boolean;
  readonly optional: boolean;
  readonly bytes: number;
  readonly files: number;
  readonly regenerability: 'original' | 'regenerable';
}

/** Contract `BackupEstimate`. */
export interface BackupEstimateDto {
  readonly totalBytes: number;
  readonly categories: readonly CategorySizeDto[];
  readonly folder: FolderStateDto;
  readonly enough: boolean;
}

/** Contract `RestoreOptions`. */
export interface RestoreOptionsDto {
  readonly mode: 'replace' | 'merge';
  readonly voiceProfiles?: boolean;
  readonly confirmation?: string | null;
}

/** Contract `StorageCategoryUsage`. */
export interface StorageCategoryUsageDto {
  readonly category: StorageCategory;
  readonly bytes: number | null;
  readonly files: number | null;
  readonly regenerability: 'original' | 'regenerable';
  readonly howToRegenerate: string | null;
  readonly costHint: string | null;
}

/** Contract `TrackStorage`. */
export interface TrackStorageDto {
  readonly trackId: string;
  readonly title: string;
  readonly artist: string | null;
  readonly totalBytes: number;
  readonly sourceBytes: number | null;
  readonly stemsBytes: number;
  readonly detailStemsBytes: number;
  readonly detailFilesRemoved: boolean;
  readonly artworkBytes: number;
  readonly exportsBytes: number;
  readonly scoresBytes: number;
  readonly generationsBytes: number;
  readonly recordingsBytes: number;
  readonly workerCacheBytes: number | null;
}

/** Contract `Volume`. */
export interface VolumeDto {
  readonly id: 'library' | 'backups' | 'workerData' | 'modelCache';
  readonly label: string;
  readonly freeBytes: number | null;
  readonly totalBytes: number | null;
  readonly sharedWith: readonly string[];
  readonly reason: string | null;
}

/** Contract `StorageOverview`. */
export interface StorageOverviewDto {
  readonly measuredAt: string;
  readonly volumes: readonly VolumeDto[];
  readonly categories: readonly StorageCategoryUsageDto[];
  readonly tracks: readonly TrackStorageDto[];
  readonly unattached: {
    readonly exportsBytes: number;
    readonly scoresBytes: number;
    readonly generationsBytes: number;
    readonly voiceProfilesBytes: number;
    readonly lyricsSheets: number;
  };
  readonly modelCaches: readonly { readonly id: string; readonly bytes: number; readonly models: readonly string[] }[];
  readonly workerEntries: readonly {
    readonly audioSha256Prefix: string;
    readonly bytes: number;
    readonly inUse: boolean;
    readonly fromLibrary: boolean;
    readonly trackId: string | null;
  }[];
  readonly backups: { readonly count: number; readonly bytes: number; readonly folderAvailable: boolean; readonly reason: string | null };
  readonly worker: { readonly available: boolean; readonly reason: string | null };
}

/** Contract `CleanupKind`. */
export type CleanupKind = 'detailStems' | 'workerCache' | 'leftovers';

/** Contract `CleanupAction` (`trackIds` null or absent = all tracks). */
export interface CleanupActionDto {
  readonly kind: CleanupKind;
  readonly trackIds?: readonly string[] | null;
}

/** Contract `CleanupItem`. */
export interface CleanupItemDto {
  readonly kind: CleanupKind;
  readonly trackId: string | null;
  readonly title: string | null;
  readonly files: number;
  readonly bytes: number;
  readonly howToRegenerate: string;
}

/** Contract `CleanupSkip`. */
export interface CleanupSkipDto {
  readonly kind: CleanupKind;
  readonly trackId: string | null;
  readonly title: string | null;
  readonly reason: 'detail-active' | 'job-active' | 'export-active' | 'worker-unavailable' | 'nothing-to-remove';
}

/** Contract `CleanupPlan`. */
export interface CleanupPlanDto {
  readonly planId: string;
  readonly createdAt: string;
  readonly expiresAt: string;
  readonly items: readonly CleanupItemDto[];
  readonly skipped: readonly CleanupSkipDto[];
  readonly totalBytes: number;
}

/** Contract `CleanupResult`. */
export interface CleanupResultDto {
  readonly planId: string;
  readonly freedBytes: number;
  readonly removed: readonly { readonly kind: CleanupKind; readonly trackId: string | null; readonly files: number; readonly bytes: number }[];
  readonly failed: readonly CleanupSkipDto[];
}

/** An RFC 9457 problem with the 013 extension members. */
export interface ProblemDto {
  readonly type?: string;
  readonly title: string;
  readonly status: number;
  readonly detail?: string;
  readonly activeWork?: readonly ActiveWorkDto[];
  readonly requiredBytes?: number;
  readonly freeBytes?: number;
}

/** Problem code of a `type` URL (`https://stemstudio.local/problems/<code>`). */
export function problemCode(problem: ProblemDto): string {
  return (problem.type ?? '').split('/').pop() ?? '';
}

const terminal: readonly LibraryJobStatus[] = ['completed', 'failed', 'cancelled'];
const pollIntervals = [250, 500, 1000];

/** Archive file names the API accepts (backup-archive 1.0.0 section 1). */
export const archiveNamePattern = /^[A-Za-z0-9][A-Za-z0-9._-]{0,119}\.zip$/;

/** Typed client of `/api/library-care/**`, `/api/backups/**`, `/api/library-jobs/**`, `/api/storage/**`. */
export class LibraryCareApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/library-care/status` (answers during maintenance too). */
  async status(): Promise<LibraryCareStatusDto> {
    return okJson<LibraryCareStatusDto>(await this.request.get('/api/library-care/status'));
  }

  /** `GET /api/backups`. */
  async listBackups(): Promise<BackupListDto> {
    return okJson<BackupListDto>(await this.request.get('/api/backups'));
  }

  /** `GET /api/backups/estimate` with the given options as query flags. */
  async estimate(options: BackupOptionsDto = {}): Promise<BackupEstimateDto> {
    const params = Object.fromEntries(Object.entries(options).map(([key, value]) => [key, String(value)]));
    return okJson<BackupEstimateDto>(await this.request.get('/api/backups/estimate', { params }));
  }

  /** `POST /api/backups`; the raw response (202 job, 409 problem). */
  async createBackupResponse(options: BackupOptionsDto = {}): Promise<APIResponse> {
    return this.request.post('/api/backups', { data: options });
  }

  /** `POST /api/backups`; returns the accepted job. */
  async createBackup(options: BackupOptionsDto = {}): Promise<LibraryJobDto> {
    return okJson<LibraryJobDto>(await this.createBackupResponse(options));
  }

  /** `GET /api/backups/{fileName}` (raw: 200 details, 404, 422 not a backup). */
  async backupDetails(fileName: string): Promise<APIResponse> {
    return this.request.get(`/api/backups/${encodeURIComponent(fileName)}`);
  }

  /** `DELETE /api/backups/{fileName}`; returns the HTTP status. */
  async deleteBackup(fileName: string): Promise<number> {
    return (await this.request.delete(`/api/backups/${encodeURIComponent(fileName)}`)).status();
  }

  /** `GET /api/backups/{fileName}/file` with an optional `Range`. */
  async download(fileName: string, range?: string): Promise<APIResponse> {
    const headers = range === undefined ? undefined : { Range: range };
    return this.request.get(`/api/backups/${encodeURIComponent(fileName)}/file`, headers ? { headers } : {});
  }

  /** The archive's bytes (whole file). */
  async archiveBytes(fileName: string): Promise<Buffer> {
    const response = await this.download(fileName);
    expect(response.status(), `download ${fileName}`).toBe(200);
    return response.body();
  }

  /** `POST /api/backups/{fileName}/verify` (raw). */
  async verifyResponse(fileName: string): Promise<APIResponse> {
    return this.request.post(`/api/backups/${encodeURIComponent(fileName)}/verify`);
  }

  /** `POST /api/backups/{fileName}/restore` (raw: 202 job, 409/422 problems). */
  async restoreResponse(fileName: string, options: RestoreOptionsDto): Promise<APIResponse> {
    return this.request.post(`/api/backups/${encodeURIComponent(fileName)}/restore`, { data: options });
  }

  /** Starts a restore and returns the accepted job. */
  async restore(fileName: string, options: RestoreOptionsDto): Promise<LibraryJobDto> {
    return okJson<LibraryJobDto>(await this.restoreResponse(fileName, options));
  }

  /** `GET /api/library-jobs/{jobId}`. */
  async job(jobId: string): Promise<LibraryJobDto> {
    return okJson<LibraryJobDto>(await this.request.get(`/api/library-jobs/${jobId}`));
  }

  /** `POST /api/library-jobs/{jobId}/cancel` (raw: 202 or 409 not-cancellable). */
  async cancel(jobId: string): Promise<APIResponse> {
    return this.request.post(`/api/library-jobs/${jobId}/cancel`);
  }

  /** Polls a library job until it is terminal; returns it. */
  async waitForJob(jobId: string, timeoutMs: number): Promise<LibraryJobDto> {
    const status = (): Promise<LibraryJobStatus> => this.job(jobId).then((job) => job.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(new RegExp(`^(${terminal.join('|')})$`));
    return this.job(jobId);
  }

  /** Polls until the job reports `status` (for example `running`). */
  async waitForJobStatus(jobId: string, status: LibraryJobStatus, timeoutMs: number): Promise<LibraryJobDto> {
    await expect.poll(() => this.job(jobId).then((job) => job.status), { timeout: timeoutMs, intervals: [100, 250] }).toBe(status);
    return this.job(jobId);
  }

  /** Polls until the gate is open again (after a backup or a restore switch). */
  async waitForGateOpen(timeoutMs: number): Promise<void> {
    await expect.poll(() => this.status().then((status) => status.gate), { timeout: timeoutMs, intervals: pollIntervals }).toBe('open');
  }

  /** `GET /api/library-care/previous` or `null` when there is none (404). */
  async previous(): Promise<PreviousLibraryDto | null> {
    const response = await this.request.get('/api/library-care/previous');
    return response.status() === 404 ? null : okJson<PreviousLibraryDto>(response);
  }

  /** `POST /api/library-care/previous/revert` ("Undo restore"); returns the undo job. */
  async revert(): Promise<LibraryJobDto> {
    return okJson<LibraryJobDto>(await this.request.post('/api/library-care/previous/revert'));
  }

  /** `POST /api/library-care/previous/discard` with the typed confirmation; returns the status. */
  async discard(confirmation = 'DISCARD'): Promise<number> {
    return (await this.request.post('/api/library-care/previous/discard', { data: { confirmation } })).status();
  }

  /** `GET /api/storage` (`refresh=true` bypasses the 30 s cache). */
  async storage(refresh = true): Promise<StorageOverviewDto> {
    return okJson<StorageOverviewDto>(await this.request.get('/api/storage', { params: { refresh: String(refresh) } }));
  }

  /** `POST /api/storage/cleanup/preview` (raw). */
  async previewResponse(actions: readonly CleanupActionDto[]): Promise<APIResponse> {
    return this.request.post('/api/storage/cleanup/preview', { data: { actions } });
  }

  /** Previews a clean-up and returns the plan. */
  async preview(actions: readonly CleanupActionDto[]): Promise<CleanupPlanDto> {
    return okJson<CleanupPlanDto>(await this.previewResponse(actions));
  }

  /** `POST /api/storage/cleanup` (raw: 200 result, 409 plan-stale / plan-expired / backup-running). */
  async executeResponse(planId: string): Promise<APIResponse> {
    return this.request.post('/api/storage/cleanup', { data: { planId } });
  }

  /** Executes a plan and returns the result. */
  async execute(planId: string): Promise<CleanupResultDto> {
    return okJson<CleanupResultDto>(await this.executeResponse(planId));
  }
}

/** Asserts a problem response with `status` and `code`; returns the problem. */
export async function expectProblem(response: APIResponse, status: number, code: string): Promise<ProblemDto> {
  expect(response.status(), response.url()).toBe(status);
  const problem = (await response.json()) as ProblemDto;
  expect(problemCode(problem)).toBe(code);
  return problem;
}

/** The newest archive written by a completed backup job (its report or `archiveFileName`). */
export function archiveOf(job: LibraryJobDto): string {
  const name = job.report?.type === 'backup' ? job.report.fileName : job.archiveFileName;
  expect(name, `backup job ${job.jobId} names its archive`).toMatch(archiveNamePattern);
  return name ?? '';
}
