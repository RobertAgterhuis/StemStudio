using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests.Library;

/// <summary>022 T016/T033: the data access of filing, backfill and the folder index.</summary>
public sealed class LibraryRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmptyLibrary_HasAnEmptyStamp()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();

        (await new LibraryRepository(context).StampAsync(Ct)).ShouldBe(new LibraryStamp("0|-|-|0|-"));
    }

    [Fact]
    public async Task PendingItems_AreCountedListedAndFiled()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        var (older, newer) = (LibraryPersistenceTests.NewAsset(track.Id, "a"), LibraryPersistenceTests.NewAsset(track.Id, "b"));
        await using (var write = db.NewContext())
        {
            write.Assets.AddRange(older, newer);
            await write.SaveChangesAsync(Ct);
        }

        await using var context = db.NewContext();
        var library = new LibraryRepository(context);
        (await library.CountPendingAsync(Ct)).ShouldBe(2);
        var pending = await library.PendingAsync(1, Ct);
        pending.Single().File(LibraryPersistenceTests.PlacementFor(pending[0]), TestData.T0);
        await library.SaveAsync(Ct);

        (await library.CountPendingAsync(Ct)).ShouldBe(1);
        (await library.ItemsOfTrackAsync(track.Id, Ct)).Count.ShouldBe(2);
        (await library.LoadTracksAsync([track.Id, Guid.NewGuid()], Ct)).ShouldBe([track.Id]);
        (await library.FindTrackAsync(track.Id, Ct))!.Id.ShouldBe(track.Id);
        (await library.FindAssetAsync(older.Id, Ct)).ShouldNotBeNull();
        (await library.AllItemsAsync(Ct)).Count.ShouldBe(2);
        (await library.TrackArtAsync(Ct)).Single().TrackId.ShouldBe(track.Id);
        var stamp = (await library.StampAsync(Ct)).Value.Split('|');
        (stamp[0], stamp[3], stamp[2].StartsWith("2026-11-14 12:00:00", StringComparison.Ordinal)).ShouldBe(("2", "1", true));
    }
}
