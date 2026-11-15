using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Artwork;

/// <summary>Feature 005 T026: source artwork from the standard job poll (FR-001, FR-002, FR-005, FR-007).</summary>
public sealed class ArtworkSyncTests : ApiTestBase
{
    internal static readonly string HashA = "9b74c9897bac770ffc029102a200c5de2c2f6f8b6e8e0b4f3b8a5a0f52a3c1d2";
    internal static readonly string HashB = new('b', 64);

    [Fact]
    public async Task APollWithNewArtwork_DownloadsBothVariantsAndOffersThem()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.Youtube, HashA));

        await Factory.SyncTracksAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        synced.Artwork!.Source.ShouldBe(ArtworkSource.Youtube);
        synced.Artwork.Sha256.ShouldBe(HashA);
        synced.Status.ShouldBe(TrackStatus.Separating);
        Worker.ArtworkDownloads.ShouldBe([Ref("cover"), Ref("thumb")], ignoreOrder: true);
        (await File.ReadAllBytesAsync(AutoFile(track.Id, "cover"), Ct)).ShouldBe(FakeWorkerClient.ArtworkBytes(Ref("cover")));
        (await File.ReadAllBytesAsync(AutoFile(track.Id, "thumb"), Ct)).ShouldBe(FakeWorkerClient.ArtworkBytes(Ref("thumb")));
    }

    [Fact]
    public async Task TheSameHash_IsNotDownloadedAgain()
    {
        await Factory.SeedAsync(SyncTestData.UrlTrack());
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.Youtube, HashA));
        await Factory.SyncTracksAsync();

        await Factory.SyncTracksAsync();

        Worker.ArtworkDownloads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ABetterSource_ReplacesTheAutomaticPictureAndAWorseOneIsNotDownloaded()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Report(WorkerJobState.Downloading, Info(WorkerArtworkSource.Youtube, HashA));
        await Factory.SyncTracksAsync();
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.Spotify, HashB));
        await Factory.SyncTracksAsync();
        Report(WorkerJobState.Analysing, Info(WorkerArtworkSource.Youtube, new string('c', 64)));

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.Artwork!.Sha256.ShouldBe(HashB);
        Worker.ArtworkDownloads.Count.ShouldBe(4);
    }

    [Fact]
    public async Task AMissingVariant_StoresNothingAndKeepsTheTrackGoing()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetArtwork = artwork => Task.FromResult(artwork.Variant == "thumb" ? null : FakeWorkerClient.ArtworkBytes(artwork));
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.Youtube, HashA));

        await Factory.SyncTracksAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        (synced.Artwork, synced.Status).ShouldBe((null, TrackStatus.Separating));
        File.Exists(AutoFile(track.Id, "cover")).ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnreachableWorkerDuringTheDownload_IsRetriedOnTheNextPoll()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetArtwork = _ => throw new WorkerUnavailableException();
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.Youtube, HashA));
        await Factory.SyncTracksAsync();
        (await Factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Separating);

        Worker.OnGetArtwork = null;
        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.Artwork!.Source.ShouldBe(ArtworkSource.Youtube);
    }

    [Fact]
    public async Task AnInvalidHash_IsLoggedAndIgnored()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.Youtube, "NOT-A-HASH"));

        await Factory.SyncTracksAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        (synced.Artwork, synced.Status).ShouldBe((null, TrackStatus.Separating));
        Worker.ArtworkDownloads.ShouldBeEmpty();
        Factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("ignoring the picture", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AWorkerUserPictureOnAJob_IsIgnored()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Report(WorkerJobState.Separating, Info(WorkerArtworkSource.User, HashA));

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.Artwork.ShouldBeNull();
        Worker.ArtworkDownloads.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACompletedCachedJobWithArtwork_CompletesWithThePicture()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Report(WorkerJobState.Completed, Info(WorkerArtworkSource.Embedded, HashA));

        await Factory.SyncTracksAsync();

        var synced = (await Factory.FindTrackAsync(track.Id))!;
        (synced.Status, synced.Artwork!.Source).ShouldBe((TrackStatus.Completed, ArtworkSource.Embedded));
    }

    internal static WorkerArtworkInfo Info(WorkerArtworkSource source, string sha) => new(source, sha, 48210, 6120);

    private static WorkerArtworkRef Ref(string variant) => new(WorkerArtworkOwner.Job, "0123456789abcdef0123456789abcdef", variant);

    private string AutoFile(Guid trackId, string variant) => Path.Combine(Factory.DataDir, "tracks", trackId.ToString(), "artwork", "auto", variant + ".jpg");

    private void Report(WorkerJobState state, WorkerArtworkInfo artwork) =>
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, state, 50) with { Artwork = artwork });
}
