using StemStudio.Agent.Ports;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;

namespace StemStudio.AgentEval.Fixture;

/// <summary>
/// Library folders over the fixture (feature 022, eval cases LIB-01..LIB-05): artist <c>ar-metallica</c> (album
/// <c>al-load</c>, song <c>so-hero</c> with the drums and bass stems of <c>t-hero</c>), artist <c>ar-motorhead</c> (album
/// <c>al-ace</c>, song <c>so-ace</c> with a snare and a bass stem) and the fallback artist <c>ar-unknown</c> "Unknown artist"
/// (song <c>so-untitled</c> with a bass stem). Search matches folder names, item names and instrument words; library info is kept
/// per track or item. Nothing leaves the process.
/// </summary>
/// <param name="library">The fixture (ids).</param>
internal sealed class FixtureLibraryActions(FixtureLibrary library) : ILibraryActions
{
    private const string Revision = "0123456789abcdef";
    private readonly List<Folder> _folders = Seed(library.Ids);
    private readonly Dictionary<LibraryInfoTarget, LibraryInfoRequest> _info = [];

    /// <summary>The library info requests received (LIB-04).</summary>
    public List<(LibraryInfoTarget Target, LibraryInfoRequest Request)> Changes { get; } = [];

    public Task<LibraryFolderViewDto> BrowseLibraryFolderAsync(LibraryFolderQuery query, CancellationToken cancellationToken)
    {
        var folder = query.FolderId is { } id ? _folders.FirstOrDefault(item => item.Id == id) ?? throw new AgentActionException(404, LibraryProblemTypes.FolderNotFound, "The folder does not exist.") : null;
        var children = _folders.Where(item => item.ParentId == folder?.Id).ToList();
        var items = folder?.Items.Where(item => query.Kind is null || item.Kind == query.Kind).ToList() ?? [];
        return Task.FromResult(new LibraryFolderViewDto
        {
            Revision = Revision,
            Folder = folder?.Dto(),
            Breadcrumb = [.. Path(folder).Select(item => item.Ref())],
            Children = [.. children.Select(item => item.Dto())],
            ChildrenTotal = children.Count,
            Items = items,
            TotalItems = items.Count,
            TotalSizeBytes = items.Sum(item => item.SizeBytes),
            PendingFiling = 0,
        });
    }

