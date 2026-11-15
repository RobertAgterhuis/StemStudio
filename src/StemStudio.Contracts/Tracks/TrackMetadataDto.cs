namespace StemStudio.Contracts.Tracks;

/// <summary>Lookup state and the main facts of a track (backend contract 1.4.0); always present on the wire.</summary>
public sealed record TrackMetadataDto
{
    /// <summary>The metadata of a track that was never looked up.</summary>
    public static TrackMetadataDto None { get; } = new() { Status = MetadataStatusDto.None };

    /// <summary>Lookup state.</summary>
    public required MetadataStatusDto Status { get; init; }

    /// <summary>Failure reason or warnings of the last lookup (&lt;= 1000 characters).</summary>
    public string? Reason { get; init; }

    /// <summary>Album of the matched recording.</summary>
    public string? Album { get; init; }

    /// <summary>First release year of the album.</summary>
    public int? Year { get; init; }

    /// <summary>Name of the matched artist.</summary>
    public string? ArtistName { get; init; }

    /// <summary>True when the user corrected title or artist for the lookup.</summary>
    public bool CorrectedByUser { get; init; }
}
