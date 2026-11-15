using Microsoft.Net.Http.Headers;
using StemStudio.Api.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Artwork;

/// <summary>Serves the picture shown for a track (FR-006, FR-007; backend 1.4.0 <c>getTrackArtwork</c>).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="files">Picture store.</param>
public sealed class ArtworkService(ITrackRepository tracks, IArtworkFileStore files)
{
    /// <summary>Media type of every picture.</summary>
    public const string ContentType = "image/jpeg";

    /// <summary><c>Cache-Control</c> of a URL whose <c>v</c> matches the current picture.</summary>
    public const string ImmutableCache = "private, max-age=31536000, immutable";

    /// <summary><c>Cache-Control</c> of a URL without or with a stale <c>v</c>.</summary>
    public const string NoCache = "no-cache";

    /// <summary>Serves one variant with a strong <c>ETag</c> (the quoted version) and conditional GET (304).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="variant"><c>cover</c> or <c>thumb</c>.</param>
    /// <param name="version">The <c>v</c> query value, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file, or 404.</returns>
    public async Task<IResult> GetAsync(Guid trackId, string variant, string? version, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(trackId);
        }

        var artwork = track.Artwork;
        var file = artwork is null ? null : files.Find(trackId, SlotOf(artwork), variant);
        return file is null ? ApiProblems.NotFound($"Track {trackId} has no picture '{variant}'.") : Serve(file, artwork!, version);
    }

    /// <summary>The folder of the shown picture.</summary>
    /// <param name="artwork">The shown picture.</param>
    /// <returns>User for an own picture, else automatic.</returns>
    public static ArtworkSlot SlotOf(TrackArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        return artwork.Source == ArtworkSource.User ? ArtworkSlot.User : ArtworkSlot.Auto;
    }

    private static HeaderResult Serve(FileInfo file, TrackArtwork artwork, string? version)
    {
        var tag = new EntityTagHeaderValue($"\"{artwork.Version}\"");
        var result = TypedResults.PhysicalFile(file.FullName, ContentType, lastModified: file.LastWriteTimeUtc, entityTag: tag);
        return new HeaderResult(result, HeaderNames.CacheControl, version == artwork.Version ? ImmutableCache : NoCache);
    }
}
