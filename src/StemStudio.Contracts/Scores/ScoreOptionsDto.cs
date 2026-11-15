using StemStudio.Contracts.Assets;

namespace StemStudio.Contracts.Scores;

/// <summary>Body of <c>GET /api/scores/options</c> (backend contract 1.7.0, data-model.md 4.5).</summary>
public sealed record ScoreOptionsDto
{
    /// <summary>The source the options are for.</summary>
    public required AudioSourceDto Source { get; init; }

    /// <summary>Duration of the source in seconds.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Suggested profile; null when the channel has none (single drum pieces).</summary>
    public ScoreProfileDto? SuggestedProfile { get; init; }

    /// <summary>Profiles that may be chosen.</summary>
    public IReadOnlyList<ScoreProfileDto> AllowedProfiles { get; init; } = [];

    /// <summary>True for the drums channel while the drums are not split.</summary>
    public bool DrumsRequireSplit { get; init; }

    /// <summary>Whether the beat grid can be used.</summary>
    public required GridStatusDto GridStatus { get; init; }

    /// <summary>Default piano quality on this worker.</summary>
    public required ScoreQualityDto DefaultQuality { get; init; }

    /// <summary>Estimates per allowed profile (two for piano).</summary>
    public IReadOnlyList<ScoreEstimateDto> Estimates { get; init; } = [];

    /// <summary>Quality notes per allowed profile.</summary>
    public IReadOnlyList<ProfileLimitDto> Limits { get; init; } = [];

    /// <summary>Credit of the piano model (CC BY 4.0).</summary>
    public required string Attribution { get; init; }
}
