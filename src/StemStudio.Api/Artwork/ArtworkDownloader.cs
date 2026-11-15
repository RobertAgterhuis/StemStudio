using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Artwork;

/// <summary>A picture the worker offers for a track: where to download it and what it is.</summary>
/// <param name="Owner">The worker resource holding the files (standard or metadata job).</param>
/// <param name="Id">Its identifier.</param>
/// <param name="Info">The worker's description of the picture.</param>
public sealed record ArtworkOffer(WorkerArtworkOwner Owner, string Id, WorkerArtworkInfo Info);

/// <summary>
/// Downloads an automatic picture from the worker and offers it to a track (FR-001, FR-005), but only when the
/// precedence would take it (so the same hash is never downloaded twice). Every failure is logged and leaves the track
/// as it was (FR-002); the next poll tries again.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="files">Picture store.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ArtworkDownloader(IWorkerClient worker, IArtworkFileStore files, TimeProvider clock, ILogger<ArtworkDownloader> logger)
{
    /// <summary>Stores and offers the picture when the track would take it (not saved).</summary>
    /// <param name="track">The track.</param>
    /// <param name="offer">The offered picture.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when the track took the picture.</returns>
    public async Task<bool> OfferAsync(Track track, ArtworkOffer offer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(offer);
        var candidate = Candidate(track.Id, offer.Info);
        return candidate is not null && track.AcceptsArtwork(candidate) && await TryStoreAsync(track, offer, candidate, cancellationToken);
    }

    private async Task<bool> TryStoreAsync(Track track, ArtworkOffer offer, ArtworkCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            return await StoreAsync(track, offer, candidate, cancellationToken);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException or IOException or UnauthorizedAccessException)
        {
            LogDownloadFailed(logger, track.Id, exception.Message);
            return false;
        }
    }

    private async Task<bool> StoreAsync(Track track, ArtworkOffer offer, ArtworkCandidate candidate, CancellationToken cancellationToken)
    {
        var cover = await worker.GetArtworkAsync(new WorkerArtworkRef(offer.Owner, offer.Id, ApiRoutes.ArtworkCover), cancellationToken);
        var thumb = await worker.GetArtworkAsync(new WorkerArtworkRef(offer.Owner, offer.Id, ApiRoutes.ArtworkThumb), cancellationToken);
        if (cover is null || thumb is null)
        {
            LogMissing(logger, track.Id);
            return false;
        }

        await files.SaveAsync(track.Id, ArtworkSlot.Auto, new ArtworkImages(cover, thumb), cancellationToken);
        return track.OfferArtwork(candidate, clock.GetUtcNow().UtcDateTime);
    }

    private ArtworkCandidate? Candidate(Guid trackId, WorkerArtworkInfo info)
    {
        try
        {
            return info.Source == WorkerArtworkSource.User ? null : new ArtworkCandidate(Enum.Parse<ArtworkSource>(info.Source.ToString()), info.Sha256);
        }
        catch (DomainValidationException invalid)
        {
            LogInvalid(logger, trackId, invalid.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: ignoring the picture offered by the processing service: {Reason}")]
    private static partial void LogInvalid(ILogger logger, Guid trackId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: the offered picture is no longer available in the processing service")]
    private static partial void LogMissing(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: storing the picture failed, retrying on the next poll: {Reason}")]
    private static partial void LogDownloadFailed(ILogger logger, Guid trackId, string reason);
}
