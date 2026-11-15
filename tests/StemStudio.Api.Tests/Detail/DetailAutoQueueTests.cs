using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Detail;
using StemStudio.Api.Storage;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Detail;

/// <summary>Auto-queue after standard completion and the detail synchroniser (T051; FR-001, FR-007, FR-008, FR-019).</summary>
public sealed class DetailAutoQueueTests : IDisposable
{
    private const string DetailJobId = SyncTestData.DetailJobId;
    private readonly ApiFactory _factory = new();

    public DetailAutoQueueTests() => _factory.CreateClient().Dispose();

    private FakeWorkerClient Worker => _factory.Worker;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CompletingTheStandardJob_QueuesTheDetailPhaseInTheSameSave()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));

        await _factory.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(Ct);
            return true;
        });

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Completed);
        (synced.DetailPhase!.Status, synced.DetailPhase.WorkerDetailJobId, synced.DetailPhase.Progress).ShouldBe((DetailPhaseStatus.Queued, null, 0));
    }

    [Fact]
    public async Task FailingTheStandardJob_DoesNotQueueADetailPhase()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Failed, 10));

        await _factory.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(Ct);
            return true;
        });

        (await _factory.FindTrackAsync(track.Id))!.DetailPhase.ShouldBeNull();
    }

    [Fact]
    public async Task PendingSubmission_IsSentWithTheStandardJobAndTheJobIdIsStored()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        Worker.OnCreateDetail = _ => Task.FromResult(FakeWorkerClient.DetailJob(DetailJobId, WorkerDetailJobState.Queued));

        await SyncAsync();

        Worker.DetailCreates.ShouldBe([track.WorkerJobId!]);
        var phase = await PhaseAsync(track);
        (phase.Status, phase.WorkerDetailJobId).ShouldBe((DetailPhaseStatus.Queued, DetailJobId));
    }

    [Fact]
    public async Task PendingSubmission_StaysPendingWhileTheWorkerIsUnreachable()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        Worker.OnCreateDetail = _ => throw new WorkerUnavailableException();

        await SyncAsync();
        Worker.OnCreateDetail = null;
        var stillPending = await PhaseAsync(track);
        await SyncAsync();

        (stillPending.Status, stillPending.WorkerDetailJobId).ShouldBe((DetailPhaseStatus.Queued, null));
        Worker.DetailCreates.Count.ShouldBe(2);
        (await PhaseAsync(track)).WorkerDetailJobId.ShouldNotBeNull();
    }

    [Fact]
    public async Task PendingSubmission_RejectedByTheWorkerFailsThePhase()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        const string Missing = "The standard stems of this track are no longer available in the worker; re-import the track.";
        Worker.OnCreateDetail = _ => throw new WorkerRequestException(HttpStatusCode.Conflict, Missing);

        await SyncAsync();

        var phase = await PhaseAsync(track);
        (phase.Status, phase.Error).ShouldBe((DetailPhaseStatus.Failed, Missing));
        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Completed);
    }

    [Fact]
    public async Task PendingSubmission_CompletedFromTheWorkerCacheCompletesInOnePass()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        Worker.OnCreateDetail = _ => Task.FromResult(FakeWorkerClient.DetailJob(DetailJobId, WorkerDetailJobState.Completed, 100));

        await SyncAsync();

        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Completed);
        Worker.DetailStemDownloads.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Running_CopiesStageProgressAndCurrentParent()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(
            FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Running, 12, "drums") with { Stage = "Splitting drums (1 of 3)" });

        await SyncAsync();

        var phase = await PhaseAsync(track);
        (phase.Status, phase.Stage, phase.Progress, phase.CurrentParent).ShouldBe((DetailPhaseStatus.Running, "Splitting drums (1 of 3)", 12, "drums"));
    }

    [Fact]
    public async Task Completed_DownloadsTheResultAndEveryDetailStemBeforeCompleting()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));
        var statusesDuringDownload = new List<DetailPhaseStatus>();
        Worker.OnDownloadDetailStem = async (stem, destination) =>
        {
            statusesDuringDownload.Add((await PhaseAsync(track)).Status);
            await destination.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"fLaC-{stem.Parent}-{stem.Name}"), Ct);
        };

        await SyncAsync();

        var phase = await PhaseAsync(track);
        (phase.Status, phase.Progress, phase.Stage).ShouldBe((DetailPhaseStatus.Completed, 100, "Completed"));
        phase.Parents.Select(p => (p.Parent, p.Outcome)).ShouldBe(
        [
            ("vocals", ParentOutcomeKind.Split), ("drums", ParentOutcomeKind.Split), ("bass", ParentOutcomeKind.NotSupported),
            ("guitar", ParentOutcomeKind.NothingFurther), ("piano", ParentOutcomeKind.SkippedSilent), ("other", ParentOutcomeKind.NotReliable),
        ]);
        statusesDuringDownload.ShouldAllBe(status => status != DetailPhaseStatus.Completed);
        await AssertStoredAsync(track.Id, phase);
    }

    [Fact]
    public async Task WorkerForgotTheJob_FailsInterruptedByRestart()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = _ => Task.FromResult<WorkerDetailJob?>(null);

        await SyncAsync();

        var phase = await PhaseAsync(track);
        (phase.Status, phase.Error).ShouldBe((DetailPhaseStatus.Failed, "interrupted by restart"));
    }

    [Fact]
    public async Task WorkerFailed_FailsWithTheWorkerReason()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Failed, 30));

        await SyncAsync();

        var phase = await PhaseAsync(track);
        (phase.Status, phase.Error).ShouldBe((DetailPhaseStatus.Failed, "Not enough memory for detail separation."));
    }

    [Fact]
    public async Task WorkerFailedWithoutReason_UsesTheDefaultText()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Failed) with { Error = null });

        await SyncAsync();

        (await PhaseAsync(track)).Error.ShouldBe(Track.DetailFailedDefault);
    }

    [Fact]
    public async Task WorkerCancelled_CancelsThePhase()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Cancelled));

        await SyncAsync();

        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Cancelled);
    }

    [Fact]
    public async Task UnreachableWorker_KeepsThePhase()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = _ => throw new WorkerUnavailableException();

        await SyncAsync();

        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Queued);
    }

    [Fact]
    public async Task UnreachableWorkerDuringTheDownload_KeepsThePhaseForTheNextTick()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));
        Worker.OnDownloadDetailStem = (_, _) => throw new WorkerUnavailableException();

        await SyncAsync();

        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Queued);
    }

    public static TheoryData<Exception, string> DownloadFailures() => new()
    {
        { new WorkerRequestException(HttpStatusCode.NotFound, "Detail stem not found."), "Downloading the detail stems failed: Detail stem not found." },
        { new IOException("disk full"), "Storing the detail stems failed: the disk could not be written." },
    };

    [Theory]
    [MemberData(nameof(DownloadFailures))]
    public async Task FailingStemDownload_FailsThePhaseAndOffersNoStems(Exception failure, string reason)
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));
        Worker.OnDownloadDetailStem = (stem, destination) => stem.Name == "snare" ? throw failure : destination.WriteAsync(new byte[3], Ct).AsTask();

        await SyncAsync();

        var phase = await PhaseAsync(track);
        (phase.Status, phase.Error).ShouldBe((DetailPhaseStatus.Failed, reason));
        phase.Parents.ShouldBeEmpty();
        Directory.Exists(Path.Combine(_factory.DataDir, "tracks", track.Id.ToString(), "detail")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("drums", "lead")]
    [InlineData("strings", "rest")]
    public async Task InvalidResult_FailsThePhase(string parent, string name)
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));
        Worker.OnGetDetailResult = id => Task.FromResult(new WorkerDetailResult
        {
            DetailJobId = id,
            Parents = [FakeWorkerClient.DetailSplit(id, parent, "kick", name, "rest")],
        });

        await SyncAsync();

        var phase = await PhaseAsync(track);
        phase.Status.ShouldBe(DetailPhaseStatus.Failed);
        phase.Error.ShouldStartWith("The detail result is invalid");
    }

    [Fact]
    public async Task OneBrokenTrack_DoesNotStopTheOthers()
    {
        var broken = await _factory.SeedAsync(SyncTestData.DetailQueued("aaaaaaaaaaa", "a".PadRight(32, '0')));
        var healthy = await _factory.SeedAsync(SyncTestData.DetailQueued("bbbbbbbbbbb", "b".PadRight(32, '0')));
        Worker.OnGetDetailJob = id => id.StartsWith('a')
            ? throw new InvalidOperationException("bug")
            : Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Running, 5));

        await SyncAsync();

        (await PhaseAsync(broken)).Status.ShouldBe(DetailPhaseStatus.Queued);
        (await PhaseAsync(healthy)).Status.ShouldBe(DetailPhaseStatus.Running);
    }

    [Fact]
    public async Task HostedService_SubmitsPendingPhasesOnTheTick()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));

        _factory.Clock.Advance(TimeSpan.FromSeconds(1));

        await Eventually.WaitUntilAsync(async () => (await PhaseAsync(track)).WorkerDetailJobId is not null);
    }

    private async Task AssertStoredAsync(Guid trackId, DetailPhase phase)
    {
        var files = _factory.Services.GetRequiredService<IDetailStemFileStore>();
        foreach (var parent in phase.Parents)
        {
            foreach (var stem in parent.Stems)
            {
                var file = files.Find(new DetailStemKey(trackId, parent.Parent, stem.Name))!;
                file.Length.ShouldBe(stem.SizeBytes);
                (await File.ReadAllTextAsync(file.FullName, Ct)).ShouldBe($"fLaC-{parent.Parent}-{stem.Name}");
            }
        }
    }

    private async Task<DetailPhase> PhaseAsync(Track track) => (await _factory.FindTrackAsync(track.Id))!.DetailPhase!;

    private Task<bool> SyncAsync() => _factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<DetailSynchronizer>().SyncAllAsync(Ct);
        return true;
    });
}
