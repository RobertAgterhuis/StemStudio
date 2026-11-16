using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Mixes;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Practice settings of a mix in SQLite (feature 012, data-model.md 2.3): JSON column, NULL = defaults, migration.</summary>
public sealed class MixPracticePersistenceTests
{
    private const string LastMigrationBefore012 = "20261116165940_AddAgentSecrets";
    private const string LegacyTrackId = "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a";

    private static readonly PracticeSettings Practice = new()
    {
        SpeedPercent = 75,
        TransposeSemitones = -2,
        Trainer = new SpeedTrainerSettings { Enabled = true, StartPercent = 60, StepPercent = 4, TargetPercent = 95, PassesPerStep = 2 },
        CountIn = new CountInSettings { Bars = 1, EveryPass = true },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Practice_RoundTripsWithNestedTrainerAndCountIn()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var mix = await AddMixAsync(db, TestData.Settings() with { Practice = Practice });

        var loaded = await FindAsync(db, mix.Id);

        loaded.Practice.ShouldBe(Practice);
        loaded.Settings.Practice.ShouldBe(Practice);
    }

    [Fact]
    public async Task Practice_IsStoredAsOneJsonColumn()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await AddMixAsync(db, TestData.Settings() with { Practice = Practice });

        var json = await db.ScalarAsync("SELECT Practice FROM Mixes");

        json.ShouldNotBeNull();
        json.ShouldContain("\"SpeedPercent\":75");
        json.ShouldContain("\"Trainer\":{");
        json.ShouldContain("\"CountIn\":{");
    }

    [Fact]
    public async Task NullPractice_ReadsAsTheDefaults()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var mix = await AddMixAsync(db, TestData.Settings());
        await db.ScalarAsync("UPDATE Mixes SET Practice = NULL");

        var loaded = await FindAsync(db, mix.Id);

        loaded.Practice.ShouldBe(PracticeSettings.Default);
    }

    [Fact]
    public async Task ChangingThePracticeOfATrackedMix_IsSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var mix = await AddMixAsync(db, TestData.Settings());
        await using (var context = db.NewContext())
        {
            var repository = new MixRepository(context);
            var tracked = (await repository.FindAsync(mix.Id, Ct))!;
            var track = (await new TrackRepository(context).FindAsync(tracked.TrackId, Ct))!;
            tracked.Update(track, tracked.Settings with { Practice = Practice }, TestData.T0.AddHours(2));
            await repository.SaveAsync(Ct);
        }

        (await FindAsync(db, mix.Id)).Practice.ShouldBe(Practice);
    }

    [Fact]
    public async Task Duplicate_CopiesThePractice()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var mix = await AddMixAsync(db, TestData.Settings() with { Practice = Practice });
        var copy = mix.Duplicate(Guid.NewGuid(), [mix.Name], TestData.T0.AddHours(3));
        await using (var context = db.NewContext())
        {
            await new MixRepository(context).AddAsync(copy, Ct);
        }

        (await FindAsync(db, copy.Id)).Practice.ShouldBe(Practice);
    }

    [Fact]
    public async Task DatabaseFromBefore012_MigratesAndItsMixesLoadWithTheDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-011-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationBefore012);
            var mixId = await InsertLegacyMixAsync(connectionString);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            var mix = (await new MixRepository(read).FindAsync(mixId, Ct))!;
            mix.Practice.ShouldBe(PracticeSettings.Default);
            mix.Name.ShouldBe("Guitar practice");
            (await db.ScalarAsync("SELECT Practice IS NULL FROM Mixes")).ShouldBe("1");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task<Guid> InsertLegacyMixAsync(string connectionString)
    {
        var mixId = Guid.NewGuid();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Mixes (Id, TrackId, Name, NormalizedName, MasterVolume, CreatedAt, UpdatedAt, Channels, Loop, Metronome, Expanded, DetailChannels)
            VALUES ($mix, $track, 'Guitar practice', 'GUITAR PRACTICE', 1.0, '2026-11-14 13:00:00', '2026-11-14 13:00:00', '[]', NULL,
                '{"BeatsPerBar":4,"BpmOverride":null,"Enabled":true,"OffsetMs":0,"Volume":0.7}', '[]', '[]')
            """;
        command.Parameters.AddWithValue("$mix", mixId.ToString("D").ToUpperInvariant());
        command.Parameters.AddWithValue("$track", LegacyTrackId);
        await command.ExecuteNonQueryAsync(Ct);
        return mixId;
    }

    private static async Task<Mix> AddMixAsync(SqliteTestDatabase db, MixSettings settings)
    {
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        var mix = Mix.Create(Guid.NewGuid(), track, settings, TestData.T0.AddHours(1));
        await using var context = db.NewContext();
        await new MixRepository(context).AddAsync(mix, Ct);
        return mix;
    }

    private static async Task<Mix> FindAsync(SqliteTestDatabase db, Guid id)
    {
        await using var read = db.NewContext();
        return (await new MixRepository(read).FindAsync(id, Ct))!;
    }
}
