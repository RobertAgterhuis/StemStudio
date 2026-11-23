using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Rendering;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 007 T029: library items in SQLite (data-model 4.1, migration AddAssets).</summary>
public sealed class AssetPersistenceTests
{
    private const string LastMigrationOf005 = "20261115143937_AddArtworkAndArtists";
    private const string JobId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
    private static readonly DateTime T0 = TestData.T0;
    private static readonly Guid TrackA = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    private static readonly Guid TrackB = Guid.Parse("1b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadyAsset_RoundTripsFilesAudioAndProvenance()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var asset = Ready(MixedAsset(TrackA, "Song - 6 channels", T0));

        await AddAsync(db, asset);

        await using var read = db.NewContext();
        var loaded = (await new AssetRepository(read).FindAsync(asset.Id, Ct))!;
        (loaded.Name, loaded.Status, loaded.Progress, loaded.SizeBytes, loaded.TrackId).ShouldBe(("Song - 6 channels", AssetStatus.Ready, 100, 1234L, TrackA));
        loaded.Files.ShouldBe(asset.Files);
        loaded.Audio.ShouldBe(asset.Audio);
        var recipe = loaded.Provenance.Recipe!;
        (recipe.Mode, recipe.Range, recipe.Format).ShouldBe((ExportMode.Mix, new ExportRange(30, 75), new ExportFormat(AudioFormat.Mp3, null, 320)));
        recipe.Mix!.Channels.Single().ShouldBe(new ChannelSettings { Stem = "bass", Volume = 0.6, Pan = -0.5, Solo = true });
        recipe.Mix.DetailChannels.Single().Parent.ShouldBe("drums");
        recipe.Mix.Expanded.ShouldBe(["drums"]);
        recipe.Channels.ShouldBeNull();
        (loaded.Provenance.Contents, loaded.Provenance.TrackArtist).ShouldBe(("6 channels, mixed, 0:30-1:15", "Rick Astley"));
        (await new AssetRepository(read).FindAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task List_FiltersPagesAndCountsNewestFirst()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (oldest, middle, newest) = (Ready(RawAsset(TrackA, "Drums practice", T0)), RawAsset(TrackB, "Bass line", T0.AddMinutes(1)), Ready(RawAsset(TrackA, "drums again", T0.AddMinutes(2))));
        await AddAsync(db, oldest, middle, newest);

        await using var read = db.NewContext();
        var assets = new AssetRepository(read);
        (await assets.ListAsync(new AssetQuery(), Ct)).Items.Select(a => a.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
        var page = await assets.ListAsync(new AssetQuery { Limit = 1, Offset = 1 }, Ct);
        (page.Items.Single().Id, page.Total, page.TotalSizeBytes).ShouldBe((middle.Id, 3, 2468L));
        (await assets.ListAsync(new AssetQuery { TrackId = TrackA }, Ct)).Total.ShouldBe(2);
        (await assets.ListAsync(new AssetQuery { Status = AssetStatus.Queued }, Ct)).Items.Single().Id.ShouldBe(middle.Id);
        (await assets.ListAsync(new AssetQuery { NameContains = "DRUMS" }, Ct)).Items.Select(a => a.Id).ShouldBe([newest.Id, oldest.Id]);
        (await assets.ListAsync(new AssetQuery { Kind = AssetKind.Score }, Ct)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task ListActive_AndNames_ServeTheSynchroniserAndTheDefaultName()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var active = RawAsset(TrackA, "Active", T0);
        active.AssignJob(JobId, T0);
        await AddAsync(db, active, Ready(RawAsset(TrackA, "Done", T0)), RawAsset(TrackB, "Other", T0));

        await using var read = db.NewContext();
        var assets = new AssetRepository(read);
        (await assets.ListActiveAsync(Ct)).Select(a => a.Name).ShouldBe(["Active", "Other"], ignoreOrder: true);
        (await assets.NamesAsync(Ct)).ShouldBe(["Active", "Done", "Other"], ignoreOrder: true);
    }

    [Fact]
    public async Task StoredStatus_ReadsPastTheTrackedCopy()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var asset = RawAsset(TrackA, "Retried", T0);
        await AddAsync(db, asset);

        await using var read = db.NewContext();
        var assets = new AssetRepository(read);
        var tracked = (await assets.FindAsync(asset.Id, Ct))!;
        tracked.AssignJob(JobId, T0);
        tracked.Fail("boom", T0);

        (await assets.StoredStatusAsync(asset.Id, Ct)).ShouldBe(AssetStatus.Queued);
        (await assets.StoredStatusAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ChangesAndRemoval_AreSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var asset = RawAsset(TrackA, "Drums", T0);
        await AddAsync(db, asset);

        await using (var write = db.NewContext())
        {
            var assets = new AssetRepository(write);
            var tracked = (await assets.FindAsync(asset.Id, Ct))!;
            tracked.Rename("Renamed", T0.AddMinutes(1));
            tracked.AssignJob(JobId, T0.AddMinutes(1));
            tracked.ApplyProgress(0.5, T0.AddMinutes(1));
            await assets.SaveAsync(Ct);
        }

        await using (var read = db.NewContext())
        {
            var loaded = (await new AssetRepository(read).FindAsync(asset.Id, Ct))!;
            (loaded.Name, loaded.Status, loaded.Progress, loaded.WorkerJobId).ShouldBe(("Renamed", AssetStatus.Rendering, 50, JobId));
            await new AssetRepository(read).RemoveAsync(loaded, Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM Assets")).ShouldBe("0");
    }

    [Fact]
    public async Task ExistingTrackIds_AreTheOnesStillStored()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);

        await using var read = db.NewContext();
        var existing = await new TrackRepository(read).ExistingIdsAsync([track.Id, Guid.NewGuid()], Ct);

        existing.ShouldBe([track.Id]);
    }

    [Fact]
    public async Task DatabaseFrom005_MigratesWithAnEmptyAssetsTable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-005-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationOf005);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            (await db.ScalarAsync("SELECT COUNT(*) FROM Assets")).ShouldBe("0");
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            (await db.ScalarAsync("SELECT COUNT(*) FROM Tracks")).ShouldBe("1");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }


    private static async Task AddAsync(SqliteTestDatabase db, params Asset[] assets)
    {
        await using var context = db.NewContext();
        var repository = new AssetRepository(context);
        foreach (var asset in assets)
        {
            repository.Add(asset);
        }

        await repository.SaveAsync(Ct);
    }

    private static Asset Ready(Asset asset)
    {
        asset.AssignJob(JobId, asset.CreatedAt);
        asset.Complete([new AssetFile("audio", "audio.flac", "audio/flac", 1234, new string('a', 64))], new AudioFacts(AudioFormat.Flac, 16, null, 213.04, -0.3, -1.8), asset.CreatedAt);
        return asset;
    }

    private static Asset RawAsset(Guid trackId, string name, DateTime at) => Asset.Create(Guid.NewGuid(), name, new Provenance
    {
        Origin = AssetOrigin.Export,
        TrackId = trackId,
        TrackTitle = "Song",
        Contents = "drums, raw",
        Recipe = new ExportRecipe { TrackId = trackId, Mode = ExportMode.Raw, Channels = ["drums"], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
    }, at);

    private static Asset MixedAsset(Guid trackId, string name, DateTime at) => Asset.Create(Guid.NewGuid(), name, new Provenance
    {
        Origin = AssetOrigin.Export,
        TrackId = trackId,
        TrackTitle = "Never Gonna Give You Up",
        TrackArtist = "Rick Astley",
        Contents = "6 channels, mixed, 0:30-1:15",
        Recipe = new ExportRecipe
        {
            TrackId = trackId,
            Mode = ExportMode.Mix,
            Mix = new ExportMix(0.9, [new ChannelSettings { Stem = "bass", Volume = 0.6, Pan = -0.5, Solo = true }])
            {
                Expanded = ["drums"],
                DetailChannels = [new DetailChannelSettings { Parent = "drums", Stem = "kick", Volume = 0.9 }],
            },
            Range = new ExportRange(30, 75),
            Format = new ExportFormat(AudioFormat.Mp3, null, 320),
        },
    }, at);
}
