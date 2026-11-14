using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Api.Storage;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Sync;

public sealed class JobSyncServiceTests : IDisposable
{
    private const string JobId = "0123456789abcdef0123456789abcdef";
    private readonly ApiFactory _factory = new();

    public JobSyncServiceTests() => _factory.CreateClient().Dispose();

    private FakeWorkerClient Worker => _factory.Worker;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Progress_IsCopiedForwardWithMetadata()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Separating, 47, "Separating stems") with
        {
            Metadata = new WorkerSourceMetadata("Never Gonna Give You Up", "Rick Astley", null),
        });

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Separating);
        synced.Progress.ShouldBe(47);
        synced.Stage.ShouldBe("Separating stems");
        synced.Title.ShouldBe("Never Gonna Give You Up");
        synced.Artist.ShouldBe("Rick Astley");
    }

    [Fact]
    public async Task Progress_BackwardStatusIsIgnored()
    {
        var track = SyncTestData.UrlTrack();
        track.ApplyProgress(new TrackProgress(TrackStatus.Analysing, "Analysing beats", 95), SyncTestData.T0);
        await _factory.SeedAsync(track);
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Downloading, 5));

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Analysing);
        synced.Progress.ShouldBe(95);
    }

    [Fact]
    public async Task Completed_DownloadsEveryStemBeforeMarkingTheTrackCompleted()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        var statusesDuringDownload = new List<TrackStatus>();
        Worker.OnDownloadStem = async (_, stem, destination) =>
        {
            statusesDuringDownload.Add((await _factory.FindTrackAsync(track.Id))!.Status);
            await destination.WriteAsync(System.Text.Encoding.UTF8.GetBytes("fLaC-" + stem));
        };

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Completed);
        synced.Progress.ShouldBe(100);
        synced.Model.ShouldBe("htdemucs_6s");
        synced.DurationSeconds.ShouldBe(213.04);
        synced.Title.ShouldBe("Never Gonna Give You Up");
        synced.BeatGrid!.Bpm.ShouldBe(113.5);
        synced.Stems.Select(s => s.Name).ShouldBe(StemCatalog.SixStem);
        statusesDuringDownload.ShouldAllBe(status => status != TrackStatus.Completed);
        var stems = Stems();
        foreach (var stem in synced.Stems)
        {
            var file = stems.FindStem(track.Id, stem.Name)!;
            file.Length.ShouldBe(stem.SizeBytes);
            (await File.ReadAllTextAsync(file.FullName, TestContext.Current.CancellationToken)).ShouldBe("fLaC-" + stem.Name);
        }
    }

    [Fact]
    public async Task Completed_FourStemModelStoresFourStems()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        Worker.OnGetResult = id => Task.FromResult(FakeWorkerClient.Result(id, "htdemucs", StemCatalog.FourStem));

        await SyncAsync();

        (await _factory.FindTrackAsync(track.Id))!.Stems.Select(s => s.Name).ShouldBe(StemCatalog.FourStem);
        Worker.StemDownloads.ShouldBe(StemCatalog.FourStem);
    }

    [Fact]
    public async Task Completed_DeletesTheApiUploadCopy()
    {
        var uploads = _factory.Services.GetRequiredService<IUploadStore>();
        var stored = await uploads.SaveAsync(new MemoryStream([1, 2, 3]), ".wav", TestContext.Current.CancellationToken);
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(stored.Sha256), new UploadedFile("a.wav", stored.FileName), SyncTestData.T0);
        track.AssignJob(JobId, SyncTestData.T0);
        await _factory.SeedAsync(track);
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));

        await SyncAsync();

        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Completed);
        uploads.Exists(stored.FileName).ShouldBeFalse();
    }

    [Fact]
    public async Task Completed_StemDownloadFailureFailsTheTrackWithReadableReason()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        Worker.OnDownloadStem = (_, stem, _) => stem == "piano"
            ? throw new WorkerRequestException(HttpStatusCode.NotFound, "Stem piano does not exist.")
            : Task.CompletedTask;

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Failed);
        synced.Error.ShouldBe("Downloading the stems failed: Stem piano does not exist.");
        synced.Stems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Completed_DiskFailureFailsTheTrackWithoutPaths()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        Worker.OnDownloadStem = (_, _, _) => throw new IOException(@"C:\secret\path is full");

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Failed);
        synced.Error.ShouldBe("Storing the stems failed: the disk could not be written.");
    }

    [Theory]
    [InlineData("htdemucs", 6, "stem set")]
    [InlineData("spleeter", 6, "Unknown separation model")]
    public async Task Completed_InvalidResultFailsTheTrack(string model, int stemCount, string reason)
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        Worker.OnGetResult = id => Task.FromResult(FakeWorkerClient.Result(id, model, StemCatalog.SixStem.Take(stemCount).ToList()));

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Failed);
        synced.Error!.ShouldStartWith("The processing result is invalid:");
        synced.Error.ShouldContain(reason);
    }

    [Fact]
    public async Task Completed_UnknownStemNameFailsTheTrack()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        Worker.OnGetResult = id => Task.FromResult(FakeWorkerClient.Result(id, stems: ["vocals", "../evil"]));

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Failed);
        synced.Error.ShouldBe("The processing result is invalid: unknown stem name.");
    }

    [Fact]
    public async Task Completed_WorkerUnavailableDuringDownloadKeepsStateForNextTick()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));
        Worker.OnGetResult = _ => throw new WorkerUnavailableException();

        await SyncAsync();

        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Queued);
    }

    [Fact]
    public async Task WorkerNotFound_FailsAsInterruptedByRestart()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = _ => Task.FromResult<WorkerJob?>(null);

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Failed);
        synced.Error.ShouldBe("interrupted by restart");
    }

    [Theory]
    [InlineData("Audio is longer than 15 minutes", "Audio is longer than 15 minutes")]
    [InlineData(null, "Processing failed.")]
    public async Task WorkerFailed_CopiesTheError(string? workerError, string expected)
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Failed, 3) with { Error = workerError });

        await SyncAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        synced.Status.ShouldBe(TrackStatus.Failed);
        synced.Error.ShouldBe(expected);
    }

    [Fact]
    public async Task WorkerUnavailable_KeepsStateAndRetriesNextTick()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = _ => throw new WorkerUnavailableException();

        await SyncAsync();
        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Queued);

        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Downloading, 4));
        await SyncAsync();
        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Downloading);
    }

    [Fact]
    public async Task OnlyActiveTracksWithJobAreSynced()
    {
        await _factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        await _factory.SeedAsync(SyncTestData.FailedTrack("bbbbbbbbbbb"));
        await _factory.SeedAsync(SyncTestData.UrlTrack("ccccccccccc", jobId: null));
        var polled = new List<string>();
        Worker.OnGetJob = id =>
        {
            polled.Add(id);
            return Task.FromResult<WorkerJob?>(null);
        };

        await SyncAsync();

        polled.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnexpectedErrorOnOneTrackDoesNotStopTheOthers()
    {
        var broken = await _factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        var healthy = await _factory.SeedAsync(SyncTestData.UrlTrack("bbbbbbbbbbb", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        Worker.OnGetJob = id => id.StartsWith('a')
            ? throw new InvalidOperationException("bug")
            : Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Separating, 20));

        await SyncAsync();

        (await _factory.FindTrackAsync(broken.Id))!.Status.ShouldBe(TrackStatus.Queued);
        (await _factory.FindTrackAsync(healthy.Id))!.Status.ShouldBe(TrackStatus.Separating);
    }

    [Fact]
    public async Task HostedService_PollsEveryIntervalOnTheFakeClock()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        var polled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Worker.OnGetJob = id =>
        {
            polled.TrySetResult();
            return Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Downloading, 2));
        };

        polled.Task.IsCompleted.ShouldBeFalse();
        _factory.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        await polled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await WaitUntilAsync(async () => (await _factory.FindTrackAsync(track.Id))!.Status == TrackStatus.Downloading);
    }

    [Fact]
    public async Task HostedService_SurvivesAFailingTick()
    {
        var calls = 0;
        var secondTick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var synchronizer = Substitute.For<ITrackSynchronizer>();
        synchronizer.SyncAllAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                return Task.FromException(new InvalidOperationException("database is locked"));
            }

            secondTick.TrySetResult();
            return Task.CompletedTask;
        });
        using var factory = new ApiFactory { ConfigureServices = services => services.AddScoped(_ => synchronizer) };
        factory.CreateClient().Dispose();

        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref calls) >= 1));
        factory.Clock.Advance(TimeSpan.FromSeconds(1));

        await secondTick.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private IStemFileStore Stems() => _factory.Services.GetRequiredService<IStemFileStore>();

    private Task<bool> SyncAsync() => _factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(TestContext.Current.CancellationToken);
        return true;
    });

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Condition not met.");
    }
}
