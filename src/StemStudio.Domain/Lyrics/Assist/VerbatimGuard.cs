using System.Globalization;
using System.Text;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>The lyrics of one library track for the corpus index.</summary>
/// <param name="Track">The track.</param>
/// <param name="Lines">Its lyric lines.</param>
public sealed record CorpusLyrics(CorpusTrack Track, IReadOnlyList<string> Lines);

/// <summary>The in-memory n-gram index over library lyrics (research R-07): 7-word shingles and full lines of 5+ words.</summary>
public sealed class LyricsCorpusIndex : ILyricsCorpus
{
    private readonly HashSet<string> _shingles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<CorpusTrack>> _lines = new(StringComparer.Ordinal);

    /// <summary>Builds the index.</summary>
    /// <param name="lyrics">The library lyrics.</param>
    public LyricsCorpusIndex(IEnumerable<CorpusLyrics> lyrics)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        foreach (var track in lyrics)
        {
            Add(track);
        }
    }

    /// <summary>An empty index.</summary>
    public static LyricsCorpusIndex Empty { get; } = new([]);

    /// <inheritdoc />
    public bool ContainsShingle(string shingle) => _shingles.Contains(shingle);

    /// <inheritdoc />
    public IReadOnlyList<CorpusTrack> TracksWithLine(string line) => _lines.TryGetValue(line, out var tracks) ? tracks : [];

    private void Add(CorpusLyrics lyrics)
    {
        var words = new List<string>();
        foreach (var key in lyrics.Lines.Select(VerbatimGuard.Key).Where(key => key.Length > 0))
        {
            var lineWords = key.Split(' ');
            words.AddRange(lineWords);
            if (lineWords.Length >= VerbatimGuard.LineMinWords)
            {
                AddLine(key, lyrics.Track);
            }
        }

        foreach (var shingle in VerbatimGuard.Shingles(words))
        {
            _shingles.Add(shingle);
        }
    }

    private void AddLine(string key, CorpusTrack track)
    {
        if (!_lines.TryGetValue(key, out var tracks))
        {
            _lines[key] = tracks = [];
        }

        if (!tracks.Any(existing => existing.TrackId == track.TrackId))
        {
            tracks.Add(track);
        }
    }
}

/// <summary>
/// Own-lyrics rules (research R-07, FR-003, FR-023, FR-031): the sheet guard (at least 3 lines of 5+ words and at least 25 % of the
/// sheet's lyric lines equal library lines; an own-work override covers one track) and the output guard (a 7-word run or a full
/// 5+-word line of library lyrics is withheld; no override applies).
/// </summary>
public static class VerbatimGuard
{
    /// <summary>Words of a shingle.</summary>
    public const int ShingleWords = 7;

    /// <summary>Words of an indexed full line.</summary>
    public const int LineMinWords = 5;

    /// <summary>Matching lines needed for a refusal.</summary>
    public const int MinLines = 3;

    /// <summary>Share of matching lines needed for a refusal.</summary>
    public const double MinShare = 0.25;

    /// <summary>The normalised key of a line: lower case, no diacritics, no apostrophes, other punctuation a space, single spaces.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The key.</returns>
    public static string Key(string? line)
    {
        var builder = new StringBuilder();
        foreach (var c in (line ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c is '\'' or '’')
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The 7-word shingles of a word sequence.</summary>
    /// <param name="words">Words.</param>
    /// <returns>The shingles.</returns>
    public static IEnumerable<string> Shingles(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        for (var i = 0; i + ShingleWords <= words.Count; i++)
        {
            yield return string.Join(' ', words.Skip(i).Take(ShingleWords));
        }
    }

    /// <summary>True when generated text must be withheld.</summary>
    /// <param name="generated">The generated text.</param>
    /// <param name="corpus">The corpus.</param>
    /// <returns>True when it contains a 7-word run or a full 5+-word line of library lyrics.</returns>
    public static bool IsVerbatim(string? generated, ILyricsCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        var keys = (generated ?? string.Empty).Split('\n').Select(Key).Where(key => key.Length > 0).ToList();
        var words = keys.SelectMany(key => key.Split(' ')).ToList();
        return keys.Any(key => key.Split(' ').Length >= LineMinWords && corpus.TracksWithLine(key).Count > 0) || Shingles(words).Any(corpus.ContainsShingle);
    }

    /// <summary>The track that makes the sheet refused, or null when it may be assisted.</summary>
    /// <param name="text">Normalised sheet text.</param>
    /// <param name="corpus">The corpus.</param>
    /// <param name="overrideTrackId">The track an own-work override covers, if any.</param>
    /// <returns>The first refusing track (most matching lines first), or null.</returns>
    public static CorpusTrack? RefusingTrack(string text, ILyricsCorpus corpus, Guid? overrideTrackId)
    {
        var (matches, lyricLines) = Matches(text, corpus);
        var uncovered = matches.Where(tracks => tracks.Any(track => track.TrackId != overrideTrackId)).ToList();
        return Refuses(uncovered.Count, lyricLines) ? Top(uncovered, overrideTrackId) : null;
    }

    /// <summary>The tracks an own-work override may name: the sheet is refused without override and the track has matching lines.</summary>
    /// <param name="text">Normalised sheet text.</param>
    /// <param name="corpus">The corpus.</param>
    /// <returns>The tracks, most matching lines first.</returns>
    public static IReadOnlyList<CorpusTrack> MatchingTracks(string text, ILyricsCorpus corpus)
    {
        var (matches, lyricLines) = Matches(text, corpus);
        return Refuses(matches.Count, lyricLines) ? Ranked(matches, null).ToList() : [];
    }

    private static (List<IReadOnlyList<CorpusTrack>> Matches, int LyricLines) Matches(string text, ILyricsCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        var lyric = SheetText.Lines(text).Where(SheetText.IsLyric).ToList();
        var matches = lyric.Select(Key).Where(key => key.Split(' ').Length >= LineMinWords).Select(corpus.TracksWithLine).Where(tracks => tracks.Count > 0).ToList();
        return (matches, lyric.Count);
    }

    private static bool Refuses(int matchingLines, int lyricLines) => matchingLines >= MinLines && matchingLines >= MinShare * lyricLines;

    private static CorpusTrack? Top(List<IReadOnlyList<CorpusTrack>> matches, Guid? excluded) => Ranked(matches, excluded).FirstOrDefault();

    private static IEnumerable<CorpusTrack> Ranked(List<IReadOnlyList<CorpusTrack>> matches, Guid? excluded) => matches
        .SelectMany(tracks => tracks)
        .Where(track => track.TrackId != excluded)
        .GroupBy(track => track.TrackId)
        .OrderByDescending(group => group.Count())
        .Select(group => group.First());
}
