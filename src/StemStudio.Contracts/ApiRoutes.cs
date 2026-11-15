namespace StemStudio.Contracts;

/// <summary>Relative routes of the backend HTTP API (see backend-api.openapi.yaml).</summary>
public static class ApiRoutes
{
    /// <summary>Liveness endpoint.</summary>
    public const string Health = "/health";

    /// <summary>The worker's active processing device (contract 1.3.0).</summary>
    public const string Device = "/api/device";

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

    /// <summary>Start, "Split further" or retry the detail phase: <c>POST /api/tracks/{id}/detail</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string StartDetail(Guid trackId) => $"{Track(trackId)}/detail";

    /// <summary>Cancel the detail phase: <c>POST /api/tracks/{id}/detail/cancel</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string CancelDetail(Guid trackId) => $"{StartDetail(trackId)}/cancel";

    /// <summary>One detail stem file: <c>GET /api/tracks/{id}/detail-stems/{parent}/{name}</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="parent">Parent stem name.</param>
    /// <param name="name">Detail stem name.</param>
    /// <returns>The route.</returns>
    public static string DetailStem(Guid trackId, string parent, string name) => $"{Track(trackId)}/detail-stems/{parent}/{name}";

    /// <summary>Route of a single mix.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string Mix(Guid mixId) => $"{Mixes}/{mixId}";

    /// <summary>Route that duplicates a mix.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string DuplicateMix(Guid mixId) => $"{Mix(mixId)}/duplicate";
}
