using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;

namespace StemStudio.Agent.Ports;

/// <summary>Arguments of GET /api/library/folders[/{folderId}] used by browse_library_folder (tool catalogue 1.1.0).</summary>
public sealed record LibraryFolderQuery
{
    /// <summary>The folder to open; null = the library root (artist folders).</summary>
    public string? FolderId { get; init; }

    /// <summary>Order of the child folders; null = the level's default.</summary>
    public FolderSortDto? Sort { get; init; }

    /// <summary>Only items of this kind.</summary>
    public AssetKindDto? Kind { get; init; }

    /// <summary>Child folders per page (the tool asks for 20).</summary>
    public int? Limit { get; init; }
}
