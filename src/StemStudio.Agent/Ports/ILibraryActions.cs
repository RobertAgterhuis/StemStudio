using StemStudio.Contracts.Library;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Library folders, live search and library info of the public API (feature 022, backend-api 1.22.0, tool catalogue 1.1.0) over
/// loopback. Folder and item names in the results are imported metadata or user text: the tools pass them to the model only as
/// <c>untrusted_text</c>.
/// </summary>
public interface ILibraryActions
{
    /// <summary>GET /api/library/folders or GET /api/library/folders/{folderId} (browse_library_folder).</summary>
    /// <param name="query">Folder, sort, kind and page size.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The folder view.</returns>
    Task<LibraryFolderViewDto> BrowseLibraryFolderAsync(LibraryFolderQuery query, CancellationToken cancellationToken);

    /// <summary>GET /api/library/search (search_library_items).</summary>
    /// <param name="query">Text, scope and hits per group.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The grouped hits.</returns>
    Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchQuery query, CancellationToken cancellationToken);

    /// <summary>GET /api/tracks/{trackId}/library-info or /api/assets/{assetId}/library-info (set_library_info reads before it writes; the inverse).</summary>
    /// <param name="target">Track or item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The current state.</returns>
    Task<LibraryInfoStateDto> GetLibraryInfoAsync(LibraryInfoTarget target, CancellationToken cancellationToken);

    /// <summary>PUT /api/tracks/{trackId}/library-info or /api/assets/{assetId}/library-info (set_library_info and its inverse).</summary>
    /// <param name="target">Track or item.</param>
    /// <param name="request">The complete request (all three levels; null = automatic).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The new state with the number of moved items.</returns>
    Task<LibraryInfoStateDto> SetLibraryInfoAsync(LibraryInfoTarget target, LibraryInfoRequest request, CancellationToken cancellationToken);
}
