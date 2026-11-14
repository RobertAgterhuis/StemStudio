namespace StemStudio.Contracts;

/// <summary>Relative routes of the backend HTTP API (see backend-api.openapi.yaml).</summary>
public static class ApiRoutes
{
    /// <summary>Liveness endpoint.</summary>
    public const string Health = "/health";

    /// <summary>Import from a YouTube or Spotify URL (JSON body).</summary>
    public const string Imports = "/api/imports";

    /// <summary>Import from an uploaded audio file (multipart/form-data, field <c>file</c>).</summary>
    public const string ImportUpload = "/api/imports/upload";

    /// <summary>Track collection.</summary>
    public const string Tracks = "/api/tracks";

    /// <summary>Mix collection; supports the <c>trackId</c> query filter.</summary>
    public const string Mixes = "/api/mixes";

    /// <summary>Route of a single track.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string Track(Guid trackId) => $"{Tracks}/{trackId}";

    /// <summary>Route that retries a failed track.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string RetryTrack(Guid trackId) => $"{Track(trackId)}/retry";

    /// <summary>Route of one stem audio file (FLAC, supports HTTP Range).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="stemName">Stem name, see <see cref="StemNames"/>.</param>
    /// <returns>The relative URL.</returns>
    public static string Stem(Guid trackId, string stemName) => $"{Track(trackId)}/stems/{stemName}";

    /// <summary>Route of a single mix.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string Mix(Guid mixId) => $"{Mixes}/{mixId}";

    /// <summary>Route that duplicates a mix.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string DuplicateMix(Guid mixId) => $"{Mix(mixId)}/duplicate";
}
