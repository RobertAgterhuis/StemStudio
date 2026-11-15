using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

public sealed class StemStudioDbContextTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_MatchesTheModel()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();

        context.Database.HasPendingModelChanges().ShouldBeFalse();
        (await context.Database.GetAppliedMigrationsAsync(Ct)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Track_RoundTripsStemsBeatGridAndTimestamps()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await using (var context = db.NewContext())
        {
            await new TrackRepository(context).AddAsync(track, Ct);
        }

        await using var read = db.NewContext();
        var loaded = await new TrackRepository(read).FindAsync(track.Id, Ct);

        loaded.ShouldNotBeNull();
        AssertSameTrack(loaded, track);
    }

    [Fact]
    public async Task Track_EnumsAreStoredAsStrings()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var context = db.NewContext())
        {
            await new TrackRepository(context).AddAsync(TestData.QueuedTrack(), Ct);
        }

        (await db.ScalarAsync("SELECT Status FROM Tracks")).ShouldBe("Queued");
        (await db.ScalarAsync("SELECT SourceType FROM Tracks")).ShouldBe("Youtube");
        (await db.ScalarAsync("SELECT SourceKey FROM Tracks")).ShouldBe("youtube:dQw4w9WgXcQ");
    }

    [Fact]
    public async Task Track_SourceKeyIsUnique()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var context = db.NewContext())
        {
            await new TrackRepository(context).AddAsync(TestData.QueuedTrack(), Ct);
        }

        await using var second = db.NewContext();
        await Should.ThrowAsync<DuplicateKeyException>(() => new TrackRepository(second).AddAsync(TestData.QueuedTrack(), Ct));
    }

    [Fact]
    public async Task Track_FindByKeyAndOrderedList()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var older = TestData.QueuedTrack("aaaaaaaaaaa", TestData.T0);
        var newer = TestData.QueuedTrack("bbbbbbbbbbb", TestData.T0.AddHours(1));
        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            await repository.AddAsync(older, Ct);
            await repository.AddAsync(newer, Ct);
        }

        await using var read = db.NewContext();
        var tracks = new TrackRepository(read);
        (await tracks.FindByKeyAsync(SourceKey.FromStored("youtube:aaaaaaaaaaa"), Ct))!.Id.ShouldBe(older.Id);
        (await tracks.FindByKeyAsync(SourceKey.FromStored("youtube:ccccccccccc"), Ct)).ShouldBeNull();
        (await tracks.ListAsync(Ct)).Select(t => t.Id).ShouldBe([newer.Id, older.Id]);
    }

    [Fact]
    public async Task Track_ListActiveReturnsNonTerminalTracksWithJob()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var active = TestData.QueuedTrack("aaaaaaaaaaa");
        active.AssignJob("0123456789abcdef0123456789abcdef", TestData.T0);
        var withoutJob = TestData.QueuedTrack("bbbbbbbbbbb");
        var completed = TestData.CompletedTrack("ccccccccccc");
        await SeedTracksAsync(db, active, withoutJob, completed);

        await using var read = db.NewContext();
        (await new TrackRepository(read).ListActiveAsync(Ct)).Select(t => t.Id).ShouldBe([active.Id]);
    }

    [Fact]
    public async Task Track_ListOrphanedReturnsQueuedTracksWithoutJobUnchangedSinceTheCutoff()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var orphan = TestData.QueuedTrack("aaaaaaaaaaa");
        var recent = TestData.QueuedTrack("bbbbbbbbbbb", TestData.T0.AddMinutes(5));
        var withJob = TestData.QueuedTrack("ccccccccccc");
        withJob.AssignJob("0123456789abcdef0123456789abcdef", TestData.T0);
        var failed = TestData.QueuedTrack("ddddddddddd");
        failed.Fail("Import interrupted", TestData.T0);
        await SeedTracksAsync(db, orphan, recent, withJob, failed);

        await using var read = db.NewContext();
        var orphaned = await new TrackRepository(read).ListOrphanedAsync(TestData.T0.AddMinutes(5), Ct);

        orphaned.Select(t => t.Id).ShouldBe([orphan.Id]);
    }

    [Fact]
    public async Task Track_SaveAndRemovePersistChanges()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.QueuedTrack();
        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            await repository.AddAsync(track, Ct);
            track.Fail("boom", TestData.T0.AddMinutes(1));
            await repository.SaveAsync(Ct);
        }

        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            var loaded = await repository.FindAsync(track.Id, Ct);
            loaded!.Error.ShouldBe("boom");
            await repository.RemoveAsync(loaded, Ct);
        }

        await using var read = db.NewContext();
        (await new TrackRepository(read).FindAsync(track.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Mix_RoundTripsAllSettings()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, mix) = await SeedMixAsync(db);

        await using var read = db.NewContext();
        var loaded = await new MixRepository(read).FindAsync(mix.Id, Ct);

        loaded.ShouldNotBeNull();
        loaded.TrackId.ShouldBe(track.Id);
        AssertSameMix(loaded, mix);
    }

    [Fact]
    public async Task Mix_WithoutLoopRoundTripsNull()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (_, mix) = await SeedMixAsync(db, TestData.Settings() with { Loop = null, Channels = [] });

        await using var read = db.NewContext();
        var loaded = await new MixRepository(read).FindAsync(mix.Id, Ct);

        loaded!.Loop.ShouldBeNull();
        loaded.Channels.ShouldBeEmpty();
    }

    [Fact]
    public async Task Mix_NameIsUniquePerTrackIgnoringCase()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, _) = await SeedMixAsync(db);

        await using var context = db.NewContext();
        var duplicate = Mix.Create(Guid.NewGuid(), track, TestData.Settings(" GUITAR practice "), TestData.T0);

        await Should.ThrowAsync<DuplicateKeyException>(() => new MixRepository(context).AddAsync(duplicate, Ct));
    }

    [Fact]
    public async Task Mix_SameNameOnOtherTrackIsAllowed()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedMixAsync(db);

        var (_, other) = await SeedMixAsync(db, track: TestData.CompletedTrack("bbbbbbbbbbb"));

        other.Name.ShouldBe("Guitar practice");
    }

    [Fact]
    public async Task Mix_SaveDetectsRenameClash()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, first) = await SeedMixAsync(db);
        var (_, second) = await SeedMixAsync(db, TestData.Settings("Other"), track);

        await using var context = db.NewContext();
        var repository = new MixRepository(context);
        var loaded = await repository.FindAsync(second.Id, Ct);
        loaded!.Update(track, TestData.Settings(first.Name), TestData.T0.AddHours(1));

        await Should.ThrowAsync<DuplicateKeyException>(() => repository.SaveAsync(Ct));
    }

    [Fact]
    public async Task Mix_ListReturnsSummariesNewestFirstAndFiltersByTrack()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, first) = await SeedMixAsync(db);
        var (otherTrack, second) = await SeedMixAsync(db, TestData.Settings("Newer"), TestData.CompletedTrack("bbbbbbbbbbb"), TestData.T0.AddHours(2));

        await using var context = db.NewContext();
        var mixes = new MixRepository(context);
        var all = await mixes.ListAsync(null, Ct);
        var filtered = await mixes.ListAsync(track.Id, Ct);

        all.Select(m => m.Id).ShouldBe([second.Id, first.Id]);
        all[0].ShouldBe(new MixSummary(second.Id, otherTrack.Id, "Never Gonna Give You Up", "Newer", TestData.T0.AddHours(2)));
        all[0].UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        filtered.Select(m => m.Id).ShouldBe([first.Id]);
    }

    [Fact]
    public async Task Mix_NameQueries()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, first) = await SeedMixAsync(db);
        await SeedMixAsync(db, TestData.Settings("Second"), track);

        await using var context = db.NewContext();
        var mixes = new MixRepository(context);

        (await mixes.NamesForTrackAsync(track.Id, Ct)).Order(StringComparer.Ordinal).ShouldBe(["Guitar practice", "Second"]);
        (await mixes.IsNameTakenAsync(track.Id, "GUITAR PRACTICE", null, Ct)).ShouldBeTrue();
        (await mixes.IsNameTakenAsync(track.Id, "GUITAR PRACTICE", first.Id, Ct)).ShouldBeFalse();
        (await mixes.IsNameTakenAsync(Guid.NewGuid(), "GUITAR PRACTICE", null, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Mix_Remove()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (_, mix) = await SeedMixAsync(db);

        await using (var context = db.NewContext())
        {
            var repository = new MixRepository(context);
            await repository.RemoveAsync((await repository.FindAsync(mix.Id, Ct))!, Ct);
        }

        await using var read = db.NewContext();
        (await new MixRepository(read).FindAsync(mix.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Mix_ForeignKeyToTrackIsRestrict()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, _) = await SeedMixAsync(db);

        await using var context = db.NewContext();
        var repository = new TrackRepository(context);
        var loaded = await repository.FindAsync(track.Id, Ct);

        await Should.ThrowAsync<DbUpdateException>(() => repository.RemoveAsync(loaded!, Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Track_RemoveWithMixesDeletesTheTrackAndOnlyItsMixes(int mixCount)
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await SeedTracksAsync(db, track);
        for (var i = 0; i < mixCount; i++)
        {
            await SeedMixAsync(db, TestData.Settings($"Mix {i}"), track);
        }

        var (other, otherMix) = await SeedMixAsync(db, track: TestData.CompletedTrack("aaaaaaaaaaa"));

        await RemoveWithMixesAsync(db, track.Id);

        await using var read = db.NewContext();
        (await new TrackRepository(read).FindAsync(track.Id, Ct)).ShouldBeNull();
        (await new MixRepository(read).ListAsync(null, Ct)).Select(m => (m.Id, m.TrackId)).ShouldBe([(otherMix.Id, other.Id)]);
    }

    [Fact]
    public async Task FileDatabase_RoundTripsTrackAndMixAcrossContexts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            var track = TestData.CompletedTrack();
            var mix = Mix.Create(Guid.NewGuid(), track, TestData.Settings(), TestData.T0.AddHours(1));
            await using (var db = await SqliteTestDatabase.CreateAsync(connectionString))
            await using (var context = db.NewContext())
            {
                await new TrackRepository(context).AddAsync(track, Ct);
                await new MixRepository(context).AddAsync(mix, Ct);
            }

            await using var reopened = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = reopened.NewContext();
            AssertSameTrack((await new TrackRepository(read).FindAsync(track.Id, Ct))!, track);
            AssertSameMix((await new MixRepository(read).FindAsync(mix.Id, Ct))!, mix);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void DesignTimeFactory_CreatesSqliteContext()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        context.Database.IsSqlite().ShouldBeTrue();
    }

    [Fact]
    public void AddStemStudioPersistence_RegistersContextAndRepositories()
    {
        var services = new ServiceCollection().AddStemStudioPersistence("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITrackRepository>().ShouldBeOfType<TrackRepository>();
        scope.ServiceProvider.GetRequiredService<IMixRepository>().ShouldBeOfType<MixRepository>();
        scope.ServiceProvider.GetRequiredService<StemStudioDbContext>().Database.IsSqlite().ShouldBeTrue();
    }

    [Fact]
    public void DuplicateKeyException_HasStandardConstructors()
    {
        new DuplicateKeyException().Message.ShouldNotBeNullOrEmpty();
        new DuplicateKeyException("x").Message.ShouldBe("x");
        new DuplicateKeyException("x", new InvalidOperationException()).InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    private static async Task<(Track Track, Mix Mix)> SeedMixAsync(
        SqliteTestDatabase db, MixSettings? settings = null, Track? track = null, DateTime? now = null)
    {
        await using var context = db.NewContext();
        var tracks = new TrackRepository(context);
        var target = track ?? TestData.CompletedTrack();
        if (await tracks.FindAsync(target.Id, Ct) is null)
        {
            await tracks.AddAsync(target, Ct);
        }

        var mix = Mix.Create(Guid.NewGuid(), target, settings ?? TestData.Settings(), now ?? TestData.T0.AddHours(1));
        await new MixRepository(context).AddAsync(mix, Ct);
        return (target, mix);
    }

    private static async Task RemoveWithMixesAsync(SqliteTestDatabase db, Guid trackId)
    {
        await using var context = db.NewContext();
        var repository = new TrackRepository(context);
        await repository.RemoveWithMixesAsync((await repository.FindAsync(trackId, Ct))!, Ct);
    }

    private static async Task SeedTracksAsync(SqliteTestDatabase db, params Track[] tracks)
    {
        await using var context = db.NewContext();
        var repository = new TrackRepository(context);
        foreach (var track in tracks)
        {
            await repository.AddAsync(track, Ct);
        }
    }

    private static void AssertSameTrack(Track loaded, Track expected)
    {
        loaded.Key.ShouldBe(expected.Key);
        loaded.SourceType.ShouldBe(expected.SourceType);
        loaded.Status.ShouldBe(expected.Status);
        loaded.Title.ShouldBe(expected.Title);
        loaded.Artist.ShouldBe(expected.Artist);
        loaded.WorkerJobId.ShouldBe(expected.WorkerJobId);
        loaded.DurationSeconds.ShouldBe(expected.DurationSeconds);
        loaded.Model.ShouldBe(expected.Model);
        loaded.Stems.ShouldBe(expected.Stems);
        loaded.BeatGrid!.Beats.ShouldBe(expected.BeatGrid!.Beats);
        loaded.BeatGrid.Downbeats.ShouldBe(expected.BeatGrid.Downbeats);
        loaded.BeatGrid.ShouldBe(expected.BeatGrid with { Beats = loaded.BeatGrid.Beats, Downbeats = loaded.BeatGrid.Downbeats });
        loaded.CreatedAt.ShouldBe(expected.CreatedAt);
        loaded.UpdatedAt.ShouldBe(expected.UpdatedAt);
        loaded.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    private static void AssertSameMix(Mix loaded, Mix expected)
    {
        loaded.Name.ShouldBe(expected.Name);
        loaded.NormalizedName.ShouldBe(expected.NormalizedName);
        loaded.MasterVolume.ShouldBe(expected.MasterVolume);
        loaded.Channels.ShouldBe(expected.Channels);
        loaded.Metronome.ShouldBe(expected.Metronome);
        loaded.Loop.ShouldBe(expected.Loop);
        loaded.CreatedAt.ShouldBe(expected.CreatedAt);
        loaded.UpdatedAt.ShouldBe(expected.UpdatedAt);
        loaded.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
