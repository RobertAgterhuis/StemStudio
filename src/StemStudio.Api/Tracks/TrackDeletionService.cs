using StemStudio.Api.Storage;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tracks;

/// <summary>
/// Deletes a failed or completed track: first the track row and its mixes (one unit of work), then best-effort its stem
/// files and stored upload. The worker is never called, so its per-source cache stays intact.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="stems">Stem file store.</param>
/// <param name="uploads">Upload store.</param>
/// <param name="generations">Generations (their source track is detached, feature 009).</param>
/// <param name="logger">Logger for file clean-up failures.</param>
public sealed partial class TrackDeletionService(
    ITrackRepository tracks, IStemFileStore stems, IUploadStore uploads, IGenerationRepository generations, ILogger<TrackDeletionService> logger)
{
    /// <summary>Title of the 409 problem for a track that is still queued or processing.</summary>
    public const string StillProcessingTitle = "Track is still being processed";

    /// <summary>Detail of the 409 problem for a track that is still queued or processing.</summary>
    public const string StillProcessingDetail = "The track is still being processed; wait until it has completed or failed.";

    /// <summary>Title of the 409 problem for a track whose detail phase is queued or running (FR-016).</summary>
    public const string DetailActiveTitle = "Detail separation in progress";

    /// <summary>Detail of the 409 problem for a track whose detail phase is queued or running (FR-016).</summary>
    public const string DetailActiveDetail = "The detail separation of this track is queued or running; cancel it first.";

    /// <summary>Deletes a track in a terminal status.</summary>
    /// <param name="id">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204, 404 or 409.</returns>
    public async Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(id, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(id);
        }

        if (DeleteBlocker(track) is { } conflict)
        {
            return conflict;
        }

        await tracks.RemoveWithMixesAsync(track, cancellationToken);
        await DetachGenerationsAsync(id, cancellationToken);
        DeleteFiles(track);
        return TypedResults.NoContent();
    }

    private async Task DetachGenerationsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        foreach (var generation in await generations.ListBySourceTrackAsync(trackId, cancellationToken))
        {
            generation.DetachSourceTrack(trackId);
        }

        await generations.SaveAsync(cancellationToken);
    }

    private static IResult? DeleteBlocker(Track track) => track switch
    {
        { IsTerminal: false } => ApiProblems.Conflict(StillProcessingTitle, StillProcessingDetail),
        { IsDetailActive: true } => ApiProblems.Conflict(DetailActiveTitle, DetailActiveDetail),
        _ => null,
    };

    private void DeleteFiles(Track track)
    {
        TryDelete(track.Id, "stem files", () => stems.DeleteTrackFiles(track.Id));
        if (track.UploadFileName is { } fileName && uploads.Exists(fileName))
        {
            TryDelete(track.Id, "upload", () => uploads.Delete(fileName));
        }
    }

    private void TryDelete(Guid trackId, string what, Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogCleanupFailed(logger, trackId, what, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId} deleted, but its {What} could not be removed from disk")]
    private static partial void LogCleanupFailed(ILogger logger, Guid trackId, string what, Exception exception);
}
