using StemStudio.Contracts.Assets;

namespace StemStudio.Contracts.Scores;

/// <summary>The normalised request of a score item (defaults resolved), stored with the item (backend contract 1.7.0).</summary>
public sealed record ScoreRecipeDto : ScoreNotationOptions
{
    /// <summary>Wire discriminator; always <c>score</c>.</summary>
    public string Kind { get; init; } = "score";

    /// <summary>The source.</summary>
    public required AudioSourceDto Source { get; init; }

    /// <summary>The profile used.</summary>
    public required ScoreProfileDto Profile { get; init; }

    /// <summary>Piano quality; null for other profiles.</summary>
    public ScoreQualityDto? Quality { get; init; }

    /// <summary>The resolution used.</summary>
    public required ScoreResolutionDto Resolution { get; init; }

    /// <summary>The key override, or null.</summary>
    public string? KeyOverride { get; init; }

    /// <summary>The manual tempo, or null when the beat grid was used.</summary>
    public ScoreTempoDto? Tempo { get; init; }

    /// <summary>The range, or null for the whole source.</summary>
    public RangeDto? Range { get; init; }
}
