namespace StemStudio.Domain.Styles;

/// <summary>The model parameters of a restyle (ADR-018 D7).</summary>
/// <param name="Caption">The caption (at most 512 characters).</param>
/// <param name="AudioCoverStrength">Cover strength 0-1 from the 5-step scale.</param>
/// <param name="InferenceSteps">Diffusion steps (turbo: 8).</param>
/// <param name="Shift">Scheduler shift (turbo: 3.0).</param>
public sealed record RestylePrompt(string Caption, double AudioCoverStrength, int InferenceSteps, double Shift);

/// <summary>
/// Deterministic caption builder (data-model 3.1): "{family} {sub-genre}, {tempo feel}, {instruments}, {moods}{,
/// instrumental}{, user description}", trimmed to 512 characters at a word boundary.
/// </summary>
public static class StylePromptBuilder
{
    /// <summary>Longest caption.</summary>
    public const int MaxCaptionLength = 512;

    /// <summary>Diffusion steps of the turbo model.</summary>
    public const int InferenceSteps = 8;

    /// <summary>Scheduler shift of the turbo model.</summary>
    public const double Shift = 3.0;

    /// <summary>Builds the caption and the parameters.</summary>
    /// <param name="style">The style snapshot.</param>
    /// <param name="strength">The chosen strength.</param>
    /// <returns>The prompt.</returns>
    public static RestylePrompt Build(StyleSnapshot style, Strength strength) =>
        new(Caption(style), CoverStrength(strength), InferenceSteps, Shift);

    /// <summary>The caption of a style.</summary>
    /// <param name="style">The style snapshot.</param>
    /// <returns>The caption.</returns>
    public static string Caption(StyleSnapshot style)
    {
        ArgumentNullException.ThrowIfNull(style);
        var parts = Parts(style.Fields).ToList();
        if (style.Kind == StyleKind.User && style.Fields.Description is { } description)
        {
            parts.Add(description);
        }

        return TrimToWords(string.Join(", ", parts), MaxCaptionLength);
    }

    /// <summary>The caption parts of the musical fields (without the user description), for 010's song captions too.</summary>
    /// <param name="fields">The style fields.</param>
    /// <returns>The non-empty parts in caption order.</returns>
    public static IEnumerable<string> Parts(StyleFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var parts = new List<string> { Head(fields), TempoPhrase(fields.TempoFeel), string.Join(", ", fields.Instrumentation), string.Join(", ", fields.Moods) };
        if (fields.VocalHandling == VocalHandling.Instrumental)
        {
            parts.Add("instrumental");
        }

        return parts.Where(part => part.Length > 0);
    }

    /// <summary>The cover strength of a step: 0.85 / 0.70 / 0.55 / 0.40 / 0.25.</summary>
    /// <param name="strength">The step.</param>
    /// <returns>The cover strength.</returns>
    public static double CoverStrength(Strength strength) => strength switch
    {
        Strength.Subtle => 0.85,
        Strength.Light => 0.70,
        Strength.Balanced => 0.55,
        Strength.Strong => 0.40,
        _ => 0.25,
    };

    /// <summary>The caption label of a genre family.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The label, e.g. "Hip hop".</returns>
    public static string FamilyLabel(GenreFamily family) => family == GenreFamily.HipHop ? "Hip hop" : family.ToString();

    /// <summary>Cuts a text to a limit at the last space that fits (or hard when there is none).</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The limit.</param>
    /// <returns>The trimmed text.</returns>
    public static string TrimToWords(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= maxLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', maxLength);
        return (cut > 0 ? text[..cut] : text[..maxLength]).TrimEnd(' ', ',');
    }

    private static string Head(StyleFields fields) =>
        fields.SubGenre is { } sub ? $"{FamilyLabel(fields.GenreFamily)} {sub}" : FamilyLabel(fields.GenreFamily);

    private static string TempoPhrase(TempoFeel feel) => feel switch
    {
        TempoFeel.Laidback => "laidback feel",
        TempoFeel.Driving => "driving feel",
        TempoFeel.HalfTime => "half-time feel",
        TempoFeel.Swing => "swing feel",
        _ => string.Empty,
    };
}
