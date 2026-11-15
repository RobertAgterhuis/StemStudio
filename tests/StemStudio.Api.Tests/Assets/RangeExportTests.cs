using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Assets;
using static StemStudio.Api.Tests.Assets.AssetTestData;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Feature 007 T054: exports of a time range (FR-006).</summary>
public sealed class RangeExportTests : ApiTestBase
{
    [Fact]
    public async Task ARange_IsRenderedAndNamedWithItsTimes()
    {
        var track = await Factory.SeedAsync(NamedTrack());

        using var response = await Client.PostExportAsync(Raw(track.Id, "drums") with { Range = new RangeDto(30, 75) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var asset = await response.BodyAsync<AssetDto>();
        (asset.Name, asset.Provenance.Contents).ShouldBe(("Never Gonna Give You Up - drums, 0:30-1:15", "drums, raw, 0:30-1:15"));
        asset.Provenance.Recipe!.Range.ShouldBe(new RangeDto(30, 75));
        Worker.RenderCreates.Single().Range.ShouldBe(new WorkerRenderRange(30, 75));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 10)]
    [InlineData(10, 10.4)]
    [InlineData(200, 214)]
    [InlineData(20, 10)]
    public async Task RangesOutsideTheTrack_Are422(double start, double end)
    {
        var track = await Factory.SeedAsync(NamedTrack());

        using var response = await Client.PostExportAsync(Raw(track.Id, "drums") with { Range = new RangeDto(start, end) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await response.FieldErrorsAsync("range")).ShouldNotBeEmpty();
        Worker.RenderCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheWholeTrackAsARange_IsAllowed()
    {
        var track = await Factory.SeedAsync(NamedTrack());

        using var response = await Client.PostExportAsync(Raw(track.Id, "drums") with { Range = new RangeDto(0, 213.04) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<AssetDto>()).Provenance.Contents.ShouldBe("drums, raw, 0:00-3:33");
    }

    [Fact]
    public async Task ARetry_KeepsTheRange()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var asset = await (await Client.PostExportAsync(Raw(track.Id, "drums") with { Range = new RangeDto(5, 6) })).BodyAsync<AssetDto>();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));
        await Factory.SyncAssetsAsync();

        (await Client.PostAsync(new Uri(StemStudio.Contracts.ApiRoutes.RetryAsset(asset.Id), UriKind.Relative), null, Ct)).Dispose();

        Worker.RenderCreates[1].Range.ShouldBe(new WorkerRenderRange(5, 6));
    }
}
