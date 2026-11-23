using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.InstrumentViews;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Insights;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Feature 020 T042: <see cref="QualityService"/>, <see cref="QualityJobApplier"/> and <see cref="QualityTrigger"/> (FR-001,
/// FR-002, data-model 5.1): requests and their 409/503 answers, one report at a time, the automatic runs, the synchroniser,
/// previous ratings, failures that keep the last ready stems, and <c>outdated</c>.
/// </summary>
public sealed class QualityServiceTests : ApiTestBase
{
    private ApiTestFactoryPair Pair => new(Factory, Client);

    [Fact]
    public async Task ARequest_QueuesAReport_WithTheSixStemsInOrder()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await Client.RequestQualityAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var request = Factory.InsightsWorker.QualityRequests.Single();
        request.Stems.Select(stem => (stem.Channel, stem.Kind)).ShouldBe(StemCatalog.SixStem.Select(name => (name, WorkerQualityInputKind.Standard)));
        request.Stems[0].Path.ShouldBe($"tracks/{track.Id:D}/stems/vocals.flac");
        (request.DetailParents.Count, request.Titles.Title).ShouldBe((0, track.Title));
        var stored = (await Factory.FindTrackAsync(track.Id))!.QualityReport!;
        (stored.Status, stored.WorkerJobId, stored.RequestedStemsVersion!.Length).ShouldBe((QualityReportStatus.Queued, Factory.InsightsWorker.OnlyJob(), StemsVersion.Length));
    }

    [Fact]
    public async Task ARequestOfASplitTrack_AddsTheKeptDetailAndRestStems()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        (await Client.RequestQualityAsync(track.Id)).Dispose();

        var request = Factory.InsightsWorker.QualityRequests.Single();
        request.Stems.Skip(6).Select(stem => (stem.Channel, stem.Kind)).ShouldBe([
            ("vocals.lead", WorkerQualityInputKind.Detail), ("vocals.backing", WorkerQualityInputKind.Detail), ("vocals.rest", WorkerQualityInputKind.Rest),
            ("drums.kick", WorkerQualityInputKind.Detail), ("drums.snare", WorkerQualityInputKind.Detail), ("drums.rest", WorkerQualityInputKind.Rest),
        ]);
        request.Stems[^1].Path.ShouldBe($"tracks/{track.Id:D}/detail/drums/rest.flac");
        request.DetailParents.ShouldBe([new WorkerDetailParentInput("vocals", null, null), new WorkerDetailParentInput("drums", null, null)]);
    }

    [Fact]
    public async Task Refusals_AreNotFoundAndTrackNotReady()
    {
        var processing = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var fourStems = await Factory.SeedAsync(AnalysisTestData.FourStem());

        using var missing = await Client.RequestQualityAsync(Guid.NewGuid());
        using var notDone = await Client.RequestQualityAsync(processing.Id);
        using var four = await Client.RequestQualityAsync(fourStems.Id);

        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await notDone.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/track-not-ready");
        (await four.JsonAsync()).GetProperty("detail").GetString().ShouldBe(QualityService.NotSixStems);
        (notDone.StatusCode, four.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.Conflict));
        Factory.InsightsWorker.QualityRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASecondRequestWhileActive_IsQualityRunning()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();

        using var again = await Client.RequestQualityAsync(track.Id);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/quality-running");
        Factory.InsightsWorker.QualityRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ParallelRequests_StartExactlyOneReport()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Client.RequestQualityAsync(track.Id)));

        responses.Count(response => response.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
        Factory.InsightsWorker.QualityRequests.Count.ShouldBe(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task AnUnreachableWorker_Is503_AndNothingIsStored()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Factory.InsightsWorker.Unavailable = true;

        using var response = await Client.RequestQualityAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/worker-unavailable");
        (await Factory.FindTrackAsync(track.Id))!.QualityReport.ShouldBeNull();
    }

    [Fact]
    public async Task ARefusedJob_IsStoredAsFailed_WithAFixedText_AndTheWorkersReasonIsLogged()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Factory.InsightsWorker.SubmitFailure = new WorkerRequestException(HttpStatusCode.NotFound, "tracks/x/stems/piano.flac does not exist.");

        using var response = await Client.RequestQualityAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var report = await response.BodyAsync<QualityReportDto>();
        (report.Status, report.Error).ShouldBe((QualityReportStatusDto.Failed, QualityService.WorkerRefused));
        QualityService.WorkerRefused.ShouldBe("The processing service could not make the quality report of this track.");
        Factory.Logs.For(nameof(QualityService)).ShouldContain(entry => entry.Message.Contains("piano.flac", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheSynchroniser_MovesProgress_ThenStoresTheResult_AndRemovesTheWorkerJob()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        var job = Factory.InsightsWorker.OnlyJob();

        Factory.InsightsWorker.SetQuality(job, WorkerRenderJobState.Running, 0.45);
        await Factory.SyncQualityAsync();
        var running = await Client.GetQualityAsync(track.Id);
        Factory.InsightsWorker.CompleteQuality(job);
        await Factory.SyncQualityAsync();
        var ready = await Client.GetQualityAsync(track.Id);

        (running.Status, running.Progress, running.Stems.Count).ShouldBe((QualityReportStatusDto.Running, 45, 0));
        (ready.Status, ready.Progress, ready.AnalyzerVersion, ready.Stems.Count).ShouldBe((QualityReportStatusDto.Ready, 100, "1.0.0", 2));
        ready.MeasuredAt.ShouldBe(ApiFactory.Start);
        Factory.InsightsWorker.Deleted.ShouldBe([$"quality:{job}"]);
        await Factory.SyncQualityAsync();
        Factory.InsightsWorker.Calls.Count(call => call.StartsWith("get-quality", StringComparison.Ordinal)).ShouldBe(2, "a ready report is not polled again");
    }

    [Fact]
    public async Task ASecondReport_ShowsThePreviousRatings_AndKeepsTheOldStemsWhileItRuns()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Pair.MeasureAsync(track.Id, QualityTestData.Result("fake-1", ("vocals", QualityRating.Poor), ("drums", QualityRating.Good)));

        (await Client.RequestQualityAsync(track.Id)).Dispose();
        var queued = await Client.GetQualityAsync(track.Id);
        var second = Factory.InsightsWorker.QualityJobs.Keys.Max(StringComparer.Ordinal)!;
        Factory.InsightsWorker.CompleteQuality(second, QualityTestData.Result("fake-1", ("vocals", QualityRating.Fair), ("drums", QualityRating.Good)));
        await Factory.SyncQualityAsync();
        var ready = await Client.GetQualityAsync(track.Id);

        (queued.Status, queued.Stems.Count, queued.Stems[0].Rating).ShouldBe((QualityReportStatusDto.Queued, 2, QualityRatingDto.Poor));
        ready.Stems.Select(stem => (stem.Rating, stem.PreviousRating)).ShouldBe([(QualityRatingDto.Fair, (QualityRatingDto?)QualityRatingDto.Poor), (QualityRatingDto.Good, QualityRatingDto.Good)]);
    }

    [Theory]
    [InlineData(WorkerRenderJobState.Failed, "The stems could not be read.", "The stems could not be read.")]
    [InlineData(WorkerRenderJobState.Cancelled, null, Track.QualityFailedDefault)]
    [InlineData(WorkerRenderJobState.Completed, null, Track.QualityFailedDefault)]
    public async Task AFailedRun_KeepsTheLastReadyStems(WorkerRenderJobState state, string? error, string expected)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Pair.MeasureAsync(track.Id, QualityTestData.SixGood());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        var job = Factory.InsightsWorker.QualityJobs.Keys.Max(StringComparer.Ordinal)!;
        Factory.InsightsWorker.SetQuality(job, state, 0.3, error);

        await Factory.SyncQualityAsync();

        var report = await Client.GetQualityAsync(track.Id);
        (report.Status, report.Error, report.Stems.Count, report.AnalyzerVersion).ShouldBe((QualityReportStatusDto.Failed, expected, 6, "fake-1"));
    }

    [Fact]
    public async Task ALostJob_FailsTheReport()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        Factory.InsightsWorker.QualityJobs.Clear();

        await Factory.SyncQualityAsync();

        (await Client.GetQualityAsync(track.Id)).Error.ShouldBe(QualityJobApplier.JobLost);
        Factory.InsightsWorker.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnusableResult_IsRefused_AndTheReportFails()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        var hostile = QualityTestData.Result("1.0.0", ("vocals", QualityRating.Good));
        hostile = hostile with { Stems = [hostile.Stems[0] with { Channel = "../../stemstudio.db" }] };
        Factory.InsightsWorker.CompleteQuality(Factory.InsightsWorker.OnlyJob(), hostile);

        await Factory.SyncQualityAsync();

        var report = await Client.GetQualityAsync(track.Id);
        (report.Status, report.Error, report.Stems.Count).ShouldBe((QualityReportStatusDto.Failed, QualityJobApplier.InvalidResult, 0));
        Factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("worker result was refused", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AResultWithANullStem_IsRefused()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        Factory.InsightsWorker.CompleteQuality(Factory.InsightsWorker.OnlyJob(), new WorkerQualityResult("1.0.0", 1, [null!]));

        await Factory.SyncQualityAsync();

        (await Client.GetQualityAsync(track.Id)).Error.ShouldBe(QualityJobApplier.InvalidResult);
    }

    [Fact]
    public async Task AnUnreachableWorker_KeepsTheStateForTheNextTick_AndAFailedDeleteIsOnlyLogged()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        Factory.InsightsWorker.Unavailable = true;

        await Factory.SyncQualityAsync();
        (await Client.GetQualityAsync(track.Id)).Status.ShouldBe(QualityReportStatusDto.Queued);

        Factory.InsightsWorker.Unavailable = false;
        Factory.InsightsWorker.CompleteQuality(Factory.InsightsWorker.OnlyJob());
        await Factory.WithScopeAsync(async sp =>
        {
            await new QualityJobApplier(sp.GetRequiredService<ITrackRepository>(), new DeleteFailingWorker(Factory.InsightsWorker), Factory.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<QualityJobApplier>.Instance).SyncAllAsync(Ct);
            return true;
        });

        (await Client.GetQualityAsync(track.Id)).Status.ShouldBe(QualityReportStatusDto.Ready);
    }

    [Fact]
    public async Task ACompletedSeparation_StartsAReportAutomatically()
    {
        await Factory.SeedAsync(SyncTestData.UrlTrack());
        Factory.CompleteSeparations();

        await Factory.SyncTracksAsync();

        var track = (await Factory.AllTracksAsync()).Single();
        track.Status.ShouldBe(TrackStatus.Completed);
        track.QualityReport!.Status.ShouldBe(QualityReportStatus.Queued);
        Factory.InsightsWorker.QualityRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ACompletedDetailSplit_StartsANewReport_WithTheDetailStems()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailQueued());
        Factory.Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));

        await Factory.SyncDetailAsync();
        await Factory.SyncDetailAsync();

        (await Factory.FindTrackAsync(track.Id))!.QualityReport!.Status.ShouldBe(QualityReportStatus.Queued);
        Factory.InsightsWorker.QualityRequests.Single().Stems.Count.ShouldBeGreaterThan(6);
    }

    [Fact]
    public async Task TheAutomaticRun_SkipsAnActiveReport_AndAnUnreachableWorker()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();

        var whileActive = await StartAutomaticallyAsync(track.Id);
        Factory.InsightsWorker.Unavailable = true;
        var other = await Factory.SeedAsync(SyncTestData.CompletedTrack("zzzzzzzzzzz"));
        var whileDown = await StartAutomaticallyAsync(other.Id);

        (whileActive, whileDown).ShouldBe((false, false));
        (await Factory.FindTrackAsync(other.Id))!.QualityReport.ShouldBeNull();
        Factory.InsightsWorker.QualityRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OutdatedFollowsTheAnalyzerVersionAndTheStemFiles()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Pair.MeasureAsync(track.Id, QualityTestData.SixGood("fake-1"));
        var current = await Client.GetQualityAsync(track.Id);

        Factory.InsightsWorker.Health = Factory.InsightsWorker.Health! with { AnalyzerVersion = "fake-2" };
        Factory.Clock.Advance(AnalyzerVersionCache.Lifetime);
        var newAnalyzer = await Client.GetQualityAsync(track.Id);
        Factory.InsightsWorker.HealthUnavailable = true;
        Factory.Clock.Advance(AnalyzerVersionCache.Lifetime);
        var unknownAnalyzer = await Client.GetQualityAsync(track.Id);
        WriteStem(track.Id, "bass");
        var newStems = await Client.GetQualityAsync(track.Id);

        (current.Outdated, newAnalyzer.Outdated, unknownAnalyzer.Outdated, newStems.Outdated).ShouldBe((false, true, false, true));
    }

    [Fact]
    public async Task OutdatedIsFalse_WithoutMeasuredStems()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        Factory.InsightsWorker.Health = null;

        (await Client.GetQualityAsync(track.Id)).Outdated.ShouldBeFalse();
    }

    [Fact]
    public async Task TheAutomaticRunCanBeSwitchedOff()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:AutoQualityReport"] = "false" } };
        factory.CreateClient().Dispose();
        await factory.SeedAsync(SyncTestData.UrlTrack());
        factory.CompleteSeparations();

        await factory.SyncTracksAsync();

        (await factory.AllTracksAsync()).Single().QualityReport.ShouldBeNull();
        factory.InsightsWorker.QualityRequests.ShouldBeEmpty();
    }

    private Task<bool> StartAutomaticallyAsync(Guid trackId) => Factory.WithScopeAsync(async sp =>
    {
        var track = (await sp.GetRequiredService<ITrackRepository>().FindAsync(trackId, CancellationToken.None))!;
        return await sp.GetRequiredService<QualityService>().StartAutomaticallyAsync(track, CancellationToken.None);
    });

    private void WriteStem(Guid trackId, string stem)
    {
        var path = Path.Combine(Factory.DataDir, "tracks", trackId.ToString("D"), "stems", stem + ".flac");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3]);
    }

    /// <summary>Passes every call to the fake but fails the job deletion.</summary>
    private sealed class DeleteFailingWorker(IWorkerInsightsClient inner) : IWorkerInsightsClient
    {
        public Task<WorkerQualityJob> CreateQualityJobAsync(WorkerQualityJobRequest request, CancellationToken cancellationToken) => inner.CreateQualityJobAsync(request, cancellationToken);

        public Task<WorkerQualityJob?> GetQualityJobAsync(string jobId, CancellationToken cancellationToken) => inner.GetQualityJobAsync(jobId, cancellationToken);

        public Task DeleteQualityJobAsync(string jobId, CancellationToken cancellationToken) => throw new WorkerRequestException(HttpStatusCode.BadRequest, "nope");

        public Task<WorkerHandoutJob> CreateHandoutJobAsync(WorkerHandoutJobRequest request, CancellationToken cancellationToken) => inner.CreateHandoutJobAsync(request, cancellationToken);

        public Task<WorkerHandoutJob?> GetHandoutJobAsync(string jobId, CancellationToken cancellationToken) => inner.GetHandoutJobAsync(jobId, cancellationToken);

        public Task DownloadHandoutFileAsync(string jobId, Stream destination, CancellationToken cancellationToken) => inner.DownloadHandoutFileAsync(jobId, destination, cancellationToken);

        public Task DeleteHandoutJobAsync(string jobId, CancellationToken cancellationToken) => inner.DeleteHandoutJobAsync(jobId, cancellationToken);

        public Task<WorkerInsightsHealth?> GetInsightsHealthAsync(CancellationToken cancellationToken) => inner.GetInsightsHealthAsync(cancellationToken);
    }
}
