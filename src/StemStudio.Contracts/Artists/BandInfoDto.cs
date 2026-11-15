using StemStudio.Contracts.Tracks;

namespace StemStudio.Contracts.Artists;

/// <summary>Body of <c>GET /api/tracks/{id}/band-info</c> (backend contract 1.4.0).</summary>
public sealed record BandInfoDto
{
    /// <summary>Lookup state (same as <c>Track.metadata.status</c>).</summary>
    public required MetadataStatusDto Status { get; init; }

    /// <summary>Failure reason or warnings.</summary>
    public string? Reason { get; init; }

    /// <summary>The matched recording; set when found.</summary>
    public RecordingInfoDto? Recording { get; init; }

    /// <summary>Artist facts; set when found.</summary>
    public ArtistInfoDto? Artist { get; init; }

    /// <summary>Where the facts came from (at most 4).</summary>
    public IReadOnlyList<DataSourceDto> Sources { get; init; } = [];
}
