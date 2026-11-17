using System.Globalization;
using Microsoft.Data.Sqlite;
using StemStudio.Agent.Ports;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>A restore request.</summary>
/// <param name="FileName">Archive name.</param>
/// <param name="Options">Restore options.</param>
/// <param name="LibraryEmpty">True when nothing is moved aside.</param>
/// <param name="CurrentCounts">Counts of the live library (kept with the previous library).</param>
public sealed record RestoreRequest(string FileName, RestoreOptionsDto Options, bool LibraryEmpty, ArchiveCountsDto CurrentCounts);

/// <summary>Services of a restore run.</summary>
/// <param name="Paths">Library-care folders.</param>
/// <param name="Validator">Archive validation.</param>
/// <param name="Extractor">Staging extraction.</param>
/// <param name="Database">The database step.</param>
/// <param name="Switcher">The switch.</param>
/// <param name="Previous">The previous library.</param>
/// <param name="Live">The live database.</param>
/// <param name="Keys">The stored Claude key reader.</param>
public sealed record RestoreRunServices(
    LibraryCarePaths Paths, ArchiveValidator Validator, StagingExtractor Extractor, StagedDatabase Database, LibrarySwitcher Switcher, PreviousLibraryStore Previous, LiveDatabase Live, IStoredClaudeKeyReader Keys);

/// <summary>
/// The work of a restore job (data-model 5.5 stages): validate, extract and verify, the database step, the journalled switch
/// (not cancellable from here) and the report. Nothing outside staging changes before the switch.
/// </summary>
/// <param name="services">Services.</param>
/// <param name="clock">Clock.</param>
public sealed class RestoreRun(RestoreRunServices services, TimeProvider clock)
{
    /// <summary>Warning of a stored Claude key that the current key ring cannot read (011 CK-FR-017).</summary>
    public const string KeyUnreadableText = "The stored Claude key must be entered again.";

    /// <summary>Runs a restore.</summary>
    /// <param name="request">The request.</param>
    /// <param name="context">Progress.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The report.</returns>
    public async Task<LibraryJobReportDto> RunAsync(RestoreRequest request, LibraryJobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var staging = services.Paths.Staging(context.JobId);
        try
        {
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Validate, "Checking the archive", 0, 0, 0, null));
            using var archive = services.Validator.Validate(request.FileName);
            await ExtractAsync(archive, staging, context, cancellationToken);
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Database, "Checking and updating the database", 91, context.Job.BytesTotal, context.Job.BytesTotal, null));
            var staged = await services.Database.PrepareAsync(staging, archive.Manifest, request.Options, cancellationToken);
            await SwitchAsync(request, context);
            return Report(request, archive.Manifest, staged);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    /// <summary>Switches the live database to WAL after a switch (research R-04 P2).</summary>
    /// <param name="live">The live database.</param>
    public static void UseWal(LiveDatabase live)
    {
        ArgumentNullException.ThrowIfNull(live);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder(live.ConnectionString) { Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL";
        command.ExecuteScalar();
    }

    private async Task ExtractAsync(ValidatedArchive archive, string staging, LibraryJobContext context, CancellationToken cancellationToken)
    {
        var total = Math.Max(1, archive.Manifest.Totals.Bytes);
        var (count, done, bytes) = (archive.Manifest.Entries.Count, 0, 0L);
        await services.Extractor.ExtractAsync(archive, staging, new SyncProgress(entry =>
        {
            (done, bytes) = (done + 1, bytes + entry.SizeBytes);
            var text = string.Create(CultureInfo.InvariantCulture, $"Unpacking and verifying {done} of {count}");
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Extract, text, 1 + (int)(90 * bytes / total), bytes, total, null));
        }), cancellationToken);
    }

    private async Task SwitchAsync(RestoreRequest request, LibraryJobContext context)
    {
        context.MarkNotCancellable();
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Switch, "Switching to the restored library", 95, context.Job.BytesTotal, context.Job.BytesTotal, null));
        var previous = request.LibraryEmpty ? null : new PreviousLibraryDto
        {
            RestoreId = context.JobId,
            MovedAsideAt = clock.GetUtcNow(),
            SizeBytes = 0,
            Counts = request.CurrentCounts,
            RestoredArchive = request.FileName,
        };
        var plan = JournalPlans.Restore(context.JobId, context.JobId, services.Paths, keepPrevious: !request.LibraryEmpty) with { Previous = previous };
        await services.Switcher.SwitchAsync(plan, CancellationToken.None);
        UseWal(services.Live);
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Reconcile, "Finishing", 98, context.Job.BytesTotal, context.Job.BytesTotal, null));
        if (previous is not null)
        {
            await services.Previous.SaveAsync(previous with { SizeBytes = services.Previous.SizeOf(context.JobId) }, CancellationToken.None);
        }
    }

    private RestoreReportDto Report(RestoreRequest request, BackupManifest manifest, StagedResult staged)
    {
        var skippedProfiles = staged.Skipped.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var warnings = staged.Warnings.Concat(manifest.Warnings.Select(warning => warning.Message)).ToList();
        if (services.Keys.Read().State == StoredClaudeKeyStateDto.Unreadable)
        {
            warnings.Add(KeyUnreadableText);
        }

        return new RestoreReportDto
        {
            Mode = request.LibraryEmpty ? RestoreReportModeDto.Empty : RestoreReportModeDto.Replace,
            Restored = CategoryCounts.Of(manifest, skippedProfiles),
            Skipped = staged.Skipped,
            MigratedFrom = staged.MigratedFrom,
            Warnings = warnings,
            PreviousLibraryKept = !request.LibraryEmpty,
        };
    }

    private sealed class SyncProgress(Action<ManifestEntry> report) : IProgress<ManifestEntry>
    {
        public void Report(ManifestEntry value) => report(value);
    }
}

/// <summary>The work of an undo job: swap the previous library back; the restored one becomes the previous library.</summary>
/// <param name="services">Services.</param>
/// <param name="clock">Clock.</param>
public sealed class UndoRun(RestoreRunServices services, TimeProvider clock)
{
    /// <summary>Runs the undo.</summary>
    /// <param name="previous">The previous library.</param>
    /// <param name="currentCounts">Counts of the live (restored) library.</param>
    /// <param name="context">Progress.</param>
    /// <returns>The report.</returns>
    public async Task<LibraryJobReportDto> RunAsync(PreviousLibraryDto previous, ArchiveCountsDto currentCounts, LibraryJobContext context)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(context);
        context.MarkNotCancellable();
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Switch, "Switching to the previous library", 50, 0, 0, null));
        var restored = new PreviousLibraryDto
        {
            RestoreId = context.JobId,
            MovedAsideAt = clock.GetUtcNow(),
            SizeBytes = 0,
            Counts = currentCounts,
            RestoredArchive = previous.RestoredArchive,
        };
        await services.Switcher.SwitchAsync(JournalPlans.Undo(previous.RestoreId, context.JobId, services.Paths) with { Previous = restored }, CancellationToken.None);
        RestoreRun.UseWal(services.Live);
        await services.Previous.SaveAsync(restored with { SizeBytes = services.Previous.SizeOf(context.JobId) }, CancellationToken.None);
        return new RestoreReportDto { Mode = RestoreReportModeDto.Undo, Restored = [], Skipped = [], MigratedFrom = null, Warnings = [], PreviousLibraryKept = true };
    }
}
