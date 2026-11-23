using System.Globalization;
using Microsoft.Extensions.Options;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>The outcome of a backup request: the queued job, or a problem with its code.</summary>
/// <param name="Job">The job, or null when refused.</param>
/// <param name="ProblemCode">The problem code when refused.</param>
/// <param name="Problem">The problem response when refused.</param>
public sealed record BackupStartOutcome(LibraryJobDto? Job, string? ProblemCode, IResult? Problem);

/// <summary>
/// Starts backups (FR-001-FR-009): checks the folder, one job at a time and the space (estimate + 64 MiB), picks the archive
/// name (<c>stemstudio-backup-&lt;UTC&gt;.zip</c>, <c>-2</c>, <c>-3</c> on a clash) and queues the run.
/// </summary>
/// <param name="folder">The backups folder.</param>
/// <param name="runner">The library job runner.</param>
/// <param name="run">The backup work.</param>
/// <param name="estimates">Estimates.</param>
/// <param name="paths">Library-care folders.</param>
/// <param name="options">API options.</param>
/// <param name="clock">Clock.</param>
public sealed class BackupService(
    BackupFolder folder, LibraryJobRunner runner, BackupRun run, BackupEstimates estimates, LibraryCarePaths paths, IOptions<StemStudioApiOptions> options, TimeProvider clock)
{
    /// <summary>Validates the request and queues the backup.</summary>
    /// <param name="request">Options.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The outcome.</returns>
    public async Task<BackupStartOutcome> StartAsync(BackupOptionsDto request, CancellationToken cancellationToken)
    {
        if (runner.Active is not null)
        {
            return Refused(LibraryCareProblems.JobRunning(runner.Active));
        }

        var state = folder.Probe();
        if (!state.Writable)
        {
            return Refused(LibraryCareProblems.FolderUnavailable(state.Reason));
        }

        var estimate = await estimates.EstimateAsync(request, state, cancellationToken);
        if (!estimate.Enough)
        {
            return Refused(LibraryCareProblems.InsufficientSpace(estimate.TotalBytes + options.Value.LibraryCare.MinFreeMarginBytes, state.FreeBytes ?? 0));
        }

        var backup = new BackupRequest(request, NextFileName());
        return runner.TryStart(new LibraryJobStart(LibraryJobKindDto.Backup, backup.FileName, (context, ct) => run.RunAsync(backup, context, ct), run.GateHooks), out var job)
            ? new BackupStartOutcome(job, null, null)
            : Refused(LibraryCareProblems.JobRunning(runner.Active));
    }

    private static BackupStartOutcome Refused((string Code, IResult Result) problem) => new(null, problem.Code, problem.Result);

    private string NextFileName()
    {
        var stamp = clock.GetUtcNow().UtcDateTime.ToString(LibraryCareLimits.ArchiveFileNameTimeFormat, CultureInfo.InvariantCulture);
        var name = $"{LibraryCareLimits.ArchiveFileNamePrefix}{stamp}.zip";
        for (var suffix = 2; Taken(name); suffix++)
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{LibraryCareLimits.ArchiveFileNamePrefix}{stamp}-{suffix}.zip");
        }

        return name;
    }

    private bool Taken(string name) => File.Exists(paths.Archive(name)) || File.Exists(paths.Archive(name) + LibraryCareLimits.PartialSuffix);
}

/// <summary>Backup estimates from a walk of the library (data-model 4.1).</summary>
/// <param name="context">The live database.</param>
/// <param name="walker">The library walker.</param>
/// <param name="options">API options.</param>
public sealed class BackupEstimates(StemStudioDbContext context, LibraryWalker walker, IOptions<StemStudioApiOptions> options)
{
    /// <summary>Estimates a backup.</summary>
    /// <param name="request">Options.</param>
    /// <param name="folder">The backups folder state.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The estimate.</returns>
    public async Task<BackupEstimateDto> EstimateAsync(BackupOptionsDto request, FolderStateDto folder, CancellationToken cancellationToken)
    {
        var index = await LibraryIndexReader.ReadAsync(context, cancellationToken);
        return BackupEstimator.Estimate(walker.Walk(index), request, folder, options.Value.LibraryCare.MinFreeMarginBytes);
    }
}