    public Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchQuery query, CancellationToken cancellationToken)
    {
        var words = query.Query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var scope = query.Within is { } within ? _folders.Where(folder => Path(folder).Any(item => item.Id == within)).ToList() : _folders;
        var folders = scope.Where(folder => words.All(word => folder.Name.Contains(word, StringComparison.OrdinalIgnoreCase))).ToList();
        var items = scope.SelectMany(folder => folder.Items.Select(item => (Folder: folder, Item: item)))
            .Where(hit => words.All(word => hit.Item.Name.Contains(word, StringComparison.OrdinalIgnoreCase) || Channels(hit.Item).Any(channel => channel.Contains(word, StringComparison.Ordinal))))
            .ToList();
        return Task.FromResult(new LibrarySearchResultDto
        {
            Query = query.Query,
            Revision = Revision,
            Artists = Hits(folders, FolderLevelDto.Artist),
            Albums = Hits(folders, FolderLevelDto.Album),
            Songs = Hits(folders, FolderLevelDto.Song),
            Items = [.. items.Select(hit => new ItemHitDto { Asset = hit.Item, Path = [.. Path(hit.Folder).Select(item => item.Ref())], MatchedInstruments = [.. Channels(hit.Item).Where(channel => words.Any(channel.Contains))] })],
            Totals = new SearchTotalsDto { Artists = Count(folders, FolderLevelDto.Artist), Albums = Count(folders, FolderLevelDto.Album), Songs = Count(folders, FolderLevelDto.Song), Items = items.Count },
        });
    }

    public Task<LibraryInfoStateDto> GetLibraryInfoAsync(LibraryInfoTarget target, CancellationToken cancellationToken) =>
        Task.FromResult(State(_info.GetValueOrDefault(target, LibraryInfoRequest.Automatic), 0));

    public Task<LibraryInfoStateDto> SetLibraryInfoAsync(LibraryInfoTarget target, LibraryInfoRequest request, CancellationToken cancellationToken)
    {
        _info[target] = request;
        Changes.Add((target, request));
        return Task.FromResult(State(request, 2));
    }

    private static LibraryInfoStateDto State(LibraryInfoRequest request, int moved) => new()
    {
        Artist = Level(request.Artist),
        Album = Level(request.Album),
        Song = Level(request.Song),
        MovedItems = moved,
    };

    private static LibraryInfoLevelDto Level(string? value) =>
        new() { Value = value, Effective = value ?? "automatic", Source = value is null ? PlacementSourceDto.Track : PlacementSourceDto.Item, Automatic = "automatic" };

    private List<Folder> Path(Folder? folder)
    {
        var path = new List<Folder>();
        for (var current = folder; current is not null; current = _folders.FirstOrDefault(item => item.Id == current.ParentId))
        {
            path.Insert(0, current);
        }

        return path;
    }

    private IReadOnlyList<FolderHitDto> Hits(List<Folder> folders, FolderLevelDto level) =>
        [.. folders.Where(folder => folder.Level == level).Select(folder => new FolderHitDto { Folder = folder.Dto(), Path = [.. Path(folder).Select(item => item.Ref())] })];

    private static int Count(List<Folder> folders, FolderLevelDto level) => folders.Count(folder => folder.Level == level);

    private static IReadOnlyList<string> Channels(AssetDto asset) => asset.Provenance.Recipe?.Channels ?? [];

    private static List<Folder> Seed(FixtureIds ids)
    {
        Folder Make(string key, FolderLevelDto level, string name, string? parent, params AssetDto[] items) =>
            new(ids.Folder(key), level, name, parent is null ? null : ids.Folder(parent), items);
        AssetDto Stem(string key, string name, string channel) => Asset(ids.Of(key), ids.Of(key.StartsWith("a-hero", StringComparison.Ordinal) ? "t-hero" : "t-ace"), name, channel);
        return
        [
            Make("ar-metallica", FolderLevelDto.Artist, "Metallica", null),
            Make("al-load", FolderLevelDto.Album, "Load", "ar-metallica"),
            Make("so-hero", FolderLevelDto.Song, "Hero of the Day", "al-load", Stem("a-hero-drums", "Hero of the Day - drums", "drums"), Stem("a-hero-bass", "Hero of the Day - bass", "bass")),
            Make("ar-motorhead", FolderLevelDto.Artist, "Motörhead", null),
            Make("al-ace", FolderLevelDto.Album, "Ace of Spades", "ar-motorhead"),
            Make("so-ace", FolderLevelDto.Song, "Ace of Spades", "al-ace", Stem("a-ace-snare", "Ace of Spades - snare", "drums.snare"), Stem("a-ace-bass", "Ace of Spades - bass", "bass")),
            Make("ar-unknown", FolderLevelDto.Artist, "Unknown artist", null),
            Make("so-untitled", FolderLevelDto.Song, "Untitled jam", "ar-unknown", Stem("a-jam-bass", "Untitled jam - bass", "bass")),
        ];
    }

    private static AssetDto Asset(Guid id, Guid trackId, string name, string channel) => new()
    {
        Id = id,
        Kind = AssetKindDto.Audio,
        Name = name,
        Status = AssetStatusDto.Ready,
        Progress = 100,
        Audio = new AudioFactsDto { Format = AudioFileFormatDto.Flac, DurationSeconds = 240, PeakDbfs = -1, AppliedGainDb = 0 },
        Provenance = new ProvenanceDto
        {
            Origin = AssetOriginDto.Export,
            TrackId = trackId,
            TrackTitle = name,
            TrackAvailable = true,
            Contents = channel,
            Recipe = new ExportRecipeDto { TrackId = trackId, Mode = ExportModeDto.Raw, Channels = [channel], Format = new ExportFormatDto(AudioFileFormatDto.Flac) },
        },
        SizeBytes = 10_000_000,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    private sealed record Folder(string Id, FolderLevelDto Level, string Name, string? ParentId, IReadOnlyList<AssetDto> Items)
    {
        public FolderRefDto Ref() => new() { Id = Id, Level = Level, Name = Name };

        public LibraryFolderDto Dto() => new()
        {
            Id = Id,
            Level = Level,
            Name = Name,
            NameSource = PlacementSourceDto.Track,
            Counts = new FolderCountsDto { Items = Items.Count },
            SizeBytes = Items.Sum(item => item.SizeBytes),
            LatestAt = DateTimeOffset.UnixEpoch,
            Art = new FolderArtDto { Kind = FolderArtKindDto.Placeholder, Placeholder = new ArtPlaceholderDto { Glyph = Name[..1], Tone = 1 } },
        };
    }
}
