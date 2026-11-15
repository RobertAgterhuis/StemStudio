using StemStudio.Contracts.Generation;
using StemStudio.Web.Api;

namespace StemStudio.Web.Generation;

/// <summary>
/// The availability of the generative features for one circuit (009 FR-010, 010 FR-012): asks
/// <c>GET /api/generation/status</c> at most every 10 s (the API caches for 10 s as well). When the status cannot be
/// read, generation counts as not installed with that reason, so the rest of the app keeps working.
/// </summary>
/// <param name="api">The backend client.</param>
/// <param name="time">The clock.</param>
public sealed class GenerationStatusSource(IStemStudioApiClient api, TimeProvider time)
{
    /// <summary>How long one answer is reused.</summary>
    public static TimeSpan CacheFor { get; } = TimeSpan.FromSeconds(10);

    private GenerationStatusDto? _status;
    private DateTimeOffset _readAt;

    /// <summary>The current status (cached for <see cref="CacheFor"/>).</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The status; never throws for API failures.</returns>
    public async Task<GenerationStatusDto> GetAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        if (_status is null || now - _readAt >= CacheFor)
        {
            (_status, _readAt) = (await ReadAsync(ct), now);
        }

        return _status;
    }

    private async Task<GenerationStatusDto> ReadAsync(CancellationToken ct)
    {
        try
        {
            return await api.GetGenerationStatusAsync(ct);
        }
        catch (ApiException ex)
        {
            return new GenerationStatusDto
            {
                State = GenerationAvailabilityDto.NotInstalled,
                Reason = $"The generation status could not be read: {ex.Error.UserMessage}",
            };
        }
    }
}
