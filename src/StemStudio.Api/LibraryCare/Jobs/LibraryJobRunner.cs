using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>Outcome of a cancel request.</summary>
public enum CancelResult
{
    /// <summary>The job is cancelling.</summary>
    Requested,

    /// <summary>Unknown job.</summary>
    NotFound,

    /// <summary>Terminal, or a restore past the point of no return.</summary>
    NotCancellable,
}

/// <summary>
/// Runs one library job at a time (backup, verify, restore, undo; data-model 5.1). Jobs run in the API and continue when the
/// browser closes; their records are JSON files (<see cref="LibraryJobStore"/>).
/// </summary>
/// <param name="store">Job records.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger (no paths or personal content).</param>
public sealed partial class LibraryJobRunner(LibraryJobStore store, TimeProvider clock, ILogger<LibraryJobRunner> logger)
{
    private readonly Lock _lock = new();
    private Running? _running;
    private Task _idle = Task.CompletedTask;

    /// <summary>The active job, or null.</summary>
    public LibraryJobDto? Active
    {
        get
        {
            lock (_lock)
            {
                return _running?.Context.Job;
            }
        }
    }

    /// <summary>Starts a job unless one is active.</summary>
    /// <param name="kind">Kind.</param>
    /// <param name="archiveFileName">Archive the job reads or writes.</param>
    /// <param name="work">The work.</param>
    /// <param name="job">The queued job.</param>
    /// <returns>False when another job is active (<c>409 library-job-running</c>).</returns>
    public bool TryStart(LibraryJobKindDto kind, string? archiveFileName, LibraryJobWork work, out LibraryJobDto job) =>
        TryStart(new LibraryJobStart(kind, archiveFileName, work), out job);

    /// <summary>Starts a job unless one is active, with actions around its visible life.</summary>
    /// <param name="start">Kind, archive, work and hooks.</param>
    /// <param name="job">The queued job.</param>
    /// <returns>False when another job is active.</returns>
    public bool TryStart(LibraryJobStart start, out LibraryJobDto job)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(start.Work);
        var (work, hooks) = (start.Work, start.Hooks);
        job = LibraryJobs.Queued(Guid.NewGuid(), start.Kind, start.ArchiveFileName, clock.GetUtcNow());
        lock (_lock)
        {
            if (_running is not null)
            {
                return false;
            }

            var running = new Running(new LibraryJobContext(job with { StartedAt = job.CreatedAt }, clock), new CancellationTokenSource(), hooks);
            hooks?.Begin();
            _running = running;
            _idle = Task.Run(() => RunAsync(running, work));
            return true;
        }
    }

    /// <summary>Completes when no job is running (tests and shutdown).</summary>
    /// <returns>A task.</returns>
    public Task WhenIdleAsync()
    {
        lock (_lock)
        {
            return _idle;
        }
    }

    /// <summary>True when the active job reads or writes the archive.</summary>
    /// <param name="fileName">Archive file name.</param>
    /// <returns>True when in use.</returns>
    public bool IsArchiveInUse(string fileName) => string.Equals(Active?.ArchiveFileName, fileName, StringComparison.Ordinal);

    /// <summary>The live active job or a stored record.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null.</returns>
    public async Task<LibraryJobDto?> GetAsync(Guid jobId, CancellationToken cancellationToken) =>
        Active is { } active && active.JobId == jobId ? active : await store.FindAsync(jobId, cancellationToken);

    /// <summary>Recent jobs, newest first, the active one live.</summary>
    /// <param name="limit">At most this many.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The jobs.</returns>
    public async Task<IReadOnlyList<LibraryJobDto>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        var active = Active;
        var stored = await store.ListAsync(cancellationToken);
        return [.. stored.Select(job => active is not null && job.JobId == active.JobId ? active : job).Take(limit)];
    }

    /// <summary>Requests cancellation of the active job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The outcome and the job (null when not found).</returns>
    public async Task<(CancelResult Result, LibraryJobDto? Job)> CancelAsync(Guid jobId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_running is { } running && running.Context.JobId == jobId)
            {
                var job = running.Context.Job;
                if (!job.Cancellable)
                {
                    return (CancelResult.NotCancellable, job);
                }

                running.Cancellation.Cancel();
                return (CancelResult.Requested, job);
            }
        }

        var stored = await store.FindAsync(jobId, cancellationToken);
        return stored is null ? (CancelResult.NotFound, null) : (CancelResult.NotCancellable, stored);
    }

    private async Task RunAsync(Running running, LibraryJobWork work)
    {
        var context = running.Context;
        context.Update(job => job with { Status = LibraryJobStatusDto.Running });
        await store.SaveAsync(context.Job, CancellationToken.None);
        var finished = await ExecuteAsync(context, work, running.Cancellation.Token);
        await store.SaveAsync(finished, CancellationToken.None);
        lock (_lock)
        {
            _running = null;
        }

        running.Hooks?.End();

        running.Cancellation.Dispose();
    }

    private async Task<LibraryJobDto> ExecuteAsync(LibraryJobContext context, LibraryJobWork work, CancellationToken cancellationToken)
    {
        try
        {
            var report = await work(context, cancellationToken);
            return LibraryJobs.Finished(context.Job, LibraryJobStatusDto.Completed, clock.GetUtcNow()) with { Progress = 100, Report = report };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LibraryJobs.Finished(context.Job, LibraryJobStatusDto.Cancelled, clock.GetUtcNow());
        }
        catch (LibraryJobException exception)
        {
            LogFailed(logger, context.Job.Kind, exception.Code);
            return LibraryJobs.Failed(context.Job, exception.Code, exception.Message, clock.GetUtcNow());
        }
        catch (Exception exception)
        {
            LogUnexpected(logger, context.Job.Kind, exception.GetType().Name);
            return LibraryJobs.Failed(context.Job, LibraryJobErrorCodeDto.IoError, LibraryJobs.UnexpectedText, clock.GetUtcNow());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Library job {Kind} failed: {Code}")]
    private static partial void LogFailed(ILogger logger, LibraryJobKindDto kind, LibraryJobErrorCodeDto code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Library job {Kind} failed unexpectedly ({ExceptionType})")]
    private static partial void LogUnexpected(ILogger logger, LibraryJobKindDto kind, string exceptionType);

    private sealed record Running(LibraryJobContext Context, CancellationTokenSource Cancellation, LibraryJobHooks? Hooks);
}
