using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Worker;

/// <summary>Request to create a job for a YouTube or Spotify URL.</summary>
/// <param name="SourceType">Youtube or Spotify.</param>
/// <param name="Url">The canonical URL.</param>
public sealed record WorkerUrlJobRequest(SourceType SourceType, string Url);

/// <summary>An upload to send to the worker, streamed from disk.</summary>
/// <param name="FilePath">Local path of the stored upload.</param>
/// <param name="FileName">File name sent in the multipart part (extension .mp3, .wav or .flac).</param>
public sealed record WorkerUpload(string FilePath, string FileName);

/// <summary>Typed client of the worker HTTP API v1 (worker-api.openapi.yaml).</summary>
public interface IWorkerClient
{
    /// <summary>Creates (or returns the existing) job for a URL.</summary>
    /// <param name="request">Source type and canonical URL.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job.</returns>
    /// <exception cref="WorkerRequestException">The worker rejected the request (4xx).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerJob> CreateUrlJobAsync(WorkerUrlJobRequest request, CancellationToken cancellationToken);

    /// <summary>Creates (or returns the existing) job for an uploaded file; the file is streamed from disk.</summary>
    /// <param name="upload">The stored upload.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job.</returns>
    /// <exception cref="WorkerRequestException">The worker rejected the request (4xx).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerJob> CreateUploadJobAsync(WorkerUpload upload, CancellationToken cancellationToken);

    /// <summary>Gets a job.</summary>
    /// <param name="jobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404).</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerJob?> GetJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Gets the result of a completed job.</summary>
    /// <param name="jobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    /// <exception cref="WorkerRequestException">Unknown or not completed job (404/409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerJobResult> GetResultAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Downloads one stem (FLAC) into a stream.</summary>
    /// <param name="jobId">Job ID.</param>
    /// <param name="stem">Stem name.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerRequestException">Unknown job or stem, or not completed (404/409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task DownloadStemAsync(string jobId, string stem, Stream destination, CancellationToken cancellationToken);

    /// <summary>Creates (or returns the existing) detail job for the stems of a completed standard job (200 or 202).</summary>
    /// <param name="standardJobId">Completed standard job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The detail job (already completed on a cache hit).</returns>
    /// <exception cref="WorkerRequestException">Unknown standard job (404) or not completed / stems missing (409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerDetailJob> CreateDetailJobAsync(string standardJobId, CancellationToken cancellationToken);

    /// <summary>Gets a detail job.</summary>
    /// <param name="detailJobId">Detail job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404, e.g. after a restart).</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerDetailJob?> GetDetailJobAsync(string detailJobId, CancellationToken cancellationToken);

    /// <summary>Cancels a queued or running detail job.</summary>
    /// <param name="detailJobId">Detail job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The cancelled job.</returns>
    /// <exception cref="WorkerRequestException">Unknown job (404) or already completed or failed (409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerDetailJob> CancelDetailJobAsync(string detailJobId, CancellationToken cancellationToken);

    /// <summary>Gets the per-parent outcomes of a completed detail job.</summary>
    /// <param name="detailJobId">Detail job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    /// <exception cref="WorkerRequestException">Unknown or not completed job (404/409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerDetailResult> GetDetailResultAsync(string detailJobId, CancellationToken cancellationToken);

    /// <summary>Downloads one detail stem (FLAC) into a stream.</summary>
    /// <param name="stem">Detail job, parent and detail stem name (allowed for the parent).</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">The names are not a valid detail stem (no request is sent).</exception>
    /// <exception cref="WorkerRequestException">Unknown job or stem, or not completed (404/409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task DownloadDetailStemAsync(WorkerDetailStemRef stem, Stream destination, CancellationToken cancellationToken);

    /// <summary>Reads the worker's <c>GET /health</c> within <see cref="WorkerTimeouts.Health"/>.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The health with the active processing device.</returns>
    /// <exception cref="WorkerRequestException">The worker rejected the request (4xx).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached in time, failed (5xx) or sent an invalid body.</exception>
    Task<WorkerHealth> GetHealthAsync(CancellationToken cancellationToken);

    /// <summary>Creates a metadata job (<c>POST /v1/metadata-jobs</c>, worker 1.3.0); never retried.</summary>
    /// <param name="request">Names and facts to look up.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="WorkerRequestException">The worker rejected the request (422).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerMetadataJob> CreateMetadataJobAsync(WorkerMetadataJobRequest request, CancellationToken cancellationToken);

    /// <summary>Gets a metadata job.</summary>
    /// <param name="metadataJobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404, e.g. swept after 24 h).</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerMetadataJob?> GetMetadataJobAsync(string metadataJobId, CancellationToken cancellationToken);

    /// <summary>Gets the result of a completed metadata job.</summary>
    /// <param name="metadataJobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    /// <exception cref="WorkerRequestException">Unknown or not completed job (404/409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerMetadataResult> GetMetadataResultAsync(string metadataJobId, CancellationToken cancellationToken);

    /// <summary>Downloads one picture file (JPEG, at most <see cref="WorkerClient.MaxArtworkBytes"/>).</summary>
    /// <param name="artwork">Owner, identifier and variant.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The JPEG bytes, or null when the worker has no such picture (404).</returns>
    /// <exception cref="ArgumentException">The reference is invalid (no request is sent).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached, failed (5xx) or sent an oversized body.</exception>
    Task<byte[]?> GetArtworkAsync(WorkerArtworkRef artwork, CancellationToken cancellationToken);

    /// <summary>Streams a user picture to <c>POST /v1/artwork</c> for re-encoding (never retried).</summary>
    /// <param name="file">The picture.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The normalised picture.</returns>
    /// <exception cref="WorkerRequestException">Not a picture (400), too large (413), wrong type (415) or over 40 MP (422).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerArtworkUpload> NormalizeArtworkAsync(WorkerArtworkFile file, CancellationToken cancellationToken);

    /// <summary>Creates a render job (<c>POST /v1/render-jobs</c>, worker 1.4.0); never retried.</summary>
    /// <param name="request">Inputs, master gain, range and output.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="WorkerRequestException">Invalid input path (400), input not found (404) or invalid values (422).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerRenderJob> CreateRenderJobAsync(WorkerRenderJobRequest request, CancellationToken cancellationToken);

    /// <summary>Gets a render job.</summary>
    /// <param name="renderJobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when the worker does not know it (404).</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task<WorkerRenderJob?> GetRenderJobAsync(string renderJobId, CancellationToken cancellationToken);

    /// <summary>Streams the file of a completed render job into a stream.</summary>
    /// <param name="renderJobId">Job ID.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerRequestException">Unknown job (404) or not completed (409).</exception>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task DownloadRenderFileAsync(string renderJobId, Stream destination, CancellationToken cancellationToken);

    /// <summary>Cancels and removes a render job (idempotent; 404 is tolerated).</summary>
    /// <param name="renderJobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached or failed (5xx).</exception>
    Task DeleteRenderJobAsync(string renderJobId, CancellationToken cancellationToken);
}
