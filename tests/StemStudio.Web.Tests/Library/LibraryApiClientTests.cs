using System.Net;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>The nine library operations of the typed client (022 T018, backend contract 1.22.0) against the contract examples.</summary>
public sealed class LibraryApiClientTests : IDisposable
{
    private readonly StubHttpMessageHandler _stub = new();
    private readonly HeaderRecorder _headers;
    private readonly HttpClient _http;
    private readonly StemStudioApiClient _client;

    public LibraryApiClientTests()
    {
        _headers = new HeaderRecorder(_stub);
        _http = new HttpClient(_headers) { BaseAddress = new Uri("http://api.test") };
        _client = new StemStudioApiClient(_http);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _http.Dispose();
        _headers.Dispose();
        _stub.Dispose();
    }

    [Fact]
    public async Task Root_ReadsTheRootViewExample()
    {
        _stub.Respond(HttpStatusCode.OK, LibraryData.RootViewJson);

        var view = await _client.GetLibraryRootAsync(LibraryFolderRequest.Default, cancellationToken: Ct);

        view!.Folder.ShouldBeNull();
        view.Children.Select(c => c.Name).ShouldBe(["Metallica", "Unknown artist"]);
        view.Children[0].Art.Kind.ShouldBe(FolderArtKindDto.Mosaic);
        view.Children[0].Counts.Albums.ShouldBe(2);
        _stub.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/library/folders");
        _headers.IfNoneMatch.ShouldBe([null]);
    }

    [Fact]
    public async Task Folder_SendsSortFiltersAndPaging()
    {
        _stub.Respond(HttpStatusCode.OK, LibraryData.SongViewJson);
        var request = new LibraryFolderRequest(FolderSortDto.Recent, AssetKindDto.Score, AssetStatusDto.Ready, 50, 100);

        var view = await _client.GetLibraryFolderAsync(LibraryData.SongId, request, cancellationToken: Ct);

        view!.Folder!.Name.ShouldBe("Battery");
        view.Breadcrumb.Select(b => b.Name).ShouldBe(["Metallica", "Master of Puppets"]);
        view.Folder.TrackIds.ShouldBe([LibraryData.BatteryTrackId]);
        _stub.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe($"/api/library/folders/{LibraryData.SongId}?sort=recent&kind=score&status=ready&limit=50&offset=100");
    }

    [Fact]
    public async Task KnownRevision_IsSentAsIfNoneMatch_And304GivesNull()
    {
        _stub.Respond(HttpStatusCode.NotModified).Respond(HttpStatusCode.NotModified);

        var root = await _client.GetLibraryRootAsync(LibraryFolderRequest.Default, LibraryData.Revision, Ct);
        var folder = await _client.GetLibraryFolderAsync(LibraryData.SongId, new LibraryFolderRequest(FolderSortDto.Name), LibraryData.Revision, Ct);

        root.ShouldBeNull();
        folder.ShouldBeNull();
        _headers.IfNoneMatch.ShouldBe(["\"lib-3f9a0c1d2e4b5a69\"", "\"lib-3f9a0c1d2e4b5a69\""]);
    }

    [Fact]
    public async Task UnknownFolder_Throws404FolderNotFound()
    {
        _stub.RespondProblem(HttpStatusCode.NotFound, LibraryData.FolderNotFoundJson);

        var ex = await Should.ThrowAsync<ApiException>(() => _client.GetLibraryFolderAsync(LibraryData.SongId, LibraryFolderRequest.Default, cancellationToken: Ct));

        ex.Error.IsNotFound.ShouldBeTrue();
        ex.Error.Code.ShouldBe(LibraryProblemTypes.FolderNotFound);
        ex.Error.UserMessage.ShouldBe("Its items were moved or deleted.");
    }

    [Fact]
    public async Task EmptyBody_Throws()
    {
        _stub.Respond(HttpStatusCode.OK, "null");

        await Should.ThrowAsync<ApiException>(() => _client.GetLibraryRootAsync(LibraryFolderRequest.Default, cancellationToken: Ct));
    }

    [Fact]
    public async Task Search_SendsTextScopeAndLimit_EscapedAndReadsTheExample()
    {
        _stub.Respond(HttpStatusCode.OK, LibraryData.EmptySearchJson);

        var result = await _client.SearchLibraryAsync(new LibrarySearchRequest("metallica bass & co", LibraryData.MetallicaId, 50), Ct);

        result.Tokens.ShouldBe(["zzz"]);
        result.Totals.Items.ShouldBe(0);
        _stub.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe($"/api/library/search?q=metallica%20bass%20%26%20co&within={LibraryData.MetallicaId}&limit=50");
    }

