using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Gpu;
using StemStudio.Api.Generator;
using StemStudio.Api.Tests.Importing;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Importing;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T052: the local model is unloaded before GPU jobs are submitted; GPU activity drives the fallback (FR-021, FR-022, SC-008).</summary>
public sealed class GpuCoordinationTests : AgentApiTestBase
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";
    private readonly List<string> _order = [];

    public GpuCoordinationTests()
    {
        GpuRelease.ReleaseAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Record("release");
            return Task.CompletedTask;
        });
        Factory.Worker.OnCreateUrl = _ => Submitted(WorkerExamples.JobId);
        Factory.Worker.OnCreateUpload = _ => Submitted(WorkerExamples.JobId);
        Factory.Worker.OnCreateDetail = id => Task.FromResult(Record("submit", FakeWorkerClient.DetailJob("7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b", WorkerDetailJobState.Queued)));
    }

    [Fact]
    public async Task ImportFromUrl_ReleasesBeforeSubmitting()
    {
        using var response = await Client.PostAsJsonAsync(ApiRoutes.Imports, new ImportUrlRequest("https://youtu.be/dQw4w9WgXcQ"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _order.ShouldBe(["release", "submit"]);
    }

    [Fact]
    public async Task ImportUpload_ReleasesBeforeSubmitting()
    {
        using var content = ImportEndpointsTests.UploadContent([1, 2, 3], "a.wav");

        using var response = await Client.PostAsync(ApiRoutes.ImportUpload, content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _order.ShouldBe(["release", "submit"]);
    }

    [Fact]
    public async Task RetryAndDetail_ReleaseBeforeSubmitting()
    {
        var failed = await Factory.SeedAsync(SyncTestData.FailedTrack("aaaaaaaaaaa"));
        var completed = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));

        using var retry = await Client.PostAsync(ApiRoutes.RetryTrack(failed.Id), null, Ct);
        using var detail = await Client.PostAsync(ApiRoutes.StartDetail(completed.Id), null, Ct);

        (retry.StatusCode, detail.StatusCode).ShouldBe((HttpStatusCode.Accepted, HttpStatusCode.Accepted));
        await Factory.SyncDetailAsync();
        _order.ShouldBe(["release", "submit", "release", "submit"]);
    }

    [Fact]
    public async Task AFailingRelease_NeverFailsTheRequest()
    {
        GpuRelease.ReleaseAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Ollama is down"));

        using var response = await Client.PostAsJsonAsync(ApiRoutes.Imports, new ImportUrlRequest("https://youtu.be/dQw4w9WgXcQ"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _order.ShouldBe(["submit"]);
    }

    [Fact]
    public async Task AHangingRelease_IsAbandonedAfterTwoSeconds()
    {
        var hanging = new TaskCompletionSource();
        GpuRelease.ReleaseAsync(Arg.Any<CancellationToken>()).Returns(hanging.Task);

        var request = Client.PostAsJsonAsync(ApiRoutes.Imports, new ImportUrlRequest("https://youtu.be/dQw4w9WgXcQ"), Ct);
        for (var attempt = 0; attempt < 200 && !request.IsCompleted; attempt++)
        {
            Factory.Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.WhenAny(request, Task.Delay(25, Ct));
        }

        using var response = await request;
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _order.ShouldBe(["submit"]);
    }

    [Theory]
    [InlineData(AgentBackendDto.None)]
    [InlineData(AgentBackendDto.Claude)]
    [InlineData(AgentBackendDto.Fake)]
    public async Task OtherBackends_SkipTheRelease(AgentBackendDto backend)
    {
        var settings = Service<IAgentSettingsStore>();
        await settings.SaveAsync((await settings.GetAsync(Ct)) with { Backend = backend }, Ct);

        using var response = await Client.PostAsJsonAsync(ApiRoutes.Imports, new ImportUrlRequest("https://youtu.be/dQw4w9WgXcQ"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _order.ShouldBe(["submit"]);
    }

    [Fact]
    public async Task ACpuWorker_SkipsTheRelease()
    {
        Factory.Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu, Reason = "No CUDA" }));
        using (await Client.GetAsync(ApiRoutes.Device, Ct))
        {
        }

        using var response = await Client.PostAsJsonAsync(ApiRoutes.Imports, new ImportUrlRequest("https://youtu.be/dQw4w9WgXcQ"), Ct);

        _order.ShouldBe(["submit"]);
        Service<IGpuActivity>().IsHeavyJobActive().ShouldBeFalse("a CPU worker job does not hold the GPU");
    }

    [Fact]
    public async Task GpuActivity_FollowsTheTrackJobsOnTheGpu()
    {
        var activity = Service<IGpuActivity>();
        activity.IsHeavyJobActive().ShouldBeFalse();

        await Factory.SeedAsync(OnDevice("ccccccccccc", new JobDevice(ProcessingDeviceKind.Cpu, null, true, "out of memory")));
        Factory.Clock.Advance(TimeSpan.FromSeconds(3));
        activity.IsHeavyJobActive().ShouldBeFalse("a CPU job does not hold the GPU");

        await Factory.SeedAsync(OnDevice("hhhhhhhhhhh", new JobDevice(ProcessingDeviceKind.Gpu, Gpu, false, null)));
        activity.IsHeavyJobActive().ShouldBeTrue("an idle answer is never reused: the next turn sees a new GPU job at once (B2)");
    }

    [Fact]
    public async Task ADetailRequest_SendsTheNextTurnToTheFallback_BeforeTheJobIsSubmitted()
    {
        // B2 (SC-008): the approved tool call queues the detail phase; the run's next turn follows about 0.9 s later, before
        // the synchronizer submits the job and releases the model. That turn must not load the primary model onto the GPU.
        var activity = Service<IGpuActivity>();
        var completed = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        activity.IsHeavyJobActive().ShouldBeFalse("a status poll just before the approval");
        var seen = ActivityAtRelease();

        using var detail = await Client.PostAsync(ApiRoutes.StartDetail(completed.Id), null, Ct);
        Factory.Clock.Advance(TimeSpan.FromMilliseconds(900));

        detail.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        activity.IsHeavyJobActive().ShouldBeTrue("the next turn must use the CPU fallback");
        await Factory.SyncDetailAsync();
        seen.ShouldBe(["release True"]);
    }

    [Fact]
    public async Task TheGate_HoldsTheGpuFromBeforeTheReleaseUntilShortlyAfterTheSubmission()
    {
        // Imports and generator jobs are stored only after the submission returns: the gate itself must mark the GPU busy.
        var seen = ActivityAtRelease();
        var inner = Substitute.For<IWorkerClient>();
        inner.CreateDetailJobAsync(default!, Ct).ReturnsForAnyArgs(_ =>
            Record(seen, "submit", FakeWorkerClient.DetailJob("7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b", WorkerDetailJobState.Queued)));

        await new GpuReleasingWorkerClient(inner, Service<GpuJobGate>()).CreateDetailJobAsync("job", Ct);

        seen.ShouldBe(["release True", "submit True"]);
        Service<IGpuActivity>().IsHeavyJobActive().ShouldBeTrue("the caller stores the job right after the submission");
        Factory.Clock.Advance(GpuSubmissions.Grace);
        Service<IGpuActivity>().IsHeavyJobActive().ShouldBeFalse("from then on the stored job states decide");
    }

    [Fact]
    public async Task TheGate_HoldsWhileSubmissionsOverlap()
    {
        var activity = Service<IGpuActivity>();
        var first = await Service<GpuJobGate>().BeforeGpuJobAsync(Ct);
        var second = await Service<GpuJobGate>().BeforeGeneratorJobAsync(Ct);

        first.Dispose();
        Factory.Clock.Advance(GpuSubmissions.Grace);
        activity.IsHeavyJobActive().ShouldBeTrue("the second submission is still in flight");
        second.Dispose();
        second.Dispose();
        Factory.Clock.Advance(GpuSubmissions.Grace);
        activity.IsHeavyJobActive().ShouldBeFalse("disposing a hold twice counts once");
    }

    [Fact]
    public async Task AFailedSubmission_EndsItsHold()
    {
        var inner = Substitute.For<IGeneratorClient>();
        inner.CreateSongAsync(default!, Ct).ThrowsAsyncForAnyArgs(new HttpRequestException("generator down"));

        await Should.ThrowAsync<HttpRequestException>(() => new GpuReleasingGeneratorClient(inner, Service<GpuJobGate>()).CreateSongAsync(null!, Ct));
        Factory.Clock.Advance(GpuSubmissions.Grace);

        Service<IGpuActivity>().IsHeavyJobActive().ShouldBeFalse();
    }

    [Fact]
    public async Task ACancelledRelease_EndsTheHold()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        GpuRelease.ReleaseAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(cancelled.Token));

        await Should.ThrowAsync<OperationCanceledException>(() => Service<GpuJobGate>().BeforeGeneratorJobAsync(cancelled.Token));
        Factory.Clock.Advance(GpuSubmissions.Grace);

        Service<IGpuActivity>().IsHeavyJobActive().ShouldBeFalse();
    }

    [Fact]
    public async Task GpuActivity_CountsDetailPhasesAndIgnoresFinishedTracks()
    {
        await Factory.SeedAsync(SyncTestData.CompletedTrack("ddddddddddd"));
        await Factory.SeedAsync(SyncTestData.FailedTrack("eeeeeeeeeee"));
        var activity = Service<IGpuActivity>();
        activity.IsHeavyJobActive().ShouldBeFalse();

        await Factory.SeedAsync(SyncTestData.DetailQueued("fffffffffff"));
        Factory.Clock.Advance(TimeSpan.FromSeconds(3));

        activity.IsHeavyJobActive().ShouldBeTrue();
    }

    [Fact]
    public async Task GpuActivity_UnknownDevicesOnACpuWorkerAreNotGpuWork()
    {
        Factory.Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu, Reason = "No CUDA" }));
        using (await Client.GetAsync(ApiRoutes.Device, Ct))
        {
        }

        await Factory.SeedAsync(SyncTestData.UrlTrack("ggggggggggg", jobId: null));

        Service<IGpuActivity>().IsHeavyJobActive().ShouldBeFalse();
    }

    private static Track OnDevice(string videoId, JobDevice device)
    {
        var track = SyncTestData.UrlTrack(videoId, jobId: null);
        track.ApplyDevice(device, SyncTestData.T0);
        return track;
    }

    private List<string> ActivityAtRelease()
    {
        var seen = new List<string>();
        GpuRelease.ReleaseAsync(Arg.Any<CancellationToken>()).Returns(_ => Record(seen, "release", Task.CompletedTask));
        return seen;
    }

    private T Record<T>(List<string> seen, string step, T value)
    {
        seen.Add($"{step} {Service<IGpuActivity>().IsHeavyJobActive()}");
        return value;
    }

    private Task<WorkerJob> Submitted(string jobId) => Task.FromResult(Record("submit", FakeWorkerClient.Job(jobId, WorkerJobState.Queued)));

    private T Record<T>(string step, T value)
    {
        Record(step);
        return value;
    }

    private void Record(string step)
    {
        lock (_order)
        {
            _order.Add(step);
        }
    }
}
