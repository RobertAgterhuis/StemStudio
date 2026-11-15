using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using static StemStudio.Api.Tests.Assets.AssetTestData;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Feature 007 T042: mixed exports from the mixer state or a saved mix (FR-002, FR-003, FR-005, FR-010).</summary>
public sealed class MixExportTests : ApiTestBase
{
    private static readonly ExportMixDto SoloBass = new(0.7, [new ChannelSettingsDto("bass", 0.6, -0.5, false, true), new ChannelSettingsDto("guitar", 0.9, 0, true, false)]);

    [Fact]
    public async Task WhatIHear_RendersTheAudibleChannelsWithFadersPansAndMaster()
    {
        var track = await Factory.SeedAsync(NamedTrack());

        using var response = await Client.PostExportAsync(Mix(track.Id, SoloBass));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var render = Worker.RenderCreates.Single();
        render.Inputs.ShouldBe([new WorkerRenderInput($"tracks/{track.Id:D}/stems/bass.flac", 0.6, -0.5)]);
        render.MasterGain.ShouldBe(0.7);
        render.Output.ShouldBe(new WorkerRenderOutput(WorkerRenderFormat.Flac) { BitDepth = 24, Dither = true });
        var asset = await response.BodyAsync<AssetDto>();
        (asset.Name, asset.Provenance.Contents).ShouldBe(("Never Gonna Give You Up - bass", "bass, mixed"));
        asset.Provenance.Recipe.Mix.ShouldNotBeNull().Channels.Count.ShouldBe(2);
        asset.Provenance.Recipe.Channels.ShouldBeNull();
    }

    [Fact]
    public async Task SelectedChannels_IgnoreMuteAndSolo()
    {
        var track = await Factory.SeedAsync(NamedTrack());

        (await Client.PostExportAsync(Mix(track.Id, SoloBass) with { Channels = ["guitar", "vocals"] })).Dispose();

        Worker.RenderCreates.Single().Inputs.Select(input => (input.Path[^11..], input.Gain)).ShouldBe([("vocals.flac", 0.8), ("guitar.flac", 0.9)]);
    }

    [Fact]
    public async Task ASavedMix_IsCopiedIntoTheRecipe()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var settings = MixEndpointsTests.Settings() with { MasterVolume = 0.5 };
        var mix = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(track.Id, settings), Ct)).BodyAsync<MixDto>();

        var asset = await (await Client.PostExportAsync(Mix(track.Id, null) with { MixId = mix.Id })).BodyAsync<AssetDto>();

        asset.Provenance.Recipe.Mix!.MasterVolume.ShouldBe(0.5);
        Worker.RenderCreates.Single().MasterGain.ShouldBe(0.5);
    }

    [Fact]
    public async Task AnUnknownOrForeignMix_Is404()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var other = await Factory.SeedAsync(NamedTrack("aaaaaaaaaaa"));
        var foreign = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(other.Id, MixEndpointsTests.Settings()), Ct)).BodyAsync<MixDto>();

        using var unknown = await Client.PostExportAsync(Mix(track.Id, null) with { MixId = Guid.NewGuid() });
        using var wrongTrack = await Client.PostExportAsync(Mix(track.Id, null) with { MixId = foreign.Id });

        (unknown.StatusCode, wrongTrack.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeitherOrBothMixSources_Are422(bool both)
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var request = Mix(track.Id, both ? SoloBass : null) with { MixId = both ? Guid.NewGuid() : null };

        using var response = await Client.PostExportAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("mix")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task MixRuleViolations_KeepTheirPaths()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        var mix = new ExportMix(1, []) { DetailChannels = [new DetailChannelSettingsDto("drums", "toms", 0.8, 0, false, false)] };

        using var response = await Client.PostExportAsync(Mix(track.Id, mix));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await response.FieldErrorsAsync("mix.detailChannels[0].stem")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task NothingAudible_Is422NothingToExport()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var muted = new ExportMix(1, new[] { "vocals", "drums", "bass", "guitar", "piano", "other" }.Select(stem => new ChannelSettingsDto(stem, 0.8, 0, true, false)).ToList());

        using var response = await Client.PostExportAsync(Mix(track.Id, muted));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var problem = await response.JsonAsync();
        (problem.GetProperty("type").GetString(), problem.GetProperty("detail").GetString()).ShouldBe(("nothing-to-export", "Nothing to export: no channel is audible"));
    }

    [Fact]
    public async Task TheAppliedGain_IsExposedOnceReady()
    {
        var track = await Factory.SeedAsync(NamedTrack());
        var asset = await (await Client.PostExportAsync(Mix(track.Id, SoloBass) with { Format = new ExportFormatDto(AudioFileFormatDto.Mp3, null, 256) })).BodyAsync<AssetDto>();
        CompleteRenders(Worker);
        var result = Result(RenderedSha) with { FileName = "output.mp3", ContentType = "audio/mpeg", AppliedGainDb = -1.8, PeakDbfs = -0.1 };
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Completed, 1, result));

        await Factory.SyncAssetsAsync();

        var ready = await (await Client.GetAsync(new Uri(ApiRoutes.Asset(asset.Id), UriKind.Relative), Ct)).BodyAsync<AssetDto>();
        ready.Audio.ShouldBe(new AudioFactsDto { Format = AudioFileFormatDto.Mp3, Mp3BitrateKbps = 256, DurationSeconds = 213.04, PeakDbfs = -0.1, AppliedGainDb = -1.8 });
        ready.Files.Single().ContentType.ShouldBe("audio/mpeg");
    }

    private static CreateAudioExportRequest Mix(Guid trackId, ExportMixDto? mix) => new()
    {
        TrackId = trackId,
        Mode = ExportModeDto.Mix,
        Mix = mix,
        Format = new ExportFormatDto(AudioFileFormatDto.Flac),
    };

    private sealed record ExportMix(double MasterVolume, IReadOnlyList<ChannelSettingsDto> Channels)
    {
        public IReadOnlyList<DetailChannelSettingsDto> DetailChannels { get; init; } = [];

        public static implicit operator ExportMixDto(ExportMix mix) => new(mix.MasterVolume, mix.Channels) { DetailChannels = mix.DetailChannels };
    }
}
