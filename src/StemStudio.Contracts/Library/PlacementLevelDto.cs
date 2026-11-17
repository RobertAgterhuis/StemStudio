namespace StemStudio.Contracts.Library;

/// <summary>One level of a placement (backend-api 1.22.0 <c>PlacementLevel</c>).</summary>
public sealed record PlacementLevelDto
{
    /// <summary>Folder id of this level.</summary>
    public required string FolderId { get; init; }

    /// <summary>The name the item is filed under (the folder may display another spelling of the same key).</summary>
    public required string Name { get; init; }

    /// <summary>Where the name comes from.</summary>
    public required PlacementSourceDto Source { get; init; }
}
