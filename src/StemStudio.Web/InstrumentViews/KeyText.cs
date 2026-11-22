using System.Globalization;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.InstrumentViews;

/// <summary>Readable song keys (016 data-model 2.8): names spelled like key signatures, confidence and region texts.</summary>
public static class KeyText
{
    /// <summary>Confidence below this marks a key "uncertain".</summary>
    public const double UncertainBelow = 0.5;

    private static readonly string[] Major = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] Minor = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B"];

    /// <summary>The name of a key: "Db major", "C# minor".</summary>
    /// <param name="tonic">Pitch class 0..11.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The name.</returns>
    public static string Name(int tonic, KeyModeDto mode)
    {
        var index = ((tonic % 12) + 12) % 12;
        return mode == KeyModeDto.Minor ? $"{Minor[index]} minor" : $"{Major[index]} major";
    }

    /// <summary>The wire name of a mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><c>major</c> or <c>minor</c>.</returns>
    public static string ModeName(KeyModeDto mode) => mode == KeyModeDto.Minor ? "minor" : "major";

    /// <summary>True when the confidence is low.</summary>
    /// <param name="confidence">0..1.</param>
    /// <returns><c>true</c>/<c>false</c> as text for <c>data-uncertain</c>.</returns>
    public static string Uncertain(double confidence) => confidence < UncertainBelow ? "true" : "false";

    /// <summary>The display with "(uncertain)" for a low confidence, unless the backend already added it.</summary>
    /// <param name="display">The backend display.</param>
    /// <param name="confidence">0..1.</param>
    /// <returns>The text.</returns>
    public static string WithUncertainty(string display, double confidence) =>
        confidence < UncertainBelow && !display.Contains("(uncertain)", StringComparison.Ordinal) ? $"{display} (uncertain)" : display;

    /// <summary>"81 % (or A minor)".</summary>
    /// <param name="key">The song key.</param>
    /// <returns>The text.</returns>
    public static string Confidence(SongKeyDto key)
    {
        var percent = ((int)Math.Round(key.Confidence * 100)).ToString(CultureInfo.InvariantCulture);
        return key.Alternative is { } alternative ? $"{percent} % (or {Name(alternative.Tonic, alternative.Mode)})" : $"{percent} %";
    }

    /// <summary>"Bar 9: D major", or the start time without a bar.</summary>
    /// <param name="region">The region.</param>
    /// <returns>The text.</returns>
    public static string Region(KeyViewRegionDto region) => region.StartBar is { } bar
        ? string.Create(CultureInfo.InvariantCulture, $"Bar {bar}: {region.Display}")
        : string.Create(CultureInfo.InvariantCulture, $"{region.StartMs / 60000}:{region.StartMs / 1000 % 60:00}: {region.Display}");
}
