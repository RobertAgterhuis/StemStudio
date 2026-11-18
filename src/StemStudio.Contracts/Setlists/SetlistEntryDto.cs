namespace StemStudio.Contracts.Setlists;

/// <summary>One song of a setlist with its facts (<c>SetlistEntry</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistEntryDto
{
    /// <summary>Entry id.</summary>
    public required Guid Id { get; init; }

    /// <summary>0-based position.</summary>
    public required int Position { get; init; }

    /// <summary>Track id.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Track title.</summary>
    public required string TrackTitle { get; init; }

    /// <summary>Artist, or null.</summary>
    public required string? Artist { get; init; }

    /// <summary>Mix, or null.</summary>
    public required Guid? MixId { get; init; }

    /// <summary>Mix name, or null.</summary>
    public required string? MixName { get; init; }

    /// <summary>Entry note.</summary>
    public required string Note { get; init; }

    /// <summary>Track length, or null.</summary>
    public required double? DurationSeconds { get; init; }

    /// <summary>Tempo, or null.</summary>
    public required double? Bpm { get; init; }

    /// <summary>Number of sections of the track.</summary>
    public required int SectionCount { get; init; }

    /// <summary>True when the track has lyrics.</summary>
    public required bool HasLyrics { get; init; }
}
