namespace StemStudio.Contracts.Library;

/// <summary>A folder in a breadcrumb or a path (backend-api 1.22.0 <c>FolderRef</c>).</summary>
public sealed record FolderRefDto
{
    /// <summary>Folder id (pattern <see cref="ContractLimits.FolderIdPattern"/>).</summary>
    public required string Id { get; init; }

    /// <summary>Level of the folder.</summary>
    public required FolderLevelDto Level { get; init; }

    /// <summary>Display name (1..200 characters).</summary>
    public required string Name { get; init; }
}
