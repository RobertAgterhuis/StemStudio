using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using static StemStudio.Api.Tests.Assets.AssetTestData;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Feature 007 T059: <c>POST /api/assets/stem-packs</c> (FR-013).</summary>
public sealed class StemPackTests : IDisposable
{
    private static readonly ExportFormatDto Flac = new(AudioFileFormatDto.Flac);
    private readonly FakeDiskSpace _disk = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public StemPackTests()
    {
        _factory = new ApiFactory { ConfigureServices = services => services.AddSingleton<IDiskSpace>(_disk) };
        _client = _factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task APack_HasOneRawItemPerStemInCatalogueOrder()
    {
        var track = await _factory.SeedAsync(NamedTrack());

        using var response = await PostAsync(new CreateStemPackRequest(track.Id, Flac));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createStemPack");
        var pack = await response.BodyAsync<StemPackDto>();
        pack.Items.Select(item => item.Provenance.Contents).ShouldBe(["vocals, raw", "drums, raw", "bass, raw", "guitar, raw", "piano, raw", "other, raw"]);
        pack.Items.ShouldAllBe(item => item.Status == AssetStatusDto.Queued && item.Provenance.Recipe.Format.BitDepth == 16);
        pack.Items[0].Name.ShouldBe("Never Gonna Give You Up - vocals");
        _factory.Worker.RenderCreates.Select(render => render.Inputs.Single().Path[^11..]).ShouldBe(["vocals.flac", "/drums.flac", "s/bass.flac", "guitar.flac", "/piano.flac", "/other.flac"]);
    }

    [Fact]
    public async Task WithDetail_AddsTheKeptDetailStemsOfSplitParents()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        var pack = await (await PostAsync(new CreateStemPackRequest(track.Id, Flac, IncludeDetail: true))).BodyAsync<StemPackDto>();

        pack.Items.Select(item => item.Provenance.Recipe.Channels!.Single()).ShouldBe(
            ["vocals", "drums", "bass", "guitar", "piano", "other", "vocals.lead", "vocals.backing", "vocals.rest", "drums.kick", "drums.snare", "drums.rest"]);
    }

    [Fact]
    public async Task WithoutDetail_LeavesDetailStemsOut()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        var pack = await (await PostAsync(new CreateStemPackRequest(track.Id, new ExportFormatDto(AudioFileFormatDto.Mp3)))).BodyAsync<StemPackDto>();

        pack.Items.Count.ShouldBe(6);
        pack.Items[0].Provenance.Recipe.Format.ShouldBe(new ExportFormatDto(AudioFileFormatDto.Mp3, null, 320));
    }

    [Fact]
    public async Task TheDiskGuard_AppliesOnceForThePack()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        _disk.Available = 1;

        using var response = await PostAsync(new CreateStemPackRequest(track.Id, Flac));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "createStemPack");
        _factory.Worker.RenderCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownAndUnfinishedTracks_Are404And409()
    {
        var processing = await _factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));

        using var unknown = await PostAsync(new CreateStemPackRequest(Guid.NewGuid(), Flac));
        using var notReady = await PostAsync(new CreateStemPackRequest(processing.Id, Flac));

        (unknown.StatusCode, notReady.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.Conflict));
        await ContractAssert.MatchesAsync(unknown, "createStemPack");
        await ContractAssert.MatchesAsync(notReady, "createStemPack");
    }

    [Fact]
    public async Task AnInvalidFormat_Is422()
    {
        var track = await _factory.SeedAsync(NamedTrack());

        using var response = await PostAsync(new CreateStemPackRequest(track.Id, new ExportFormatDto(AudioFileFormatDto.Wav, 8)));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createStemPack");
    }

    [Fact]
    public async Task AnUnreachableWorker_Is503AndStoresNothing()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        _factory.Worker.OnCreateRender = _ => throw new WorkerUnavailableException();

        using var response = await PostAsync(new CreateStemPackRequest(track.Id, Flac));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "createStemPack");
        (await (await _client.GetAsync(new Uri(ApiRoutes.Assets, UriKind.Relative), Ct)).BodyAsync<AssetListDto>()).Total.ShouldBe(0);
    }

    private Task<HttpResponseMessage> PostAsync(CreateStemPackRequest request) =>
        _client.PostAsJsonAsync(new Uri(ApiRoutes.StemPacks, UriKind.Relative), request, Ct);
}
