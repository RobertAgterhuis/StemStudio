namespace StemStudio.Domain.Styles;

/// <summary>The editable fields of a style (data-model 3.1); equality compares the tag lists by value.</summary>
public sealed record StyleFields
{
    /// <summary>Name (1-60, unique case-insensitively).</summary>
    public required string Name { get; init; }

    /// <summary>Genre family.</summary>
    public required GenreFamily GenreFamily { get; init; }

    /// <summary>Optional sub-genre (at most 40).</summary>
    public string? SubGenre { get; init; }

    /// <summary>Tempo feel.</summary>
    public required TempoFeel TempoFeel { get; init; }

    /// <summary>Instrument tags (0-8, each 1-30).</summary>
    public IReadOnlyList<string> Instrumentation { get; init; } = [];

    /// <summary>Mood tags (0-8, each 1-30).</summary>
    public IReadOnlyList<string> Moods { get; init; } = [];

    /// <summary>Vocal handling.</summary>
    public required VocalHandling VocalHandling { get; init; }

    /// <summary>Default strength.</summary>
    public required Strength DefaultStrength { get; init; }

    /// <summary>Optional description (at most 300).</summary>
    public string? Description { get; init; }

    /// <summary>Trims the texts, turns blank optional texts into null and drops blank or repeated tags.</summary>
    /// <returns>The normalised fields.</returns>
    public StyleFields Normalized() => this with
    {
        Name = (Name ?? string.Empty).Trim(),
        SubGenre = Blank(SubGenre),
        Description = Blank(Description),
        Instrumentation = Tags(Instrumentation),
        Moods = Tags(Moods),
    };

    /// <inheritdoc />
    public bool Equals(StyleFields? other) =>
        other is not null
        && (Name, GenreFamily, SubGenre, TempoFeel, VocalHandling, DefaultStrength, Description)
            == (other.Name, other.GenreFamily, other.SubGenre, other.TempoFeel, other.VocalHandling, other.DefaultStrength, other.Description)
        && Instrumentation.SequenceEqual(other.Instrumentation, StringComparer.Ordinal)
        && Moods.SequenceEqual(other.Moods, StringComparer.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, GenreFamily, SubGenre, TempoFeel, Instrumentation.Count, Moods.Count, Description);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> Tags(IReadOnlyList<string>? tags) =>
        (tags ?? []).Select(tag => (tag ?? string.Empty).Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
