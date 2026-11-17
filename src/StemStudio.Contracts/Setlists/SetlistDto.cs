namespace StemStudio.Contracts.Setlists;

/// <summary>A setlist with its entries (<c>Setlist</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistDto
{
    /// <summary>Setlist id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Name.</summary>
    public required string Name { get; init; }

    /// <summary>Note.</summary>
    public required string Note { get; init; }

    /// <summary>Entries in order.</summary>
    public required IReadOnlyList<SetlistEntryDto> Entries { get; init; }

    /// <summary>Sum of the entries' lengths.</summary>
    public required double TotalSeconds { get; init; }

    /// <summary>Entries removed by track deletes since the last acknowledgement.</summary>
    public required int RemovedEntries { get; init; }

    /// <summary>Created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
