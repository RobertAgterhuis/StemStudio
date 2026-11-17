using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// Rebuilds a staged database from this app's migrations (security review 013 item 2, CTO decision "rebuild"): a fresh
/// database is migrated to the archive's migration level and only row data is copied, table by table, for the tables of
/// that fresh schema, with column lists taken from the fresh schema (quoted identifiers, never archived DDL). The archived
/// file is then replaced, so no trigger, view, index, default, CHECK or generated column of the archive reaches the live
/// library; the later steps (forward migration, stored paths, consent, reconcile) run on the rebuilt database.
/// </summary>
public static class DatabaseRebuild
{
    private const string RebuiltName = "stemstudio.rebuilt.db";

    /// <summary>The fresh schema's data tables (SQLite's own and EF Core's history and lock tables are not copied).</summary>
    private const string TablesSql =
        "SELECT name FROM main.sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite!_%' ESCAPE '!' AND name NOT LIKE '!_!_EFMigrations%' ESCAPE '!'";

    /// <summary>Replaces the staged database by a rebuilt copy.</summary>
    /// <param name="database">The staged (archived) database file, already through the read-only check.</param>
    /// <param name="lastMigration">The archive's last migration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>archive-corrupt</c> when the rows do not fit the schema of their level.</exception>
    public static async Task RunAsync(string database, string lastMigration, CancellationToken cancellationToken)
    {
        var rebuilt = Path.Combine(Path.GetDirectoryName(database)!, RebuiltName);
        try
        {
            await using (var context = ArchivedDatabase.Context(rebuilt))
            {
                await context.GetService<IMigrator>().MigrateAsync(lastMigration, cancellationToken);
            }

            await CopyRowsAsync(rebuilt, database, cancellationToken);
        }
        catch (SqliteException exception)
        {
            File.Delete(rebuilt);
            throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveCorrupt, ArchivedDatabase.CorruptText, exception);
        }

        File.Move(rebuilt, database, overwrite: true);
    }

    /// <summary>A quoted SQLite identifier.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The quoted name.</returns>
    public static string Quote(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static async Task CopyRowsAsync(string rebuilt, string archived, CancellationToken cancellationToken)
    {
        await using var connection = await ArchivedDatabase.OpenAsync(rebuilt, readOnly: false, cancellationToken);
        await ExecuteAsync(connection, "PRAGMA foreign_keys=OFF", null, cancellationToken);
        await ExecuteAsync(connection, "ATTACH DATABASE $path AS archived", archived, cancellationToken);
        await ExecuteAsync(connection, "BEGIN", null, cancellationToken);
        foreach (var table in await NamesAsync(connection, TablesSql, null, cancellationToken))
        {
            var columns = string.Join(", ", (await NamesAsync(connection, "SELECT name FROM pragma_table_xinfo($path) WHERE hidden = 0", table, cancellationToken)).Select(Quote));
            await ExecuteAsync(connection, $"DELETE FROM main.{Quote(table)}", null, cancellationToken);
            await ExecuteAsync(connection, $"INSERT INTO main.{Quote(table)} ({columns}) SELECT {columns} FROM archived.{Quote(table)}", null, cancellationToken);
        }

        await ExecuteAsync(connection, "COMMIT", null, cancellationToken);

        await ExecuteAsync(connection, "DETACH DATABASE archived", null, cancellationToken);
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, string? path)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (path is not null)
        {
            command.Parameters.AddWithValue("$path", path);
        }

        return command;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, string? path, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql, path);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<string>> NamesAsync(SqliteConnection connection, string sql, string? path, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql, path);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
