using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Lyrics;

/// <summary>
/// The lyrics endpoints of a track (FR-004, FR-007, FR-009, FR-013): every answer is <c>no-store</c>; the text is only
/// ever part of these responses, never of the track resources or the logs (FR-010).
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="trackService">Schedules the lookups of a track opened for the first time.</param>
/// <param name="aligner">The automatic rule and alignment submission.</param>
/// <param name="applier">Deletes worker jobs.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsService(ITrackRepository tracks, TrackService trackService, LyricsAligner aligner, LyricsJobApplier applier, TimeProvider clock)
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

    /// <summary>Replaces the lyrics with text entered by the user and applies the automatic rule (FR-004).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="request">The text and the language hint.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 422.</returns>
    public Task<IResult> SetAsync(Guid trackId, SetLyricsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(trackId, StatusCodes.Status202Accepted, async (track, lyrics) =>
        {
            lyrics.Paste(LyricsDocument.FromUserText(request.Text, request.Language), Now);
            await EvaluateQuietlyAsync(track, cancellationToken);
            return null;
        }, cancellationToken);
    }

    /// <summary>Starts the alignment the user asked for.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public Task<IResult> AlignAsync(Guid trackId, CancellationToken cancellationToken) =>
        ChangeAsync(trackId, StatusCodes.Status202Accepted, (track, _) => aligner.StartAsync(track, cancellationToken), cancellationToken);

    /// <summary>Cancels the running alignment and removes its worker job.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 409.</returns>
    public Task<IResult> CancelAlignAsync(Guid trackId, CancellationToken cancellationToken) =>
        ChangeAsync(trackId, StatusCodes.Status200OK, async (_, lyrics) =>
        {
            var jobId = lyrics.LyricsJobId;
            lyrics.CancelAlign(Now);
            await DeleteJobAsync(jobId, cancellationToken);
            return null;
        }, cancellationToken);

    /// <summary>Stores the offset (FR-007).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="request">The offset.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 or 422.</returns>
    public Task<IResult> SetOffsetAsync(Guid trackId, LyricsOffsetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(trackId, StatusCodes.Status200OK, (_, lyrics) =>
        {
            lyrics.SetOffset(request.OffsetMs, Now);
            return Task.FromResult<IResult?>(null);
        }, cancellationToken);
    }

    /// <summary>Applies a lyrics transition and saves; a conflict (409), a broken rule (422) or a returned problem saves nothing.</summary>
    private async Task<IResult> ChangeAsync(Guid trackId, int status, Func<Track, TrackLyrics, Task<IResult?>> change, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsProblems.NotStored(TrackService.NotFound(trackId));
        }

        if (await ApplyAsync(track, change) is { } problem)
        {
            return LyricsProblems.NotStored(problem);
        }

        await tracks.SaveAsync(cancellationToken);
        return Document(track, status);
    }

    private async Task<IResult?> ApplyAsync(Track track, Func<Track, TrackLyrics, Task<IResult?>> change)
    {
        try
        {
            return await change(track, track.EnsureLyrics(Now));
        }
        catch (Exception exception) when (LyricsProblems.For(exception) is { } problem)
        {
            return problem;
        }
    }

    /// <summary>Applies the automatic rule; an unreachable worker leaves it pending for the synchroniser.</summary>
    private async Task EvaluateQuietlyAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            await aligner.EvaluateAutoAsync(track, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            // The synchroniser evaluates the rule (or submits the started alignment) on its next pass.
        }
    }

    private async Task DeleteJobAsync(string? lyricsJobId, CancellationToken cancellationToken)
    {
        if (lyricsJobId is not null)
        {
            await applier.DeleteQuietlyAsync(lyricsJobId, cancellationToken);
        }
    }

    private static IResult Document(Track track, int status) =>
        LyricsProblems.NotStored(TypedResults.Json(LyricsMapper.ToDto(track.Lyrics), statusCode: status));
}
