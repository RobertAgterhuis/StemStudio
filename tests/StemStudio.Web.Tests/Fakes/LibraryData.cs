using System.Text.Json;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>
/// Contract-shaped library folder data (backend-api 1.22.0, tag <c>library</c>): the response examples <c>RootView</c>,
/// <c>SongView</c>, <c>EmptySearch</c>, the problem examples and the <c>Asset</c> example placement, copied from
/// <c>specs/022-library-folders/contracts/backend-api.library-folders.openapi.yaml</c> (consumer-driven fakes), plus
/// builders for component tests.
/// </summary>
public static class LibraryData
{
    public const string Revision = "3f9a0c1d2e4b5a69";
    public const string MetallicaId = "ar1b2c3d4e5f607182";
    public const string UnknownArtistId = "ar9f8e7d6c5b4a3921";
    public const string AlbumId = "al2c3d4e5f60718293";
    public const string SongId = "so0a1b2c3d4e5f6071";
    public const string RickArtistId = "arb385da54bfe8c6a9";
    public const string RickAlbumId = "alb7d8a7e8ed84d5af";
    public const string RickSongId = "soc02cef10277ea444";
    public static readonly Guid BatteryTrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    public const string RootViewJson = """
        {"revision":"3f9a0c1d2e4b5a69","folder":null,"breadcrumb":[],"children":[
          {"id":"ar1b2c3d4e5f607182","level":"artist","name":"Metallica","nameSource":"match","year":null,
           "counts":{"albums":2,"songs":3,"items":7},"sizeBytes":214958080,"latestAt":"2026-11-16T10:12:00Z",
           "art":{"kind":"mosaic","images":[
             {"thumbUrl":"/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/artwork/thumb?v=4f2a9c0e1b3d","coverUrl":"/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/artwork/cover?v=4f2a9c0e1b3d"},
             {"thumbUrl":"/api/tracks/5d1c2b3a-4e5f-4a6b-8c7d-9e0f1a2b3c4d/artwork/thumb?v=8e1d2c3b4a59","coverUrl":"/api/tracks/5d1c2b3a-4e5f-4a6b-8c7d-9e0f1a2b3c4d/artwork/cover?v=8e1d2c3b4a59"}],
             "placeholder":{"glyph":"M","tone":3}},"trackIds":[]},
          {"id":"ar9f8e7d6c5b4a3921","level":"artist","name":"Unknown artist","nameSource":"fallback","year":null,
           "counts":{"albums":1,"songs":1,"items":1},"sizeBytes":30410752,"latestAt":"2026-11-15T19:40:00Z",
           "art":{"kind":"placeholder","images":[],"placeholder":{"glyph":"?","tone":0}},"trackIds":[]}],
         "childrenTotal":2,"items":[],"totalItems":8,"totalSizeBytes":245368832,"pendingFiling":0}
        """;

    public const string SongViewJson = """
        {"revision":"3f9a0c1d2e4b5a69","folder":{"id":"so0a1b2c3d4e5f6071","level":"song","name":"Battery","nameSource":"match",
          "year":null,"counts":{"items":3},"sizeBytes":92274688,"latestAt":"2026-11-16T10:12:00Z",
          "art":{"kind":"cover","images":[{"thumbUrl":"/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/artwork/thumb?v=4f2a9c0e1b3d","coverUrl":"/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/artwork/cover?v=4f2a9c0e1b3d"}],
            "placeholder":{"glyph":"B","tone":5}},"trackIds":["0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"]},
         "breadcrumb":[{"id":"ar1b2c3d4e5f607182","level":"artist","name":"Metallica"},{"id":"al2c3d4e5f60718293","level":"album","name":"Master of Puppets"}],
         "children":[],"childrenTotal":0,"items":[],"totalItems":3,"totalSizeBytes":92274688,"pendingFiling":0}
        """;

    public const string EmptySearchJson = """
        {"query":"zzz","tokens":["zzz"],"revision":"3f9a0c1d2e4b5a69","artists":[],"albums":[],"songs":[],"items":[],
         "totals":{"artists":0,"albums":0,"songs":0,"items":0}}
        """;

    public const string FolderNotFoundJson = """
        {"type":"https://stemstudio.local/problems/folder-not-found","title":"This folder is empty now","status":404,"detail":"Its items were moved or deleted."}
        """;

    public const string LibraryQueryInvalidJson = """
        {"type":"https://stemstudio.local/problems/library-query-invalid","title":"One or more validation errors occurred.","status":422,"errors":{"q":["Type at least one letter or digit."]}}
        """;

    public const string LibraryInfoInvalidJson = """
        {"title":"One or more validation errors occurred.","status":400,"errors":{"album":["Use 1 to 200 characters, or leave it automatic."]}}
        """;

    public const string NotFileableJson = """
        {"type":"https://stemstudio.local/problems/not-fileable","title":"Recorded takes are not filed","status":409,"detail":"Recorded takes stay with their track."}
        """;

