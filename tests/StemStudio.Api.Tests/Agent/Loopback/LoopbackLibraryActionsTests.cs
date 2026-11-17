using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Library;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Agent.Loopback;

/// <summary>
/// 022 T079: <see cref="IStemStudioActions.Library"/> calls the public library endpoints of the in-process API (behind a
/// recording handler) with the Contracts DTOs; problems become readable tool errors.
/// </summary>
public sealed class LoopbackLibraryActionsTests : ApiTestBase
{
    private readonly RecordingHandler _http;
    private readonly LoopbackClient _client;
    private readonly ILibraryActions _library;

    public LoopbackLibraryActionsTests()
    {
        _http = new RecordingHandler(Factory.Server.CreateHandler());
        _client = new LoopbackClient(new HttpClient(_http) { BaseAddress = new Uri("http://127.0.0.1:5000/") });
        _library = new LoopbackActions(_client).Library;
    }

    [Fact]
    public void LoopbackActions_ExposeTheLibraryGroup() =>
        _library.ShouldBeOfType<LoopbackLibraryActions>().Client.ShouldBeSameAs(_client);

    [Fact]
    public async Task Browse_Root_ThenAFolder()
    {
        await FiledItemAsync();

        var root = await _library.BrowseLibraryFolderAsync(new LibraryFolderQuery { Sort = FolderSortDto.Name, Kind = AssetKindDto.Audio, Limit = 20 }, Ct);
        var artist = root.Children.Single();
        var folder = await _library.BrowseLibraryFolderAsync(new LibraryFolderQuery { FolderId = artist.Id }, Ct);

        (root.Folder, artist.Name).ShouldBe((null, "Rick Astley"));
        (folder.Folder!.Id, folder.Folder.Level).ShouldBe((artist.Id, FolderLevelDto.Artist));
        _http.Requests.Select(r => r.PathAndQuery).ShouldBe(["/api/library/folders?sort=name&kind=audio&limit=20", $"/api/library/folders/{artist.Id}"]);
    }

    [Fact]
    public async Task Browse_UnknownFolder_IsAReadableNotFound()
    {
        await FiledItemAsync();

        var error = await Should.ThrowAsync<AgentActionException>(() => _library.BrowseLibraryFolderAsync(new LibraryFolderQuery { FolderId = "ar0000000000000000" }, Ct));

        (error.StatusCode, error.ProblemCode).ShouldBe((404, LibraryProblemTypes.FolderNotFound));
        error.Message.ShouldNotContain("http");
    }

    [Theory]
    [InlineData("../tracks", "/api/library/folders/..%2Ftracks")]
    [InlineData("ar1?status=failed", "/api/library/folders/ar1%3Fstatus%3Dfailed")]
    public async Task Browse_FolderIdFromTheModel_StaysOnePathSegment(string folderId, string expected)
    {
        await Should.ThrowAsync<AgentActionException>(() => _library.BrowseLibraryFolderAsync(new LibraryFolderQuery { FolderId = folderId }, Ct));

        _http.Requests.Single().PathAndQuery.ShouldBe(expected);
    }

    [Fact]
    public async Task GetThenSet_Track_AndTheInverseRestores()
    {
        var item = await FiledItemAsync();
        var target = LibraryInfoTarget.ForTrack(item.TrackId!.Value);

        var before = await _library.GetLibraryInfoAsync(target, Ct);
        var changed = await _library.SetLibraryInfoAsync(target, LibraryInfoApi.Names(album: "Ace of Spades"), Ct);
        var restored = await _library.SetLibraryInfoAsync(target, LibraryInfoApi.Names(before.Artist.Value, before.Album.Value, before.Song.Value), Ct);

        (changed.Album.Effective, changed.MovedItems).ShouldBe(("Ace of Spades", 1));
        (restored.Album.Effective, restored.MovedItems).ShouldBe((before.Album.Effective, 1));
        var path = $"/api/tracks/{target.Id}/library-info";
        _http.Requests.ShouldBe([(HttpMethod.Get, path), (HttpMethod.Put, path), (HttpMethod.Put, path)]);
    }

    [Fact]
    public async Task GetThenSet_Item()
    {
        var item = await FiledItemAsync();
        var target = LibraryInfoTarget.ForItem(item.Id);

        var before = await _library.GetLibraryInfoAsync(target, Ct);
        var changed = await _library.SetLibraryInfoAsync(target, LibraryInfoApi.Names(song: "Jazz take"), Ct);

        before.Placement!.Filed.ShouldBeTrue();
        (changed.Song.Source, changed.Placement!.Song.Name).ShouldBe((PlacementSourceDto.Item, "Jazz take"));
        _http.Requests.Select(r => r.PathAndQuery).Distinct().ShouldBe([$"/api/assets/{item.Id}/library-info"]);
    }

    [Fact]
    public async Task Set_InvalidName_IsAReadableValidationError()
    {
        var item = await FiledItemAsync();

        var error = await Should.ThrowAsync<AgentActionException>(() =>
            _library.SetLibraryInfoAsync(LibraryInfoTarget.ForItem(item.Id), LibraryInfoApi.Names(album: "   "), Ct));

        error.StatusCode.ShouldBe(400);
        error.Message.ShouldContain("album: " + LibraryInfoRules.Message);
    }

    [Fact]
    public async Task Set_OnARecordedTake_ReportsNotFileable()
    {
        var take = Asset.Create(Guid.NewGuid(), "take", ExportProvenance(null) with { Origin = (AssetOrigin)99 }, T0);
        await Factory.AddUnfiledAsync(take);

        var error = await Should.ThrowAsync<AgentActionException>(() =>
            _library.SetLibraryInfoAsync(LibraryInfoTarget.ForItem(take.Id), LibraryInfoRequest.Automatic, Ct));

        (error.StatusCode, error.ProblemCode).ShouldBe((409, LibraryProblemTypes.NotFileable));
    }

    [Fact]
    public async Task UnknownTrack_IsNotFound()
    {
        var error = await Should.ThrowAsync<AgentActionException>(() => _library.GetLibraryInfoAsync(LibraryInfoTarget.ForTrack(Guid.NewGuid()), Ct));

        error.IsNotFound.ShouldBeTrue();
    }

    [Fact]
    public async Task NullArguments_AreRejected()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _library.BrowseLibraryFolderAsync(null!, Ct));
        await Should.ThrowAsync<ArgumentNullException>(() => _library.SearchLibraryAsync(null!, Ct));
        await Should.ThrowAsync<ArgumentNullException>(() => _library.GetLibraryInfoAsync(null!, Ct));
        await Should.ThrowAsync<ArgumentNullException>(() => _library.SetLibraryInfoAsync(LibraryInfoTarget.ForTrack(Guid.NewGuid()), null!, Ct));
    }

    private async Task<Asset> FiledItemAsync()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var item = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);
        return item;
    }

    private sealed class RecordingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<(HttpMethod Method, string PathAndQuery)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery));
            return base.SendAsync(request, cancellationToken);
        }
    }
}
