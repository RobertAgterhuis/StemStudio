using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.LibraryCare;

/// <summary>The row state of an archive (<c>backup-row-*</c> <c>data-state</c>, ui-test-ids 1.12.0) and what can be done with it.</summary>
public static class ArchiveRules
{
    /// <summary>A StemStudio backup this version can restore as it is.</summary>
    public const string Restorable = "restorable";

    /// <summary>An older backup whose database is updated in staging.</summary>
    public const string Migration = "migration";

    /// <summary>A newer or other-format backup.</summary>
    public const string Incompatible = "incompatible";

    /// <summary>A ZIP without a readable StemStudio manifest.</summary>
    public const string Unrecognised = "unrecognised";

    /// <summary>The row state.</summary>
    /// <param name="archive">The archive.</param>
    /// <returns>One of the four constants.</returns>
    public static string State(BackupSummaryDto archive) =>
        !archive.Recognised ? Unrecognised
        : !archive.Compatibility.Restorable ? Incompatible
        : archive.Compatibility.MigrationNeeded ? Migration
        : Restorable;

    /// <summary>Why the archive cannot be restored now, or null when it can.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="jobRunning">A library job is queued or running.</param>
    /// <returns>The reason shown next to the disabled Restore button.</returns>
    public static string? RestoreBlocked(BackupSummaryDto archive, bool jobRunning) =>
        State(archive) is Incompatible or Unrecognised ? "This archive cannot be restored."
        : jobRunning ? "Wait for the running library job to finish."
        : null;

    /// <summary>Why a check (verify) cannot start now, or null when it can.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="jobRunning">A library job is queued or running.</param>
    /// <returns>The reason, or null.</returns>
    public static string? VerifyBlocked(BackupSummaryDto archive, bool jobRunning) =>
        !archive.Recognised ? "This file is not a StemStudio backup."
        : jobRunning ? "Wait for the running library job to finish."
        : null;
}