    public const string MaintenanceJson = """
        {"type":"https://stemstudio.local/problems/library-maintenance","title":"The library is being restored","status":503}
        """;

    public static LibraryFolderViewDto RootView() => Parse<LibraryFolderViewDto>(RootViewJson);

    public static LibraryFolderViewDto SongView() => Parse<LibraryFolderViewDto>(SongViewJson);

    public static LibrarySearchResultDto EmptySearch() => Parse<LibrarySearchResultDto>(EmptySearchJson);

    public static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, StemStudioApiClient.Json)!;

    public static string Json(object value) => JsonSerializer.Serialize(value, value.GetType(), StemStudioApiClient.Json);

    /// <summary>The placement of the six 1.22.0 <c>Asset</c> examples (Rick Astley > Unknown album > Never Gonna Give You Up).</summary>
    public static PlacementDto Placement(string songId = RickSongId, string song = "Never Gonna Give You Up", bool filed = true) => new()
    {
        Artist = new PlacementLevelDto { FolderId = RickArtistId, Name = "Rick Astley", Source = PlacementSourceDto.Import },
        Album = new PlacementLevelDto { FolderId = RickAlbumId, Name = "Unknown album", Source = PlacementSourceDto.Fallback },
        Song = new PlacementLevelDto { FolderId = songId, Name = song, Source = PlacementSourceDto.Import },
        Year = null,
        Filed = filed,
    };

    /// <summary>A library item with the example placement.</summary>
    public static AssetDto Asset(Guid? id = null, AssetStatusDto status = AssetStatusDto.Ready, string name = "Never Gonna Give You Up - bass") =>
        AssetData.Asset(id, status, name) with { Placement = Placement() };

    public static LibraryFolderDto Folder(string id, string name, FolderArtDto? art = null, int items = 1, int? year = null, IReadOnlyList<Guid>? trackIds = null) => new()
    {
        Id = id,
        Level = StemStudio.Web.Library.LibraryLocation.LevelOf(id),
        Name = name,
        NameSource = PlacementSourceDto.Match,
        Year = year,
        Counts = StemStudio.Web.Library.LibraryLocation.LevelOf(id) switch
        {
            FolderLevelDto.Artist => new FolderCountsDto { Albums = 3, Songs = 5, Items = items },
            FolderLevelDto.Album => new FolderCountsDto { Songs = 2, Items = items },
            _ => new FolderCountsDto { Items = items },
        },
        SizeBytes = 12_700_000L * items,
        LatestAt = TestData.Created,
        Art = art ?? Placeholder(name[..1]),
        TrackIds = trackIds ?? [],
    };

    public static FolderArtDto Placeholder(string glyph = "M", int tone = 3) => new()
    {
        Kind = FolderArtKindDto.Placeholder,
        Placeholder = new ArtPlaceholderDto { Glyph = glyph, Tone = tone },
    };

    public static FolderArtDto Cover(int images = 1) => new()
    {
        Kind = images == 1 ? FolderArtKindDto.Cover : FolderArtKindDto.Mosaic,
        Images = [.. Enumerable.Range(1, images).Select(Image)],
        Placeholder = new ArtPlaceholderDto { Glyph = "M", Tone = 3 },
    };

    public static ArtImageDto Image(int n) => new()
    {
        ThumbUrl = $"/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0{n}/artwork/thumb?v=4f2a",
        CoverUrl = $"/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0{n}/artwork/cover?v=4f2a",
    };

    public static FolderRefDto Ref(string id, string name) => new() { Id = id, Level = StemStudio.Web.Library.LibraryLocation.LevelOf(id), Name = name };

    /// <summary>A view of a folder (null = root) with its children and items.</summary>
    public static LibraryFolderViewDto View(LibraryFolderDto? folder, IReadOnlyList<FolderRefDto>? breadcrumb = null, IReadOnlyList<LibraryFolderDto>? children = null,
        IReadOnlyList<AssetDto>? items = null, int pending = 0, string revision = Revision) => new()
        {
            Revision = revision,
            Folder = folder,
            Breadcrumb = breadcrumb ?? [],
            Children = children ?? [],
            ChildrenTotal = children?.Count ?? 0,
            Items = items ?? [],
            TotalItems = items?.Count ?? children?.Sum(c => c.Counts.Items) ?? 0,
            TotalSizeBytes = 245_368_832,
            PendingFiling = pending,
        };

    public static LibraryInfoLevelDto Level(string automatic, string? value = null, PlacementSourceDto source = PlacementSourceDto.Import) => new()
    {
        Value = value,
        Effective = value ?? automatic,
        Source = value is null ? source : PlacementSourceDto.Track,
        Automatic = automatic,
    };

    public static LibraryInfoStateDto InfoState(string? album = null, int moved = 0) => new()
    {
        Artist = Level("Rick Astley"),
        Album = Level("Unknown album", album, PlacementSourceDto.Fallback),
        Song = Level("Never Gonna Give You Up"),
        MovedItems = moved,
    };
}
