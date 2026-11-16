using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Gpu;
using StemStudio.Api.Generator;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 T078: the local model is unloaded before the GPU jobs of 006-010 are submitted (lyrics alignment, scores, restyles, songs,
/// generation retries, voice conversions, take imports; DELTA.md section 5), and their jobs count as GPU activity (FR-021, FR-022).
/// </summary>
public sealed class GpuJobEndpointsTests : AgentApiTestBase
{
    private readonly List<string> _order = [];

    public GpuJobEndpointsTests()
    {
        GpuRelease.ReleaseAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Record($"release after {Submissions()} submissions");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Restyle_ReleasesBeforeTheGeneratorJob()
    {
        var track = await RestyleEndpointTests.SeedTrackAsync(Factory);

        using var response = await Client.PostAsJsonAsync(ApiRoutes.Restyles(track.Id), RestyleEndpointTests.Request(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (_order.Single(), Factory.Generator.Created.Count).ShouldBe(("release after 0 submissions", 1));
    }

    [Fact]
    public async Task Score_ReleasesBeforeTheTranscriptionJob()
    {
        var track = await Factory.SeedAsync(DrumsSplitTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (_order.Single(), Factory.Worker.TranscriptionCreates.Count).ShouldBe(("release after 0 submissions", 1));
    }

    [Fact]
    public async Task LyricsAlignment_ReleasesBeforeTheAlignJob()
    {
        await Factory.SeedAsync(LyricsTestData.AlignPending());
        Factory.Worker.OnGetHealth = () => Task.FromResult(LyricsTestData.Health(ProcessingDeviceKind.Gpu, AlignAuto.Gpu, true));
        Factory.Worker.OnCreateLyricsAlign = _ => Task.FromResult(FakeWorkerClient.LyricsJob(LyricsTestData.AlignJobId, WorkerLyricsJobMode.Align, WorkerLyricsJobState.Queued, 0));

        await Factory.SyncLyricsAsync();

        (_order.Single(), Factory.Worker.LyricsAligns.Count).ShouldBe(("release after 0 submissions", 1));
    }

    [Fact]
    public void TheGeneratorAndSingerClients_AreDecorated()
    {
        Service<IGeneratorClient>().ShouldBeOfType<GpuReleasingGeneratorClient>();
        Service<ISingerClient>().ShouldBeOfType<GpuReleasingSingerClient>();
        Service<IWorkerClient>().ShouldBeOfType<GpuReleasingWorkerClient>();
    }

    [Fact]
    public async Task TheGeneratorDecorator_GatesTheThreeJobSubmissionsOnly()
    {
        var inner = Substitute.For<ISingerClient>();
        inner.CreateRestyleAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<GeneratorJob>());
        inner.CreateSongAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<GeneratorJob>());
        inner.CreateVoiceConversionAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<GeneratorJob>());
        var client = new GpuReleasingSingerClient(inner, Service<GpuJobGate>());

        await client.CreateRestyleAsync(null!, Ct);
        await client.CreateSongAsync(null!, Ct);
        await client.CreateVoiceConversionAsync(null!, Ct);
        await client.GetAsync("g1", Ct);
        await client.CancelAsync("g1", Ct);
        await client.DeleteAsync("g1", Ct);
        await client.GetHealthAsync(Ct);
        await client.CheckVoiceSampleAsync(Stream.Null, "a.wav", Ct);
        await client.DownloadCheckedSampleAsync("c1", Stream.Null, Ct);
        await client.DownloadTakeAsync(null!, Stream.Null, Ct);
        await client.DownloadTakeVocalAsync("g1", 1, Stream.Null, Ct);

        _order.ShouldBe(["release after 0 submissions", "submit", "release after 0 submissions", "submit", "release after 0 submissions", "submit"]);
        _ = client.IsConfigured;
        _ = inner.Received(1).IsConfigured;
        await inner.Received(1).DownloadTakeVocalAsync("g1", 1, Stream.Null, Ct);
    }

    [Fact]
    public async Task TheWorkerDecorator_GatesLyricsAlignmentAndTranscription()
    {
        var inner = Substitute.For<IWorkerClient>();
        inner.CreateLyricsAlignJobAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<WorkerLyricsJob>());
        inner.CreateTranscriptionJobAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<WorkerTranscriptionJob>());
        var client = new GpuReleasingWorkerClient(inner, Service<GpuJobGate>());

        await client.CreateLyricsAlignJobAsync(null!, Ct);
        await client.CreateTranscriptionJobAsync(null!, Ct);
        await client.CreateLyricsLookupJobAsync(null!, Ct);

        _order.ShouldBe(["release after 0 submissions", "submit", "release after 0 submissions", "submit"]);
    }

    [Fact]
    public async Task GeneratorJobs_ReleaseEvenOnACpuWorker()
    {
        Factory.Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu, Reason = "No CUDA" }));
        using (await Client.GetAsync(ApiRoutes.Device, Ct))
        {
        }

        var inner = Substitute.For<IGeneratorClient>();
        inner.CreateSongAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<GeneratorJob>());
        var generator = new GpuReleasingGeneratorClient(inner, Service<GpuJobGate>());
        var worker = new GpuReleasingWorkerClient(Substitute.For<IWorkerClient>(), Service<GpuJobGate>());

        await generator.CreateSongAsync(null!, Ct);
        await worker.CreateTranscriptionJobAsync(null!, Ct);

        _order.ShouldBe(["release after 0 submissions", "submit"], "the generator always runs on the GPU; a CPU worker does not");
    }

    [Fact]
    public async Task GeneratorJobs_SkipTheReleaseForOtherBackends()
    {
        var settings = Service<IAgentSettingsStore>();
        await settings.SaveAsync((await settings.GetAsync(Ct)) with { Backend = AgentBackendDto.Claude }, Ct);
        var inner = Substitute.For<IGeneratorClient>();
        inner.CreateSongAsync(default!, Ct).ReturnsForAnyArgs(_ => Submit<GeneratorJob>());

        await new GpuReleasingGeneratorClient(inner, Service<GpuJobGate>()).CreateSongAsync(null!, Ct);

        _order.ShouldBe(["submit"]);
    }

    [Fact]
    public async Task GpuActivity_CountsActiveGenerations()
    {
        var track = await RestyleEndpointTests.SeedTrackAsync(Factory);
        var activity = Service<IGpuActivity>();
        activity.IsHeavyJobActive().ShouldBeFalse();

        (await Client.PostAsJsonAsync(ApiRoutes.Restyles(track.Id), RestyleEndpointTests.Request(), Ct)).Dispose();
        Factory.Clock.Advance(TimeSpan.FromSeconds(3));

        activity.IsHeavyJobActive().ShouldBeTrue();
    }

    [Fact]
    public async Task GpuActivity_CountsLyricsAlignmentAndScoreJobs()
    {
        var activity = Service<IGpuActivity>();
        activity.IsHeavyJobActive().ShouldBeFalse();
        await Factory.SeedAsync(LyricsTestData.Aligning());
        Factory.Clock.Advance(TimeSpan.FromSeconds(3));
        activity.IsHeavyJobActive().ShouldBeTrue("an alignment runs on the worker");

        using var other = new ApiFactory();
        var track = await other.SeedAsync(DrumsSplitTrack());
        using var client = other.CreateClient();
        (await client.PostScoreAsync(Request(track.Id, "drums"))).Dispose();

        other.Services.GetRequiredService<IGpuActivity>().IsHeavyJobActive().ShouldBeTrue("a transcription runs on the worker");
    }

    [Fact]
    public async Task GpuActivity_IgnoresWorkerJobsOnACpuWorkerButNotGenerations()
    {
        Factory.Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu, Reason = "No CUDA" }));
        using (await Client.GetAsync(ApiRoutes.Device, Ct))
        {
        }

        await Factory.SeedAsync(LyricsTestData.Aligning());
        var activity = Service<IGpuActivity>();
        activity.IsHeavyJobActive().ShouldBeFalse();

        var track = await RestyleEndpointTests.SeedTrackAsync(Factory, "zzzzzzzzzzz");
        (await Client.PostAsJsonAsync(ApiRoutes.Restyles(track.Id), RestyleEndpointTests.Request(), Ct)).Dispose();
        Factory.Clock.Advance(TimeSpan.FromSeconds(3));

        activity.IsHeavyJobActive().ShouldBeTrue();
    }

    private int Submissions() =>
        Factory.Generator.Created.Count + Factory.Singer.Created.Count + Factory.Worker.TranscriptionCreates.Count + Factory.Worker.LyricsAligns.Count;

    private Task<T> Submit<T>()
        where T : class
    {
        Record("submit");
        return Task.FromResult<T>(null!);
    }

    private void Record(string step)
    {
        lock (_order)
        {
            _order.Add(step);
        }
    }
}
