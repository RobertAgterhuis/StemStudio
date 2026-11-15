using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using static StemStudio.Api.Tests.Assets.AssetTestData;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Feature 007 T049: the library: list, rename, delete, retry (FR-007..FR-010).</summary>
public sealed class LibraryTests : ApiTestBase
{
    [Fact]
    public async Task List_IsNewestFirstWithTotalsAndFilters()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var other = await Factory.SeedAsync(NamedTrack("aaaaaaaaaaa"));
        var ready = await Factory.ReadyDrumsAsync(Client, track.Id);
        Factory.Clock.Advance(TimeSpan.FromMilliseconds(10));
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Running, 0.1));
        var queued = await CreateAsync(Raw(other.Id, "bass"));

        using var all = await ListAsync(string.Empty);
        var page = await all.BodyAsync<AssetListDto>();

        await ContractAssert.MatchesAsync(all, "listAssets");
        page.Items.Select(item => item.Id).ShouldBe([queued.Id, ready.Id]);
        (page.Total, page.TotalSizeBytes).ShouldBe((2, (long)RenderedBytes.Length));
        (await PageAsync($"?trackId={track.Id}")).Items.Single().Id.ShouldBe(ready.Id);
        (await PageAsync("?status=queued")).Items.Single().Id.ShouldBe(queued.Id);
        (await PageAsync("?q=BASS")).Items.Single().Id.ShouldBe(queued.Id);
        (await PageAsync("?kind=score")).Total.ShouldBe(0);
        var second = await PageAsync("?limit=1&offset=1");
        (second.Items.Single().Id, second.Total).ShouldBe((ready.Id, 2));
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=201")]
    [InlineData("?offset=-1")]
    [InlineData("?status=done")]
    public async Task List_InvalidFiltersAre422(string query)
    {
        using var response = await ListAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "listAssets");
    }

    [Fact]
    public async Task List_ALongSearchTextIs422()
    {
        using var response = await ListAsync("?q=" + new string('q', 101));

        (await response.FieldErrorsAsync("q")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Rename_StoresTheTrimmedName()
    {
        var asset = await CreateDrumsAsync();

        using var response = await PatchAsync(asset.Id, "  Drums for practice ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "renameAsset");
        (await response.BodyAsync<AssetDto>()).Name.ShouldBe("Drums for practice");
    }

    [Fact]
    public async Task Rename_InvalidNamesAre422AndUnknownItemsAre404()
    {
        var asset = await CreateDrumsAsync();

        using var invalid = await PatchAsync(asset.Id, " ");
        using var unknown = await PatchAsync(Guid.NewGuid(), "x");

        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(invalid, "renameAsset");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "renameAsset");
    }

    [Fact]
    public async Task Delete_RemovesRowAndFilesAndCancelsAnActiveRender()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var ready = await Factory.ReadyDrumsAsync(Client, track.Id);
        var active = await CreateAsync(Raw(track.Id, "bass"));
        var deletesBefore = Worker.RenderDeletes.Count;

        using var deleteReady = await DeleteAsync(ready.Id);
        using var deleteActive = await DeleteAsync(active.Id);

        (deleteReady.StatusCode, deleteActive.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NoContent));
        await ContractAssert.MatchesAsync(deleteReady, "deleteAsset");
        Directory.Exists(Path.Combine(Factory.DataDir, "assets", ready.Id.ToString())).ShouldBeFalse();
        Worker.RenderDeletes.Count.ShouldBe(deletesBefore + 1);
        (await PageAsync(string.Empty)).Total.ShouldBe(0);
        await Factory.SyncAssetsAsync();
    }

    [Fact]
    public async Task Delete_AnUnreachableWorkerStillDeletesTheItem()
    {
        var asset = await CreateDrumsAsync();
        Worker.OnDeleteRender = _ => throw new WorkerUnavailableException();

        using var response = await DeleteAsync(asset.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_UnknownIs404()
    {
        using var response = await DeleteAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "deleteAsset");
    }

    [Fact]
    public async Task Retry_RendersAFailedItemAgain()
    {
        var asset = await FailedDrumsAsync();

        using var response = await RetryAsync(asset.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "retryAsset");
        var retried = await response.BodyAsync<AssetDto>();
        (retried.Status, retried.Error).ShouldBe((AssetStatusDto.Queued, null));
        Worker.RenderCreates.Count.ShouldBe(2);
        Worker.RenderCreates[1].Inputs.ShouldBe(Worker.RenderCreates[0].Inputs);
    }

    [Fact]
    public async Task Retry_OfAnItemThatIsNotFailedIs409()
    {
        var asset = await CreateDrumsAsync();

        using var response = await RetryAsync(asset.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "retryAsset");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("asset-not-failed");
    }

    [Fact]
    public async Task Retry_AfterTheTrackWasDeletedIs409SourceUnavailable()
    {
        var asset = await FailedDrumsAsync();
        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(asset.Provenance.TrackId!.Value), UriKind.Relative), Ct)).Dispose();

        using var response = await RetryAsync(asset.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "retryAsset");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("source-unavailable");
    }

    [Fact]
    public async Task Retry_WithAnUnreachableWorkerIs503AndStaysFailed()
    {
        var asset = await FailedDrumsAsync();
        Worker.OnCreateRender = _ => throw new WorkerUnavailableException();

        using var response = await RetryAsync(asset.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "retryAsset");
        (await GetAsync(asset.Id)).Status.ShouldBe(AssetStatusDto.Failed);
    }

    [Fact]
    public async Task Retry_UnknownIs404()
    {
        using var response = await RetryAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "retryAsset");
    }

    [Fact]
    public async Task DeletingTheTrack_KeepsItsItemsAsUnavailable()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var asset = await Factory.ReadyDrumsAsync(Client, track.Id);

        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        var kept = await GetAsync(asset.Id);
        (kept.Status, kept.Provenance.TrackAvailable, kept.Provenance.TrackTitle).ShouldBe((AssetStatusDto.Ready, false, "Never Gonna Give You Up"));
        (await PageAsync($"?trackId={track.Id}")).Items.Single().Provenance.TrackAvailable.ShouldBeFalse();
    }

    private async Task<AssetDto> CreateDrumsAsync()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        return await CreateAsync(Raw(track.Id, "drums"));
    }

    private async Task<AssetDto> FailedDrumsAsync()
    {
        var asset = await CreateDrumsAsync();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));
        await Factory.SyncAssetsAsync();
        Worker.OnGetRender = null;
        return asset;
    }

    private async Task<AssetDto> CreateAsync(CreateAudioExportRequest request) => await (await Client.PostExportAsync(request)).BodyAsync<AssetDto>();

    private async Task<AssetDto> GetAsync(Guid id) => await (await Client.GetAsync(new Uri(ApiRoutes.Asset(id), UriKind.Relative), Ct)).BodyAsync<AssetDto>();

    private Task<HttpResponseMessage> ListAsync(string query) => Client.GetAsync(new Uri(ApiRoutes.Assets + query, UriKind.Relative), Ct);

    private async Task<AssetListDto> PageAsync(string query) => await (await ListAsync(query)).BodyAsync<AssetListDto>();

    private Task<HttpResponseMessage> PatchAsync(Guid id, string name) =>
        Client.PatchAsJsonAsync(new Uri(ApiRoutes.Asset(id), UriKind.Relative), new RenameAssetRequest(name), Ct);

    private Task<HttpResponseMessage> DeleteAsync(Guid id) => Client.DeleteAsync(new Uri(ApiRoutes.Asset(id), UriKind.Relative), Ct);

    private Task<HttpResponseMessage> RetryAsync(Guid id) => Client.PostAsync(new Uri(ApiRoutes.RetryAsset(id), UriKind.Relative), null, Ct);
}
