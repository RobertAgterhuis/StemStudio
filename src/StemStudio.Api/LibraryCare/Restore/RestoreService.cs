using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>Services of the restore endpoints.</summary>
/// <param name="Runner">Library jobs.</param>
/// <param name="Catalog">Archives.</param>
/// <param name="Previous">The previous library.</param>
/// <param name="Files">Free-space seam.</param>
/// <param name="Paths">Library-care folders.</param>
public sealed record RestoreEndpointServices(LibraryJobRunner Runner, BackupCatalog Catalog, PreviousLibraryStore Previous, ILibraryFiles Files, LibraryCarePaths Paths);

/// <summary>
/// Starts restores and undos and discards the previous library (FR-012-FR-018): typed confirmations, no active work, at most
/// one previous library, an archive that is recognised and restorable, enough space; then the job. A merge (US5) needs no
/// typed confirmation and is not blocked by a kept previous library (it never moves the library aside).
/// </summary>
/// <param name="services">Services.</param>
/// <param name="state">Library emptiness and active work.</param>
/// <param name="context">The live database.</param>
/// <param name="restore">The restore work.</param>
/// <param name="undo">The undo work.</param>
public sealed class RestoreService(RestoreEndpointServices services, LibraryState state, StemStudioDbContext context, RestoreRun restore, UndoRun undo)
{
    /// <summary>Starts a restore.</summary>
    /// <param name="fileName">Archive name.</param>
    /// <param name="options">Options.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 422.</returns>
    public async Task<IResult> RestoreAsync(string fileName, RestoreOptionsDto options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (services.Catalog.Find(fileName) is not { } archive)
        {
            return ApiProblems.NotFound($"Archive {fileName} is not in the backups folder.");
        }

        var empty = await state.IsEmptyAsync(cancellationToken);
        if (await RefusalAsync(archive, options, empty, cancellationToken) is { } refused)
        {
            return refused;
        }

        var request = new RestoreRequest(fileName, options, empty, await CountsAsync(cancellationToken));
        return Started(services.Runner.TryStart(LibraryJobKindDto.Restore, fileName, (job, ct) => restore.RunAsync(request, job, ct), out var started), started);
    }

    /// <summary>Starts an undo of the last replace restore.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404 or 409.</returns>
    public async Task<IResult> RevertAsync(CancellationToken cancellationToken)
    {
        if (await services.Previous.ReadAsync(cancellationToken) is not { } previous)
        {
            return NoPrevious();
        }

        if (await BusyAsync(cancellationToken) is { } busy)
        {
            return busy;
        }

        var counts = await CountsAsync(cancellationToken);
        return Started(services.Runner.TryStart(LibraryJobKindDto.UndoRestore, previous.RestoredArchive, (job, _) => undo.RunAsync(previous, counts, job), out var started), started);
    }

    /// <summary>Deletes the previous library.</summary>
    /// <param name="confirmation">Must be <c>DISCARD</c>.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204, 404, 409 or 422.</returns>
    public async Task<IResult> DiscardAsync(ConfirmationDto confirmation, CancellationToken cancellationToken)
    {
        if (confirmation?.Confirmation != LibraryCareLimits.DiscardConfirmation)
        {
            return ConfirmationRequired(LibraryCareLimits.DiscardConfirmation);
        }

        if (await services.Previous.ReadAsync(cancellationToken) is not { } previous)
        {
            return NoPrevious();
        }

        if (services.Runner.Active is { } active)
        {
            return LibraryCareProblems.JobRunning(active).Result;
        }

        services.Previous.Delete(previous.RestoreId);
        return TypedResults.NoContent();
    }

    /// <summary>The previous library.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> PreviousAsync(CancellationToken cancellationToken) =>
        await services.Previous.ReadAsync(cancellationToken) is { } previous ? TypedResults.Ok(previous) : NoPrevious();

    private static IResult Started(bool started, LibraryJobDto job) =>
        started ? TypedResults.Accepted(StemStudio.Contracts.ApiRoutes.LibraryJob(job.JobId), job) : LibraryCareProblems.JobRunning(null).Result;

    private static IResult NoPrevious() => ApiProblems.NotFound("There is no previous library.");

    private static IResult ConfirmationRequired(string word) => LibraryCareProblems.Problem(
        StatusCodes.Status422UnprocessableEntity, LibraryCareProblemTypes.ConfirmationRequired, "Confirmation required", $"Type {word} to confirm.").Result;

    private async Task<IResult?> RefusalAsync(BackupDetailsDto archive, RestoreOptionsDto options, bool empty, CancellationToken cancellationToken)
    {
        if (!empty && options.Mode == RestoreModeDto.Replace && options.Confirmation != LibraryCareLimits.ReplaceConfirmation)
        {
            return ConfirmationRequired(LibraryCareLimits.ReplaceConfirmation);
        }

        if (!archive.Recognised)
        {
            return LibraryCareProblems.ArchiveInvalid(archive.Reason ?? BackupCatalog.NotABackup).Result;
        }

        if (!archive.Compatibility.Restorable)
        {
            return LibraryCareProblems.Problem(StatusCodes.Status422UnprocessableEntity, LibraryCareProblemTypes.ArchiveIncompatible, "The backup cannot be restored", archive.Compatibility.Reason ?? "Incompatible.").Result;
        }

        var movesAside = !empty && options.Mode == RestoreModeDto.Replace;
        return await BusyAsync(cancellationToken) ?? await PreviousOrSpaceAsync(archive, movesAside, cancellationToken);
    }

    private async Task<IResult?> PreviousOrSpaceAsync(BackupDetailsDto archive, bool movesAside, CancellationToken cancellationToken)
    {
        if (movesAside && await services.Previous.ReadAsync(cancellationToken) is not null)
        {
            return LibraryCareProblems.Problem(
                StatusCodes.Status409Conflict, LibraryCareProblemTypes.PreviousLibraryExists, "A previous library exists", "Undo the last restore or discard the previous library first.").Result;
        }

        var needed = archive.TotalBytes + LibraryCareLimits.RestoreFreeMarginBytes;
        return services.Files.Space(services.Paths.Data.Root) is { } space && space.FreeBytes < needed ? LibraryCareProblems.InsufficientSpace(needed, space.FreeBytes).Result : null;
    }

    private async Task<IResult?> BusyAsync(CancellationToken cancellationToken)
    {
        if (services.Runner.Active is { } active)
        {
            return LibraryCareProblems.JobRunning(active).Result;
        }

        var work = await state.ActiveWorkAsync(cancellationToken);
        if (work.Count == 0)
        {
            return null;
        }

        var text = new ProblemText(StatusCodes.Status409Conflict, LibraryCareProblemTypes.JobsActive, "Jobs are running", $"Wait for {work.Count} jobs to finish or cancel them, then try again.");
        return GateProblems.Typed(text, new Dictionary<string, object?> { ["activeWork"] = work });
    }

    private async Task<ArchiveCountsDto> CountsAsync(CancellationToken cancellationToken) =>
        BackupDescription.Counts(await BackupSelection.CountAsync(context, cancellationToken));
}
