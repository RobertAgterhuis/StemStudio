using System.Globalization;
using Microsoft.Net.Http.Headers;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Cleanup;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare;

/// <summary>Endpoints of the <c>library-care</c> tag (backend-api 1.12.0): status, backups and library jobs.</summary>
public static class LibraryCareEndpoints
{
    private const string Tag = "library-care";
    private const string BackupRoute = ApiRoutes.Backups + "/{fileName}";
    private const string JobRoute = ApiRoutes.LibraryJobs + "/{jobId:guid}";

    /// <summary>Maps the endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapLibraryCareEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.LibraryCareStatus, StatusAsync).WithName("getLibraryCareStatus").WithTags(Tag);
        app.MapGet(ApiRoutes.Backups, (BackupFolder folder, BackupCatalog catalog) => TypedResults.Ok(new BackupListDto { Folder = folder.Probe(), Archives = catalog.List() }))
            .WithName("listBackups").WithTags(Tag);
        app.MapPost(ApiRoutes.Backups, CreateAsync).WithName("createBackup").WithTags(Tag);
        app.MapGet(ApiRoutes.BackupEstimate, EstimateAsync).WithName("estimateBackup").WithTags(Tag);
        app.MapGet(BackupRoute, Details).WithName("getBackup").WithTags(Tag);
        app.MapDelete(BackupRoute, Delete).WithName("deleteBackup").RequireGateOpen().WithTags(Tag);
        app.MapGet(BackupRoute + "/file", Download).WithName("downloadBackup").WithTags(Tag);
        app.MapPost(BackupRoute + "/verify", Verify).WithName("verifyBackup").WithTags(Tag);
        app.MapLibraryJobEndpoints();
        app.MapPost(BackupRoute + "/restore", (string fileName, RestoreOptionsDto options, RestoreService restore, CancellationToken ct) => restore.RestoreAsync(fileName, options, ct))
            .WithName("restoreBackup").RequireGateOpen().WithTags(Tag);
        app.MapGet(ApiRoutes.PreviousLibrary, (RestoreService restore, CancellationToken ct) => restore.PreviousAsync(ct)).WithName("getPreviousLibrary").WithTags(Tag);
        app.MapPost(ApiRoutes.RevertRestore, (RestoreService restore, CancellationToken ct) => restore.RevertAsync(ct)).WithName("revertRestore").RequireGateOpen().WithTags(Tag);
        app.MapPost(ApiRoutes.DiscardPreviousLibrary, (ConfirmationDto confirmation, RestoreService restore, CancellationToken ct) => restore.DiscardAsync(confirmation, ct))
            .WithName("discardPreviousLibrary").RequireGateOpen().WithTags(Tag);
        app.MapGet(ApiRoutes.Storage, async (bool? refresh, StorageOverviewService storage, CancellationToken ct) => TypedResults.Ok(await storage.GetAsync(refresh ?? false, ct)))
            .WithName("getStorageOverview").WithTags("storage");
        app.MapPost(ApiRoutes.CleanupPreview, PreviewCleanupAsync).WithName("previewCleanup").RequireGateOpen().WithTags("storage");
        app.MapPost(ApiRoutes.Cleanup, ExecuteCleanupAsync).WithName("executeCleanup").RequireGateOpen().WithTags("storage");
        return app;
    }

    private static IEndpointRouteBuilder MapLibraryJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.LibraryJobs, ListJobsAsync).WithName("listLibraryJobs").WithTags(Tag);
        app.MapGet(JobRoute, async (Guid jobId, LibraryJobRunner runner, CancellationToken ct) =>
            await runner.GetAsync(jobId, ct) is { } job ? TypedResults.Ok(job) : JobNotFound(jobId)).WithName("getLibraryJob").WithTags(Tag);
        app.MapPost(JobRoute + "/cancel", CancelAsync).WithName("cancelLibraryJob").WithTags(Tag);
        return app;
    }

    private static async Task<IResult> StatusAsync(LibraryGate gate, LibraryJobRunner runner, IServiceProvider services, CancellationToken ct)
    {
        var maintenance = gate.IsMaintenance;
        var state = maintenance ? null : services.GetRequiredService<LibraryState>();
        return TypedResults.Ok(new LibraryCareStatusDto
        {
            Gate = (GateModeDto)(int)gate.Mode,
            ActiveJob = runner.Active,
            BackupFolder = services.GetRequiredService<BackupFolder>().Probe(),
            PreviousLibrary = await PreviousAsync(services, ct),
            ActiveWork = state is null ? [] : await state.ActiveWorkAsync(ct),
            LibraryEmpty = state is not null && await state.IsEmptyAsync(ct),
        });
    }

    private static Task<PreviousLibraryDto?> PreviousAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetService<IPreviousLibraryReader>()?.ReadAsync(ct) ?? Task.FromResult<PreviousLibraryDto?>(null);

    private static async Task<IResult> PreviewCleanupAsync(CleanupRequestDto request, LibraryJobRunner runner, CleanupPlanner planner, CancellationToken ct)
    {
        if (request.Actions is not { Count: >= 1 and <= LibraryCareLimits.MaxCleanupActions } || request.Actions.Any(action => action.TrackIds?.Count > LibraryCareLimits.MaxCleanupTrackIds))
        {
            return ApiProblems.Validation("actions", "Send 1 to 3 actions with at most 10,000 track ids each.");
        }

        return runner.Active is { } active ? LibraryCareProblems.JobRunning(active).Result : TypedResults.Ok(await planner.PreviewAsync(request, ct));
    }

    private static async Task<IResult> ExecuteCleanupAsync(ExecuteCleanupRequestDto request, LibraryJobRunner runner, CleanupExecutor executor, CancellationToken ct)
    {
        if (runner.Active is { } active)
        {
            return LibraryCareProblems.JobRunning(active).Result;
        }

        var (result, problem) = await executor.ExecuteAsync(request.PlanId, ct);
        return problem switch
        {
            null => TypedResults.Ok(result),
            LibraryCareProblemTypes.PlanStale => LibraryCareProblems.Problem(StatusCodes.Status409Conflict, problem, "The library changed", "The library changed since the preview; preview again.").Result,
            _ => LibraryCareProblems.Problem(StatusCodes.Status409Conflict, problem, "The plan expired", "The clean-up plan is unknown or older than 10 minutes; preview again.").Result,
        };
    }

    private static async Task<IResult> CreateAsync(BackupOptionsDto options, BackupService backups, CancellationToken ct)
    {
        var outcome = await backups.StartAsync(options, ct);
        return outcome.Job is { } job ? TypedResults.Accepted(ApiRoutes.LibraryJob(job.JobId), job) : outcome.Problem!;
    }

    private static async Task<IResult> EstimateAsync([AsParameters] EstimateQuery query, BackupFolder folder, BackupEstimates estimates, CancellationToken ct) =>
        TypedResults.Ok(await estimates.EstimateAsync(query.ToOptions(), folder.Probe(), ct));

    private static IResult Details(string fileName, BackupCatalog catalog) => catalog.Find(fileName) switch
    {
        null => ArchiveNotFound(fileName),
        { Recognised: false } unrecognised => LibraryCareProblems.ArchiveInvalid(unrecognised.Reason ?? BackupCatalog.NotABackup).Result,
        var details => TypedResults.Ok(details),
    };

    private static IResult Delete(string fileName, BackupCatalog catalog, LibraryJobRunner runner)
    {
        if (catalog.File(fileName) is not { } file)
        {
            return ArchiveNotFound(fileName);
        }

        if (runner.IsArchiveInUse(fileName))
        {
            return LibraryCareProblems.ArchiveInUse().Result;
        }

        file.Delete();
        return TypedResults.NoContent();
    }

    private static IResult Download(string fileName, BackupCatalog catalog) => catalog.File(fileName) is { } file
        ? TypedResults.PhysicalFile(file.FullName, "application/zip", fileDownloadName: file.Name, lastModified: file.LastWriteTimeUtc, entityTag: EntityTag(file), enableRangeProcessing: true)
        : ArchiveNotFound(fileName);

    private static IResult Verify(string fileName, BackupCatalog catalog, LibraryJobRunner runner, VerifyRun verify)
    {
        if (catalog.File(fileName) is null)
        {
            return ArchiveNotFound(fileName);
        }

        return runner.TryStart(LibraryJobKindDto.Verify, fileName, (context, ct) => verify.RunAsync(fileName, context, ct), out var job)
            ? TypedResults.Accepted(ApiRoutes.LibraryJob(job.JobId), job)
            : LibraryCareProblems.JobRunning(runner.Active).Result;
    }

    private static async Task<IResult> ListJobsAsync(int? limit, LibraryJobRunner runner, CancellationToken ct)
    {
        var count = limit ?? LibraryCareLimits.DefaultJobListLimit;
        if (count is < LibraryCareLimits.MinJobListLimit or > LibraryCareLimits.MaxJobListLimit)
        {
            return ApiProblems.Validation("limit", "The limit must be between 1 and 50.");
        }

        return TypedResults.Ok(await runner.ListAsync(count, ct));
    }

    private static async Task<IResult> CancelAsync(Guid jobId, LibraryJobRunner runner, CancellationToken ct)
    {
        var (result, job) = await runner.CancelAsync(jobId, ct);
        return result switch
        {
            CancelResult.Requested => TypedResults.Accepted(ApiRoutes.LibraryJob(jobId), job),
            CancelResult.NotFound => JobNotFound(jobId),
            _ => LibraryCareProblems.NotCancellable().Result,
        };
    }

    private static IResult ArchiveNotFound(string fileName) =>
        ApiProblems.NotFound(ArchiveNames.IsValid(fileName) ? $"Archive {fileName} is not in the backups folder." : "No such archive.");

    private static IResult JobNotFound(Guid jobId) => ApiProblems.NotFound($"Library job {jobId} does not exist.");

    private static EntityTagHeaderValue EntityTag(FileInfo file) =>
        new(string.Create(CultureInfo.InvariantCulture, $"\"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}\""));
}

