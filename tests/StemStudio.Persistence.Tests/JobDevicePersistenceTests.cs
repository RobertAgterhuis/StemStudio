using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Job devices in SQLite (feature 003, data-model.md 3.1): <c>Tracks.Device</c> and inside <c>Tracks.DetailPhase</c>.</summary>
public sealed class JobDevicePersistenceTests
{
    private const string LastMigrationOf002 = "20261115091608_AddMixDetailSettings";
    private const string Oom = "The GPU ran out of memory during Mega 53-stem; the step was repeated on the CPU.";

    private static readonly JobDevice WithFallback = new(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", true, Oom);
    private static readonly JobDevice OnCpu = new(ProcessingDeviceKind.Cpu, null, false, null);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TrackDevice_RoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.QueuedTrack();
        track.ApplyDevice(WithFallback, TestData.T0);

        var loaded = await db.SaveAndReloadAsync(track);

        loaded.Device.ShouldBe(WithFallback);
        (await db.ScalarAsync("SELECT Device FROM Tracks"))!.ShouldContain("\"Gpu\"");
    }

    [Fact]
    public async Task TrackWithoutDevice_StoresNull()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var loaded = await db.SaveAndReloadAsync(TestData.QueuedTrack());

        loaded.Device.ShouldBeNull();
        (await db.ScalarAsync("SELECT Device IS NULL FROM Tracks")).ShouldBe("1");
    }

    [Fact]
    public async Task DetailPhaseDevice_RoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.RunningDetailTrack();
        track.ApplyDetailDevice(WithFallback, TestData.T0.AddMinutes(8));

        var loaded = await db.SaveAndReloadAsync(track);

        loaded.DetailPhase!.Device.ShouldBe(WithFallback);
        loaded.Device.ShouldBeNull();
    }

    [Fact]
    public async Task ChangingTheDevicesOfATrackedTrack_IsSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.QueuedTrack();
        track.AssignJob("3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c", TestData.T0);
        await db.SeedAsync(track);

        await ApplyAndSaveTwiceAsync(db, track.Id, OnCpu, WithFallback);

        await using var read = db.NewContext();
        (await new TrackRepository(read).FindAsync(track.Id, Ct))!.Device.ShouldBe(WithFallback);
    }

    private static async Task ApplyAndSaveTwiceAsync(SqliteTestDatabase db, Guid trackId, JobDevice first, JobDevice second)
    {
        await using var context = db.NewContext();
        var repository = new TrackRepository(context);
        var tracked = (await repository.FindAsync(trackId, Ct))!;
        tracked.ApplyDevice(first, TestData.T0.AddMinutes(1));
        await repository.SaveAsync(Ct);
        tracked.ApplyDevice(second, TestData.T0.AddMinutes(2));
        await repository.SaveAsync(Ct);
    }

    [Fact]
    public async Task DatabaseFrom002_MigratesAndLoadsWithNullDevices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-002-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            var trackId = await Create002DatabaseAsync(connectionString);

            await AssertLoadsWithNullDevicesAsync(connectionString, trackId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task AssertLoadsWithNullDevicesAsync(string connectionString, Guid trackId)
    {
        await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
        await using var read = db.NewContext();
        var track = (await new TrackRepository(read).FindAsync(trackId, Ct))!;

        track.Device.ShouldBeNull();
        (track.DetailPhase!.Status, track.DetailPhase.Device).ShouldBe((DetailPhaseStatus.Running, null));
        read.Database.GetPendingMigrations().ShouldBeEmpty();
    }

    private static async Task<Guid> Create002DatabaseAsync(string connectionString)
    {
        var trackId = Guid.NewGuid();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using (var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options))
        {
            await context.GetService<IMigrator>().MigrateAsync(LastMigrationOf002, Ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = Rows002Sql;
        command.Parameters.AddWithValue("$track", trackId.ToString("D").ToUpperInvariant());
        await command.ExecuteNonQueryAsync(Ct);
        return trackId;
    }

    private const string Rows002Sql = """
        INSERT INTO Tracks (Id, SourceType, SourceKey, SourceUrl, Title, Artist, Status, Stage, Progress, Error, WorkerJobId,
            UploadFileName, DurationSeconds, Model, CreatedAt, UpdatedAt, BeatGrid, Stems, DetailPhase)
        VALUES ($track, 'Youtube', 'youtube:dQw4w9WgXcQ', 'https://youtu.be/dQw4w9WgXcQ', 'Song', NULL, 'Completed', 'Completed', 100,
            NULL, '3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c', NULL, 213.04, 'htdemucs_6s', '2026-11-14 12:00:00', '2026-11-14 12:06:00',
            '{"Beats":[0.52,1.05],"BeatsPerBar":4,"Bpm":113.5,"Confidence":0.87,"Downbeats":[0.52],"Reliable":true}',
            '[{"Name":"vocals","SizeBytes":1},{"Name":"drums","SizeBytes":1},{"Name":"bass","SizeBytes":1},{"Name":"guitar","SizeBytes":1},{"Name":"piano","SizeBytes":1},{"Name":"other","SizeBytes":1}]',
            '{"Status":"Running","Stage":"Splitting drums (1 of 3)","CurrentParent":"drums","Progress":12,"Error":null,"WorkerDetailJobId":"7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b","Parents":[],"RequestedAt":"2026-11-14T12:06:00Z","UpdatedAt":"2026-11-14T12:08:00Z"}');
        """;

}
