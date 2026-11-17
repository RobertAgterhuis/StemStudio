namespace StemStudio.Contracts.LibraryCare;

/// <summary>Free and total space of one storage area (<c>Volume</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record StorageVolumeDto
{
    /// <summary>Volume id.</summary>
    public required StorageVolumeIdDto Id { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>Free bytes; null when unknown.</summary>
    public required long? FreeBytes { get; init; }

    /// <summary>Total bytes; null when unknown.</summary>
    public required long? TotalBytes { get; init; }

    /// <summary>Other volume ids on the same disk (same total and free bytes).</summary>
    public required IReadOnlyList<string> SharedWith { get; init; }

    /// <summary>Why the numbers are unknown.</summary>
    public required string? Reason { get; init; }
}
