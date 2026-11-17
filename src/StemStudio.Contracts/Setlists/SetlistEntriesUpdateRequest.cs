namespace StemStudio.Contracts.Setlists;

/// <summary>Body of <c>replaceSetlistEntries</c> (<c>SetlistEntriesUpdate</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistEntriesUpdateRequest
{
    /// <summary>The whole ordered list, 0..100 entries (duplicates allowed).</summary>
    public required IReadOnlyList<SetlistEntryInputDto> Entries { get; init; }
}
