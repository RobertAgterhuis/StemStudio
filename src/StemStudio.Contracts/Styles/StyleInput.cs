namespace StemStudio.Contracts.Styles;

/// <summary>Body of <c>POST /api/styles</c> and <c>PUT /api/styles/{styleId}</c> (1.8.0); every text passes the imitation guard.</summary>
public sealed record StyleInput
{
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
