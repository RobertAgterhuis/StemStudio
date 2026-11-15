using System.Net;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.TestSupport.LyricsTestData;

namespace StemStudio.Api.Tests.Lyrics;

/// <summary>Feature 006 T031: scheduling and synchronising the lyrics lookup (FR-001, FR-002, FR-013; data-model 3.2).</summary>
public sealed class LyricsSyncTests : ApiTestBase
{
    [Fact]
    public async Task AStandardPollWithADuration_StartsTheLyricsSearch()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        ReportStandard(new WorkerSourceMetadata("Fake Song (Official Video)", "The Fixture Band", null, "Fixture Album", null, 262.0, "Fake Song", "The Fixture Band"));

        await Factory.SyncTracksAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        (synced.LyricsStatus, synced.Lyrics!.LyricsJobId).ShouldBe((LyricsStatus.Searching, null));
    }

    [Fact]
    public async Task AStandardPollWithoutADuration_DoesNotSearch()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        ReportStandard(new WorkerSourceMetadata("Song", "Band", null));

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.None);
    }

    [Fact]
    public async Task ACompletionWithoutAnEarlierDuration_StartsTheSearch()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.Searching);
    }

    [Fact]
    public async Task OpeningAnOldCompletedTrack_StartsItsSearchOnce()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var dto = await (await Client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).BodyAsync<TrackDto>();
        await Client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        dto.LyricsStatus.ShouldBe(LyricsStatusDto.Searching);
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.Searching);
    }

    [Fact]
    public async Task ASearchWithoutAJob_CreatesTheLookupJobWithThe005LookupNames()
    {
        var track = await Factory.SeedAsync(Searching());
        Worker.OnCreateLyricsLookup = _ => Task.FromResult(FakeWorkerClient.LyricsJob(LookupJobId, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Queued));

        await Factory.SyncLyricsAsync();

        var request = Worker.LyricsLookups.Single();
        (request.Mode, request.Title, request.Artist, request.Album, request.DurationSeconds, request.StandardJobId)
            .ShouldBe(("lookup", "Fake Song", "The Fixture Band", "Fixture Album", 262.0, StandardJobId));
        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.LyricsJobId).ShouldBe((LyricsStatus.Searching, LookupJobId));
    }

    [Fact]
    public async Task WithoutLookupNames_TheTrackTitleArtistAndDurationAreUsed()
    {
        var track = SyncTestData.CompletedTrack();
        track.ApplyMetadata(new SourceMetadata("Song", "Band"), SyncTestData.T0);
        track.ScheduleLyricsLookup(SyncTestData.T0);
        await Factory.SeedAsync(track);

        await Factory.SyncLyricsAsync();

        var request = Worker.LyricsLookups.Single();
        (request.Title, request.Artist, request.Album, request.DurationSeconds).ShouldBe((track.Title, track.Artist, null, 213.04));
    }

    [Fact]
    public async Task ARejectedCreation_FailsTheLyrics()
    {
        var track = await Factory.SeedAsync(Searching());
        Worker.OnCreateLyricsLookup = _ => throw new WorkerRequestException(HttpStatusCode.UnprocessableEntity, "The request body is not valid.");

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Reason).ShouldBe((LyricsStatus.Failed, "The request body is not valid."));
    }

    [Fact]
    public async Task AnUnreachableWorker_KeepsTheState()
    {
        var waiting = await Factory.SeedAsync(Searching("aaaaaaaaaaa"));
        var polled = await Factory.SeedAsync(SearchingWithJob("bbbbbbbbbbb"));
        Worker.OnCreateLyricsLookup = _ => throw new WorkerUnavailableException();
        Worker.OnGetLyricsJob = _ => throw new WorkerUnavailableException();

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(waiting.Id))!.Lyrics!.LyricsJobId.ShouldBeNull();
        var lyrics = (await Factory.FindTrackAsync(polled.Id))!.Lyrics!;
        (lyrics.Status, lyrics.LyricsJobId).ShouldBe((LyricsStatus.Searching, LookupJobId));
    }

    [Fact]
    public async Task ARunningLookup_IsKept()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.Status.ShouldBe(LyricsStatus.Searching);
        Worker.LyricsDeletes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("lyrics-result-word", LyricsStatus.Found, SyncLevel.Word)]
    [InlineData("lyrics-result-line", LyricsStatus.Found, SyncLevel.Line)]
    [InlineData("lyrics-result-plain", LyricsStatus.AlignPending, SyncLevel.Plain)]
    [InlineData("lyrics-result-instrumental", LyricsStatus.Instrumental, SyncLevel.Plain)]
    public async Task ACompletedLookup_StoresTheDocumentAndDeletesTheWorkerJob(string example, LyricsStatus status, SyncLevel level)
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        CompleteLookupWith(example);

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Document!.SyncLevel, lyrics.LyricsJobId).ShouldBe((status, level, null));
        Worker.LyricsDeletes.ShouldBe([LookupJobId]);
    }

    [Theory]
    [InlineData("lyrics-result-not-found", LyricsStatus.NotFound)]
    [InlineData("lyrics-result-disabled", LyricsStatus.Disabled)]
    public async Task ALookupWithoutLyrics_ExplainsWhy(string example, LyricsStatus status)
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        CompleteLookupWith(example);

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Document, lyrics.Reason).ShouldBe((status, null, status == LyricsStatus.NotFound ? TrackLyrics.NotFoundReason : TrackLyrics.DisabledReason));
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(status);
    }

    [Fact]
    public async Task InvalidLyricsFromTheWorker_FailWithoutKeepingThem()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        var word = Document("lyrics-result-word");
        Worker.OnGetLyricsJob = id => Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => Task.FromResult(new WorkerLyricsResult { Outcome = WorkerLyricsOutcome.Found, Lyrics = word with { Language = "English" } });

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Document).ShouldBe((LyricsStatus.Failed, null));
        lyrics.Reason.ShouldStartWith(TrackLyrics.UnreadableReason);
        Worker.LyricsDeletes.ShouldBe([LookupJobId]);
    }

    [Fact]
    public async Task AWorkerThatForgotTheJob_FailsTheSearch()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        Worker.OnGetLyricsJob = _ => Task.FromResult<WorkerLyricsJob?>(null);

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Reason).ShouldBe((LyricsStatus.Failed, Api.Lyrics.LyricsJobApplier.ForgottenReason));
    }

    [Fact]
    public async Task AFailedLookup_FailsWithTheWorkerText()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        Worker.OnGetLyricsJob = id => Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Failed) with { Error = "Lyrics could not be retrieved; try again later." });

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Reason).ShouldBe((LyricsStatus.Failed, "Lyrics could not be retrieved; try again later."));
    }

    [Fact]
    public async Task AnUnreadableResult_FailsTheSearch()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        Worker.OnGetLyricsJob = id => Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => throw new WorkerRequestException(HttpStatusCode.Conflict, "Lyrics job is in state running.");

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Reason).ShouldBe((LyricsStatus.Failed, "Reading the lyrics failed: Lyrics job is in state running."));
    }

    [Fact]
    public async Task AFailedDeleteAfterTheCopy_KeepsTheLyrics()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        CompleteLookupWith("lyrics-result-word");
        Worker.OnDeleteLyrics = _ => throw new WorkerUnavailableException();

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.Status.ShouldBe(LyricsStatus.Found);
    }

    [Fact]
    public async Task AnUnreachableWorkerWhileReadingTheResult_KeepsSearching()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());
        Worker.OnGetLyricsJob = id => Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => throw new WorkerUnavailableException();

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.LyricsJobId).ShouldBe((LyricsStatus.Searching, LookupJobId));
        Worker.LyricsDeletes.ShouldBeEmpty();
    }

    [Fact]
    public async Task OneBrokenTrack_DoesNotStopTheOthers()
    {
        var brokenTrack = Searching("aaaaaaaaaaa");
        brokenTrack.Lyrics!.AssignJob(AlignJobId, SyncTestData.T0);
        var broken = await Factory.SeedAsync(brokenTrack);
        var healthy = await Factory.SeedAsync(SearchingWithJob("bbbbbbbbbbb"));
        Worker.OnGetLyricsJob = id => id == AlignJobId
            ? throw new InvalidOperationException("boom")
            : Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => Task.FromResult(Result("lyrics-result-word"));

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(broken.Id))!.Lyrics!.Status.ShouldBe(LyricsStatus.Searching);
        (await Factory.FindTrackAsync(healthy.Id))!.Lyrics!.Status.ShouldBe(LyricsStatus.Found);
    }

    [Fact]
    public async Task TheHostedService_RunsTheLyricsPass()
    {
        await Factory.SeedAsync(Searching());
        var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Worker.OnCreateLyricsLookup = _ =>
        {
            created.TrySetResult();
            return Task.FromResult(FakeWorkerClient.LyricsJob(LookupJobId, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Queued));
        };

        Factory.Clock.Advance(TimeSpan.FromSeconds(1));

        await created.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    private void CompleteLookupWith(string example)
    {
        Worker.OnGetLyricsJob = id => Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => Task.FromResult(Result(example));
    }

    private void ReportStandard(WorkerSourceMetadata metadata) =>
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Separating, 40) with { Metadata = metadata });
}
