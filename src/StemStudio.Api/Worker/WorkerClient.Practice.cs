using System.Globalization;
using System.Net;

namespace StemStudio.Api.Worker;

/// <summary>
/// Practice-lane calls of the worker API 1.8.0 (feature 014): section detection, score views and setlist documents. A separate
/// interface so the 001..013 <see cref="IWorkerClient"/> and its decorators stay unchanged.
/// </summary>
public interface IWorkerPracticeClient
{
    /// <summary>Submits a section detection (<c>POST /v1/section-jobs</c>).</summary>
    /// <param name="request">Stems, details, grid and length.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed.</exception>
    /// <exception cref="WorkerRequestException">The worker rejected the request (4xx).</exception>
    Task<WorkerSectionJob> CreateSectionJobAsync(WorkerSectionJobRequest request, CancellationToken cancellationToken);

    /// <summary>Reads a section job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404).</returns>
    Task<WorkerSectionJob?> GetSectionJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Deletes a section job (404 counts as done).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteSectionJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Submits a score view (<c>POST /v1/score-view-jobs</c>).</summary>
    /// <param name="request">MusicXML path, grid and length.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    Task<WorkerScoreViewJob> CreateScoreViewJobAsync(WorkerScoreViewJobRequest request, CancellationToken cancellationToken);

    /// <summary>Reads a score view job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null (404).</returns>
    Task<WorkerScoreViewJob?> GetScoreViewJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Streams one SVG page of a completed score view job (at most <see cref="WorkerClient.MaxScoreViewPageBytes"/>).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerResponseTooLargeException">The page is larger than the cap.</exception>
    Task DownloadScoreViewPageAsync(string jobId, int page, Stream destination, CancellationToken cancellationToken);

    /// <summary>Deletes a score view job (404 counts as done).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteScoreViewJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Submits a setlist document (<c>POST /v1/setlist-document-jobs</c>).</summary>
    /// <param name="request">The content.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    Task<WorkerSetlistDocumentJob> CreateSetlistDocumentJobAsync(WorkerSetlistDocumentRequest request, CancellationToken cancellationToken);

    /// <summary>Reads a setlist document job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null (404).</returns>
    Task<WorkerSetlistDocumentJob?> GetSetlistDocumentJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Streams the PDF of a completed setlist document job (at most <see cref="WorkerClient.MaxSetlistDocumentBytes"/>).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerResponseTooLargeException">The PDF is larger than the cap.</exception>
    Task DownloadSetlistDocumentAsync(string jobId, Stream destination, CancellationToken cancellationToken);

    /// <summary>Deletes a setlist document job (404 counts as done).</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteSetlistDocumentJobAsync(string jobId, CancellationToken cancellationToken);
}

/// <summary>Practice-lane calls of the worker API 1.8.0 (feature 014).</summary>
public sealed partial class WorkerClient : IWorkerPracticeClient
{
    /// <summary>Largest score view page (SVG) the API takes from the worker: 8 MiB.</summary>
    public const long MaxScoreViewPageBytes = 8L * 1024 * 1024;

    /// <summary>Largest setlist PDF the API takes from the worker: 16 MiB.</summary>
    public const long MaxSetlistDocumentBytes = 16L * 1024 * 1024;

    private const string SectionJobs = "v1/section-jobs";
    private const string ScoreViewJobs = "v1/score-view-jobs";
    private const string SetlistDocumentJobs = "v1/setlist-document-jobs";

    /// <inheritdoc />
    public Task<WorkerSectionJob> CreateSectionJobAsync(WorkerSectionJobRequest request, CancellationToken cancellationToken) =>
        PostJobAsync<WorkerSectionJob>(SectionJobs, request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerSectionJob?> GetSectionJobAsync(string jobId, CancellationToken cancellationToken) =>
        GetJobOrNullAsync<WorkerSectionJob>(PracticePath(SectionJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task DeleteSectionJobAsync(string jobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(PracticePath(SectionJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task<WorkerScoreViewJob> CreateScoreViewJobAsync(WorkerScoreViewJobRequest request, CancellationToken cancellationToken) =>
        PostJobAsync<WorkerScoreViewJob>(ScoreViewJobs, request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerScoreViewJob?> GetScoreViewJobAsync(string jobId, CancellationToken cancellationToken) =>
        GetJobOrNullAsync<WorkerScoreViewJob>(PracticePath(ScoreViewJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task DownloadScoreViewPageAsync(string jobId, int page, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        return DownloadAsync(string.Create(CultureInfo.InvariantCulture, $"{PracticePath(ScoreViewJobs, jobId)}/pages/{page}"), destination, MaxScoreViewPageBytes, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteScoreViewJobAsync(string jobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(PracticePath(ScoreViewJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task<WorkerSetlistDocumentJob> CreateSetlistDocumentJobAsync(WorkerSetlistDocumentRequest request, CancellationToken cancellationToken) =>
        PostJobAsync<WorkerSetlistDocumentJob>(SetlistDocumentJobs, request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerSetlistDocumentJob?> GetSetlistDocumentJobAsync(string jobId, CancellationToken cancellationToken) =>
        GetJobOrNullAsync<WorkerSetlistDocumentJob>(PracticePath(SetlistDocumentJobs, jobId), cancellationToken);

    /// <inheritdoc />
    public Task DownloadSetlistDocumentAsync(string jobId, Stream destination, CancellationToken cancellationToken) =>
        DownloadAsync($"{PracticePath(SetlistDocumentJobs, jobId)}/file", destination, MaxSetlistDocumentBytes, cancellationToken);

    /// <inheritdoc />
    public Task DeleteSetlistDocumentJobAsync(string jobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(PracticePath(SetlistDocumentJobs, jobId), cancellationToken);

    private static string PracticePath(string collection, string jobId) => $"{collection}/{ValidJobId(jobId)}";

    /// <summary>Streams a file response into the destination, refusing a declared or counted size above <paramref name="maxBytes"/>.</summary>
    private async Task DownloadAsync(string path, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(message, response, cancellationToken);
        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw TooLarge(message, maxBytes);
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (!await CopyWithinAsync(source, destination, maxBytes, cancellationToken))
        {
            throw TooLarge(message, maxBytes);
        }
    }

    /// <summary>Copies at most <paramref name="maxBytes"/>; false as soon as the source has more (the rest is never read).</summary>
    private static async Task<bool> CopyWithinAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        var total = 0L;
        while (await source.ReadAsync(chunk, cancellationToken) is var read and > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return false;
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private WorkerResponseTooLargeException TooLarge(HttpRequestMessage message, long maxBytes)
    {
        var refused = Describe(message);
        LogTooLarge(logger, refused, maxBytes);
        return new WorkerResponseTooLargeException(string.Create(CultureInfo.InvariantCulture, $"The processing service sent a file above {maxBytes / (1024 * 1024)} MiB."));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Processing service file refused on {Request}: above {MaxBytes} bytes")]
    private static partial void LogTooLarge(ILogger logger, string request, long maxBytes);

    private async Task<T> PostJobAsync<T>(string path, object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request, request.GetType(), options: Json) };
        return await SendForAsync<T>(message, cancellationToken);
    }

    private async Task<T?> GetJobOrNullAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<T>(message, response, cancellationToken);
    }
}
