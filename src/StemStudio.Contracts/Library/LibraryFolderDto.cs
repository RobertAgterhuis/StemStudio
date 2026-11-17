namespace StemStudio.Contracts.Library;

/// <summary>An artist, album or song folder (backend-api 1.22.0 <c>LibraryFolder</c>); folders are derived from the items' placements.</summary>
public sealed record LibraryFolderDto
{
    /// <summary>Folder id.</summary>
    public required string Id { get; init; }

    /// <summary>Level.</summary>
    public required FolderLevelDto Level { get; init; }

    /// <summary>Display name (match spelling when any item was filed from a match, else the most used spelling).</summary>
    public required string Name { get; init; }

    /// <summary>Source of the displayed name.</summary>
    public required PlacementSourceDto NameSource { get; init; }

    /// <summary>Album year (album folders only); null otherwise.</summary>
    public int? Year { get; init; }

    /// <summary>Counts.</summary>
    public required FolderCountsDto Counts { get; init; }

    /// <summary>Total size of the items inside in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary><c>createdAt</c> of the newest item inside (UTC).</summary>
    public required DateTimeOffset LatestAt { get; init; }

    /// <summary>Folder art.</summary>
    public required FolderArtDto Art { get; init; }

    /// <summary>Song folders only: existing tracks whose items are inside (at most 10); empty otherwise.</summary>
    public IReadOnlyList<Guid> TrackIds { get; init; } = [];
}
