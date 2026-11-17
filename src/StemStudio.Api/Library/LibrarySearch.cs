using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>A validated search request.</summary>
/// <param name="Text">The search text (trimmed, at most 100 characters).</param>
/// <param name="Tokens">Its folded words (1..8).</param>
/// <param name="Within">Only inside this folder, or null.</param>
/// <param name="Limit">Hits per group.</param>
public sealed record SearchRequest(string Text, IReadOnlyList<string> Tokens, string? Within, int Limit);

/// <summary>
/// The live search (feature 022, research R-07, FR-022..FR-025, FR-029, FR-036): case- and accent-insensitive words
/// (at most 8); every word must occur in a field; folders need a word in their own name; groups ordered by relevance
/// (whole field, start of a word, inside a word) then name; highlights as UTF-16 ranges of the displayed text. Pure.
/// </summary>
public static class LibrarySearch
{
    private const int AncestorClass = 3;

    /// <summary>The folded words of a search text: R-02 steps 1-4, split on spaces, at most 8.</summary>
    /// <param name="text">The trimmed text.</param>
    /// <returns>The words.</returns>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. LibraryKey.Fold(text).Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Take(ContractLimits.LibrarySearchMaxTokens)];
    }

    /// <summary>Runs a search.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="request">The request.</param>
    /// <returns>The result, or null when <c>within</c> is not a folder (any more).</returns>
    public static LibrarySearchResultDto? Run(LibrarySnapshot snapshot, SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        var index = snapshot.Search;
        if (request.Within is { } within && index.Folder(within) is null)
        {
            return null;
        }

        var folders = index.Folders.Where(node => InScope(node, request.Within)).Select(node => FolderMatch.Of(node, index, request.Tokens)).OfType<FolderMatch>().ToList();
        var items = index.Items.Where(item => request.Within is null || IsInside(item.Entry, request.Within))
            .Select(item => ItemMatch.Of(item, index, request.Tokens)).OfType<ItemMatch>().OrderBy(hit => hit.Score).ThenBy(hit => hit.Item.Name.Value, StringComparer.InvariantCulture)
            .ThenBy(hit => hit.Item.Entry.Asset.Id).ToList();
        return Result(snapshot, request, folders, items);
    }

    /// <summary>The match class of a word in a folded field: 0 whole field, 1 start of a word, 2 inside a word, -1 none.</summary>
    /// <param name="field">Folded field.</param>
    /// <param name="token">Folded word.</param>
    /// <returns>The class.</returns>
    public static int ClassOf(string field, string token)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(token);
        if (field == token)
        {
            return 0;
        }

        var best = -1;
        for (var start = field.IndexOf(token, StringComparison.Ordinal); start >= 0 && best != 1; start = field.IndexOf(token, start + 1, StringComparison.Ordinal))
        {
            best = start == 0 || !char.IsLetterOrDigit(field[start - 1]) ? 1 : 2;
        }

        return best;
    }

    /// <summary>The highlight of a word in a displayed text, or null when it does not occur.</summary>
    /// <param name="field">The field.</param>
    /// <param name="text">The folded displayed text.</param>
    /// <param name="token">The folded word.</param>
    /// <param name="label">The instrument label the offsets refer to (instrument field only).</param>
    /// <returns>The highlight.</returns>
    public static HighlightDto? Highlight(SearchFieldDto field, FoldedText text, string token, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = text.Value.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var (offset, length) = text.ToOriginal(start, token.Length);
        return new HighlightDto { Field = field, Start = offset, Length = length, Label = label };
    }

    /// <summary>The field of a folder level.</summary>
    /// <param name="level">Folder level.</param>
    /// <returns>The search field.</returns>
    public static SearchFieldDto FieldOf(FolderLevel level) => (SearchFieldDto)(int)level;

    private static bool InScope(FolderNode node, string? within) => within is null || node.AncestorIds.Contains(within, StringComparer.Ordinal);

    private static bool IsInside(LibraryEntry entry, string within) => entry.ArtistId == within || entry.AlbumId == within || entry.SongId == within;

    private static LibrarySearchResultDto Result(LibrarySnapshot snapshot, SearchRequest request, List<FolderMatch> folders, List<ItemMatch> items)
    {
        var groups = new[] { FolderLevel.Artist, FolderLevel.Album, FolderLevel.Song }
            .Select(level => folders.Where(hit => hit.Node.Level == level).OrderBy(hit => hit.Score).ThenBy(hit => hit.Node.SortName, StringComparer.InvariantCulture).ThenBy(hit => hit.Node.Id, StringComparer.Ordinal).ToList())
            .ToArray();
        return new LibrarySearchResultDto
        {
            Query = request.Text,
            Tokens = request.Tokens,
            Revision = snapshot.Revision,
            Artists = [.. groups[0].Take(request.Limit).Select(hit => hit.ToDto(snapshot))],
            Albums = [.. groups[1].Take(request.Limit).Select(hit => hit.ToDto(snapshot))],
            Songs = [.. groups[2].Take(request.Limit).Select(hit => hit.ToDto(snapshot))],
            Items = [.. items.Take(request.Limit).Select(hit => hit.ToDto(snapshot))],
            Totals = new SearchTotalsDto { Artists = groups[0].Count, Albums = groups[1].Count, Songs = groups[2].Count, Items = items.Count },
        };
    }

    /// <summary>A folder hit: at least one word in its own name, every word in its name or its parents' names.</summary>
    private sealed record FolderMatch(FolderNode Node, int Score, IReadOnlyList<HighlightDto> Highlights, IReadOnlyList<FolderNode> Parents)
    {
        public static FolderMatch? Of(FolderNode node, SearchIndex index, IReadOnlyList<string> tokens)
        {
            var parents = node.AncestorIds.Select(id => index.Folder(id)!).ToList();
            var classes = tokens.Select(token => ClassOf(node.Folded.Value, token) is var own and >= 0 ? own : InParents(parents, token)).ToList();
            if (classes.Contains(-1) || !classes.Any(value => value is >= 0 and < AncestorClass))
            {
                return null;
            }

            var highlights = tokens.Select(token => Highlight(FieldOf(node.Level), node.Folded, token)).OfType<HighlightDto>();
            return new FolderMatch(node, classes.Sum(), [.. highlights.Take(ContractLimits.SearchHighlightsMax)], parents);
        }

        public FolderHitDto ToDto(LibrarySnapshot snapshot) => new()
        {
            Folder = FolderTreeBuilder.Folder(snapshot, Node.Level, Node.Entries, Node.Entries),
            Path = [.. Parents.Select(RefOf)],
            Highlights = Highlights,
        };

        private static int InParents(List<FolderNode> parents, string token) => parents.Any(parent => ClassOf(parent.Folded.Value, token) >= 0) ? AncestorClass : -1;
    }

    /// <summary>An item hit: every word in one of its fields (folder names, artist credit, name, instruments).</summary>
    private sealed record ItemMatch(ItemNode Item, int Score, IReadOnlyList<HighlightDto> Highlights, IReadOnlyList<string> Instruments, IReadOnlyList<FolderNode> Path)
    {
        public static ItemMatch? Of(ItemNode item, SearchIndex index, IReadOnlyList<string> tokens)
        {
            var path = new[] { item.Entry.ArtistId, item.Entry.AlbumId, item.Entry.SongId }.Select(id => index.Folder(id)!).ToList();
            var instruments = InstrumentMatcher.Match(item.Entry.Terms.Terms, tokens);
            var classes = tokens.Select((token, i) => Best(TextClasses(item, path, token).Append(instruments.ClassOf(i)))).ToList();
            if (classes.Contains(-1))
            {
                return null;
            }

            return new ItemMatch(item, classes.Sum(), [.. ItemHighlights(item, path, tokens, instruments).Take(ContractLimits.SearchHighlightsMax)], instruments.Labels, path);
        }

        public ItemHitDto ToDto(LibrarySnapshot snapshot) => new()
        {
            Asset = AssetMapper.ToDto(Item.Entry.Asset, snapshot.TrackExists(Item.Entry.Asset)),
            Path = [.. Path.Select(RefOf)],
            Highlights = Highlights,
            MatchedInstruments = [.. Instruments.Take(ContractLimits.MatchedInstrumentsMax)],
        };

        private static IEnumerable<int> TextClasses(ItemNode item, List<FolderNode> path, string token) =>
            path.Select(node => node.Folded.Value).Append(item.Name.Value).Append(item.Credit).Select(field => ClassOf(field, token));

        private static int Best(IEnumerable<int> classes) => classes.Where(value => value >= 0).DefaultIfEmpty(-1).Min();

        private static IEnumerable<HighlightDto> ItemHighlights(ItemNode item, List<FolderNode> path, IReadOnlyList<string> tokens, InstrumentMatcher.Result instruments) =>
            tokens.SelectMany(token => path.Select(node => Highlight(FieldOf(node.Level), node.Folded, token)).Append(Highlight(SearchFieldDto.Name, item.Name, token)))
                .Concat(instruments.Highlights).OfType<HighlightDto>();
    }

    private static FolderRefDto RefOf(FolderNode node) => new() { Id = node.Id, Level = LibraryMapper.ToDto(node.Level), Name = node.Name };
}
