using StemStudio.Api.Tracks;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Lyrics;

/// <summary>
/// The lyrics endpoints of a track (FR-007, FR-009, FR-013): every answer is <c>no-store</c>; the text is only ever
/// part of these responses, never of the track resources or the logs (FR-010).
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="trackService">Schedules the lookups of a track opened for the first time.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsService(ITrackRepository tracks, TrackService trackService, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The lyrics; a completed track whose status is none starts its search first (FR-013).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsProblems.NotStored(TrackService.NotFound(trackId));
        }

        await trackService.ScheduleOwnLookupAsync(track, cancellationToken);
        return Document(track, StatusCodes.Status200OK);
    }

    /// <summary>Stores the offset (FR-007).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="request">The offset.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 or 422.</returns>
    public Task<IResult> SetOffsetAsync(Guid trackId, LyricsOffsetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(trackId, lyrics => lyrics.SetOffset(request.OffsetMs, Now), StatusCodes.Status200OK, cancellationToken);
    }

    /// <summary>Applies a lyrics transition and saves; conflicts become 409 and broken rules 422 (nothing is saved).</summary>
    private async Task<IResult> ChangeAsync(Guid trackId, Action<TrackLyrics> change, int status, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsProblems.NotStored(TrackService.NotFound(trackId));
        }

        if (Apply(track, change) is { } problem)
        {
            return LyricsProblems.NotStored(problem);
        }

        await tracks.SaveAsync(cancellationToken);
        return Document(track, status);
    }

    private IResult? Apply(Track track, Action<TrackLyrics> change)
    {
        try
        {
            change(track.EnsureLyrics(Now));
            return null;
        }
        catch (LyricsConflictException conflict)
        {
            return LyricsProblems.Conflict(conflict.Conflict);
        }
        catch (DomainValidationException invalid)
        {
            return LyricsProblems.Invalid(invalid);
        }
    }

    private static IResult Document(Track track, int status) =>
        LyricsProblems.NotStored(TypedResults.Json(LyricsMapper.ToDto(track.Lyrics), statusCode: status));
}
