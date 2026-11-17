namespace StemStudio.Contracts.LibraryCare;

/// <summary>
/// Limits and fixed values of library care that are part of the contract (backend-api 1.12.0, backup-archive 1.0.0,
/// data-model.md 1 and 3.4 of feature 013). The <c>Default*</c> values are the defaults of the <c>StemStudio:LibraryCare</c> settings.
/// </summary>
public static class LibraryCareLimits
{
    /// <summary>Pattern of an archive file name (the <c>fileName</c> path parameter); no endpoint accepts a path.</summary>
    public const string ArchiveFileNamePattern = @"^[A-Za-z0-9][A-Za-z0-9._-]{0,119}\.zip$";

    /// <summary>Prefix of the archive names StemStudio writes: <c>stemstudio-backup-yyyyMMdd-HHmmss.zip</c> (UTC).</summary>
    public const string ArchiveFileNamePrefix = "stemstudio-backup-";

    /// <summary>UTC time format inside a written archive name.</summary>
    public const string ArchiveFileNameTimeFormat = "yyyyMMdd-HHmmss";

    /// <summary>Suffix of an archive while it is written; such files are never listed.</summary>
    public const string PartialSuffix = ".partial";

    /// <summary>Value of the manifest member <c>format</c>.</summary>
    public const string ArchiveFormat = "stemstudio-backup";

    /// <summary>Archive format version written by this release.</summary>
    public const string ArchiveFormatVersion = "1.0.0";

    /// <summary>Highest archive format major version this release reads.</summary>
    public const int ArchiveFormatMajor = 1;

    /// <summary>Most entries an archive may have (data-model 3.4 step 2).</summary>
    public const int MaxArchiveEntries = 2_000_000;

    /// <summary>Largest <c>manifest.json</c> in bytes (256 MiB).</summary>
    public const long MaxManifestBytes = 256L * 1024 * 1024;

    /// <summary>Space kept free on top of a restore's <c>totals.bytes</c> (256 MiB, data-model 3.4 step 5).</summary>
    public const long RestoreFreeMarginBytes = 256L * 1024 * 1024;

    /// <summary>Default margin above the estimate before a backup starts (64 MiB, FR-007).</summary>
    public const long DefaultMinFreeMarginBytes = 64L * 1024 * 1024;

    /// <summary>Default free space below which a running backup or restore stops (256 MiB).</summary>
    public const long DefaultStopBelowFreeBytes = 256L * 1024 * 1024;

    /// <summary>Default cache time of <c>GET /api/storage</c> in seconds.</summary>
    public const int DefaultStorageCacheSeconds = 30;

    /// <summary>Default validity of a clean-up plan in minutes (FR-023).</summary>
    public const int DefaultCleanupPlanMinutes = 10;

    /// <summary>Default copy buffer in bytes (1 MiB).</summary>
    public const int DefaultCopyBufferBytes = 1024 * 1024;

    /// <summary>Library job records kept (the oldest are removed).</summary>
    public const int KeptLibraryJobs = 50;

    /// <summary>Smallest <c>limit</c> of <c>GET /api/library-jobs</c>.</summary>
    public const int MinJobListLimit = 1;

    /// <summary>Largest <c>limit</c> of <c>GET /api/library-jobs</c>.</summary>
    public const int MaxJobListLimit = 50;

    /// <summary>Default <c>limit</c> of <c>GET /api/library-jobs</c>.</summary>
    public const int DefaultJobListLimit = 10;

    /// <summary>Most actions in one clean-up request.</summary>
    public const int MaxCleanupActions = 3;

    /// <summary>Most track ids in one clean-up action.</summary>
    public const int MaxCleanupTrackIds = 10_000;

    /// <summary>Typed confirmation of a replace restore on a non-empty library.</summary>
    public const string ReplaceConfirmation = "REPLACE";

    /// <summary>Typed confirmation of discarding the previous library.</summary>
    public const string DiscardConfirmation = "DISCARD";

    /// <summary>Longest typed confirmation accepted (<c>Confirmation.confirmation</c>).</summary>
    public const int MaxConfirmationLength = 16;

    /// <summary>Seconds in the <c>Retry-After</c> header of <c>503 library-maintenance</c>.</summary>
    public const int MaintenanceRetryAfterSeconds = 5;
}
