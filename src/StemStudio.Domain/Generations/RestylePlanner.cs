using System.Globalization;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Generations;

/// <summary>A restyle request as the user sent it.</summary>
/// <param name="Mode">Range or in place.</param>
/// <param name="Start">Range start (default 0).</param>
/// <param name="End">Range end (default min(duration, start + 480)).</param>
/// <param name="IncludedStems">Stems (default all of the track, minus vocals for an instrumental style).</param>
/// <param name="Strength">Strength (default the style's).</param>
/// <param name="Takes">1 or 2.</param>
public sealed record RestyleOptions(RestyleMode Mode, double? Start, double? End, IReadOnlyList<string>? IncludedStems, Strength? Strength, int Takes);

/// <summary>What the source track offers.</summary>
/// <param name="DurationSeconds">Track duration.</param>
/// <param name="Stems">The track's stems.</param>
public sealed record RestyleSource(double DurationSeconds, IReadOnlyList<string> Stems);

/// <summary>The resolved restyle.</summary>
/// <param name="Range">The range.</param>
/// <param name="Stems">The stems in catalog order.</param>
/// <param name="Strength">The strength.</param>
/// <param name="Takes">Number of takes.</param>
public sealed record RestylePlan(SourceRange Range, IReadOnlyList<string> Stems, Strength Strength, int Takes);

/// <summary>Restyle request rules: range 10-480 s inside the track, in place only for tracks up to 480 s, stems a subset of the track's, 1-2 takes.</summary>
public static class RestylePlanner
{
    /// <summary>Shortest range.</summary>
    public const double MinSeconds = 10;

    /// <summary>Longest range (and longest track for in place).</summary>
    public const double MaxSeconds = 480;

    private const double Tolerance = 0.01;

    /// <summary>Resolves the defaults and checks the rules.</summary>
    /// <param name="options">The request.</param>
    /// <param name="source">The track.</param>
    /// <param name="style">The chosen style.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="DomainValidationException">A rule is broken (fields as in the request).</exception>
    public static RestylePlan Plan(RestyleOptions options, RestyleSource source, StyleFields style)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        var errors = new ValidationErrors();
        var range = Range(options, source.DurationSeconds, errors);
        var stems = Stems(options.IncludedStems, source.Stems, style.VocalHandling, errors);
        errors.Check(options.Takes is >= 1 and <= Generation.MaxTakes, "takes", "Choose 1 or 2 takes.");
        errors.ThrowIfAny();
        return new RestylePlan(range, stems, options.Strength ?? style.DefaultStrength, options.Takes);
    }

    private static SourceRange Range(RestyleOptions options, double duration, ValidationErrors errors)
    {
        var start = options.Start ?? 0;
        var end = options.End ?? Math.Min(duration, start + MaxSeconds);
        errors.Check(start >= 0, "rangeStartSeconds", "The start must be 0 or later.");
        errors.Check(end - start is >= MinSeconds and <= MaxSeconds, "rangeEndSeconds", "The range must be 10 to 480 seconds long.");
        errors.Check(end <= duration + Tolerance, "rangeEndSeconds", string.Create(CultureInfo.InvariantCulture, $"The range must end inside the track ({duration:0.##} s)."));
        errors.Check(options.Mode == RestyleMode.Range || duration <= MaxSeconds, "mode", "In-place restyles need a track of at most 480 seconds; restyle a range instead.");
        return new SourceRange(start, end, options.Mode);
    }

    private static List<string> Stems(IReadOnlyList<string>? requested, IReadOnlyList<string> available, VocalHandling vocals, ValidationErrors errors)
    {
        var defaults = available.Where(stem => vocals == VocalHandling.Keep || stem != "vocals");
        var stems = (requested ?? defaults.ToList()).ToList();
        var valid = stems.Count > 0 && stems.Distinct(StringComparer.Ordinal).Count() == stems.Count && stems.All(available.Contains);
        errors.Check(valid, "includedStems", "Choose one or more different stems of this track.");
        return stems.OrderBy(StemCatalog.Order).ToList();
    }
}
