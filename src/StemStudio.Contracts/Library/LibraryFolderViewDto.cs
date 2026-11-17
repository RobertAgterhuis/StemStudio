using StemStudio.Contracts.Assets;

namespace StemStudio.Contracts.Library;

/// <summary>Response of <c>GET /api/library/folders[/{folderId}]</c> (backend-api 1.22.0 <c>LibraryFolderView</c>).</summary>
public sealed record LibraryFolderViewDto
{
    /// <summary>Library revision (16 hex); the ETag is <c>"lib-&lt;revision&gt;"</c>.</summary>
    public required string Revision { get; init; }

    /// <summary>The open folder; null at the root.</summary>
    public LibraryFolderDto? Folder { get; init; }

    /// <summary>Parents from the artist down, without the folder itself and the root (0..3).</summary>
    public IReadOnlyList<FolderRefDto> Breadcrumb { get; init; } = [];

    /// <summary>Child folders of this page (empty for a song).</summary>
    public IReadOnlyList<LibraryFolderDto> Children { get; init; } = [];

    /// <summary>Child folders in total (paging).</summary>
    public required int ChildrenTotal { get; init; }

    /// <summary>Song level only: its items in instrument order, then newest first (at most 500).</summary>
    public IReadOnlyList<AssetDto> Items { get; init; } = [];

    /// <summary>Items in this folder (root: in the library).</summary>
    public required int TotalItems { get; init; }

    /// <summary>Their size in bytes.</summary>
    public required long TotalSizeBytes { get; init; }

    /// <summary>Items the start-up filing has not reached yet.</summary>
    public required int PendingFiling { get; init; }
}