    [Fact]
    public async Task InvalidQuery_MapsThe422FieldMessage()
    {
        _stub.RespondProblem(HttpStatusCode.UnprocessableEntity, LibraryData.LibraryQueryInvalidJson);

        var ex = await Should.ThrowAsync<ApiException>(() => _client.SearchLibraryAsync(new LibrarySearchRequest("!!"), Ct));

        ex.Error.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        ex.Error.Code.ShouldBe(LibraryProblemTypes.LibraryQueryInvalid);
        ex.Error.FieldErrors["q"].ShouldBe(["Type at least one letter or digit."]);
        ex.Error.UserMessage.ShouldBe("Type at least one letter or digit.");
    }

    [Fact]
    public async Task TrackLibraryInfo_GetPutDelete()
    {
        var state = LibraryData.Json(LibraryData.InfoState("Ace of Spades", 3));
        _stub.Respond(HttpStatusCode.OK, state).Respond(HttpStatusCode.OK, state).Respond(HttpStatusCode.OK, state);
        var request = new LibraryInfoRequest { Artist = null, Album = "Ace of Spades", Song = null };

        (await _client.GetTrackLibraryInfoAsync(TestData.TrackId, Ct)).Album.Effective.ShouldBe("Ace of Spades");
        (await _client.SetTrackLibraryInfoAsync(TestData.TrackId, request, Ct)).MovedItems.ShouldBe(3);
        await _client.ResetTrackLibraryInfoAsync(TestData.TrackId, Ct);

        var path = $"/api/tracks/{TestData.TrackId}/library-info";
        _stub.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe([(HttpMethod.Get, path), (HttpMethod.Put, path), (HttpMethod.Delete, path)]);
        _stub.Requests[1].Body.ShouldBe("{\"artist\":null,\"album\":\"Ace of Spades\",\"song\":null}");
        _stub.Requests[1].ContentType.ShouldBe("application/json");
    }

    [Fact]
    public async Task AssetLibraryInfo_GetPutDelete()
    {
        var state = LibraryData.Json(LibraryData.InfoState() with { Placement = LibraryData.Placement() });
        _stub.Respond(HttpStatusCode.OK, state).Respond(HttpStatusCode.OK, state).Respond(HttpStatusCode.OK, state);

        (await _client.GetAssetLibraryInfoAsync(AssetData.AssetId, Ct)).Placement!.Song.FolderId.ShouldBe(LibraryData.RickSongId);
        await _client.SetAssetLibraryInfoAsync(AssetData.AssetId, LibraryInfoRequest.Automatic, Ct);
        await _client.ResetAssetLibraryInfoAsync(AssetData.AssetId, Ct);

        var path = $"/api/assets/{AssetData.AssetId}/library-info";
        _stub.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe([(HttpMethod.Get, path), (HttpMethod.Put, path), (HttpMethod.Delete, path)]);
        _stub.Requests[1].Body.ShouldBe("{\"artist\":null,\"album\":null,\"song\":null}");
    }

    [Fact]
    public async Task InvalidLibraryInfo_MapsThe400FieldMessage()
    {
        _stub.RespondProblem(HttpStatusCode.BadRequest, LibraryData.LibraryInfoInvalidJson);

        var ex = await Should.ThrowAsync<ApiException>(() =>
            _client.SetTrackLibraryInfoAsync(TestData.TrackId, new LibraryInfoRequest { Artist = null, Album = " ", Song = null }, Ct));

        ex.Error.FieldErrors["album"].ShouldBe(["Use 1 to 200 characters, or leave it automatic."]);
    }

    [Fact]
    public async Task RecordedTake_Throws409NotFileable()
    {
        _stub.RespondProblem(HttpStatusCode.Conflict, LibraryData.NotFileableJson);

        var ex = await Should.ThrowAsync<ApiException>(() => _client.ResetAssetLibraryInfoAsync(AssetData.AssetId, Ct));

        ex.Error.IsConflict.ShouldBeTrue();
        ex.Error.Code.ShouldBe(LibraryProblemTypes.NotFileable);
    }

    /// <summary>Records the <c>If-None-Match</c> header of every request, then forwards it to the stub.</summary>
    private sealed class HeaderRecorder(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<string?> IfNoneMatch { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            IfNoneMatch.Add(request.Headers.TryGetValues("If-None-Match", out var values) ? string.Join(",", values) : null);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
