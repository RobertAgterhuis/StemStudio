using System.Globalization;
using Microsoft.Net.Http.Headers;
using StemStudio.Api.Storage;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tracks;

/// <summary>
/// Maps <c>GET /api/tracks/{trackId}/stems/{stemName}</c> (ADR-004) and
/// <c>GET /api/tracks/{trackId}/detail-stems/{parent}/{name}</c> (backend 1.2.0).
/// </summary>
public static class StemEndpoints
{
    /// <summary>Maps the stem and detail stem endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapStemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(TrackEndpoints.TrackRoute + "/stems/{stemName}", (Guid trackId, string stemName, StemService stems, CancellationToken ct) =>
                stems.GetAsync(trackId, stemName, ct))
            .WithName("getStem").WithStemResponses();
        app.MapGet(TrackEndpoints.TrackRoute + "/detail-stems/{parent}/{name}", (Guid trackId, string parent, string name, StemService stems, CancellationToken ct) =>
                stems.GetDetailAsync(new DetailStemKey(trackId, parent, name), ct))
            .WithName("getDetailStem").WithStemResponses();
        return app;
    }

    private static RouteHandlerBuilder WithStemResponses(this RouteHandlerBuilder route) => route
        .WithTags("tracks")
        .Produces(StatusCodes.Status200OK, contentType: StemService.ContentType)
        .Produces(StatusCodes.Status206PartialContent, contentType: StemService.ContentType)
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status416RangeNotSatisfiable);
}

/// <summary>
/// Serves stem and detail stem files with static-file semantics: <c>Range</c> (206/416), <c>If-Range</c>, <c>ETag</c>,
/// <c>Last-Modified</c> and conditional GET (304), via <see cref="TypedResults.PhysicalFile"/>.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="stems">Stem store (paths from the GUID and the stem enum only).</param>
/// <param name="detailStems">Detail stem store (paths from the GUID and the detail enums only).</param>
public sealed class StemService(ITrackRepository tracks, IStemFileStore stems, IDetailStemFileStore detailStems)
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
        return ServeFile(file, $"Track {trackId} has no stem '{stemName}'.");
    }

    /// <summary>Serves one detail stem of a track whose detail phase completed.</summary>
    /// <param name="key">Track, parent and detail stem name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file result or 404.</returns>
    public async Task<IResult> GetDetailAsync(DetailStemKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var track = await tracks.FindAsync(key.TrackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(key.TrackId);
        }

        var file = HasDetailStem(track, key) ? detailStems.Find(key) : null;
        return ServeFile(file, $"Track {key.TrackId} has no detail stem '{key.Parent}/{key.Name}'.");
    }

    /// <summary>Serves a FLAC file with Range, ETag and conditional semantics, or 404 with the given detail.</summary>
    /// <param name="file">The file, or null when missing.</param>
    /// <param name="missing">404 detail.</param>
    /// <returns>The result.</returns>
    public static IResult ServeFile(FileInfo? file, string missing) => file is null
        ? ApiProblems.NotFound(missing)
        : TypedResults.PhysicalFile(file.FullName, ContentType, lastModified: file.LastWriteTimeUtc, entityTag: EntityTag(file), enableRangeProcessing: true);

    private static bool HasStem(Track track, string stemName) =>
        track.Status == TrackStatus.Completed && track.Stems.Any(stem => stem.Name == stemName);

    private static bool HasDetailStem(Track track, DetailStemKey key) =>
        track.DetailPhase is { Status: DetailPhaseStatus.Completed } phase
        && phase.Parents.Any(parent => parent.Parent == key.Parent && parent.Stems.Any(stem => stem.Name == key.Name));

    private static EntityTagHeaderValue EntityTag(FileInfo file) =>
        new(string.Create(CultureInfo.InvariantCulture, $"\"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}\""));
}
