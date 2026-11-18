using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;

namespace StemStudio.Api.Tests.Recording;

/// <summary>
/// Feature 015 T071: exports with the take - raw take (default range = span, explicit range intersected), mix with the
/// take strip (sent as <c>mix.take</c> or the saved mix's), explicit <c>["take", ...]</c>, the problem codes, provenance
/// <c>contents</c>/<c>sourceAssetIds</c> and the recipe's <c>takeId</c>.
/// </summary>
public sealed class TakeExportTests : RecordingTestBase
{
    private async Task<(Guid TrackId, RecordingTakeDto Take)> ReadyTakeAsync(long startSample = 44_100, long lengthSamples = 88_200)
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id, Metadata(startSample, lengthSamples));
        CompleteRenders("fLaC"u8.ToArray());
        await Factory.SyncAssetsAsync();
        Worker.RenderCreates.Clear();
        return (track.Id, take);
    }

    private static CreateAudioExportRequest RawTake(Guid trackId, Guid? takeId, RangeDto? range = null) => new()
    {
        TrackId = trackId,
        Mode = ExportModeDto.Raw,
        Channels = ["take"],
        TakeId = takeId,
        Range = range,
        Format = new ExportFormatDto(AudioFileFormatDto.Flac),
    };

    private static CreateAudioExportRequest Mix(Guid trackId, ExportMixDto? mix = null, Guid? mixId = null, IReadOnlyList<string>? channels = null) => new()
    {
        TrackId = trackId,
        Mode = ExportModeDto.Mix,
        Mix = mix,
        MixId = mixId,
        Channels = channels,
        Format = new ExportFormatDto(AudioFileFormatDto.Wav),
    };

    private static TakeChannelSettingsDto Strip(Guid takeId, double volume = 0.7, double pan = -0.4, bool solo = false) =>
        new() { TakeId = takeId, Volume = volume, Pan = pan, Mute = false, Solo = solo };

    [Fact]
    public async Task RawTake_RendersTheTakeAlone_OverItsSpan_AndRecordsTheTake()
    {
        var (trackId, take) = await ReadyTakeAsync();

        using var response = await Client.PostExportAsync(RawTake(trackId, take.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var asset = await response.BodyAsync<AssetDto>();
        var request = Worker.RenderCreates.ShouldHaveSingleItem();
        request.Inputs.ShouldBe([new WorkerRenderInput($"assets/{take.AssetId:D}/audio.flac", 1, 0)]);
        (request.MasterGain, request.Range, request.Output.Dither).ShouldBe((1.0, new WorkerRenderRange(1, 3), false));
        (asset.Provenance.Contents, asset.Provenance.Recipe!.TakeId).ShouldBe(("Take 1 - Bass, raw, 0:01-0:03", (Guid?)take.Id));
        asset.Provenance.SourceAssetIds.ShouldBe([take.AssetId]);
        asset.Provenance.Recipe.Range.ShouldBe(new RangeDto(1, 3));
    }

    [Fact]
    public async Task RawTake_IntersectsAnExplicitRange_AndRefusesOneOutsideTheTake()
    {
        var (trackId, take) = await ReadyTakeAsync();

        using var inside = await Client.PostExportAsync(RawTake(trackId, take.Id, new RangeDto(2, 10)));
        using var outside = await Client.PostExportAsync(RawTake(trackId, take.Id, new RangeDto(10, 20)));

        inside.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.RenderCreates.ShouldHaveSingleItem().Range.ShouldBe(new WorkerRenderRange(2, 3));
        outside.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(outside, "createAudioExport");
    }

    [Fact]
    public async Task MixWithTheSentTakeStrip_AddsTheTakeLast_WithItsFader()
    {
        var (trackId, take) = await ReadyTakeAsync();
        var mix = new ExportMixDto(0.8, []) { Take = Strip(take.Id) };

        using var response = await Client.PostExportAsync(Mix(trackId, mix));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var asset = await response.BodyAsync<AssetDto>();
        var inputs = Worker.RenderCreates.ShouldHaveSingleItem().Inputs;
        (inputs.Count, inputs[^1]).ShouldBe((7, new WorkerRenderInput($"assets/{take.AssetId:D}/audio.flac", 0.7, -0.4)));
        (asset.Provenance.Contents, asset.Provenance.Recipe!.TakeId, asset.Provenance.Recipe.Mix!.Take).ShouldBe(("Take 1 - Bass - 6 channels, mixed", (Guid?)take.Id, Strip(take.Id)));
    }

    [Fact]
    public async Task MixFromASavedMix_UsesItsTakeChannel_SoloedTakeAlone()
    {
        var (trackId, take) = await ReadyTakeAsync();
        var settings = Mixes.MixEndpointsTests.Settings() with { Take = Strip(take.Id, 0.5, 0, solo: true), Channels = [] };
        var saved = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), ApiFactory.Json, Ct)).BodyAsync<MixDto>();

        using var response = await Client.PostExportAsync(Mix(trackId, mixId: saved.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        Worker.RenderCreates.ShouldHaveSingleItem().Inputs.ShouldBe([new WorkerRenderInput($"assets/{take.AssetId:D}/audio.flac", 0.5, 0)]);
        (await response.BodyAsync<AssetDto>()).Provenance.Contents.ShouldBe("Take 1 - Bass, mixed");
    }

    [Fact]
    public async Task ExplicitTakeAndBass_AreRenderedInMixerOrder()
    {
        var (trackId, take) = await ReadyTakeAsync();
        var mix = new ExportMixDto(1, [new ChannelSettingsDto("bass", 0.6, 0, false, false)]) { Take = Strip(take.Id, 0.8, 0) };

        using var response = await Client.PostExportAsync(Mix(trackId, mix, channels: ["take", "bass"]));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.RenderCreates.ShouldHaveSingleItem().Inputs.Select(input => input.Path).ShouldBe([$"tracks/{trackId:D}/stems/bass.flac", $"assets/{take.AssetId:D}/audio.flac"]);
    }

    [Fact]
    public async Task TakeChannelWithoutATake_Is422TakeRequired()
    {
        var (trackId, _) = await ReadyTakeAsync();

        using var raw = await Client.PostExportAsync(RawTake(trackId, null));
        using var mixed = await Client.PostExportAsync(Mix(trackId, new ExportMixDto(1, []), channels: ["take", "bass"]));

        foreach (var response in new[] { raw, mixed })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await ProblemTypeAsync(response)).ShouldBe(Problem("take-required"));
            await ContractAssert.MatchesAsync(response, "createAudioExport");
        }
    }

    [Fact]
    public async Task UnknownForeignOrUnreadyTakes_AreRefused()
    {
        var (trackId, ready) = await ReadyTakeAsync();
        var other = await TrackAsync("bbbbbbbbbbb");
        Worker.OnGetRender = null;
        var foreign = await CreateAsync(other.Id);
        var saving = await CreateAsync(trackId);

        using var unknown = await Client.PostExportAsync(RawTake(trackId, Guid.NewGuid()));
        using var notInTrack = await Client.PostExportAsync(RawTake(trackId, foreign.Id));
        using var notReady = await Client.PostExportAsync(RawTake(trackId, saving.Id));

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "createAudioExport");
        (await ProblemTypeAsync(notInTrack)).ShouldBe(Problem("take-not-in-track"));
        notInTrack.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ProblemTypeAsync(notReady)).ShouldBe(Problem("take-not-ready"));
        notReady.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(notReady, "createAudioExport");
        ready.Status.ShouldBe(TakeStatusDto.Saving);
    }

    [Fact]
    public async Task AStaleTakeInASavedMix_IsIgnored()
    {
        var (trackId, take) = await ReadyTakeAsync();
        var settings = Mixes.MixEndpointsTests.Settings() with { Take = Strip(take.Id) };
        var saved = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), ApiFactory.Json, Ct)).BodyAsync<MixDto>();
        (await Client.DeleteAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await Client.PostExportAsync(Mix(trackId, mixId: saved.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.RenderCreates.ShouldHaveSingleItem().Inputs.ShouldNotContain(input => input.Path.StartsWith("assets/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RetryOfATakeExport_RendersTheTakeAgain_OrIsSourceUnavailableWhenItIsGone()
    {
        var (trackId, take) = await ReadyTakeAsync();
        var asset = await (await Client.PostExportAsync(RawTake(trackId, take.Id))).BodyAsync<AssetDto>();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Failed));
        await Factory.SyncAssetsAsync();
        Worker.RenderCreates.Clear();

        using var retried = await Client.PostAsync(new Uri(ApiRoutes.Asset(asset.Id) + "/retry", UriKind.Relative), null, Ct);
        retried.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.RenderCreates.ShouldHaveSingleItem().Inputs.ShouldBe([new WorkerRenderInput($"assets/{take.AssetId:D}/audio.flac", 1, 0)]);

        await Factory.SyncAssetsAsync();
        (await Client.DeleteAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var gone = await Client.PostAsync(new Uri(ApiRoutes.Asset(asset.Id) + "/retry", UriKind.Relative), null, Ct);
        (await ProblemTypeAsync(gone)).ShouldBe("source-unavailable");
    }
}
