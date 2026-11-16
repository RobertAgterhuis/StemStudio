using StemStudio.Contracts.Styles;

namespace StemStudio.Web.Styles;

/// <summary>Readable names of the style fields (feature 009, data-model 3.1).</summary>
public static class StyleText
{
    private static readonly string[] StrengthLabels = ["Subtle", "Light", "Balanced", "Strong", "Free"];

    private static readonly string[] GenreLabels =
        ["Rock", "Pop", "Jazz", "Blues", "Electronic", "Hip hop", "Latin", "Folk", "Classical", "World", "Country", "Metal", "Soul", "Ambient"];

    private static readonly string[] TempoLabels = ["Follow the source", "Laid back", "Driving", "Half time", "Swing"];

    /// <summary>Every genre family in contract order.</summary>
    public static IReadOnlyList<GenreFamilyDto> Genres { get; } = Enum.GetValues<GenreFamilyDto>();

    /// <summary>Every tempo feel in contract order.</summary>
    public static IReadOnlyList<TempoFeelDto> Tempos { get; } = Enum.GetValues<TempoFeelDto>();

    /// <summary>Every strength in contract order.</summary>
    public static IReadOnlyList<StrengthDto> Strengths { get; } = Enum.GetValues<StrengthDto>();

    /// <summary>The name of a strength step ("Subtle" ... "Free").</summary>
    /// <param name="strength">The strength.</param>
    /// <returns>The name.</returns>
    public static string Strength(StrengthDto strength) => StrengthLabels[(int)strength];

    /// <summary>The name of a genre family, e.g. "Hip hop".</summary>
    /// <param name="genre">The family.</param>
    /// <returns>The name.</returns>
    public static string Genre(GenreFamilyDto genre) => GenreLabels[(int)genre];

    /// <summary>The name of a tempo feel, e.g. "Laid back".</summary>
    /// <param name="tempo">The feel.</param>
    /// <returns>The name.</returns>
    public static string Tempo(TempoFeelDto tempo) => TempoLabels[(int)tempo];

    /// <summary>What a style does with the vocals.</summary>
    /// <param name="vocals">The handling.</param>
    /// <returns>"Keeps the vocals" or "Instrumental (no vocals)".</returns>
    public static string Vocals(VocalHandlingDto vocals) => vocals == VocalHandlingDto.Instrumental ? "Instrumental (no vocals)" : "Keeps the vocals";
}
