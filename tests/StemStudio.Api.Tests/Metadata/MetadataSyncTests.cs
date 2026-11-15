using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Metadata;

/// <summary>Feature 005 T037: scheduling and synchronising the online lookup (FR-003, FR-009, FR-012; data-model 3.4).</summary>
public sealed class MetadataSyncTests : ApiTestBase
{
    internal const string MetadataJobId = "5d2c7e9f1a3b4c6d8e0f2a4b6c8d0e1f";
    internal static readonly Guid ArtistMbid = Guid.Parse("6b7c8d9e-0f1a-4b2c-8d3e-4f5a6b7c8d9e");
    private const string StandardJobId = "0123456789abcdef0123456789abcdef";
    private static readonly string CoverHash = "2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae";

    [Fact]
    public async Task AStandardPollWithADuration_SchedulesTheLookupWithTheReportedNames()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        ReportStandard(new WorkerSourceMetadata("Hero of the Day (Official Video)", "Metallica", null, "Load", "USEE19600011", 262.3, "Hero of the Day", "Metallica"));

        await Factory.SyncTracksAsync();

        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        metadata.Status.ShouldBe(MetadataStatus.Pending);
        (metadata.LookupTitle, metadata.LookupArtist, metadata.LookupAlbum, metadata.LookupDurationSeconds, metadata.Isrc)
            .ShouldBe(("Hero of the Day", "Metallica", "Load", 262.3, "USEE19600011"));
    }

    [Fact]
    public async Task WithoutLookupNames_TheTitleAndArtistAreUsed()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        ReportStandard(new WorkerSourceMetadata("Song", "Band", null, DurationSeconds: 200));

        await Factory.SyncTracksAsync();

        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        (metadata.LookupTitle, metadata.LookupArtist).ShouldBe(("Song", "Band"));
    }

    [Fact]
    public async Task AStandardPollWithoutADuration_DoesNotScheduleALookup()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        ReportStandard(new WorkerSourceMetadata("Song", "Band", null));

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.Metadata.Status.ShouldBe(MetadataStatus.None);
    }

    [Fact]
    public async Task ACompletionWithoutAnEarlierDuration_SchedulesTheLookup()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Completed, 100));

        await Factory.SyncTracksAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        (synced.Status, synced.Metadata.Status, synced.Metadata.LookupDurationSeconds).ShouldBe((TrackStatus.Completed, MetadataStatus.Pending, 213.04));
    }

    [Fact]
    public async Task OpeningAnOldCompletedTrack_SchedulesItsLookupOnce()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var dto = await (await Client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).BodyAsync<TrackDto>();

        dto.Metadata.Status.ShouldBe(MetadataStatusDto.Pending);
        (await Factory.FindTrackAsync(track.Id))!.Metadata.LookupTitle.ShouldBe(track.Title);
    }

    [Fact]
    public async Task APendingLookup_CreatesTheMetadataJobAndSearches()
    {
        var track = await SeedPendingAsync();

        await Factory.SyncMetadataAsync();

        var request = Worker.MetadataCreates.Single();
        (request.Title, request.Artist, request.Album, request.DurationSeconds, request.Isrc, request.StandardJobId)
            .ShouldBe(("Hero of the Day", "Metallica", "Load", 262.3, "USEE19600011", StandardJobId));
        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        (metadata.Status, metadata.MetadataJobId).ShouldBe((MetadataStatus.Searching, MetadataJobId));
    }

    [Fact]
    public async Task ARejectedCreation_FailsTheLookup()
    {
        var track = await SeedPendingAsync();
        Worker.OnCreateMetadata = _ => throw new WorkerRequestException(HttpStatusCode.UnprocessableEntity, "The request body is not valid.");

        await Factory.SyncMetadataAsync();

        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        (metadata.Status, metadata.Reason).ShouldBe((MetadataStatus.Failed, "The request body is not valid."));
    }

    [Fact]
    public async Task AnUnreachableWorker_KeepsPendingAndSearching()
    {
        var pending = await SeedPendingAsync("aaaaaaaaaaa");
        var searching = await SeedSearchingAsync("bbbbbbbbbbb");
        Worker.OnCreateMetadata = _ => throw new WorkerUnavailableException();
        Worker.OnGetMetadataJob = _ => throw new WorkerUnavailableException();

        await Factory.SyncMetadataAsync();

        (await Factory.FindTrackAsync(pending.Id))!.Metadata.Status.ShouldBe(MetadataStatus.Pending);
        (await Factory.FindTrackAsync(searching.Id))!.Metadata.Status.ShouldBe(MetadataStatus.Searching);
    }

    [Theory]
    [InlineData(WorkerMetadataJobState.Queued)]
    [InlineData(WorkerMetadataJobState.Running)]
    public async Task ARunningJob_KeepsSearching(WorkerMetadataJobState state)
    {
        var track = await SeedSearchingAsync();
        Worker.OnGetMetadataJob = id => Task.FromResult<WorkerMetadataJob?>(FakeWorkerClient.MetadataJob(id, state));

        await Factory.SyncMetadataAsync();

        (await Factory.FindTrackAsync(track.Id))!.Metadata.Status.ShouldBe(MetadataStatus.Searching);
    }

    [Fact]
    public async Task AFailedJob_FailsWithTheWorkerReason()
    {
        var track = await SeedSearchingAsync();
        Worker.OnGetMetadataJob = id => Task.FromResult<WorkerMetadataJob?>(FakeWorkerClient.MetadataJob(id, WorkerMetadataJobState.Failed, "Band information could not be retrieved; try again later."));

        await Factory.SyncMetadataAsync();

        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        (metadata.Status, metadata.Reason).ShouldBe((MetadataStatus.Failed, "Band information could not be retrieved; try again later."));
    }

    [Fact]
    public async Task AJobTheWorkerForgot_Fails()
    {
        var track = await SeedSearchingAsync();
        Worker.OnGetMetadataJob = _ => Task.FromResult<WorkerMetadataJob?>(null);

        await Factory.SyncMetadataAsync();

        (await Factory.FindTrackAsync(track.Id))!.Metadata.Status.ShouldBe(MetadataStatus.Failed);
    }

    [Fact]
    public async Task ARejectedResult_Fails()
    {
        var track = await SeedSearchingAsync();
        Completed();
        Worker.OnGetMetadataResult = _ => throw new WorkerRequestException(HttpStatusCode.Conflict, "The job has not completed.");

        await Factory.SyncMetadataAsync();

        (await Factory.FindTrackAsync(track.Id))!.Metadata.Status.ShouldBe(MetadataStatus.Failed);
    }

    [Fact]
    public async Task AFoundResult_StoresTheFactsTheArtistAndTheCover()
    {
        var track = await SeedSearchingAsync();
        Completed(FoundResult());

        await Factory.SyncMetadataAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        var metadata = synced.Metadata;
        (metadata.Status, metadata.RecordingTitle, metadata.Album, metadata.Year, metadata.ArtistName)
            .ShouldBe((MetadataStatus.Found, "Example Song", "Example Album", 1996, "The Example Band"));
        synced.ArtistId.ShouldBe(ArtistMbid);
        (synced.Artwork!.Source, synced.Artwork.Sha256).ShouldBe((ArtworkSource.CoverArtArchive, CoverHash));
        Worker.ArtworkDownloads.ShouldAllBe(reference => reference.Owner == WorkerArtworkOwner.MetadataJob && reference.Id == MetadataJobId);
        var artist = (await FindArtistAsync(ArtistMbid))!;
        (artist.Name, artist.Type, artist.Genres.Count, artist.Members.Count, artist.Summary!.Language).ShouldBe(("The Example Band", ArtistType.Group, 2, 2, "en"));
    }

    [Fact]
    public async Task WarningsBecomeTheReason()
    {
        var track = await SeedSearchingAsync();
        Completed(FoundResult() with { Warnings = ["Wikipedia could not be reached.", "The album cover could not be loaded."] });

        await Factory.SyncMetadataAsync();

        (await Factory.FindTrackAsync(track.Id))!.Metadata.Reason.ShouldBe("Wikipedia could not be reached. The album cover could not be loaded.");
    }

    [Fact]
    public async Task AFreshStoredArtist_IsKeptAndAStaleOneIsRefreshed()
    {
        await SeedArtistAsync("Stored Name", SyncTestData.T0.AddDays(-1));
        var first = await SeedSearchingAsync("aaaaaaaaaaa");
        Completed(FoundResult());
        await Factory.SyncMetadataAsync();
        (await FindArtistAsync(ArtistMbid))!.Name.ShouldBe("Stored Name");

        Factory.Clock.Advance(TimeSpan.FromDays(31));
        await SeedSearchingAsync("bbbbbbbbbbb");
        await Factory.SyncMetadataAsync();

        (await FindArtistAsync(ArtistMbid))!.Name.ShouldBe("The Example Band");
        (await Factory.FindTrackAsync(first.Id))!.ArtistId.ShouldBe(ArtistMbid);
    }

    [Fact]
    public async Task InvalidArtistFacts_FailTheLookup()
    {
        var track = await SeedSearchingAsync();
        var result = FoundResult();
        Completed(result with { Artist = result.Artist! with { Website = "http://insecure.example" } });

        await Factory.SyncMetadataAsync();

        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        metadata.Status.ShouldBe(MetadataStatus.Failed);
        metadata.Reason!.ShouldStartWith("The band information is invalid");
    }

    [Theory]
    [InlineData(WorkerMetadataOutcome.NotFound, MetadataStatus.NotFound)]
    [InlineData(WorkerMetadataOutcome.Disabled, MetadataStatus.Disabled)]
    public async Task OutcomesWithoutMatch_ClearTheArtistAndDeleteItWhenUnused(WorkerMetadataOutcome outcome, MetadataStatus status)
    {
        var track = await SeedSearchingAsync();
        Completed(FoundResult());
        await Factory.SyncMetadataAsync();
        await Factory.UpdateTrackAsync(track.Id, t => t.RetryLookup(null, null, SyncTestData.T0));
        await Factory.SyncMetadataAsync();
        Completed(new WorkerMetadataResult { Outcome = outcome });

        await Factory.SyncMetadataAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        (synced.Metadata.Status, synced.ArtistId).ShouldBe((status, null));
        (await FindArtistAsync(ArtistMbid)).ShouldBeNull();
    }

    [Fact]
    public async Task TheHostedService_RunsTheMetadataPass()
    {
        var track = await SeedPendingAsync();
        Factory.CreateClient().Dispose();

        Factory.Clock.Advance(TimeSpan.FromSeconds(1));

        await Eventually.WaitUntilAsync(async () => (await Factory.FindTrackAsync(track.Id))!.Metadata.Status == MetadataStatus.Searching);
    }

    internal static WorkerMetadataResult FoundResult() => new()
    {
        Outcome = WorkerMetadataOutcome.Found,
        Recording = new WorkerRecordingMatch(Guid.Parse("0f4e8a7c-3b2d-4c1e-9f8a-7b6c5d4e3f2a"), "Example Song", 262, 100, "isrc"),
        ReleaseGroup = new WorkerReleaseGroup(Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d"), "Example Album", "Album", 1996),
        Artwork = new WorkerArtworkInfo(WorkerArtworkSource.CoverArtArchive, CoverHash, 84211, 9120),
        Artist = new WorkerArtistInfo
        {
            Mbid = ArtistMbid,
            Name = "The Example Band",
            Type = WorkerArtistType.Group,
            Country = "US",
            BeginYear = 1981,
            Genres = ["heavy metal", "thrash metal"],
            Members = [new WorkerArtistMember("First Member", 1981), new WorkerArtistMember("Second Member", 1983, 2001)],
            Website = "https://www.example.org/",
            WikidataId = "Q15920",
            Summary = new WorkerWikipediaSummary
            {
                Language = "en",
                Title = "The Example Band",
                Extract = "A placeholder group.",
                PageUrl = "https://en.wikipedia.org/wiki/The_Example_Band",
                HistoryUrl = "https://en.wikipedia.org/w/index.php?title=The_Example_Band&action=history",
                RevisionId = "1255000000",
            },
            FetchedAt = SyncTestData.T0,
        },
    };

    private void Completed(WorkerMetadataResult? result = null)
    {
        Worker.OnCreateMetadata = _ => Task.FromResult(FakeWorkerClient.MetadataJob(MetadataJobId, WorkerMetadataJobState.Queued));
        Worker.OnGetMetadataJob = id => Task.FromResult<WorkerMetadataJob?>(FakeWorkerClient.MetadataJob(id, WorkerMetadataJobState.Completed));
        Worker.OnGetMetadataResult = _ => Task.FromResult(result ?? FoundResult());
    }

    private void ReportStandard(WorkerSourceMetadata metadata) =>
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Separating, 40) with { Metadata = metadata });

    private Task<Track> SeedPendingAsync(string videoId = "dQw4w9WgXcQ")
    {
        Worker.OnCreateMetadata ??= _ => Task.FromResult(FakeWorkerClient.MetadataJob(MetadataJobId, WorkerMetadataJobState.Queued));
        var track = SyncTestData.CompletedTrack(videoId);
        track.ScheduleLookup(new LookupQuery("Hero of the Day", "Metallica", "Load", 262.3, "USEE19600011"), SyncTestData.T0);
        return Factory.SeedAsync(track);
    }

    private Task<Track> SeedSearchingAsync(string videoId = "dQw4w9WgXcQ")
    {
        var track = SyncTestData.CompletedTrack(videoId);
        track.ScheduleLookup(new LookupQuery("Hero of the Day", "Metallica"), SyncTestData.T0);
        track.StartLookup(MetadataJobId, SyncTestData.T0);
        return Factory.SeedAsync(track);
    }

    private Task<Artist?> FindArtistAsync(Guid mbid) =>
        Factory.WithScopeAsync(sp => sp.GetRequiredService<IArtistRepository>().FindAsync(mbid, CancellationToken.None));

    private Task<bool> SeedArtistAsync(string name, DateTime fetchedAt) => Factory.WithScopeAsync(async sp =>
    {
        var artists = sp.GetRequiredService<IArtistRepository>();
        artists.Add(Artist.Create(new ArtistFacts { Mbid = ArtistMbid, Name = name, FetchedAt = fetchedAt }));
        await artists.SaveAsync(CancellationToken.None);
        return true;
    });
}
