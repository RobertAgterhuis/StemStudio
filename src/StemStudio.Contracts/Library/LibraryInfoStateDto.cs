namespace StemStudio.Contracts.Library;

/// <summary>Response of the library info operations (backend-api 1.22.0 <c>LibraryInfoState</c>).</summary>
public sealed record LibraryInfoStateDto
{
    /// <summary>Artist level.</summary>
    public required LibraryInfoLevelDto Artist { get; init; }

    /// <summary>Album level.</summary>
    public required LibraryInfoLevelDto Album { get; init; }

    /// <summary>Song level.</summary>
    public required LibraryInfoLevelDto Song { get; init; }

    /// <summary>The item's placement (item scope); null for a track.</summary>
    public PlacementDto? Placement { get; init; }

    /// <summary>Items that changed folder (0 for GET).</summary>
    public required int MovedItems { get; init; }
}
