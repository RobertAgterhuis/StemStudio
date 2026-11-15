using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    public async Task<string?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
