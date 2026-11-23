namespace StemStudio.Api.Worker;

/// <summary>
/// Insights-lane calls of the worker API 1.11.0 (feature 020): quality jobs and handout jobs. A separate interface so the
/// earlier worker interfaces and their decorators stay unchanged.
/// </summary>
public interface IWorkerInsightsClient
{
    /// <summary>Submits a quality job (<c>POST /v1/quality-jobs</c>).</summary>
    /// <param name="request">The stems and metrics.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed.</exception>
    /// <exception cref="WorkerRequestException">The worker rejected the request (400 path, 404 input, 422 body).</exception>
    Task<WorkerQualityJob> CreateQualityJobAsync(WorkerQualityJobRequest request, CancellationToken cancellationToken);

    /// <summary>Reads a quality job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404).</returns>
    Task<WorkerQualityJob?> GetQualityJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Cancels and removes a quality job (404 counts as done).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteQualityJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Submits a handout job (<c>POST /v1/handout-jobs</c>).</summary>
    /// <param name="request">The resolved document and the paper.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed.</exception>
    /// <exception cref="WorkerRequestException">The worker rejected the request (400 path, 404 input, 422 body).</exception>
    Task<WorkerHandoutJob> CreateHandoutJobAsync(WorkerHandoutJobRequest request, CancellationToken cancellationToken);

    /// <summary>Reads a handout job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null (404).</returns>
    Task<WorkerHandoutJob?> GetHandoutJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Streams the PDF of a completed handout job (at most <see cref="WorkerClient.MaxHandoutBytes"/>).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerRequestException">409 <c>handout-not-completed</c> or 404.</exception>
    /// <exception cref="WorkerResponseTooLargeException">The PDF is larger than the cap.</exception>
    Task DownloadHandoutFileAsync(string jobId, Stream destination, CancellationToken cancellationToken);

    /// <summary>Cancels and removes a handout job and its file (404 counts as done).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteHandoutJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>The worker's insights health (<c>/health</c> <c>insights</c>).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The block, or null for a worker before 1.11.0.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached.</exception>
    Task<WorkerInsightsHealth?> GetInsightsHealthAsync(CancellationToken cancellationToken);
}

/// <summary>Insights-lane calls of the worker API 1.11.0 (feature 020).</summary>
public sealed partial class WorkerClient : IWorkerInsightsClient
{
    /// <summary>Largest handout PDF the API takes from the worker: 64 MiB (a 50-song setlist with notation).</summary>
    public const long MaxHandoutBytes = 64L * 1024 * 1024;

    private const string QualityJobs = "v1/quality-jobs";
    private const string HandoutJobs = "v1/handout-jobs";

    /// <inheritdoc />
    public Task<WorkerQualityJob> CreateQualityJobAsync(WorkerQualityJobRequest request, CancellationToken cancellationToken) =>
        PostJobAsync<WorkerQualityJob>(QualityJobs, request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerQualityJob?> GetQualityJobAsync(string jobId, CancellationToken cancellationToken) =>
        GetJobOrNullAsync<WorkerQualityJob>(PracticePath(QualityJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task DeleteQualityJobAsync(string jobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(PracticePath(QualityJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task<WorkerHandoutJob> CreateHandoutJobAsync(WorkerHandoutJobRequest request, CancellationToken cancellationToken) =>
        PostJobAsync<WorkerHandoutJob>(HandoutJobs, request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerHandoutJob?> GetHandoutJobAsync(string jobId, CancellationToken cancellationToken) =>
        GetJobOrNullAsync<WorkerHandoutJob>(PracticePath(HandoutJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task DownloadHandoutFileAsync(string jobId, Stream destination, CancellationToken cancellationToken) =>
        DownloadAsync($"{PracticePath(HandoutJobs, jobId)}/file", destination, MaxHandoutBytes, cancellationToken);

    /// <inheritdoc />
    public Task DeleteHandoutJobAsync(string jobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(PracticePath(HandoutJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public async Task<WorkerInsightsHealth?> GetInsightsHealthAsync(CancellationToken cancellationToken) =>
        (await GetHealthAsync(cancellationToken)).Insights;
}
