using System.Globalization;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>Syllables of one line (FR-017).</summary>
/// <param name="Line">1-based line.</param>
/// <param name="Syllables">Syllables (0 for markers and blank lines).</param>
/// <param name="IsMarker">Marker or blank line.</param>
/// <param name="Section">The 010 section name (<c>verse 2</c>), if any.</param>
/// <param name="SectionMedian">Median syllables of the section's lyric lines.</param>
/// <param name="Target">Target syllables of the rhythm target.</param>
/// <param name="Deviates">More than 2 from the target, or from the section median without a target.</param>
public sealed record SyllableLine(int Line, int Syllables, bool IsMarker, string? Section, double? SectionMedian, int? Target, bool Deviates);

/// <summary>The syllable report of a text.</summary>
/// <param name="Lines">Every line.</param>
/// <param name="TargetDurationSeconds">Song length at the rhythm target, if set.</param>
/// <param name="WordsPerSecond">010 density at that length.</param>
public sealed record SyllableReport(IReadOnlyList<SyllableLine> Lines, double? TargetDurationSeconds, double? WordsPerSecond)
{
    /// <summary>Largest allowed distance from the reference.</summary>
    public const int MaxDeviation = 2;

    /// <summary>Builds the report (research R-04 "Rhythm target").</summary>
    /// <param name="text">Normalised text.</param>
    /// <param name="language">Language.</param>
    /// <param name="target">Rhythm target, if any.</param>
    /// <param name="phonology">Syllable counter.</param>
    /// <returns>The report.</returns>
    public static SyllableReport Build(string text, AssistLanguage language, RhythmTarget? target, LyricPhonology phonology)
    {
        ArgumentNullException.ThrowIfNull(phonology);
        var lines = SheetText.Lines(text);
        var counted = lines.Select((line, i) => (Index: i, Text: line, Syllables: phonology.LineSyllables(line, language), Lyric: SheetText.IsLyric(line))).ToList();
        var sections = Sections(lines);
        var medians = counted.Where(l => l.Lyric).GroupBy(l => sections[l.Index].Key).ToDictionary(g => g.Key, g => Median(g.Select(l => l.Syllables)));
        var result = counted.Select(l => Line(l.Index, l.Syllables, l.Lyric, sections[l.Index], medians, target?.SyllablesPerLine)).ToList();
        var duration = target is null ? (double?)null : counted.Count(l => l.Lyric) * target.SecondsPerLine;
        return new SyllableReport(result, duration, duration is > 0 ? LyricsDensity.WordsPerSecond(text, duration.Value) : null);
    }

    private static SyllableLine Line(int index, int syllables, bool lyric, (int Key, string? Name) section, Dictionary<int, double> medians, int? target)
    {
        double? median = lyric && medians.TryGetValue(section.Key, out var m) ? m : null;
        var reference = target ?? median;
        var deviates = lyric && reference is { } r && Math.Abs(syllables - r) > MaxDeviation;
        return new SyllableLine(index + 1, syllables, !lyric, section.Name, median, lyric ? target : null, deviates);
    }

    // With markers the 010 sections; without, blank-line blocks (unnamed).
    private static (int Key, string? Name)[] Sections(string[] lines)
    {
        var hasMarkers = lines.Any(SheetText.IsMarker);
        var result = new (int Key, string? Name)[lines.Length];
        var (key, name) = (0, (string?)null);
        for (var i = 0; i < lines.Length; i++)
        {
            if (SheetText.IsMarker(lines[i]))
            {
                (key, name) = (key + 1, Name(lines[i]));
            }
            else if (!hasMarkers && string.IsNullOrWhiteSpace(lines[i]))
            {
                key++;
            }

            result[i] = (key, name);
        }

        return result;
    }

    private static string Name(string marker)
    {
        var name = marker.Trim().Trim('[', ']').Trim().ToLower(CultureInfo.InvariantCulture);
        return name.Length <= 40 ? name : name[..40];
    }

    private static double Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }
}
