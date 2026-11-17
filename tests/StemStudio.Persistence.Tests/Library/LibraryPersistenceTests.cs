using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Rendering;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests.Library;

/// <summary>022 T016: placement and library info columns, <c>FiledVersion</c> default 0, indexes, migration up/down (data-model 3).</summary>
public sealed class LibraryPersistenceTests
{
    private const string LastMigrationBefore022 = "20261116211814_AddMixPractice";
    private static readonly DateTime T0 = TestData.T0;
    private static readonly Guid TrackA = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Asset NewAsset(Guid? trackId = null, string title = "Enter Sandman") => Asset.Create(Guid.NewGuid(), title + " - drums", new Provenance
    {
        Origin = AssetOrigin.Export,
        TrackId = trackId ?? TrackA,
        TrackTitle = title,
        TrackArtist = "Metallica",
        Contents = "drums, raw",
        Recipe = new ExportRecipe { TrackId = trackId ?? TrackA, Mode = ExportMode.Raw, Channels = ["drums"], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
    }, T0);

    public static Placement PlacementFor(Asset asset, LibraryInfo? info = null) =>
        PlacementResolver.Resolve(new PlacementInput { Provenance = asset.Provenance, ItemInfo = info });

    [Fact]
    public async Task FiledAsset_RoundTripsPlacementAndLibraryInfo()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var asset = NewAsset();
        asset.SetLibraryInfo(new LibraryInfo { Album = "Black Album" }, T0);
        var placement = PlacementFor(asset, asset.LibraryInfo) with { Year = 1991 };
        asset.File(placement, T0.AddMinutes(1));
        await AddAsync(db, asset);

        await using var read = db.NewContext();
        var loaded = (await new AssetRepository(read).FindAsync(asset.Id, Ct))!;
        loaded.Placement.ShouldBe(placement);
        (loaded.FiledVersion, loaded.FiledAt, loaded.IsFiled).ShouldBe((LibraryFilingRules.Version, T0.AddMinutes(1), true));
        loaded.LibraryInfo.ShouldBe(new LibraryInfo { Album = "Black Album" });
        (await db.ScalarAsync("SELECT FolderAlbumSource || '|' || FolderArtistKey || '|' || FolderYear FROM Assets")).ShouldBe("item|metallica|1991");
    }

    [Fact]
    public async Task UnfiledAsset_HasNoPlacementAndVersion0()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var asset = NewAsset();
        await AddAsync(db, asset);

        await using var read = db.NewContext();
        var loaded = (await new AssetRepository(read).FindAsync(asset.Id, Ct))!;
        (loaded.Placement, loaded.FiledVersion, loaded.FiledAt).ShouldBe((null, 0, null));
        loaded.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
    }

    [Fact]
    public async Task Refile_UpdatesTheColumns()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var asset = NewAsset();
        asset.File(PlacementFor(asset), T0);
        await AddAsync(db, asset);

        await using (var write = db.NewContext())
        {
            var repository = new AssetRepository(write);
            var tracked = (await repository.FindAsync(asset.Id, Ct))!;
            tracked.File(PlacementFor(tracked, new LibraryInfo { Artist = "Other" }), T0.AddMinutes(2));
            await repository.SaveAsync(Ct);
        }

        (await db.ScalarAsync("SELECT FolderArtist || '|' || FolderArtistSource FROM Assets")).ShouldBe("Other|item");
    }

    [Fact]
    public async Task TrackLibraryInfo_RoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        track.SetLibraryInfo(new LibraryInfo { Artist = "Rick", Song = "Never" }, T0);

        var loaded = await db.SaveAndReloadAsync(track);

        loaded.LibraryInfo.ShouldBe(new LibraryInfo { Artist = "Rick", Song = "Never" });
    }

    [Fact]
    public async Task Indexes_Exist()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        (await db.ScalarAsync("SELECT sql FROM sqlite_master WHERE name = 'IX_Assets_Folder'")).ShouldBe(
            "CREATE INDEX \"IX_Assets_Folder\" ON \"Assets\" (\"FolderArtistKey\", \"FolderAlbumKey\", \"FolderSongKey\")");
        (await db.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'IX_Assets_FiledVersion'")).ShouldBe("1");
    }

    [Fact]
    public async Task Migration_DownAndUpOnAPre022Database_KeepsItemsUnfiled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-022-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await using (var db = await SqliteTestDatabase.CreateAsync(connectionString))
            {
                var asset = NewAsset();
                asset.File(PlacementFor(asset), T0);
                await AddAsync(db, asset);
            }

            await MigrateToAsync(connectionString, LastMigrationBefore022);
            (await ScalarAsync(connectionString, "SELECT COUNT(*) FROM pragma_table_info('Assets') WHERE name LIKE 'Folder%' OR name LIKE 'Filed%'")).ShouldBe("0");
            (await ScalarAsync(connectionString, "SELECT COUNT(*) FROM pragma_table_info('Tracks') WHERE name LIKE 'Library%'")).ShouldBe("0");

            await using var migrated = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = migrated.NewContext();
            var loaded = (await new AssetRepository(read).ListAsync(new AssetQuery(), Ct)).Items.Single();
            (loaded.Placement, loaded.FiledVersion, loaded.IsFiled).ShouldBe((null, 0, false));
            (await migrated.ScalarAsync("SELECT FiledVersion FROM Assets")).ShouldBe("0");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task MigrateToAsync(string connectionString, string migration)
    {
        await using var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connectionString).Options);
        await context.GetService<IMigrator>().MigrateAsync(migration, Ct);
    }

    private static async Task<string?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(Ct))?.ToString();
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
}
