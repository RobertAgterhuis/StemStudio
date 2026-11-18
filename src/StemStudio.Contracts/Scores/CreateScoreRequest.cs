using StemStudio.Contracts.Assets;

namespace StemStudio.Contracts.Scores;

/// <summary>Body of <c>POST /api/assets/scores</c> (backend contract 1.7.0); null members take the defaults of data-model.md 4.2.</summary>
public sealed record CreateScoreRequest : ScoreNotationOptions
{
    /// <summary>A channel of a completed track or a ready audio item.</summary>
    public required AudioSourceDto Source { get; init; }

    /// <summary>Profile; null = the suggested profile of the channel.</summary>
    public ScoreProfileDto? Profile { get; init; }

    /// <summary>Piano quality; null = accurate on a GPU worker, else fast.</summary>
    public ScoreQualityDto? Quality { get; init; }

    /// <summary>Resolution; null = the profile default.</summary>
    public ScoreResolutionDto? Resolution { get; init; }

    /// <summary>One of <see cref="ContractLimits.KeyNames"/>, or null = estimate.</summary>
    public string? KeyOverride { get; init; }

    /// <summary>Manual tempo; required when the grid is unreliable or absent.</summary>
    public ScoreTempoDto? Tempo { get; init; }

    /// <summary>Part of the source, at least <see cref="ContractLimits.MinScoreRangeSeconds"/> long; null = all.</summary>
    public RangeDto? Range { get; init; }

    /// <summary>Item name (1..120); null = "&lt;track title&gt; - &lt;channel&gt; (sheet music)".</summary>
    public string? Name { get; init; }
}
