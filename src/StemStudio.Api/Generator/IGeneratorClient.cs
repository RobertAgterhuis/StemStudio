namespace StemStudio.Api.Generator;

/// <summary>Typed client of the generator service (generator contract 1.0.0/1.1.0).</summary>
public interface IGeneratorClient
{
    /// <summary>True when a base URL is configured.</summary>
    bool IsConfigured { get; }

    /// <summary><c>GET /health</c> within 2 seconds.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The health.</returns>
    /// <exception cref="GeneratorUnavailableException">Not installed, unreachable or too slow.</exception>
    Task<GeneratorHealth> GetHealthAsync(CancellationToken cancellationToken);

    /// <summary>Creates a restyle job (multipart: <c>params</c> + 1-6 <c>stem</c> parts); never retried.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    /// <exception cref="GeneratorRequestException">Refused (4xx).</exception>
    /// <exception cref="GeneratorUnavailableException">Unreachable or 5xx.</exception>
    Task<GeneratorJob> CreateRestyleAsync(GeneratorRestyleRequest request, CancellationToken cancellationToken);

    /// <summary>One job; null when unknown.</summary>
    /// <param name="generationId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job or null.</returns>
    Task<GeneratorJob?> GetAsync(string generationId, CancellationToken cancellationToken);

    /// <summary>Cancels a job; null when unknown.</summary>
    /// <param name="generationId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job after the request, or null.</returns>
    Task<GeneratorJob?> CancelAsync(string generationId, CancellationToken cancellationToken);

    /// <summary>Streams a take into the destination and verifies its SHA-256.</summary>
    /// <param name="take">The job, take number and expected hash.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="GeneratorIntegrityException">The hash does not match.</exception>
    Task DownloadTakeAsync(GeneratorTakeRef take, Stream destination, CancellationToken cancellationToken);

    /// <summary>Deletes a job and its files (404 counts as done).</summary>
    /// <param name="generationId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(string generationId, CancellationToken cancellationToken);
}
