using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.Assets;

/// <summary>Where a library item came from; snapshots survive deletion of the track.</summary>
public sealed record ProvenanceDto
{
    /// <summary>How the item was made.</summary>
    public required AssetOriginDto Origin { get; init; }

    /// <summary>The source track, when the item was made from one.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Track title at creation (&lt;= 300 characters).</summary>
    public required string TrackTitle { get; init; }

    /// <summary>Track artist at creation.</summary>
    public string? TrackArtist { get; init; }

    /// <summary>Whether the track still exists (computed on read).</summary>
    public required bool TrackAvailable { get; init; }

    /// <summary>Library items this item was made from (empty for exports).</summary>
    public IReadOnlyList<Guid> SourceAssetIds { get; init; } = [];

    /// <summary>Short human text, e.g. "drums, raw" (&lt;= 200 characters).</summary>
    public required string Contents { get; init; }

    /// <summary>How an export was made (origin export); null for score items (1.7.0).</summary>
    public ExportRecipeDto? Recipe { get; init; }

    /// <summary>How a score item was made (origin transcription, 1.7.0); null for exports.</summary>
    public ScoreRecipeDto? ScoreRecipe { get; init; }
}
