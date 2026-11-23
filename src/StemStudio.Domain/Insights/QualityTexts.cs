using System.Text.RegularExpressions;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Insights;

/// <summary>The fixed reason texts of the quality report (data-model 4.6, UI and API).</summary>
public static class QualityReasonTexts
{
    /// <summary>The source named when the worker gave none.</summary>
    public const string UnknownSource = "another instrument";

    /// <summary>The reason text of a stem's measures; <c>{source}</c> is the main bleed source in lower case.</summary>
    /// <param name="stem">The measures.</param>
    /// <returns>The text (at most 300 characters).</returns>
    public static string For(StemQuality stem)
    {
        ArgumentNullException.ThrowIfNull(stem);
        var source = stem.Bleed.MainSource?.ToLowerInvariant() ?? UnknownSource;
        return stem.Reason switch
        {
            QualityReason.Clean => "Little bleed and few artefacts.",
            QualityReason.Bleed when stem.Bleed.Level == QualityLevel.High => $"Much of {source} is in this stem.",
            QualityReason.Bleed => $"Some sound of {source} is in this stem.",
            QualityReason.BleedTogether => $"May be bleed of {source}, or the two instruments playing together.",
            QualityReason.Artefacts => "The stem has separation artefacts (short dropouts in the sound).",
            QualityReason.BleedAndArtefacts => $"Bleed of {source} and separation artefacts.",
            QualityReason.NotPresent => "This instrument is probably not in this song; what you hear is spill from other instruments.",
            _ => "Too little audio to rate.",
        };
    }
}

/// <summary>Checks the worker's quality result before it is stored (the worker is a separate process: its data is checked once).</summary>
public static partial class QualityResultRules
{
    /// <summary>Most stems in a result.</summary>
    public const int MaxStems = 64;

    /// <summary>Longest analyzer version.</summary>
    public const int MaxVersionLength = 20;

    /// <summary>Validates a result: version, stem count, known unique channels, standard main sources, finite indices in 0..1.</summary>
    /// <param name="analyzerVersion">The analyzer version.</param>
    /// <param name="stems">The measures.</param>
    /// <exception cref="DomainValidationException">A rule is broken.</exception>
    public static void Validate(string? analyzerVersion, IReadOnlyList<StemQuality> stems)
    {
        ArgumentNullException.ThrowIfNull(stems);
        var errors = new ValidationErrors();
        errors.Check(analyzerVersion is { Length: > 0 and <= MaxVersionLength } && VersionPattern().IsMatch(analyzerVersion), "analyzerVersion", "Must be a short version text.");
        errors.Check(stems.Count is > 0 and <= MaxStems, "stems", $"Must be 1 to {MaxStems} stems.");
        errors.Check(stems.Select(stem => stem.Channel).Distinct(StringComparer.Ordinal).Count() == stems.Count, "stems", "Every channel must appear once.");
        for (var index = 0; index < stems.Count; index++)
        {
            CheckStem(errors.Nested($"stems[{index}]"), stems[index]);
        }

        errors.ThrowIfAny();
    }

    private static void CheckStem(ValidationErrors errors, StemQuality stem)
    {
        errors.Check(Rendering.RenderChannel.Parse(stem.Channel) is not null, "channel", "Must be a stem or an allowed detail stem.");
        errors.Check(Enum.IsDefined(stem.Rating) && Enum.IsDefined(stem.Reason), "rating", "Must be a known rating and reason.");
        errors.Check(double.IsFinite(stem.ActiveSeconds) && stem.ActiveSeconds >= 0 && double.IsFinite(stem.LevelDb), "activeSeconds", "Must be finite.");
        errors.Check(IsUnit(stem.ActiveShare), "activeShare", "Must be between 0 and 1.");
        CheckBleed(errors.Nested("bleed"), stem.Bleed);
        CheckArtefacts(errors.Nested("artefacts"), stem.Artefacts);
    }

    private static void CheckBleed(ValidationErrors errors, BleedEstimate? bleed)
    {
        errors.Check(bleed is not null && IsUnit(bleed.Index), "index", "Must be between 0 and 1.");
        errors.Check(bleed?.MainSource is null || StemCatalog.IsKnown(bleed.MainSource), "mainSource", "Must be a standard stem.");
    }

    private static void CheckArtefacts(ValidationErrors errors, ArtefactEstimate? artefacts)
    {
        errors.Check(artefacts is not null && IsUnit(artefacts.Index), "index", "Must be between 0 and 1.");
        errors.Check(artefacts?.Kinds is { Count: <= 4 } kinds && kinds.Distinct().Count() == kinds.Count, "kinds", "Must be unique.");
    }

    private static bool IsUnit(double? value) => value is { } number && double.IsFinite(number) && number is >= 0 and <= 1;

    [GeneratedRegex(@"\A[0-9A-Za-z][0-9A-Za-z.+-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
