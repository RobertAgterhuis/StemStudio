namespace StemStudio.Contracts.Styles;

/// <summary>A style as it was when a generation was created (1.8.0).</summary>
public sealed record StyleSnapshotDto
{
    /// <summary>The style's id.</summary>
    public required Guid StyleId { get; init; }

    /// <summary>Preset or user style.</summary>
    public required StyleKindDto Kind { get; init; }

    /// <summary>Preset key; null for user styles.</summary>
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
}
