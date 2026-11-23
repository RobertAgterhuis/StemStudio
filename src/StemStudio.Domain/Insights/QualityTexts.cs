#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    private static void CheckStem(ValidationErrors errors, StemQuality stem)
    {
        errors.Check(Rendering.RenderChannel.Parse(stem.Channel) is not null, "channel", "Must be a stem or an allowed detail stem.");
        errors.Check(stem.Bleed?.MainSource is null || StemCatalog.IsKnown(stem.Bleed.MainSource), "bleed.mainSource", "Must be a standard stem.");
        errors.Check(IsUnit(stem.Bleed?.Index) && IsUnit(stem.Artefacts?.Index) && IsUnit(stem.ActiveShare), "index", "Must be between 0 and 1.");
        errors.Check(double.IsFinite(stem.ActiveSeconds) && stem.ActiveSeconds >= 0 && double.IsFinite(stem.LevelDb), "activeSeconds", "Must be finite.");
        errors.Check(stem.Artefacts?.Kinds is { Count: <= 4 } kinds && kinds.Distinct().Count() == kinds.Count, "artefacts.kinds", "Must be unique.");
        errors.Check(Enum.IsDefined(stem.Rating) && Enum.IsDefined(stem.Reason), "rating", "Must be a known rating and reason.");
    }

    private static bool IsUnit(double? value) => value is { } number && double.IsFinite(number) && number is >= 0 and <= 1;

    [GeneratedRegex(@"\A[0-9A-Za-z][0-9A-Za-z.+-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
