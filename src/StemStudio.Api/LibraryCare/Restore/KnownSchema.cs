using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// What this app's migrations know (from the migrations assembly, never from an archive): the migration ids and every table
/// a migration creates or renames to at any level. The read-only check of an archived database (security review 013
/// item 2, CTO decision "rebuild") refuses any <c>sqlite_master</c> object that is not a table or an index, any
/// <c>pragma_table_list</c> entry that is not an ordinary table (virtual, shadow, view), whatever its name, and any table
/// that is not known; the restore then rebuilds the database from the migrations (<see cref="DatabaseRebuild"/>).
/// </summary>
public static class KnownSchema
{
    private static readonly string[] SqliteTables = ["sqlite_sequence", "sqlite_stat1", "sqlite_stat4", "__EFMigrationsHistory", "__EFMigrationsLock"];

    private static readonly Lazy<(HashSet<string> Migrations, HashSet<string> Tables)> Known = new(Load);

    /// <summary>Every migration id of the app.</summary>
    public static IReadOnlySet<string> Migrations => Known.Value.Migrations;

    /// <summary>Every table a migration creates or renames to, plus SQLite's and EF Core's own tables (case-insensitive).</summary>
    public static IReadOnlySet<string> Tables => Known.Value.Tables;

    /// <summary>True when the database holds an object outside tables and indexes, a non-ordinary table, or an unknown table.</summary>
    /// <param name="connection">An open (read-only) connection to the archived database.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when unsafe.</returns>
    public static async Task<bool> HasUnsafeObjectsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var objects = await RowsAsync(connection, "SELECT type, name FROM sqlite_master", cancellationToken);
        var tables = await RowsAsync(connection, "SELECT type, name FROM pragma_table_list WHERE schema = 'main'", cancellationToken);
        return objects.Exists(row => row.Type is not ("table" or "index") || (row.Type == "table" && !Tables.Contains(row.Name)))
            || tables.Exists(row => row.Type != "table" || (!Tables.Contains(row.Name) && !IsSchemaTable(row.Name)));
    }

    private static bool IsSchemaTable(string name) => name is "sqlite_schema" or "sqlite_master";

    private static async Task<List<(string Type, string Name)>> RowsAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static (HashSet<string>, HashSet<string>) Load()
    {
        using var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite("Data Source=:memory:").Options);
        var assembly = context.GetService<IMigrationsAssembly>();
        var tables = new HashSet<string>(SqliteTables, StringComparer.OrdinalIgnoreCase);
        foreach (var migration in assembly.Migrations.Values)
        {
            tables.UnionWith(assembly.CreateMigration(migration, context.Database.ProviderName!).UpOperations.Select(TableName).OfType<string>());
        }

        return (assembly.Migrations.Keys.ToHashSet(StringComparer.Ordinal), tables);
    }

    private static string? TableName(MigrationOperation operation) => operation switch
    {
        CreateTableOperation create => create.Name,
        RenameTableOperation rename => rename.NewName,
        _ => null,
    };
}
