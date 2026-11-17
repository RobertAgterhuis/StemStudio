using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.Api;

/// <summary>
/// Library care operations of backend API 1.12.0 (feature 013): status, backups, restore, library jobs, the previous
/// library, the storage overview and the clean-up. Failures throw <see cref="ApiException"/>; its
/// <see cref="ApiError.Code"/> carries the problem code (<c>library-job-running</c>, <c>plan-stale</c>, ...).
/// </summary>
public interface ILibraryCareClient
{
    /// <summary><c>GET /api/library-care/status</c> (answered also during maintenance).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Gate mode, active job, backups folder, previous library and restore blockers.</returns>
    Task<LibraryCareStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/backups</c>: the archives in the backups folder, newest first.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The folder state and the archives.</returns>
    Task<BackupListDto> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/backups/estimate</c>: size per category and free space for the options.</summary>
    /// <param name="options">The optional categories.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The estimate.</returns>
    Task<BackupEstimateDto> EstimateBackupAsync(BackupOptionsDto options, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/backups</c>: starts a backup job.</summary>
    /// <param name="options">The optional categories.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued job (202).</returns>
    Task<LibraryJobDto> CreateBackupAsync(BackupOptionsDto options, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/backups/{fileName}</c>: manifest summary, consent proofs and compatibility.</summary>
    /// <param name="fileName">The archive file name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The details.</returns>
    Task<BackupDetailsDto> GetBackupAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/backups/{fileName}</c>.</summary>
    /// <param name="fileName">The archive file name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the archive is deleted (204).</returns>
    Task DeleteBackupAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/backups/{fileName}/verify</c>: starts a verify job.</summary>
    /// <param name="fileName">The archive file name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued job (202).</returns>
    Task<LibraryJobDto> VerifyBackupAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/backups/{fileName}/restore</c>: starts a restore job.</summary>
    /// <param name="fileName">The archive file name.</param>
    /// <param name="options">Mode, voice profiles and the typed confirmation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued job (202).</returns>
    Task<LibraryJobDto> RestoreBackupAsync(string fileName, RestoreOptionsDto options, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/library-jobs?limit=</c>: recent library jobs, newest first.</summary>
    /// <param name="limit">1-50.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The jobs.</returns>
    Task<IReadOnlyList<LibraryJobDto>> ListJobsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/library-jobs/{jobId}</c>.</summary>
    /// <param name="jobId">The job.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The job.</returns>
    Task<LibraryJobDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/library-jobs/{jobId}/cancel</c>.</summary>
    /// <param name="jobId">The job.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The job (202); <c>not-cancellable</c> throws.</returns>
    Task<LibraryJobDto> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/library-care/previous/revert</c>: "Undo restore".</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued undo job (202).</returns>
    Task<LibraryJobDto> RevertRestoreAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/library-care/previous/discard</c>: deletes the previous library for good.</summary>
    /// <param name="confirmation">The typed word, <c>DISCARD</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when it is deleted (204).</returns>
    Task DiscardPreviousLibraryAsync(string confirmation, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/storage?refresh=</c>.</summary>
    /// <param name="refresh">Bypass the 30-second cache.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The storage overview.</returns>
    Task<StorageOverviewDto> GetStorageOverviewAsync(bool refresh = false, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/storage/cleanup/preview</c>: a plan; nothing is removed.</summary>
    /// <param name="request">The clean-up actions.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The plan (valid for 10 minutes).</returns>
    Task<CleanupPlanDto> PreviewCleanupAsync(CleanupRequestDto request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/storage/cleanup</c>: executes a previewed plan.</summary>
    /// <param name="planId">The plan.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>What was removed; <c>plan-stale</c>, <c>plan-expired</c> and <c>backup-running</c> throw.</returns>
    Task<CleanupResultDto> ExecuteCleanupAsync(Guid planId, CancellationToken cancellationToken = default);
}
