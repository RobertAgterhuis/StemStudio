using System.Globalization;
using StemStudio.Contracts.Insights;
using StemStudio.Web.Services;

namespace StemStudio.Web.Insights;

/// <summary>
/// Display texts of the stem quality report (feature 020, ui-test-ids 1.19.0, R20-1): rating words with an icon, the
/// accessible label, the Advanced bleed and artefact lines and the Expert indices. Every rating carries text and an
/// icon, never colour alone.
/// </summary>
public static class QualityText
{
    /// <summary>The rating word ("Fair", "Probably not in this song").</summary>
    /// <param name="rating">The rating.</param>
    /// <returns>The word.</returns>
    public static string Rating(QualityRatingDto rating) => rating switch
    {
        QualityRatingDto.Good => "Good",
        QualityRatingDto.Fair => "Fair",
        QualityRatingDto.Poor => "Poor",
        QualityRatingDto.NotPresent => "Probably not in this song",
        _ => "Not rated",
    };

    /// <summary>The Material icon name of a rating (shape differs per rating).</summary>
    /// <param name="rating">The rating.</param>
    /// <returns>The icon name.</returns>
    public static string Icon(QualityRatingDto rating) => rating switch
    {
        QualityRatingDto.Good => "check_circle",
        QualityRatingDto.Fair => "info",
        QualityRatingDto.Poor => "warning",
        QualityRatingDto.NotPresent => "block",
        _ => "help_outline",
    };

    /// <summary>"Vocals", "Kick (drums)", "Rest (drums)".</summary>
    /// <param name="channel">ChannelId.</param>
    /// <returns>The label.</returns>
    public static string Channel(string channel)
    {
        var dot = channel.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? Capital(channel) : $"{Capital(channel[(dot + 1)..])} ({channel[..dot]})";
    }

    /// <summary>The test-id key of a channel (<c>drums.kick</c> -> <c>drums-kick</c>, R20-3).</summary>
    /// <param name="channel">ChannelId.</param>
    /// <returns>The key.</returns>
    public static string Key(string channel) => channel.Replace('.', '-');

    /// <summary>Accessible label of a rating, e.g. "Vocals: fair, some sound of drums is in this stem".</summary>
    /// <param name="stem">The stem view.</param>
    /// <returns>The label (it contains the visible rating word, WCAG 2.5.3).</returns>
    public static string AriaLabel(StemQualityViewDto stem)
    {
        var reason = stem.Reason.Trim().TrimEnd('.');
        var detail = reason.Length == 0 ? string.Empty : $", {Lower(reason)}";
        return $"{Channel(stem.Channel)}: {Lower(Rating(stem.Rating))}{detail}";
    }

    /// <summary>"was Fair" when the previous rating differs; null otherwise.</summary>
    /// <param name="stem">The stem view.</param>
    /// <returns>The text or null.</returns>
    public static string? Previous(StemQualityViewDto stem) =>
        stem.PreviousRating is { } previous && previous != stem.Rating ? $"was {Rating(previous)}" : null;

    /// <summary>"Bleed: medium, mostly drums" (Advanced).</summary>
    /// <param name="bleed">The bleed estimate.</param>
    /// <returns>The line.</returns>
    public static string Bleed(BleedViewDto bleed)
    {
        var source = bleed.MainSource is { Length: > 0 } main ? $", mostly {main.Replace('.', ' ')}" : string.Empty;
        var together = bleed.CoOccurrence ? " (may be the two instruments playing together)" : string.Empty;
        return $"Bleed: {Level(bleed.Level)}{source}{together}";
    }

    /// <summary>"Artefacts: low" or "Artefacts: medium (flicker, dropouts)" (Advanced).</summary>
    /// <param name="artefacts">The artefact estimate.</param>
    /// <returns>The line.</returns>
    public static string Artefacts(ArtefactViewDto artefacts)
    {
        var kinds = artefacts.Kinds.Count == 0 ? string.Empty : $" ({string.Join(", ", artefacts.Kinds.Select(Kind))})";
        return $"Artefacts: {Level(artefacts.Level)}{kinds}";
    }

    /// <summary>"Bleed index 0.19 (validated), artefact index 0.20 (rough indication)" (Expert).</summary>
    /// <param name="stem">The stem view.</param>
    /// <returns>The line.</returns>
    public static string Indices(StemQualityViewDto stem) => string.Create(
        CultureInfo.InvariantCulture,
        $"Bleed index {stem.Bleed.Index:0.00} ({Reliability(stem.Bleed.Reliability)}), artefact index {stem.Artefacts.Index:0.00} ({Reliability(stem.Artefacts.Reliability)})");

    /// <summary>The reliability label.</summary>
    /// <param name="reliability">The reliability.</param>
    /// <returns>"validated" or "rough indication".</returns>
    public static string Reliability(ReliabilityDto reliability) => reliability == ReliabilityDto.Validated ? "validated" : "rough indication";

    /// <summary>The level word ("low", "medium", "high", "unknown").</summary>
    /// <param name="level">The level.</param>
    /// <returns>The word.</returns>
    public static string Level(QualityLevelDto level) => WireName.Of(level);

    /// <summary>"Analyzer 1.0.0, measured 16 Nov 2026 10:00 UTC" (Expert); null when no stems were measured.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The line or null.</returns>
    public static string? Analyzer(QualityReportDto report) => report is { AnalyzerVersion: { } version, MeasuredAt: { } at }
        ? string.Create(CultureInfo.InvariantCulture, $"Analyzer {version}, measured {at.UtcDateTime:d MMM yyyy HH:mm} UTC")
        : null;

    private static string Kind(ArtefactKindDto kind) => kind switch
    {
        ArtefactKindDto.Flicker => "flicker",
        ArtefactKindDto.Dropouts => "dropouts",
        ArtefactKindDto.ModelResidual => "left-over of the detail model",
        _ => "much went to the rest stem",
    };

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Lower(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];
}