/// <summary>Reads the previous library kept aside by a replace restore (feature 013 US2).</summary>
public interface IPreviousLibraryReader
{
    /// <summary>Reads it.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The previous library, or null.</returns>
    Task<PreviousLibraryDto?> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Query of <c>estimateBackup</c>: the optional categories, each true when absent.</summary>
public sealed record EstimateQuery
{
    /// <summary>Include detail stems.</summary>
    public bool? DetailStems { get; init; }

    /// <summary>Include exports.</summary>
    public bool? Exports { get; init; }

    /// <summary>Include sheet music.</summary>
    public bool? Scores { get; init; }

    /// <summary>Include generations.</summary>
    public bool? Generations { get; init; }

    /// <summary>Include recordings.</summary>
    public bool? Recordings { get; init; }

    /// <summary>Include voice profiles.</summary>
    public bool? VoiceProfiles { get; init; }

    /// <summary>The options.</summary>
    /// <returns>Backup options.</returns>
    public BackupOptionsDto ToOptions() => new()
    {
        DetailStems = DetailStems ?? true,
        Exports = Exports ?? true,
        Scores = Scores ?? true,
        Generations = Generations ?? true,
        Recordings = Recordings ?? true,
        VoiceProfiles = VoiceProfiles ?? true,
    };
}
