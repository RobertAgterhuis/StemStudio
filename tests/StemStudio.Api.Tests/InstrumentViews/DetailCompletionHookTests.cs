using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T051/T052: the detail sync runs the completion hooks once, so drum hits start after the drums split (data-model 7.3).</summary>
public sealed class DetailCompletionHookTests : IDisposable
{
    private readonly ApiFactory _factory = new() { ConfigureServices = services => services.AddScoped<IDetailCompletionHook, ThrowingHook>() };

    public DetailCompletionHookTests() => _factory.CreateClient().Dispose();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ACompletedDrumsSplit_StartsDrumHits_EvenWhenAnotherHookFails()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        _factory.Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));

        await _factory.SyncDetailAsync();
        await _factory.SyncDetailAsync();

        (await _factory.FindTrackAsync(track.Id))!.DetailPhase!.Status.ShouldBe(Domain.Tracks.Detail.DetailPhaseStatus.Completed);
        _factory.AnalysisWorker.Requests.Select(request => request.Kind).ShouldBe(["drum-hits"]);
        (await _factory.FindAnalysisAsync(track.Id, AnalysisKind.DrumHits))!.Status.ShouldBe(AnalysisStatus.Queued);
        _factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("detail completion hook failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARunningDetailPhase_RunsNoHook()
    {
        await _factory.SeedAsync(SyncTestData.DetailQueued());
        _factory.Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Running, 40));

        await _factory.SyncDetailAsync();

        _factory.AnalysisWorker.Requests.ShouldBeEmpty();
    }

    private sealed class ThrowingHook : IDetailCompletionHook
    {
        public Task OnDetailCompletedAsync(Track track, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }
}
