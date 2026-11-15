using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.Assets;

/// <summary>A library item (backend contract 1.5.0; score items 1.7.0).</summary>
public sealed record AssetDto
{
    /// <summary>Item id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Kind of item.</summary>
    public required AssetKindDto Kind { get; init; }

    /// <summary>Display name (1..120 characters; not unique).</summary>
    public required string Name { get; init; }

    /// <summary>Status.</summary>
    public required AssetStatusDto Status { get; init; }

    /// <summary>Progress 0..100.</summary>
    public required int Progress { get; init; }

    /// <summary>Readable reason; set iff <see cref="Status"/> is failed.</summary>
    public string? Error { get; init; }

    /// <summary>Files; empty until ready.</summary>
    public IReadOnlyList<AssetFileDto> Files { get; init; } = [];

    /// <summary>Audio facts of a ready audio item.</summary>
    public AudioFactsDto? Audio { get; init; }

    /// <summary>Score facts of a ready score item (1.7.0); null otherwise.</summary>
    public ScoreFactsDto? Score { get; init; }

    /// <summary>Where the item came from.</summary>
    public required ProvenanceDto Provenance { get; init; }

    /// <summary>Total size of the files in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
