using System.Globalization;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Contracts;

/// <summary>Routes of library care and storage (backend-api 1.12.0, feature 013, tags <c>library-care</c> and <c>storage</c>).</summary>
public static partial class ApiRoutes
{
    /// <summary>GET: gate mode, active library job, backups folder and restore blockers (also during maintenance).</summary>
    public const string LibraryCareStatus = "/api/library-care/status";

    /// <summary>GET (list) / POST (create a backup) archives in the backups folder.</summary>
    public const string Backups = "/api/backups";

    /// <summary>GET: estimate of a backup (query: the six options).</summary>
    public const string BackupEstimate = Backups + "/estimate";

    /// <summary>GET: library jobs, newest first (query <c>limit</c>).</summary>
    public const string LibraryJobs = "/api/library-jobs";

    /// <summary>GET: the previous library kept aside by a replace restore.</summary>
    public const string PreviousLibrary = "/api/library-care/previous";

    /// <summary>POST: undo the restore (switch back to the previous library).</summary>
    public const string RevertRestore = PreviousLibrary + "/revert";

    /// <summary>POST: delete the previous library (typed confirmation <c>DISCARD</c>).</summary>
    public const string DiscardPreviousLibrary = PreviousLibrary + "/discard";

    /// <summary>GET: the storage overview (query <c>refresh</c>).</summary>
    public const string Storage = "/api/storage";

    /// <summary>POST: execute a clean-up plan.</summary>
    public const string Cleanup = Storage + "/cleanup";

    /// <summary>POST: preview a clean-up (returns a plan).</summary>
    public const string CleanupPreview = Cleanup + "/preview";

    /// <summary>GET (details) / DELETE one archive.</summary>
    /// <param name="fileName">Archive file name (<see cref="LibraryCareLimits.ArchiveFileNamePattern"/>).</param>
    /// <returns>The relative URL.</returns>
    public static string Backup(string fileName) => $"{Backups}/{Uri.EscapeDataString(fileName)}";

    /// <summary>GET: download an archive (Range, ETag).</summary>
    /// <param name="fileName">Archive file name.</param>
    /// <returns>The relative URL.</returns>
    public static string BackupFile(string fileName) => $"{Backup(fileName)}/file";

    /// <summary>POST: start a verify job for an archive.</summary>
    /// <param name="fileName">Archive file name.</param>
    /// <returns>The relative URL.</returns>
    public static string VerifyBackup(string fileName) => $"{Backup(fileName)}/verify";

    /// <summary>POST: start a restore job for an archive.</summary>
    /// <param name="fileName">Archive file name.</param>
    /// <returns>The relative URL.</returns>
    public static string RestoreBackup(string fileName) => $"{Backup(fileName)}/restore";

    /// <summary>GET: estimate of a backup with these options; every option is sent.</summary>
    /// <param name="options">The backup options.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string BackupEstimateFor(BackupOptionsDto options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"{BackupEstimate}?detailStems={Flag(options.DetailStems)}&exports={Flag(options.Exports)}&scores={Flag(options.Scores)}"
            + $"&generations={Flag(options.Generations)}&recordings={Flag(options.Recordings)}&voiceProfiles={Flag(options.VoiceProfiles)}";
    }

    /// <summary>GET: library jobs, newest first, at most <paramref name="limit"/>.</summary>
    /// <param name="limit">1..50.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string LibraryJobsWithLimit(int limit) => string.Create(CultureInfo.InvariantCulture, $"{LibraryJobs}?limit={limit}");

    /// <summary>GET: one library job (also during maintenance).</summary>
    /// <param name="jobId">Library job id.</param>
    /// <returns>The relative URL.</returns>
    public static string LibraryJob(Guid jobId) => $"{LibraryJobs}/{jobId}";

    /// <summary>POST: cancel a library job.</summary>
    /// <param name="jobId">Library job id.</param>
    /// <returns>The relative URL.</returns>
    public static string CancelLibraryJob(Guid jobId) => $"{LibraryJob(jobId)}/cancel";

    /// <summary>GET: the storage overview; measured again (bypassing the 30 s cache) when <paramref name="refresh"/> is true.</summary>
    /// <param name="refresh">Bypass the cache.</param>
    /// <returns>The relative URL.</returns>
    public static string StorageOverview(bool refresh) => refresh ? $"{Storage}?refresh=true" : Storage;

    private static string Flag(bool value) => value ? "true" : "false";
}
