using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;

namespace StemStudio.Api.Tests.Recording;

/// <summary>
/// Feature 015 T079: <c>replaceRecordingTakeAudio</c> (data-model 2.4) - the previous audio stays until the new one is ready,
/// <c>alignmentRevision</c> + 1, <c>adjustmentSamples</c> summed, <c>take-saving</c> 409, a failed render keeps the old
/// audio, and the engine's adjusted span starting at sample 0 with a shorter WAV.
/// </summary>
public sealed class ReplaceTakeAudioEndpointTests : RecordingTestBase
{
    private static readonly byte[] OldFlac = "fLaC-old"u8.ToArray();
    private static readonly byte[] NewFlac = "fLaC-new-audio"u8.ToArray();

    private async Task<RecordingTakeDto> ReadyTakeAsync()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id, Metadata(startSample: 2_000, lengthSamples: 30_000));
        CompleteRenders(OldFlac);
        await Factory.SyncAssetsAsync();
        Worker.OnGetRender = null;
        return take;
    }

    private Task<HttpResponseMessage> ReplaceAsync(Guid takeId, long start, long length, int adjustment, int value = 90_000) =>
        Client.PutAsync(
            new Uri(ApiRoutes.RecordingTakeAudioOf(takeId), UriKind.Relative),
            Form(Wav(Samples((int)length, value)), new ReplaceTakeAudioMetadataDto { StartSample = start, LengthSamples = length, AdjustmentSamples = adjustment }),
            Ct);

    private async Task<byte[]> AudioAsync(RecordingTakeDto take) =>
        await Client.GetByteArrayAsync(new Uri(ApiRoutes.AssetFile(take.AssetId, "audio"), UriKind.Relative), Ct);

    private Task<RecordingTakeDto> GetAsync(Guid takeId) =>
        Client.GetFromJsonAsync<RecordingTakeDto>(new Uri(ApiRoutes.RecordingTakeOf(takeId), UriKind.Relative), ApiFactory.Json, Ct)!;

    [Fact]
    public async Task Replace_StartingAtZeroWithAShorterWav_KeepsTheOldAudioUntilTheNewIsReady()
    {
        var take = await ReadyTakeAsync();

        using var response = await ReplaceAsync(take.Id, 0, 28_000, -2_000);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        await ContractAssert.MatchesAsync(response, "replaceRecordingTakeAudio");
        var saving = await response.BodyAsync<RecordingTakeDto>();
        (saving.Status, saving.StartSample, saving.LengthSamples, saving.AdjustmentSamples, saving.AlignmentRevision).ShouldBe((TakeStatusDto.Saving, 0L, 28_000L, -2_000, 1));
        saving.AudioUrl.ShouldNotBeNull();
        (await AudioAsync(take)).ShouldBe(OldFlac);
        new FileInfo(CapturePath(take.AssetId)).Length.ShouldBe(44 + (28_000 * 3));
        Worker.RenderCreates.Count.ShouldBe(2);

        CompleteRenders(NewFlac);
        await Factory.SyncAssetsAsync();

        (await GetAsync(take.Id)).Status.ShouldBe(TakeStatusDto.Ready);
        (await AudioAsync(take)).ShouldBe(NewFlac);
        File.Exists(CapturePath(take.AssetId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Adjustments_AreSummed_AndEachCountsARevision()
    {
        var take = await ReadyTakeAsync();
        (await ReplaceAsync(take.Id, 2_441, 30_000, 441)).Dispose();
        CompleteRenders(NewFlac);
        await Factory.SyncAssetsAsync();

        using var second = await ReplaceAsync(take.Id, 2_341, 30_000, -100);

        var again = await second.BodyAsync<RecordingTakeDto>();
        (again.AdjustmentSamples, again.AlignmentRevision, again.StartSample).ShouldBe((341, 2, 2_341L));
    }

    [Fact]
    public async Task WhileSaving_Is409TakeSaving()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);

        using var response = await ReplaceAsync(take.Id, 0, 22_050, 0);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemTypeAsync(response)).ShouldBe(Problem("take-saving"));
        await ContractAssert.MatchesAsync(response, "replaceRecordingTakeAudio");
    }

    [Fact]
    public async Task AFailedRender_KeepsTheOldAudio_AndShowsTheError()
    {
        var take = await ReadyTakeAsync();
        (await ReplaceAsync(take.Id, 2_100, 30_000, 100)).Dispose();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));

        await Factory.SyncAssetsAsync();

        var failed = await GetAsync(take.Id);
        (failed.Status, failed.Error is null, failed.AudioUrl is null).ShouldBe((TakeStatusDto.Failed, false, false));
        (await AudioAsync(take)).ShouldBe(OldFlac);
        File.Exists(CapturePath(take.AssetId)).ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidReplacements_AreRefused_AndChangeNothing()
    {
        var take = await ReadyTakeAsync();

        using var adjustment = await ReplaceAsync(take.Id, 0, 22_050, 9_000);
        using var tooShort = await ReplaceAsync(take.Id, 0, 22_049, 0);
        using var silent = await ReplaceAsync(take.Id, 0, 22_050, 0, value: 0);
        using var outside = await ReplaceAsync(take.Id, 9_395_064, 22_050, 0);
        using var mismatch = await Client.PutAsync(
            new Uri(ApiRoutes.RecordingTakeAudioOf(take.Id), UriKind.Relative),
            Form(Wav(Samples(25_000)), new ReplaceTakeAudioMetadataDto { StartSample = 0, LengthSamples = 22_050, AdjustmentSamples = 0 }),
            Ct);
        using var unknown = await ReplaceAsync(Guid.NewGuid(), 0, 22_050, 0);

        foreach (var response in new[] { adjustment, tooShort, silent, outside, mismatch })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            await ContractAssert.MatchesAsync(response, "replaceRecordingTakeAudio");
        }

        (await ProblemTypeAsync(silent)).ShouldBe(Problem("silent-take"));
        (await ProblemTypeAsync(mismatch)).ShouldBe(Problem("length-mismatch"));
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var unchanged = await GetAsync(take.Id);
        (unchanged.Status, unchanged.AlignmentRevision, unchanged.StartSample).ShouldBe((TakeStatusDto.Ready, 0, 2_000L));
        File.Exists(CapturePath(take.AssetId)).ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnreachableWorker_Is503_AndTheTakeStaysReady()
    {
        var take = await ReadyTakeAsync();
        Worker.OnCreateRender = _ => throw new WorkerUnavailableException();

        using var response = await ReplaceAsync(take.Id, 0, 22_050, 0);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await GetAsync(take.Id)).Status.ShouldBe(TakeStatusDto.Ready);
        _ = AssetTestData.RenderJobId;
    }
}
