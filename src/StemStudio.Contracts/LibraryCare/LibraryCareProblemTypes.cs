namespace StemStudio.Contracts.LibraryCare;

/// <summary>
/// Problem <c>type</c> codes of library care (backend-api 1.12.0, feature 013); the full type is <see cref="BaseUri"/> + code.
/// <c>insufficient-disk-space</c> (007) is reused with the members <c>requiredBytes</c> and <c>freeBytes</c>.
/// </summary>
public static class LibraryCareProblemTypes
{
    /// <summary>Prefix of every problem type.</summary>
    public const string BaseUri = "https://stemstudio.local/problems/";

    /// <summary>409: a backup is running (write gate).</summary>
    public const string BackupRunning = "backup-running";

    /// <summary>409: another library job is active.</summary>
    public const string LibraryJobRunning = "library-job-running";

    /// <summary>503 with <c>Retry-After</c>: a restore switches the library.</summary>
    public const string LibraryMaintenance = "library-maintenance";

    /// <summary>409: the backups folder is missing or not writable.</summary>
    public const string BackupsFolderUnavailable = "backups-folder-unavailable";

    /// <summary>409: a library job reads or writes the archive.</summary>
    public const string ArchiveInUse = "archive-in-use";

    /// <summary>422 / job error: not a readable StemStudio archive.</summary>
    public const string ArchiveInvalid = "archive-invalid";

    /// <summary>422 / job error: the archive needs a newer StemStudio.</summary>
    public const string ArchiveIncompatible = "archive-incompatible";

    /// <summary>Job error: an entry violates the layout or safety rules.</summary>
    public const string ArchiveUnsafe = "archive-unsafe";

    /// <summary>Job error: an entry does not match its size or SHA-256.</summary>
    public const string ArchiveCorrupt = "archive-corrupt";

    /// <summary>409: jobs are queued or running (member <c>activeWork</c>).</summary>
    public const string JobsActive = "jobs-active";

    /// <summary>409: a previous library exists; undo or discard it first.</summary>
    public const string PreviousLibraryExists = "previous-library-exists";

    /// <summary>422: the typed confirmation is missing or wrong.</summary>
    public const string ConfirmationRequired = "confirmation-required";

    /// <summary>409: the library job can no longer be cancelled.</summary>
    public const string NotCancellable = "not-cancellable";

    /// <summary>409: the clean-up plan is unknown or expired.</summary>
    public const string PlanExpired = "plan-expired";

    /// <summary>409: the library changed since the clean-up preview.</summary>
    public const string PlanStale = "plan-stale";

    /// <summary>Job error only: the older database could not be updated.</summary>
    public const string MigrationFailed = "migration-failed";

    /// <summary>409: not enough free space (007 type, reused).</summary>
    public const string InsufficientDiskSpace = "insufficient-disk-space";
}
