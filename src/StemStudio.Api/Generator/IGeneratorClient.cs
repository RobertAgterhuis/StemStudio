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

    /// <summary>Creates a song job (JSON, no audio; 1.1.0); never retried.</summary>
    /// <param name="song">The parameters.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    Task<GeneratorJob> CreateSongAsync(GeneratorSongParams song, CancellationToken cancellationToken);

    /// <summary>Creates a voice-conversion job (multipart <c>target</c>, <c>prompt</c>, <c>accompaniment</c>; singer, 1.1.0).</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued job.</returns>
    Task<GeneratorJob> CreateVoiceConversionAsync(GeneratorVoiceConversionRequest request, CancellationToken cancellationToken);

    /// <summary>Checks a voice sample (duration, voiced ratio; singer, 1.1.0).</summary>
    /// <param name="sample">The sample bytes.</param>
    /// <param name="fileName">Its file name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The check.</returns>
    Task<GeneratorSampleCheck> CheckVoiceSampleAsync(Stream sample, string fileName, CancellationToken cancellationToken);

    /// <summary>Downloads the normalised sample of an ok check (once, within 10 minutes).</summary>
    /// <param name="checkId">The check id.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DownloadCheckedSampleAsync(string checkId, Stream destination, CancellationToken cancellationToken);

    /// <summary>Downloads the converted vocal of a voice-conversion take.</summary>
    /// <param name="generationId">Job id.</param>
    /// <param name="index">Take number.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DownloadTakeVocalAsync(string generationId, int index, Stream destination, CancellationToken cancellationToken);

    /// <summary>Deletes a job and its files (404 counts as done).</summary>
    /// <param name="generationId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(string generationId, CancellationToken cancellationToken);
}
