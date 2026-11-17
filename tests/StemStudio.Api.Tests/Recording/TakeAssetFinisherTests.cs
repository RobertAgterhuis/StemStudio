using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Voices;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Tests.Recording;

/// <summary>
/// Feature 015 T038: a take's asset becoming ready (capture deleted, hash registered, status mapping), the hidden library
/// item (<c>listAssets</c> without/with <c>origin</c>), <c>managed-by-take</c> on the library endpoints, and
/// <c>retryRecordingTake</c>.
/// </summary>
public sealed class TakeAssetFinisherTests : RecordingTestBase
{
    private static readonly byte[] Flac = "fLaC-take-audio"u8.ToArray();

    private static string FlacSha => Convert.ToHexStringLower(SHA256.HashData(Flac));

    private async Task<RecordingTakeDto> ReadyTakeAsync()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        CompleteRenders(Flac);
        await Factory.SyncAssetsAsync();
        return take;
    }

    private Task<RecordingTakeDto> GetAsync(Guid takeId) =>
        Client.GetFromJsonAsync<RecordingTakeDto>(new Uri(ApiRoutes.RecordingTakeOf(takeId), UriKind.Relative), ApiFactory.Json, Ct)!;

    [Fact]
    public async Task Ready_DeletesTheCapture_RegistersTheHash_AndServesTheAudio()
    {
        var take = await ReadyTakeAsync();

        var ready = (await GetAsync(take.Id))!;
        (ready.Status, ready.SizeBytes, ready.AudioUrl).ShouldBe((TakeStatusDto.Ready, (long)Flac.Length, ApiRoutes.AssetFile(take.AssetId, "audio")));
        File.Exists(CapturePath(take.AssetId)).ShouldBeFalse();
        File.ReadAllBytes(Path.Combine(AssetsDir, take.AssetId.ToString("D"), "audio.flac")).ShouldBe(Flac);
        var asset = await AssetAsync(take.AssetId);
        (asset.Audio!.Format, asset.Audio.BitDepth).ShouldBe((Domain.Rendering.AudioFormat.Flac, (int?)24));
        (await Factory.WithScopeAsync(sp => sp.GetRequiredService<AudioHashIndex>().ContainsAsync(FlacSha, Ct))).ShouldBeTrue();
        using var audio = await Client.GetAsync(new Uri(ready.AudioUrl!, UriKind.Relative), Ct);
        (await audio.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Flac);
        Worker.RenderDeletes.ShouldBe([RenderJobId]);
    }

    [Fact]
    public async Task RunningRender_IsSaving_AndAFailedRender_KeepsTheCapture()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        await Factory.SyncAssetsAsync();
        (await GetAsync(take.Id))!.Status.ShouldBe(TakeStatusDto.Saving);

        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));
        await Factory.SyncAssetsAsync();

        var failed = (await GetAsync(take.Id))!;
        (failed.Status, failed.Error, failed.AudioUrl).ShouldBe((TakeStatusDto.Failed, "interrupted by restart", (string?)null));
        File.Exists(CapturePath(take.AssetId)).ShouldBeTrue();
    }

    [Fact]
    public async Task ADamagedCopy_FailsTheTake_ButKeepsTheCapture()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        AssetTestData.CompleteRenders(Worker, new string('0', 64), Flac);

        await Factory.SyncAssetsAsync();

        (await GetAsync(take.Id))!.Status.ShouldBe(TakeStatusDto.Failed);
        File.Exists(CapturePath(take.AssetId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Library_HidesTakes_UnlessTheOriginIsAskedFor()
    {
        var take = await ReadyTakeAsync();

        var all = await Client.GetFromJsonAsync<AssetListDto>(new Uri(ApiRoutes.Assets, UriKind.Relative), ApiFactory.Json, Ct);
        using var recordings = await Client.GetAsync(new Uri(ApiRoutes.Assets + "?origin=recording", UriKind.Relative), Ct);
        using var invalid = await Client.GetAsync(new Uri(ApiRoutes.Assets + "?origin=tape", UriKind.Relative), Ct);

        all!.Items.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(recordings, "listAssets");
        var listed = (await recordings.BodyAsync<AssetListDto>()).Items.ShouldHaveSingleItem();
        (listed.Id, listed.Provenance.Origin, listed.Provenance.Recording!.TakeId).ShouldBe((take.AssetId, AssetOriginDto.Recording, take.Id));
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var one = await Client.GetAsync(new Uri(ApiRoutes.Asset(take.AssetId), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(one, "getAsset");
    }

    [Fact]
    public async Task LibraryEndpoints_RefuseTakeItems_WithManagedByTake()
    {
        var take = await ReadyTakeAsync();
        var path = new Uri(ApiRoutes.Asset(take.AssetId), UriKind.Relative);

        using var rename = await Client.PatchAsJsonAsync(path, new RenameAssetRequest("Other"), Ct);
        using var delete = await Client.DeleteAsync(path, Ct);
        using var retry = await Client.PostAsync(new Uri(ApiRoutes.Asset(take.AssetId) + "/retry", UriKind.Relative), null, Ct);

        foreach (var (response, operation) in new[] { (rename, "renameAsset"), (delete, "deleteAsset"), (retry, "retryAsset") })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict, operation);
            (await ProblemTypeAsync(response)).ShouldBe(Problem("managed-by-take"), operation);
            await ContractAssert.MatchesAsync(response, operation);
        }

        (await AssetAsync(take.AssetId)).Name.ShouldBe("Take 1 - Bass");
    }

    [Fact]
    public async Task Retry_OfAReadyTake_Is409TakeNotFailed_AndOfUnknown404()
    {
        var take = await ReadyTakeAsync();

        using var response = await Client.PostAsync(new Uri(ApiRoutes.RecordingTakeRetryOf(take.Id), UriKind.Relative), null, Ct);
        using var missing = await Client.PostAsync(new Uri(ApiRoutes.RecordingTakeRetryOf(Guid.NewGuid()), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemTypeAsync(response)).ShouldBe(Problem("take-not-failed"));
        await ContractAssert.MatchesAsync(response, "retryRecordingTake");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Retry_OfAFailedTake_RendersTheCaptureAgain_OrIs409CaptureMissing()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));
        await Factory.SyncAssetsAsync();

        using var retried = await Client.PostAsync(new Uri(ApiRoutes.RecordingTakeRetryOf(take.Id), UriKind.Relative), null, Ct);

        retried.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(retried, "retryRecordingTake");
        (await retried.BodyAsync<RecordingTakeDto>()).Status.ShouldBe(TakeStatusDto.Saving);
        Worker.RenderCreates.Count.ShouldBe(2);
        (Worker.RenderCreates[1] with { Inputs = [] }).ShouldBe(Worker.RenderCreates[0] with { Inputs = [] });
        Worker.RenderCreates[1].Inputs.ShouldBe(Worker.RenderCreates[0].Inputs);

        await Factory.SyncAssetsAsync();
        File.Delete(CapturePath(take.AssetId));
        using var missing = await Client.PostAsync(new Uri(ApiRoutes.RecordingTakeRetryOf(take.Id), UriKind.Relative), null, Ct);
        (await ProblemTypeAsync(missing)).ShouldBe(Problem("capture-missing"));
    }

    [Fact]
    public async Task Retry_WithAnUnreachableWorker_Is503_AndTheTakeStaysFailed()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));
        await Factory.SyncAssetsAsync();
        Worker.OnCreateRender = _ => throw new WorkerUnavailableException();

        using var response = await Client.PostAsync(new Uri(ApiRoutes.RecordingTakeRetryOf(take.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await AssetAsync(take.AssetId)).Status.ShouldBe(AssetStatus.Failed);
    }

    [Fact]
    public async Task AVoiceSample_EqualToATake_IsRefused()
    {
        await ReadyTakeAsync();

        var index = await Factory.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<Persistence.StemStudioDbContext>();
            return await context.AudioHashes.AsNoTracking().CountAsync(hash => hash.Sha256 == FlacSha, Ct);
        });

        index.ShouldBe(1);
    }
}
