using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T054: the storage overview (FR-020, FR-021, SC-006).</summary>
public sealed class StorageOverviewTests : ApiTestBase
{
    private const string Sha = "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b";
    private readonly LibraryFixture _library;

    public StorageOverviewTests() => _library = new LibraryFixture(Factory);

    [Fact]
    public async Task Overview_SumsEveryCategoryExactly_AndAttributesFilesToTracks()
    {
        var track = await _library.TrackAsync();
        await _library.ExportAsync(track);
        await _library.GenerationAsync(track);
        await _library.VoiceProfileAsync();
        await _library.PendingUploadAsync();
        await _library.FileAsync("tracks/x.tmp", 7);
        Worker.OnGetStorage = () => Task.FromResult(Storage(track.WorkerJobId!));

        using var response = await Client.GetAsync(ApiRoutes.StorageOverview(refresh: true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getStorageOverview");
        var overview = await response.BodyAsync<StorageOverviewDto>();
        var walked = _library.LibraryFiles().Keys.Sum(path => new FileInfo(_library.PathOf(path)).Length);
        var library = overview.Categories.Where(category => category.Category is not (StorageCategoryDto.Database or StorageCategoryDto.WorkerSeparationCache
            or StorageCategoryDto.WorkerDetailCache or StorageCategoryDto.WorkerJobFiles or StorageCategoryDto.WorkerScratch)).Sum(category => category.Bytes ?? 0);
        library.ShouldBe(walked);
        Bytes(overview, StorageCategoryDto.Stems).ShouldBe(6 * 64);
        Bytes(overview, StorageCategoryDto.Leftovers).ShouldBe(7);
        Bytes(overview, StorageCategoryDto.WorkerSeparationCache).ShouldBe(151);
        Bytes(overview, StorageCategoryDto.WorkerDetailCache).ShouldBe(402);
        overview.Categories.Single(category => category.Category == StorageCategoryDto.DetailStems).HowToRegenerate.ShouldNotBeNull();
        var row = overview.Tracks.Single(t => t.TrackId == track.Id);
        (row.StemsBytes, row.DetailStemsBytes, row.ArtworkBytes, row.ExportsBytes, row.GenerationsBytes, row.WorkerCacheBytes, row.SourceBytes, row.DetailFilesRemoved)
            .ShouldBe((384L, 192L, 16L, 48L, 40L, 553L, 151L, false));
        overview.Tracks.Select(t => t.TotalBytes).ShouldBeInOrder(SortDirection.Descending);
        overview.Unattached.VoiceProfilesBytes.ShouldBe(24);
        overview.ModelCaches.ShouldHaveSingleItem().Id.ShouldBe("torch");
        overview.Worker.Available.ShouldBeTrue();
        overview.Volumes.Select(volume => volume.Id).ShouldBe([StorageVolumeIdDto.Library, StorageVolumeIdDto.Backups, StorageVolumeIdDto.WorkerData, StorageVolumeIdDto.ModelCache]);
        overview.Volumes.Single(volume => volume.Id == StorageVolumeIdDto.WorkerData).SharedWith.ShouldContain("modelCache");
    }

    [Fact]
    public async Task WorkerUnavailable_GivesReasonsInsteadOfNumbers()
    {
        await _library.TrackAsync();

        var overview = await (await Client.GetAsync(ApiRoutes.StorageOverview(refresh: true), Ct)).BodyAsync<StorageOverviewDto>();

        (overview.Worker.Available, overview.Worker.Reason is not null).ShouldBe((false, true));
        overview.Categories.Single(category => category.Category == StorageCategoryDto.WorkerSeparationCache).Bytes.ShouldBeNull();
        overview.Tracks.Single().WorkerCacheBytes.ShouldBeNull();
        overview.Volumes.Single(volume => volume.Id == StorageVolumeIdDto.WorkerData).Reason.ShouldNotBeNull();
    }

    [Fact]
    public async Task Overview_IsCachedFor30SecondsUnlessRefreshed()
    {
        var first = await (await Client.GetAsync(ApiRoutes.StorageOverview(refresh: false), Ct)).BodyAsync<StorageOverviewDto>();
        await _library.TrackAsync();

        var cached = await (await Client.GetAsync(ApiRoutes.StorageOverview(refresh: false), Ct)).BodyAsync<StorageOverviewDto>();
        Factory.Clock.Advance(TimeSpan.FromSeconds(31));
        var expired = await (await Client.GetAsync(ApiRoutes.StorageOverview(refresh: false), Ct)).BodyAsync<StorageOverviewDto>();

        (first.Tracks.Count, cached.Tracks.Count, expired.Tracks.Count).ShouldBe((0, 0, 1));
        Factory.Services.GetRequiredService<StorageOverviewCache>().Invalidate();
    }

    [Fact]
    public async Task Overview_CountsBackupsAndARemovedDetailMark()
    {
        await _library.TrackAsync();
        await RestoreServiceTests.BackupAsync(Client, Factory);
        var removed = SyncTestData.DetailCompleted("eeeeeeeeeee");
        removed.RemoveDetailFiles(SyncTestData.T0);
        await Factory.SeedAsync(removed);

        var overview = await (await Client.GetAsync(ApiRoutes.StorageOverview(refresh: true), Ct)).BodyAsync<StorageOverviewDto>();

        (overview.Backups.Count, overview.Backups.FolderAvailable, overview.Backups.Bytes > 0).ShouldBe((1, true, true));
        overview.Tracks.Single(t => t.TrackId == removed.Id).DetailFilesRemoved.ShouldBeTrue();
    }

    [Fact]
    public async Task FiveHundredTracks_AreMeasuredQuickly()
    {
        for (var i = 0; i < 500; i++)
        {
            await Factory.SeedAsync(SyncTestData.CompletedTrack($"t{i:D10}"));
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var response = await Client.GetAsync(ApiRoutes.StorageOverview(refresh: true), Ct);

        (await response.BodyAsync<StorageOverviewDto>()).Tracks.Count.ShouldBe(500);
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    private static long? Bytes(StorageOverviewDto overview, StorageCategoryDto category) => overview.Categories.Single(usage => usage.Category == category).Bytes;

    private static WorkerStorage Storage(string jobId) => new(
        ApiFactory.Start,
        [new("data", 1000, 5000), new("models", 1000, 5000)],
        [new(Sha, 151, 400, 2, 0, [jobId], false, false, null)],
        5,
        2,
        0,
        [new("torch", 33, ["htdemucs_6s"])]);
}
