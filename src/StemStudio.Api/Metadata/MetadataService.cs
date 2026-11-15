using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Metadata;

/// <summary>Band information and the lookup retry/correction of a track (FR-008, FR-009, FR-012).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="artists">Artist repository.</param>
/// <param name="trackService">Schedules the lookup of a track opened for the first time.</param>
/// <param name="clock">Clock.</param>
public sealed class MetadataService(ITrackRepository tracks, IArtistRepository artists, TrackService trackService, TimeProvider clock)
{
    /// <summary>Title of the 409 problem while a lookup runs.</summary>
    public const string InProgressTitle = "Lookup in progress";

    /// <summary>Detail of the 409 problem while a lookup runs.</summary>
    public const string InProgressDetail = "Band information is being looked up; try again when it has finished.";

    /// <summary>The band information of a track; a completed track never looked up is scheduled first (FR-012).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetBandInfoAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(trackId);
        }

        await trackService.ScheduleOwnLookupAsync(track, cancellationToken);
        var artist = track.ArtistId is { } artistId ? await artists.FindAsync(artistId, cancellationToken) : null;
        return TypedResults.Ok(BandInfoMapper.ToDto(track, artist));
    }

    /// <summary>Retries the lookup, optionally with corrected names (stored with <c>correctedByUser</c>).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="request">Corrected names, or null/empty to retry.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 422.</returns>
    public async Task<IResult> LookupAsync(Guid trackId, MetadataLookupRequest? request, CancellationToken cancellationToken)
    {
        var names = request ?? new MetadataLookupRequest();
        if (Validate(names) is { } invalid)
        {
            return invalid;
        }

        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(trackId);
        }

        return track.CanRetryLookup ? await RetryAsync(track, names, cancellationToken) : ApiProblems.Conflict(InProgressTitle, InProgressDetail);
    }

    private async Task<IResult> RetryAsync(Track track, MetadataLookupRequest names, CancellationToken cancellationToken)
    {
        track.RetryLookup(names.Title, names.Artist, clock.GetUtcNow().UtcDateTime);
        await tracks.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Track(track.Id), TrackMapper.ToDto(track));
    }

    private static IResult? Validate(MetadataLookupRequest names)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Check(errors, "title", names.Title);
        Check(errors, "artist", names.Artist);
        return errors.Count == 0 ? null : ApiProblems.Unprocessable(errors);
    }

    private static void Check(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (value is not null && value.Trim().Length is 0 or > ContractLimits.MetadataNameMaxLength)
        {
            errors[field] = [$"The {field} must be 1 to {ContractLimits.MetadataNameMaxLength} characters long."];
        }
    }
}
