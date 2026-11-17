using System.Text.Json;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Fully populated library DTOs (every optional member set) for the schema and round-trip tests; consumer lanes may copy them
/// for their fakes (frontend fake API client, ai-agent fake actions).
/// </summary>
public sealed partial class LibraryContractTests
{
    /// <summary>The nine operations of the <c>library</c> tag (backend-api 1.22.0).</summary>
    public static readonly string[] LibraryOperations =
    [
        "getLibraryRoot", "getLibraryFolder", "searchLibrary",
        "getTrackLibraryInfo", "setTrackLibraryInfo", "resetTrackLibraryInfo",
        "getAssetLibraryInfo", "setAssetLibraryInfo", "resetAssetLibraryInfo",
    ];

    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static readonly DateTimeOffset Latest = new(2026, 11, 16, 10, 12, 0, TimeSpan.Zero);

    public static TheoryData<string, object> Samples => new()
    {
        { "FolderRef", ArtistRef },
        { "FolderCounts", new FolderCountsDto { Albums = 2, Songs = 3, Items = 7 } },
        { "ArtImage", Image },
        { "ArtPlaceholder", new ArtPlaceholderDto { Glyph = "M", Tone = 3 } },
        { "FolderArt", Mosaic },
        { "LibraryFolder", ArtistFolder },
        { "LibraryFolderView", SongView },
        { "Highlight", InstrumentHighlight },
        { "FolderHit", new FolderHitDto { Folder = ArtistFolder, Path = [], Highlights = [new HighlightDto { Field = SearchFieldDto.Artist, Start = 0, Length = 5 }] } },
        { "ItemHit", ItemHit },
        { "SearchTotals", new SearchTotalsDto { Artists = 1, Albums = 0, Songs = 0, Items = 1 } },
        { "LibrarySearchResult", SearchResult },
        { "LibraryInfoRequest", new LibraryInfoRequest { Artist = null, Album = "Ace of Spades", Song = null } },
        { "LibraryInfoLevel", AlbumLevel },
        { "LibraryInfoState", InfoState },
        { "PlacementLevel", Placement.Artist },
        { "Placement", Placement },
    };

    private static FolderRefDto ArtistRef => new() { Id = "ar1b2c3d4e5f607182", Level = FolderLevelDto.Artist, Name = "Metallica" };

    private static FolderRefDto AlbumRef => new() { Id = "al2c3d4e5f60718293", Level = FolderLevelDto.Album, Name = "Master of Puppets" };

    private static FolderRefDto SongRef => new() { Id = "so0a1b2c3d4e5f6071", Level = FolderLevelDto.Song, Name = "Battery" };

    private static ArtImageDto Image => new()
    {
        ThumbUrl = $"/api/tracks/{TrackId}/artwork/thumb?v=4f2a9c0e1b3d",
        CoverUrl = $"/api/tracks/{TrackId}/artwork/cover?v=4f2a9c0e1b3d",
    };

    private static FolderArtDto Mosaic => new()
    {
        Kind = FolderArtKindDto.Mosaic,
        Images = [Image, Image with { ThumbUrl = Image.ThumbUrl.Replace("4f2a", "8e1d", StringComparison.Ordinal) }],
        Placeholder = new ArtPlaceholderDto { Glyph = "M", Tone = 3 },
    };

    private static LibraryFolderDto ArtistFolder => new()
    {
        Id = ArtistRef.Id,
        Level = FolderLevelDto.Artist,
        Name = "Metallica",
        NameSource = PlacementSourceDto.Match,
        Year = null,
        Counts = new FolderCountsDto { Albums = 2, Songs = 3, Items = 7 },
        SizeBytes = 214_958_080,
        LatestAt = Latest,
        Art = Mosaic,
        TrackIds = [],
    };

    private static LibraryFolderDto SongFolder => ArtistFolder with
    {
        Id = SongRef.Id,
        Level = FolderLevelDto.Song,
        Name = "Battery",
        Counts = new FolderCountsDto { Items = 1 },
        Art = new FolderArtDto { Kind = FolderArtKindDto.Cover, Images = [Image], Placeholder = new ArtPlaceholderDto { Glyph = "B", Tone = 5 } },
        TrackIds = [TrackId],
    };

    private static PlacementDto Placement => new()
    {
        Artist = new PlacementLevelDto { FolderId = ArtistRef.Id, Name = "Metallica", Source = PlacementSourceDto.Match },
        Album = new PlacementLevelDto { FolderId = AlbumRef.Id, Name = "Master of Puppets", Source = PlacementSourceDto.Track },
        Song = new PlacementLevelDto { FolderId = SongRef.Id, Name = "Battery", Source = PlacementSourceDto.Item },
        Year = 1986,
        Filed = true,
    };

    private static AssetDto Item => Example("ReadyAsset").Deserialize<AssetDto>(Web)! with { Placement = Placement };

    private static LibraryFolderViewDto SongView => new()
    {
        Revision = "3f9a0c1d2e4b5a69",
        Folder = SongFolder,
        Breadcrumb = [ArtistRef, AlbumRef],
        Children = [],
        ChildrenTotal = 0,
        Items = [Item],
        TotalItems = 1,
        TotalSizeBytes = Item.SizeBytes,
        PendingFiling = 0,
    };

    private static HighlightDto InstrumentHighlight => new() { Field = SearchFieldDto.Instrument, Start = 0, Length = 5, Label = "Drums" };

    private static ItemHitDto ItemHit => new()
    {
        Asset = Item,
        Path = [ArtistRef, AlbumRef, SongRef],
        Highlights = [InstrumentHighlight, new HighlightDto { Field = SearchFieldDto.Song, Start = 0, Length = 4 }],
        MatchedInstruments = ["Drums"],
    };

    private static LibrarySearchResultDto SearchResult => new()
    {
        Query = "batt drums",
        Tokens = ["batt", "drums"],
        Revision = "3f9a0c1d2e4b5a69",
        Artists = [],
        Albums = [],
        Songs = [new FolderHitDto { Folder = SongFolder, Path = [ArtistRef, AlbumRef], Highlights = [new HighlightDto { Field = SearchFieldDto.Song, Start = 0, Length = 4 }] }],
        Items = [ItemHit],
        Totals = new SearchTotalsDto { Artists = 0, Albums = 0, Songs = 1, Items = 1 },
    };

    private static LibraryInfoLevelDto AlbumLevel => new()
    {
        Value = "Master of Puppets",
        Effective = "Master of Puppets",
        Source = PlacementSourceDto.Track,
        Automatic = "Unknown album",
    };

    private static LibraryInfoStateDto InfoState => new()
    {
        Artist = new LibraryInfoLevelDto { Value = null, Effective = "Metallica", Source = PlacementSourceDto.Match, Automatic = "Metallica" },
        Album = AlbumLevel,
        Song = new LibraryInfoLevelDto { Value = "Battery", Effective = "Battery", Source = PlacementSourceDto.Item, Automatic = "Battery (Remastered)" },
        Placement = Placement,
        MovedItems = 1,
    };
}
