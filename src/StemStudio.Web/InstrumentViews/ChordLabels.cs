using System.Globalization;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Audio;
using StemStudio.Web.Services;

namespace StemStudio.Web.InstrumentViews;

/// <summary>Chord labels of the edit dialog (Harte syntax, data-model 2.1) and the chart sent to the engine (data-model 9).</summary>
public static class ChordLabels
{
    // Bass interval above the root per semitone distance (Harte `/b?[1-7]`); 0 = the root itself (no slash).
    private static readonly string[] Intervals = ["1", "b2", "2", "b3", "3", "4", "b5", "5", "b6", "6", "b7", "7"];

    private static readonly Dictionary<ChordQualityDto, string> Names = new()
    {
        [ChordQualityDto.Maj] = "major",
        [ChordQualityDto.Min] = "minor",
        [ChordQualityDto.Dim] = "diminished",
        [ChordQualityDto.Aug] = "augmented",
        [ChordQualityDto.Min6] = "minor 6",
        [ChordQualityDto.Maj6] = "major 6",
        [ChordQualityDto.Min7] = "minor 7",
        [ChordQualityDto.Minmaj7] = "minor major 7",
        [ChordQualityDto.Maj7] = "major 7",
        [ChordQualityDto.Seventh] = "dominant 7",
        [ChordQualityDto.Dim7] = "diminished 7",
        [ChordQualityDto.Hdim7] = "half-diminished 7 (m7b5)",
        [ChordQualityDto.Sus2] = "suspended 2",
        [ChordQualityDto.Sus4] = "suspended 4",
    };

    /// <summary>Root names by pitch class (C = 0), as Harte roots.</summary>
    public static IReadOnlyList<string> RootNames { get; } = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    /// <summary>The qualities a vocabulary offers.</summary>
    /// <param name="vocabulary">The chart's vocabulary.</param>
    /// <returns>The qualities in contract order.</returns>
    public static IReadOnlyList<ChordQualityDto> Qualities(ChordVocabularyDto vocabulary) =>
        vocabulary == ChordVocabularyDto.Simple ? [ChordQualityDto.Maj, ChordQualityDto.Min] : Enum.GetValues<ChordQualityDto>();

    /// <summary>The readable name of a quality.</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>E.g. "minor 7".</returns>
    public static string QualityName(ChordQualityDto quality) => Names[quality];

    /// <summary>The Harte label of a chord.</summary>
    /// <param name="root">Root pitch class, or null for no chord.</param>
    /// <param name="quality">Quality (ignored without a root).</param>
    /// <param name="bass">Bass pitch class, or null (or the root) for no slash.</param>
    /// <returns>E.g. <c>A:min7/b7</c> or <c>N</c>.</returns>
    public static string Harte(int? root, ChordQualityDto? quality, int? bass)
    {
        if (root is not { } r)
        {
            return "N";
        }

        var label = $"{RootNames[r]}:{WireName.Of(quality ?? ChordQualityDto.Maj)}";
        return bass is { } b && b != r ? $"{label}/{Intervals[(b - r + 12) % 12]}" : label;
    }

    /// <summary>The chart as the engine takes it (<c>ChordChartData</c>).</summary>
    /// <param name="document">The chord chart.</param>
    /// <returns>The engine chart.</returns>
    public static ChordChartData ToEngine(ChordChartDocumentDto document) => new(
        document.Segments.Select(s => new ChordSymbolEvent(s.StartMs, s.EndMs, s.Root, s.Quality is { } q ? WireName.Of(q) : null, s.Bass)).ToList(),
        document.Key?.Tonic,
        document.Key is { } key ? Mode(key.Mode) : null,
        document.KeyRegions.Select(r => new KeyRegionData(r.StartMs, r.EndMs, r.Tonic, Mode(r.Mode))).ToList());

    /// <summary>Track time as "m:ss".</summary>
    /// <param name="ms">Milliseconds.</param>
    /// <returns>The text.</returns>
    public static string Time(int ms) => TimeSpan.FromMilliseconds(ms).ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static string Mode(KeyModeDto mode) => WireName.Of(mode);
}
