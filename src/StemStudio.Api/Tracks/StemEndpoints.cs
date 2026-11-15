using System.Globalization;
using Microsoft.Net.Http.Headers;
using StemStudio.Api.Storage;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tracks;

/// <summary>Maps <c>GET /api/tracks/{trackId}/stems/{stemName}</c> (ADR-004).</summary>
public static class StemEndpoints
{
    /// <summary>Maps the stem endpoint.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapStemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(TrackEndpoints.TrackRoute + "/stems/{stemName}", (Guid trackId, string stemName, StemService stems, CancellationToken ct) =>
                stems.GetAsync(trackId, stemName, ct))
            .WithName("getStem").WithTags("tracks")
            .Produces(StatusCodes.Status200OK, contentType: StemService.ContentType)
            .Produces(StatusCodes.Status206PartialContent, contentType: StemService.ContentType)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status416RangeNotSatisfiable);
        return app;
    }
}

/// <summary>
/// Serves stem files with static-file semantics: <c>Range</c> (206/416), <c>If-Range</c>, <c>ETag</c>,
/// <c>Last-Modified</c> and conditional GET (304), via <see cref="TypedResults.PhysicalFile"/>.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="stems">Stem store (paths from the GUID and the stem enum only).</param>
public sealed class StemService(ITrackRepository tracks, IStemFileStore stems)
{
    /// <summary>Media type of stem files.</summary>
    public const string ContentType = "audio/flac";

    /// <summary>Serves one stem of a completed track.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="stemName">Stem name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file result or 404.</returns>
    public async Task<IResult> GetAsync(Guid trackId, string stemName, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(trackId);
        }

        var file = HasStem(track, stemName) ? stems.FindStem(trackId, stemName) : null;
        return file is null
            ? ApiProblems.NotFound($"Track {trackId} has no stem '{stemName}'.")
            : TypedResults.PhysicalFile(file.FullName, ContentType, lastModified: file.LastWriteTimeUtc, entityTag: EntityTag(file), enableRangeProcessing: true);
    }

    private static bool HasStem(Track track, string stemName) =>
        track.Status == TrackStatus.Completed && track.Stems.Any(stem => stem.Name == stemName);

    private static EntityTagHeaderValue EntityTag(FileInfo file) =>
        new(string.Create(CultureInfo.InvariantCulture, $"\"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}\""));
}
