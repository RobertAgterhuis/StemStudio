using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StemStudio.Domain.Lyrics;

/// <summary>Section markers recognised in own lyrics.</summary>
public enum SectionMarker
{
    /// <summary>[intro].</summary>
    Intro,

    /// <summary>[verse].</summary>
    Verse,

    /// <summary>[pre-chorus].</summary>
    PreChorus,

    /// <summary>[chorus].</summary>
    Chorus,

    /// <summary>[bridge].</summary>
    Bridge,

    /// <summary>[outro].</summary>
    Outro,

    /// <summary>[instrumental].</summary>
    Instrumental,
}

/// <summary>A section of the lyrics.</summary>
/// <param name="Marker">The marker.</param>
/// <param name="Number">The number (<c>[verse 2]</c>), if any.</param>
/// <param name="LineCount">Non-blank lines until the next marker.</param>
public sealed record LyricsSection(SectionMarker Marker, int? Number, int LineCount);

/// <summary>A text with section markers and its sections.</summary>
/// <param name="Text">The text.</param>
/// <param name="Sections">The sections.</param>
/// <param name="WordsPerSecondAt120s">Density at the default song length.</param>
public sealed record SectionProposal(string Text, IReadOnlyList<LyricsSection> Sections, double WordsPerSecondAt120s);

/// <summary>Parses and proposes section markers (010 data-model 3.1).</summary>
public static partial class LyricsSections
{
    /// <summary>The sections of a text; lines before the first marker and unknown brackets are text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The sections in order.</returns>
    public static IReadOnlyList<LyricsSection> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sections = new List<LyricsSection>();
        foreach (var line in text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            if (Marker(line) is { } section)
            {
                sections.Add(section);
            }
            else if (sections.Count > 0)
            {
                sections[^1] = sections[^1] with { LineCount = sections[^1].LineCount + 1 };
            }
        }

        return sections;
    }

    /// <summary>The text with markers: a repeated block becomes <c>[chorus]</c>, the others <c>[verse]</c>, <c>[verse 2]</c>, ...; text that has markers is kept.</summary>
    /// <param name="text">The text (normalised here).</param>
    /// <returns>The proposal.</returns>
    public static SectionProposal Propose(string text)
    {
        var normalized = LyricsNormaliser.Normalize(text);
        var proposed = Parse(normalized).Count > 0 ? normalized : WithMarkers(normalized);
        return new SectionProposal(proposed, Parse(proposed), LyricsDensity.WordsPerSecond(proposed, 120));
    }

    /// <summary>The marker of a line, if it is one.</summary>
    /// <param name="line">A trimmed line.</param>
    /// <returns>The section with no lines yet, or null.</returns>
    public static LyricsSection? Marker(string line)
    {
        var match = MarkerPattern().Match(line ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var marker = Enum.Parse<SectionMarker>(match.Groups[1].Value.Replace("-", string.Empty, StringComparison.Ordinal), true);
        int? number = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : null;
        return new LyricsSection(marker, number, 0);
    }

    private static string WithMarkers(string text)
    {
        var blocks = text.Split("\n\n").Select(block => block.Trim('\n')).Where(block => block.Length > 0).ToList();
        var counts = blocks.GroupBy(Key).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var verses = 0;
        var marked = blocks.Select(block => counts[Key(block)] > 1 ? "[chorus]\n" + block : VerseMarker(++verses) + "\n" + block);
        return string.Join("\n\n", marked);
    }

    private static string VerseMarker(int number) => number == 1 ? "[verse]" : string.Create(CultureInfo.InvariantCulture, $"[verse {number}]");

    private static string Key(string block)
    {
        var builder = new StringBuilder();
        foreach (var c in block.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
        {
            builder.Append(char.IsWhiteSpace(c) ? ' ' : c);
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex(@"\A\[(intro|verse|pre-chorus|chorus|bridge|outro|instrumental)(?:\s+(\d{1,2}))?\]\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MarkerPattern();
}

/// <summary>Lyrics density: words outside marker lines per second; a warning above 4 words per second.</summary>
public static class LyricsDensity
{
    /// <summary>Highest density that sings clearly.</summary>
    public const double MaxWordsPerSecond = 4.0;

    /// <summary>Words outside marker lines.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The word count.</returns>
    public static int Words(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split('\n').Where(line => LyricsSections.Marker(line.Trim()) is null)
            .Sum(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    /// <summary>Words per second at a duration (0 for a non-positive duration).</summary>
    /// <param name="text">The text.</param>
    /// <param name="durationSeconds">The song length.</param>
    /// <returns>Words per second.</returns>
    public static double WordsPerSecond(string text, double durationSeconds) => durationSeconds > 0 ? Words(text) / durationSeconds : 0;

    /// <summary>True above 4 words per second.</summary>
    /// <param name="text">The text.</param>
    /// <param name="durationSeconds">The song length.</param>
    /// <returns>True when too dense.</returns>
    public static bool IsTooDense(string text, double durationSeconds) => WordsPerSecond(text, durationSeconds) > MaxWordsPerSecond;

    /// <summary>The readable warning.</summary>
    /// <param name="words">Word count.</param>
    /// <param name="durationSeconds">Song length.</param>
    /// <returns>The warning text.</returns>
    public static string Warning(int words, double durationSeconds) => string.Create(
        CultureInfo.InvariantCulture,
        $"The lyrics have {words / durationSeconds:0.0} words per second; at most 4 sing clearly. Choose a longer duration or shorten the text.");
}
