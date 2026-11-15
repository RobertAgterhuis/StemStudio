namespace StemStudio.Contracts.Styles;

/// <summary>A style of the library (1.8.0).</summary>
public sealed record StyleDto
{
    /// <summary>Style id (stable for presets).</summary>
    public required Guid Id { get; init; }

    /// <summary>Preset or user style.</summary>
    public required StyleKindDto Kind { get; init; }

    /// <summary>Stable key of a preset, e.g. <c>jazz-trio</c>; null for user styles.</summary>
    public string? PresetKey { get; init; }

    /// <summary>Display name, 1-60 characters, unique across the library (case-insensitive).</summary>
    public required string Name { get; init; }

    /// <summary>Genre family.</summary>
    public required GenreFamilyDto GenreFamily { get; init; }

    /// <summary>Sub-genre, at most 40 characters.</summary>
    public string? SubGenre { get; init; }

    /// <summary>Tempo feel.</summary>
    public required TempoFeelDto TempoFeel { get; init; }

    /// <summary>Instrument tags (0-8, each 1-30 characters).</summary>
    public IReadOnlyList<string> Instrumentation { get; init; } = [];

    /// <summary>Mood tags (0-8, each 1-30 characters).</summary>
    public IReadOnlyList<string> Moods { get; init; } = [];

    /// <summary>What happens with the vocals.</summary>
    public required VocalHandlingDto VocalHandling { get; init; }

    /// <summary>Default strength of restyles with this style.</summary>
    public required StrengthDto DefaultStrength { get; init; }

    /// <summary>Description, at most 300 characters; part of the caption for user styles only.</summary>
    public string? Description { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
