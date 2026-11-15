using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Detail phase and mix detail settings in SQLite (feature 002, data-model.md 3.1 and 3.2).</summary>
public sealed class DetailPersistenceTests
{
    private const string InitialMigration = "20261114215330_Initial";
    private const string DetailJobId = "7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompletedDetailPhase_RoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = DetailCompletedTrack();

        var loaded = await SaveAndReloadAsync(db, track);

        AssertSamePhase(loaded.DetailPhase!, track.DetailPhase!);
        loaded.DetailPhase!.RequestedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task RunningDetailPhase_RoundTripsWithJobAndCurrentParent()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        track.RequestDetail(TestData.T0.AddMinutes(7));
        track.AssignDetailJob(DetailJobId, TestData.T0.AddMinutes(7));
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), TestData.T0.AddMinutes(8));

        var loaded = await SaveAndReloadAsync(db, track);

        AssertSamePhase(loaded.DetailPhase!, track.DetailPhase!);
    }

    [Fact]
    public async Task TrackWithoutDetailPhase_StoresNull()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var loaded = await SaveAndReloadAsync(db, TestData.CompletedTrack());

        loaded.DetailPhase.ShouldBeNull();
        (await db.ScalarAsync("SELECT DetailPhase IS NULL FROM Tracks")).ShouldBe("1");
    }

    [Fact]
    public async Task DetailPhase_IsStoredAsJsonWithEnumNames()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SaveAndReloadAsync(db, DetailCompletedTrack());

        var json = await db.ScalarAsync("SELECT DetailPhase FROM Tracks");

        json.ShouldNotBeNull();
        json.ShouldContain("\"Completed\"");
        json.ShouldContain("\"SkippedSilent\"");
    }

    [Fact]
    public async Task ChangingTheDetailPhaseOfATrackedTrack_IsSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SaveAndReloadAsync(db, TestData.CompletedTrack());
        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            var tracked = (await repository.FindAsync(track.Id, Ct))!;
            tracked.RequestDetail(TestData.T0.AddMinutes(9));
            await repository.SaveAsync(Ct);
            tracked.AssignDetailJob(DetailJobId, TestData.T0.AddMinutes(10));
            await repository.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new TrackRepository(read).FindAsync(track.Id, Ct))!;
        (loaded.DetailPhase!.Status, loaded.DetailPhase.WorkerDetailJobId).ShouldBe((DetailPhaseStatus.Queued, DetailJobId));
    }

    [Fact]
    public async Task ListDetailActive_ReturnsTracksWithAQueuedOrRunningPhase()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var queued = TestData.CompletedTrack("aaaaaaaaaaa");
        queued.RequestDetail(TestData.T0);
        var running = TestData.CompletedTrack("bbbbbbbbbbb");
        running.RequestDetail(TestData.T0);
        running.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "x", "drums", 5), TestData.T0);
        var cancelled = TestData.CompletedTrack("ccccccccccc");
        cancelled.RequestDetail(TestData.T0);
        cancelled.CancelDetail(TestData.T0);
        await db.SeedAsync(TestData.CompletedTrack("ddddddddddd"), DetailCompletedTrack("eeeeeeeeeee"), cancelled, running, queued, TestData.QueuedTrack("fffffffffff"));

        await using var read = db.NewContext();
        var active = await new TrackRepository(read).ListDetailActiveAsync(Ct);

        active.Select(t => t.Id).ShouldBe([queued.Id, running.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task MixDetailSettings_RoundTrip()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = DetailCompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, MixWithDetail(), TestData.T0.AddHours(1));
        await db.SeedAsync(track);
        await using (var context = db.NewContext())
        {
            await new MixRepository(context).AddAsync(mix, Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new MixRepository(read).FindAsync(mix.Id, Ct))!;

        loaded.Expanded.ShouldBe(["drums", "vocals"]);
        loaded.DetailChannels.ShouldBe(mix.DetailChannels);
    }

    [Fact]
    public async Task MixWithoutDetailSettings_StoresEmptyArrays()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        await using (var context = db.NewContext())
        {
            await new MixRepository(context).AddAsync(Mix.Create(Guid.NewGuid(), track, TestData.Settings(), TestData.T0), Ct);
        }

        (await db.ScalarAsync("SELECT Expanded FROM Mixes")).ShouldBe("[]");
        (await db.ScalarAsync("SELECT DetailChannels FROM Mixes")).ShouldBe("[]");
    }

    [Fact]
    public async Task DatabaseFrom001_MigratesAndLoadsWithoutDetail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-legacy-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            var (trackId, mixId) = await CreateLegacyDatabaseAsync(connectionString);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await AssertLoadsWithoutDetailAsync(db, trackId, mixId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task AssertLoadsWithoutDetailAsync(SqliteTestDatabase db, Guid trackId, Guid mixId)
    {
        await using var read = db.NewContext();
        var track = (await new TrackRepository(read).FindAsync(trackId, Ct))!;
        var mix = (await new MixRepository(read).FindAsync(mixId, Ct))!;

        track.DetailPhase.ShouldBeNull();
        track.Stems.Count.ShouldBe(6);
        mix.Expanded.ShouldBeEmpty();
        mix.DetailChannels.ShouldBeEmpty();
        mix.Channels.Single().Stem.ShouldBe("guitar");
        read.Database.GetPendingMigrations().ShouldBeEmpty();
    }

    private static async Task<(Guid TrackId, Guid MixId)> CreateLegacyDatabaseAsync(string connectionString)
    {
        var (trackId, mixId) = (Guid.NewGuid(), Guid.NewGuid());
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using (var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options))
        {
            await context.GetService<IMigrator>().MigrateAsync(InitialMigration, Ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = LegacyRowsSql;
        command.Parameters.AddWithValue("$track", trackId.ToString("D").ToUpperInvariant());
        command.Parameters.AddWithValue("$mix", mixId.ToString("D").ToUpperInvariant());
        await command.ExecuteNonQueryAsync(Ct);
        return (trackId, mixId);
    }

    private const string LegacyRowsSql = """
        INSERT INTO Tracks (Id, SourceType, SourceKey, SourceUrl, Title, Artist, Status, Stage, Progress, Error, WorkerJobId,
            UploadFileName, DurationSeconds, Model, CreatedAt, UpdatedAt, BeatGrid, Stems)
        VALUES ($track, 'Youtube', 'youtube:dQw4w9WgXcQ', 'https://youtu.be/dQw4w9WgXcQ', 'Song', NULL, 'Completed', 'Completed', 100,
            NULL, '3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c', NULL, 213.04, 'htdemucs_6s', '2026-11-14 12:00:00', '2026-11-14 12:06:00',
            '{"Beats":[0.52,1.05],"BeatsPerBar":4,"Bpm":113.5,"Confidence":0.87,"Downbeats":[0.52],"Reliable":true}',
            '[{"Name":"vocals","SizeBytes":1},{"Name":"drums","SizeBytes":1},{"Name":"bass","SizeBytes":1},{"Name":"guitar","SizeBytes":1},{"Name":"piano","SizeBytes":1},{"Name":"other","SizeBytes":1}]');
        INSERT INTO Mixes (Id, TrackId, Name, NormalizedName, MasterVolume, CreatedAt, UpdatedAt, Channels, Loop, Metronome)
        VALUES ($mix, $track, 'Guitar practice', 'GUITAR PRACTICE', 1.0, '2026-11-14 13:00:00', '2026-11-14 13:00:00',
            '[{"Mute":true,"Pan":0.0,"Solo":false,"Stem":"guitar","Volume":0.0}]', NULL,
            '{"BeatsPerBar":4,"BpmOverride":null,"Enabled":true,"OffsetMs":0,"Volume":0.7}');
        """;

    private static Track DetailCompletedTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = TestData.CompletedTrack(videoId);
        track.RequestDetail(TestData.T0.AddMinutes(7));
        track.AssignDetailJob(DetailJobId, TestData.T0.AddMinutes(7));
        track.CompleteDetail(
        [
            new ParentOutcome("vocals", ParentOutcomeKind.Split, [new("lead", 10), new("backing", 20), new("rest", 30)]),
            new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 1), new("snare", 2), new("rest", 3)]),
            new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
            new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
            new ParentOutcome("other", ParentOutcomeKind.NotReliable, []),
        ],
            TestData.T0.AddMinutes(30));
        return track;
    }

    private static MixSettings MixWithDetail() => TestData.Settings("No snare") with
    {
        Expanded = ["drums", "vocals"],
        DetailChannels =
        [
            new DetailChannelSettings { Parent = "drums", Stem = "snare", Volume = 0.8, Pan = 0, Mute = true, Solo = false },
            new DetailChannelSettings { Parent = "vocals", Stem = "lead", Volume = 0.5, Pan = -0.3, Mute = false, Solo = true },
        ],
    };

    private static async Task<Track> SaveAndReloadAsync(SqliteTestDatabase db, Track track)
    {
        await db.SeedAsync(track);
        await using var read = db.NewContext();
        return (await new TrackRepository(read).FindAsync(track.Id, Ct))!;
    }


    private static void AssertSamePhase(DetailPhase loaded, DetailPhase expected)
    {
        loaded.ShouldBe(expected with { Parents = loaded.Parents });
        loaded.Parents.Count.ShouldBe(expected.Parents.Count);
        for (var i = 0; i < expected.Parents.Count; i++)
        {
            loaded.Parents[i].Stems.ShouldBe(expected.Parents[i].Stems);
            loaded.Parents[i].ShouldBe(expected.Parents[i] with { Stems = loaded.Parents[i].Stems });
        }
    }
}
