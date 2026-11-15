using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Api;

/// <summary>A file to upload, streamed from its source (never buffered in memory).</summary>
/// <param name="Content">The readable source stream (e.g. <c>IBrowserFile.OpenReadStream</c>).</param>
/// <param name="FileName">The original file name including extension.</param>
/// <param name="ContentType">The MIME type reported by the browser.</param>
public sealed record UploadFile(Stream Content, string FileName, string ContentType);

/// <summary>
/// Typed client of the StemStudio backend API (<c>backend-api.openapi.yaml</c>), used server-side.
/// Every method throws <see cref="ApiException"/> on a non-success response or transport failure.
/// </summary>
public interface IStemStudioApiClient
{
    /// <summary><c>POST /api/imports</c>: imports a YouTube or Spotify URL.</summary>
    /// <param name="url">The URL entered by the user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new (202) or cached (200) track.</returns>
    Task<TrackDto> ImportUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/imports/upload</c>: uploads a local audio file as a streamed multipart body.</summary>
    /// <param name="file">The file to upload.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new (202) or cached (200) track.</returns>
    Task<TrackDto> ImportUploadAsync(UploadFile file, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>All tracks, most recently updated first.</returns>
    Task<IReadOnlyList<TrackDto>> ListTracksAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}</c>.</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track with status, progress, stems and beat grid.</returns>
    Task<TrackDto> GetTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/retry</c>.</summary>
    /// <param name="trackId">The failed track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track, back in status queued.</returns>
    Task<TrackDto> RetryTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{id}</c>: deletes a failed or completed track with its stems and mixes.</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the track is deleted (204); 404 and 409 throw <see cref="ApiException"/>.</returns>
    Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/detail</c>: queues the detail phase ("Split further", or retry after failed/cancelled).</summary>
    /// <param name="trackId">A completed track whose detail status is none, failed or cancelled.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track with the detail phase back in status queued (202); 404 and 409 throw <see cref="ApiException"/>.</returns>
    Task<TrackDto> StartDetailAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/detail/cancel</c>: cancels a queued or running detail phase.</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track with the cancelled detail phase (200); 404, 409 and 503 throw <see cref="ApiException"/>.</returns>
    Task<TrackDto> CancelDetailAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/mixes[?trackId=]</c>.</summary>
    /// <param name="trackId">Optional track filter.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Mix library entries, most recently updated first.</returns>
    Task<IReadOnlyList<MixSummaryDto>> ListMixesAsync(Guid? trackId = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/mixes/{id}</c>.</summary>
    /// <param name="mixId">The mix id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The mix.</returns>
    Task<MixDto> GetMixAsync(Guid mixId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/mixes</c>: save a new mix ("save as").</summary>
    /// <param name="request">Track and settings.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created mix.</returns>
    Task<MixDto> CreateMixAsync(CreateMixRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/mixes/{id}</c>: replace name and settings.</summary>
    /// <param name="mixId">The mix id.</param>
    /// <param name="settings">The new settings.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated mix.</returns>
    Task<MixDto> UpdateMixAsync(Guid mixId, MixSettingsDto settings, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/mixes/{id}</c>.</summary>
    /// <param name="mixId">The mix id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the mix is deleted.</returns>
    Task DeleteMixAsync(Guid mixId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/mixes/{id}/duplicate</c>.</summary>
    /// <param name="mixId">The mix to copy.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The copy, named "&lt;name&gt; (copy)".</returns>
    Task<MixDto> DuplicateMixAsync(Guid mixId, CancellationToken cancellationToken = default);
}
