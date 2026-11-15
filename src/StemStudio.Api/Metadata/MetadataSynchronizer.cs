using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Metadata;

/// <summary>
/// One pass over every track whose lookup is pending or searching (plan.md key flow 3): creates metadata jobs for
/// pending lookups and polls the others. An unreachable worker keeps the state; one broken track never stops the others.
/// </summary>
/// <param name="tracks">Track repository (scoped).</param>
/// <param name="worker">Worker client.</param>
/// <param name="applier">Applies metadata job states.</param>
/// <param name="logger">Logger.</param>
public sealed partial class MetadataSynchronizer(
    ITrackRepository tracks, IWorkerClient worker, MetadataJobApplier applier, ILogger<MetadataSynchronizer> logger)
{
    /// <summary>Synchronises every active lookup and saves after each track.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        foreach (var track in await tracks.ListLookupActiveAsync(cancellationToken))
        {
            try
            {
                await SyncAsync(track, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSyncFailed(logger, track.Id, exception);
            }
        }
    }

    private async Task SyncAsync(Track track, CancellationToken cancellationToken)
    {
        var (before, artist) = (track.Metadata.Status, track.ArtistId);
        try
        {
            await (before == MetadataStatus.Pending ? SubmitAsync(track, cancellationToken) : PollAsync(track, cancellationToken));
        }
        catch (WorkerUnavailableException)
        {
            LogWorkerUnavailable(logger, track.Id);
            return;
        }

        await SaveAsync(track, (before, artist), cancellationToken);
    }

    /// <summary>Saves, deletes the artist the track no longer references when unused, and logs a status change.</summary>
    private async Task SaveAsync(Track track, (MetadataStatus Status, Guid? Artist) before, CancellationToken cancellationToken)
    {
        await tracks.SaveAsync(cancellationToken);
        await applier.RemoveReplacedArtistAsync(before.Artist, track, cancellationToken);
        LogTransition(track, before.Status);
    }

    private async Task SubmitAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            applier.ApplySubmission(track, await worker.CreateMetadataJobAsync(MetadataMapper.ToRequest(track), cancellationToken));
        }
        catch (WorkerRequestException rejection)
        {
            applier.ApplyRejection(track, rejection.Detail);
        }
    }

    private async Task PollAsync(Track track, CancellationToken cancellationToken) =>
        await applier.ApplyAsync(track, await worker.GetMetadataJobAsync(track.Metadata.MetadataJobId!, cancellationToken), cancellationToken);

    private void LogTransition(Track track, MetadataStatus before)
    {
        if (track.Metadata.Status != before)
        {
            LogStatusChanged(logger, track.Id, before, track.Metadata.Status, track.Metadata.Reason);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Track {TrackId}: lookup {From} -> {To} {Reason}")]
    private static partial void LogStatusChanged(ILogger logger, Guid trackId, MetadataStatus from, MetadataStatus to, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: processing service unreachable for the lookup, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Track {TrackId}: lookup synchronisation failed unexpectedly")]
    private static partial void LogSyncFailed(ILogger logger, Guid trackId, Exception exception);
}
