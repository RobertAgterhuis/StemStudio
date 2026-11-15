using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Scores;
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
public partial interface IStemStudioApiClient
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

    /// <summary><c>GET /api/device</c>: the worker's active processing device (backend contract 1.3.0).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The device; <c>reachable</c> is false when the API could not ask the worker.</returns>
    Task<ProcessingDeviceDto> GetDeviceAsync(CancellationToken cancellationToken = default);

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

    /// <summary><c>GET /api/tracks/{id}/band-info</c>: lookup state, recording, artist facts and sources (backend contract 1.4.0).</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The band information; facts only when the status is found.</returns>
    Task<BandInfoDto> GetBandInfoAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/metadata/lookup</c>: retry the lookup, or run it with corrected names (1.4.0).</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="request">Corrected names, or null to retry with the current ones.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track with the lookup scheduled (202); 404, 409 (searching) and 422 throw <see cref="ApiException"/>.</returns>
    Task<TrackDto> LookupMetadataAsync(Guid trackId, MetadataLookupRequest? request, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{id}/artwork</c>: sets the user's own picture (multipart field <c>file</c>, 1.4.0).</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="file">A JPEG, PNG or WebP picture of at most 10 MiB.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track with <c>artwork.source = user</c>; 400/413/415/422/503 throw <see cref="ApiException"/>.</returns>
    Task<TrackDto> SetArtworkAsync(Guid trackId, UploadFile file, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{id}/artwork</c>: back to the automatic picture (1.4.0).</summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The track with its automatic picture (or none).</returns>
    Task<TrackDto> ResetArtworkAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/assets?limit=200</c>: the newest library items (backend contract 1.5.0).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>One page of items, newest first, with the total count and size of all items.</returns>
    Task<AssetListDto> ListAssetsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/assets/{id}</c>.</summary>
    /// <param name="assetId">The item id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The item.</returns>
    Task<AssetDto> GetAssetAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/assets/audio-exports</c>: saves a stem, detail stem or mix as a library item.</summary>
    /// <param name="request">The export request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued item (202); 404, 409, 422 and 503 throw <see cref="ApiException"/>.</returns>
    Task<AssetDto> CreateAudioExportAsync(CreateAudioExportRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/assets/stem-packs</c>: one raw item per stem.</summary>
    /// <param name="request">The pack request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued items (202).</returns>
    Task<StemPackDto> CreateStemPackAsync(CreateStemPackRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH /api/assets/{id}</c>: renames an item (1..120 characters).</summary>
    /// <param name="assetId">The item id.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The renamed item.</returns>
    Task<AssetDto> RenameAssetAsync(Guid assetId, string name, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/assets/{id}</c>: deletes an item and its files.</summary>
    /// <param name="assetId">The item id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the item is deleted (204).</returns>
    Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/assets/{id}/retry</c>: renders a failed item again from its recipe.</summary>
    /// <param name="assetId">The failed item.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued item; 409 (not failed, source unavailable) and 503 throw <see cref="ApiException"/>.</returns>
    Task<AssetDto> RetryAssetAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/lyrics</c> (no-store): the lyrics; a completed track with status none starts its search.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics.</returns>
    Task<LyricsDto> GetLyricsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{id}/lyrics/offset</c>: stores the karaoke offset (-5000..5000 ms, steps of 10).</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="offsetMs">The offset; positive = the lyrics come later.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics; 409 without lyrics.</returns>
    Task<LyricsDto> SetLyricsOffsetAsync(Guid trackId, int offsetMs, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{id}/lyrics</c>: replaces the lyrics with text entered by the user; it is aligned next.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="request">The text (1..20,000 characters) and an optional language hint.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics (alignPending or aligning); 409 while searching or aligning, 422 for invalid text.</returns>
    Task<LyricsDto> SetLyricsAsync(Guid trackId, SetLyricsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/lyrics/align</c>: aligns the text to the vocal stem.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics (aligning); 409 without text or while busy, 503 when the worker is unavailable.</returns>
    Task<LyricsDto> AlignLyricsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/lyrics/align/cancel</c>: stops a running alignment.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics (alignPending, or found when line timing was being refined); 409 when nothing is aligning.</returns>
    Task<LyricsDto> CancelLyricsAlignmentAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/lyrics/search</c>: searches LRCLIB again.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics (searching); 409 while searching or aligning.</returns>
    Task<LyricsDto> SearchLyricsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{id}/lyrics</c>: removes the lyrics; no automatic lookup follows.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics (removed).</returns>
    Task<LyricsDto> RemoveLyricsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/scores/options</c>: profiles, defaults, estimates and limits for sheet music of one source.</summary>
    /// <param name="source">A channel of a completed track or a ready audio item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The options; 409 when the track is not ready, 422 for an unusable source.</returns>
    Task<ScoreOptionsDto> GetScoreOptionsAsync(AudioSourceDto source, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/assets/scores</c>: creates a score item; it is transcribed in the background.</summary>
    /// <param name="request">The source and the choices; null members take the defaults.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued item; 409, 422 or 503 as problem details.</returns>
    Task<AssetDto> CreateScoreAsync(CreateScoreRequest request, CancellationToken cancellationToken = default);
}
