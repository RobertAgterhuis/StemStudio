using StemStudio.Contracts.Library;
using StemStudio.Web.Library;

namespace StemStudio.Web.Api;

/// <summary>Library folders, live search and library info (feature 022, backend contract 1.22.0, tag <c>library</c>).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/library/folders</c>: the artist folders.</summary>
    /// <param name="request">Order, filters and paging.</param>
    /// <param name="knownRevision">The revision of the view already shown; sent as <c>If-None-Match</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The view, or null when it is unchanged (304); 422 throws <see cref="ApiException"/>.</returns>
    Task<LibraryFolderViewDto?> GetLibraryRootAsync(LibraryFolderRequest request, string? knownRevision = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/library/folders/{folderId}</c>: an artist, album or song folder.</summary>
    /// <param name="folderId">The folder id (<c>ar|al|so</c> + 16 hex).</param>
    /// <param name="request">Order, filters and paging.</param>
    /// <param name="knownRevision">The revision of the view already shown; sent as <c>If-None-Match</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The view, or null when it is unchanged (304); 404 <c>folder-not-found</c> and 422 throw <see cref="ApiException"/>.</returns>
    Task<LibraryFolderViewDto?> GetLibraryFolderAsync(string folderId, LibraryFolderRequest request, string? knownRevision = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/library/search</c>: grouped hits with paths and highlights.</summary>
    /// <param name="request">The text, the optional folder and the group size.</param>
    /// <param name="cancellationToken">Cancels the request (a newer text replaces it).</param>
    /// <returns>The result; 404 (unknown <c>within</c>) and 422 throw <see cref="ApiException"/>.</returns>
    Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{trackId}/library-info</c>.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The names per level with their source; 404 throws <see cref="ApiException"/>.</returns>
    Task<LibraryInfoStateDto> GetTrackLibraryInfoAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{trackId}/library-info</c>: re-files every item of the track without own names.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="request">The names (null = automatic).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new state; 400 (field messages) and 404 throw <see cref="ApiException"/>.</returns>
    Task<LibraryInfoStateDto> SetTrackLibraryInfoAsync(Guid trackId, LibraryInfoRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{trackId}/library-info</c>: every level automatic again.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new state; 404 throws <see cref="ApiException"/>.</returns>
    Task<LibraryInfoStateDto> ResetTrackLibraryInfoAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/assets/{assetId}/library-info</c>.</summary>
    /// <param name="assetId">The item.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The names per level with their source and the placement; 404 throws <see cref="ApiException"/>.</returns>
    Task<LibraryInfoStateDto> GetAssetLibraryInfoAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/assets/{assetId}/library-info</c>: moves only this item.</summary>
    /// <param name="assetId">The item.</param>
    /// <param name="request">The names (null = the track's or automatic name).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new state; 400, 404 and 409 <c>not-fileable</c> throw <see cref="ApiException"/>.</returns>
    Task<LibraryInfoStateDto> SetAssetLibraryInfoAsync(Guid assetId, LibraryInfoRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/assets/{assetId}/library-info</c>.</summary>
    /// <param name="assetId">The item.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new state; 404 and 409 throw <see cref="ApiException"/>.</returns>
    Task<LibraryInfoStateDto> ResetAssetLibraryInfoAsync(Guid assetId, CancellationToken cancellationToken = default);
}
