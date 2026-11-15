using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StemStudio.Persistence.Tests;

/// <summary>A migrated SQLite database (in-memory by default) shared by several contexts.</summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection) => _connection = connection;

    public static async Task<SqliteTestDatabase> CreateAsync(string connectionString = "DataSource=:memory:")
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var database = new SqliteTestDatabase(connection);
        await using var context = database.NewContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return database;
    }

    /// <summary>Migrates a file database to an older migration and inserts one completed track with raw SQL (the current model has newer columns).</summary>
    public static async Task MigrateLegacyAsync(string connectionString, string migration)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync(migration, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tracks (Id, SourceType, SourceKey, Title, Status, Progress, CreatedAt, UpdatedAt, Stems, DetailPhase, Metadata)
            VALUES ('0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a', 'Youtube', 'youtube:dQw4w9WgXcQ', 'Song', 'Completed', 100, '2026-11-14 12:00:00', '2026-11-14 12:00:00', '[]', NULL, '{"Status":"None","CorrectedByUser":false}')
            """;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public StemStudioDbContext NewContext() =>
        new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(_connection).Options);

    public async Task SeedAsync(params Domain.Tracks.Track[] tracks)
    {
        await using var context = NewContext();
        var repository = new Repositories.TrackRepository(context);
        foreach (var track in tracks)
        {
            await repository.AddAsync(track, TestContext.Current.CancellationToken);
        }
    }

    public async Task<Domain.Tracks.Track> SaveAndReloadAsync(Domain.Tracks.Track track)
    {
        await SeedAsync(track);
        await using var read = NewContext();
        return (await new Repositories.TrackRepository(read).FindAsync(track.Id, TestContext.Current.CancellationToken))!;
    }

    public async Task<string?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
