using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using static StemStudio.Api.Tests.Assets.AssetTestData;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Feature 007 T035: failure paths of the asset sync and the stores.</summary>
public sealed class AssetSyncFailureTests : ApiTestBase
{
    [Fact]
    public async Task ACompletedJobWithoutResult_Fails()
    {
        var asset = await CreateAsync();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Completed, 1));

        await Factory.SyncAssetsAsync();

        (await GetAsync(asset.Id)).Error.ShouldBe("The completed job has no result.");
    }

    [Fact]
    public async Task AnUnexpectedFileType_Fails()
    {
        var asset = await CreateAsync();
        CompleteRenders(Worker);
        var result = Result(RenderedSha) with { FileName = "output.ogg" };
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Completed, 1, result));

        await Factory.SyncAssetsAsync();

        (await GetAsync(asset.Id)).Error.ShouldBe("The processing service sent an unexpected file type.");
    }

    [Fact]
    public async Task ADiskErrorWhileCopying_Fails()
    {
        var asset = await CreateAsync();
        CompleteRenders(Worker);
        Worker.OnDownloadRender = (_, _) => throw new IOException("disk full");

        await Factory.SyncAssetsAsync();

        (await GetAsync(asset.Id)).Error!.ShouldStartWith("Storing the rendered file failed");
    }

    [Fact]
    public async Task AnUnreachableWorkerWhileCopying_KeepsTheItemQueued()
    {
        var asset = await CreateAsync();
        CompleteRenders(Worker);
        Worker.OnDownloadRender = (_, _) => throw new WorkerUnavailableException();

        await Factory.SyncAssetsAsync();

        (await GetAsync(asset.Id)).Status.ShouldBe(AssetStatusDto.Queued);
    }

    [Fact]
    public async Task AFailedDeleteOfTheWorkerJob_IsOnlyLogged()
    {
        var asset = await CreateAsync();
        CompleteRenders(Worker);
        Worker.OnDeleteRender = _ => throw new WorkerUnavailableException();

        await Factory.SyncAssetsAsync();

        (await GetAsync(asset.Id)).Status.ShouldBe(AssetStatusDto.Ready);
        Factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("could not be removed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnexpectedErrorOfOneItem_DoesNotStopTheOthers()
    {
        await CreateAsync("aaaaaaaaaaa");
        var second = await CreateAsync("bbbbbbbbbbb");
        Worker.OnGetRender = id => id.EndsWith('1')
            ? throw new InvalidOperationException("boom")
            : Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Running, 0.3));

        await Factory.SyncAssetsAsync();

        (await GetAsync(second.Id)).Status.ShouldBe(AssetStatusDto.Rendering);
        Factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("synchronisation failed unexpectedly", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheHostedService_RunsTheAssetPass()
    {
        var asset = await CreateAsync();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Running, 0.4));

        Factory.Clock.Advance(TimeSpan.FromSeconds(1));

        await Eventually.WaitUntilAsync(async () => (await GetAsync(asset.Id)).Status == AssetStatusDto.Rendering);
    }

    [Fact]
    public async Task TheFileStore_RejectsNamesOutsideThePattern()
    {
        var store = Factory.Services.GetRequiredService<IAssetFileStore>();
        var id = Guid.NewGuid();

        await Should.ThrowAsync<ArgumentException>(() => store.SaveAsync(id, "../audio.flac", (_, _) => Task.CompletedTask, Ct));
        store.Find(id, "audio.exe").ShouldBeNull();
        store.Find(id, "audio.flac").ShouldBeNull();
        Should.NotThrow(() => store.DeleteAll(id));
    }

    [Fact]
    public void TheDriveDiskSpace_ReportsFreeBytes()
    {
        new DriveDiskSpace().AvailableBytes(Factory.Services.GetRequiredService<DataDirectory>().Root).ShouldBeGreaterThan(0);
    }

    private async Task<AssetDto> CreateAsync(string videoId = "dQw4w9WgXcQ")
    {
        var track = await Factory.SeedAsync(NamedTrack(videoId));
        return await (await Client.PostExportAsync(Raw(track.Id, "drums"))).BodyAsync<AssetDto>();
    }

    private async Task<AssetDto> GetAsync(Guid id) =>
        await (await Client.GetAsync(new Uri(ApiRoutes.Asset(id), UriKind.Relative), Ct)).BodyAsync<AssetDto>();
}
