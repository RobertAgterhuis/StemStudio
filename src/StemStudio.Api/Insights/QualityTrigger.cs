using StemStudio.Api.Sync;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Insights;

/// <summary>Starts a quality report after every standard and detail separation (FR-002, settings <c>QualityReport:Enabled</c> and <c>AutoQualityReport</c>).</summary>
/// <param name="service">The quality service.</param>
/// <param name="logger">Logger.</param>
public sealed partial class QualityTrigger(QualityService service, ILogger<QualityTrigger> logger) : ITrackCompletionHook, IDetailCompletionHook
{
    /// <inheritdoc />
    public Task OnCompletedAsync(Track track, CancellationToken cancellationToken) => StartAsync(track, cancellationToken);

    /// <inheritdoc />
    public Task OnDetailCompletedAsync(Track track, CancellationToken cancellationToken) => StartAsync(track, cancellationToken);

    private async Task StartAsync(Track track, CancellationToken cancellationToken)
    {
        if (!await service.StartAutomaticallyAsync(track, cancellationToken))
        {
            LogNotStarted(logger, track.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Quality report of track {TrackId} was not started automatically")]
    private static partial void LogNotStarted(ILogger logger, Guid trackId);
}
