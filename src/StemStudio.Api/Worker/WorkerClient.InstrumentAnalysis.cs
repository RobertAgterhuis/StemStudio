namespace StemStudio.Api.Worker;

/// <summary>
/// Instrument-analysis lane of the worker API 1.9.0 (feature 016): chords, drum hits and the reference melody. A separate
/// interface so the earlier clients and their decorators stay unchanged.
/// </summary>
public interface IWorkerAnalysisClient
{
    /// <summary>Submits an analysis (<c>POST /v1/instrument-analysis-jobs</c>).</summary>
    /// <param name="request">A chords, drum hits or vocal pitch request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed.</exception>
    /// <exception cref="WorkerRequestException">The worker rejected the request (4xx).</exception>
    Task<WorkerAnalysisJob> CreateAnalysisJobAsync(WorkerAnalysisRequest request, CancellationToken cancellationToken);

    /// <summary>Reads an analysis job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404, restart).</returns>
    Task<WorkerAnalysisJob?> GetAnalysisJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Cancels and removes an analysis job (404 counts as done).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteAnalysisJobAsync(string jobId, CancellationToken cancellationToken);
}

/// <summary>Instrument-analysis lane of the worker API 1.9.0 (feature 016).</summary>
public sealed partial class WorkerClient : IWorkerAnalysisClient
{
    private const string AnalysisJobs = "v1/instrument-analysis-jobs";

    /// <inheritdoc />
    public Task<WorkerAnalysisJob> CreateAnalysisJobAsync(WorkerAnalysisRequest request, CancellationToken cancellationToken) =>
        PostJobAsync<WorkerAnalysisJob>(AnalysisJobs, request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerAnalysisJob?> GetAnalysisJobAsync(string jobId, CancellationToken cancellationToken) =>
        GetJobOrNullAsync<WorkerAnalysisJob>(PracticePath(AnalysisJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task DeleteAnalysisJobAsync(string jobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(PracticePath(AnalysisJobs, jobId), cancellationToken);
}
