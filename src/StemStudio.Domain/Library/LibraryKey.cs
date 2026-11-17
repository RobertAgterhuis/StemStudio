using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StemStudio.Domain.Library;

/// <summary>
/// Normalised folder keys (feature 022, research R-02, FR-004/FR-005): names with the same key share one folder. Pure.
/// </summary>
public static partial class LibraryKey
{
    /// <summary>Fallback artist label.</summary>
    public const string UnknownArtist = "Unknown artist";

    /// <summary>Fallback album label.</summary>
    public const string UnknownAlbum = "Unknown album";

    /// <summary>Fallback song label.</summary>
    public const string UntitledSong = "Untitled song";

    /// <summary>Artist of items without a track and without a source item (FR-006).</summary>
    public const string OwnSongsArtist = "My songs";

    /// <summary>Album of items without a track and without a source item (FR-006).</summary>
    public const string OwnSongsAlbum = "Songs from my lyrics";

    /// <summary>Longest key.</summary>
    public const int MaxKeyLength = 400;

    /// <summary>The MusicBrainz special purpose artist "Various Artists" (ignored as match, FR-005).</summary>
    public static readonly Guid VariousArtistsMbid = Guid.Parse("89ad4ac3-39f7-470e-963a-56509c546377");

    private const string The = "the ";

    /// <summary>The key of a name at a level ("The" removed for artist and album, featuring parts for artist).</summary>
    /// <param name="level">Folder level.</param>
    /// <param name="name">Name.</param>
    /// <returns>The key (at most 400 characters); the fallback label's key when nothing meaningful is left.</returns>
    public static string For(FolderLevel level, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var key = WithoutThe(level, level == FolderLevel.Artist ? WithoutFeaturing(Fold(name).Value) : Fold(name).Value);
        if (!key.Any(char.IsLetterOrDigit))
        {
            key = WithoutThe(level, Fold(FallbackOf(level)).Value);
        }

        return key.Length <= MaxKeyLength ? key : key[..MaxKeyLength];
    }

    /// <summary>The sort text of a display name: folded, a leading "The" ignored for artist and album.</summary>
    /// <param name="level">Folder level.</param>
    /// <param name="name">Display name.</param>
    /// <returns>The sort text.</returns>
    public static string SortKey(FolderLevel level, string name) => WithoutThe(level, Fold(name).Value);

    /// <summary>True when the name has a letter or digit after folding (not only spaces, punctuation or emoji).</summary>
    /// <param name="name">Name.</param>
    /// <returns>True when usable as a folder name.</returns>
    public static bool IsMeaningful(string? name) => name is not null && Fold(name).Value.Any(char.IsLetterOrDigit);

    /// <summary>True for the "Various Artists" match (by MusicBrainz id or by key).</summary>
    /// <param name="artistName">Matched artist name.</param>
    /// <param name="artistMbid">Matched artist id.</param>
    /// <returns>True when the match must be ignored for filing.</returns>
    public static bool IsVariousArtists(string? artistName, Guid? artistMbid) =>
        artistMbid == VariousArtistsMbid || (artistName is not null && Fold(artistName).Value == "various artists");

    /// <summary>The fallback label of a level.</summary>
    /// <param name="level">Folder level.</param>
    /// <returns>"Unknown artist", "Unknown album" or "Untitled song".</returns>
    public static string FallbackOf(FolderLevel level) => level switch
    {
        FolderLevel.Artist => UnknownArtist,
        FolderLevel.Album => UnknownAlbum,
        _ => UntitledSong,
    };

    /// <summary>Folds a text (R-02 steps 1-4, no level rules) and keeps the map to the original indices.</summary>
    /// <param name="text">Text.</param>
    /// <returns>The folded text.</returns>
    public static FoldedText Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new FoldBuilder();
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            builder.Add(FoldRune(rune), index, index + rune.Utf16SequenceLength);
            index += rune.Utf16SequenceLength;
        }

        return builder.Build();
    }

    private static string WithoutThe(FolderLevel level, string key) =>
        level != FolderLevel.Song && key.StartsWith(The, StringComparison.Ordinal) && key.Length > The.Length ? key[The.Length..] : key;

    private static string WithoutFeaturing(string folded)
    {
        var withoutBrackets = FeaturingInBrackets().Replace(folded, string.Empty);
        return FeaturingTail().Replace(withoutBrackets, string.Empty).Trim();
    }

    private static string FoldRune(Rune rune)
    {
        if (rune.IsBmp && Punctuation((char)rune.Value) is { } unified)
        {
            return unified;
        }

        var decomposed = rune.ToString().Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsWhiteSpace(c) || !char.IsControl(c))))
        {
            builder.Append(c);
        }

        var lower = builder.ToString().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return lower.Length == 1 ? Letter(lower[0]) : lower;
    }

    private static string? Punctuation(char c) => c switch
    {
        '’' or '‘' or '‛' or '´' or '`' => "'",
        '“' or '”' or '„' => "\"",
        '‐' or '‑' or '‒' or '–' or '—' or '―' => "-",
        _ => null,
    };

    private static string Letter(char c) => c switch
    {
        'ß' => "ss",
        'æ' => "ae",
        'ø' => "o",
        'ł' => "l",
        'đ' => "d",
        'þ' => "th",
        'œ' => "oe",
        _ => c.ToString(),
    };

    [GeneratedRegex(@"\s*[\(\[]\s*(feat|ft|featuring)\b[^\)\]]*[\)\]]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FeaturingInBrackets();

    [GeneratedRegex(@"\s+(feat\.?|ft\.?|featuring)\s.*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FeaturingTail();

    /// <summary>Collects folded pieces, collapses whitespace and records the index map.</summary>
    private sealed class FoldBuilder
    {
        private readonly StringBuilder _value = new();
        private readonly List<int> _map = [];
        private readonly List<int> _ends = [];
        private (int Start, int End)? _space;

        public void Add(string piece, int start, int end)
        {
            if (piece.Length == 0)
            {
                ExtendLast(end);
                return;
            }

            foreach (var c in piece)
            {
                AddChar(c, start, end);
            }
        }

        public FoldedText Build() => new(_value.ToString(), [.. _map], [.. _ends]);

        private void AddChar(char c, int start, int end)
        {
            if (char.IsWhiteSpace(c))
            {
                _space ??= _value.Length > 0 ? (start, end) : null;
                return;
            }

            if (_space is { } space)
            {
                Append(' ', space.Start, space.End);
                _space = null;
            }

            Append(c, start, end);
        }

        private void Append(char c, int start, int end)
        {
            _value.Append(c);
            _map.Add(start);
            _ends.Add(end);
        }

        private void ExtendLast(int end)
        {
            if (_ends.Count > 0 && _space is null)
            {
                _ends[^1] = end;
            }
        }
    }
}
