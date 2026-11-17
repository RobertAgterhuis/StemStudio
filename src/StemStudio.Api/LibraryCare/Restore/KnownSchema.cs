using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// The schema objects an archived database may contain (security review 013 item 2): every <c>sqlite_master</c> row must be
/// a <c>table</c> or an <c>index</c> whose name one of the app's migrations creates or renames to (any migration level, so
/// older archives pass) or the current model has, plus SQLite's own <c>sqlite_sequence</c>, <c>sqlite_stat1</c>/<c>4</c>
/// and <c>sqlite_autoindex_*</c> of a known table; and <c>pragma_table_list</c> must list ordinary tables only (no
/// virtual or shadow tables, no views). The allow-list is derived from the migrations assembly, never from the archive.
/// </summary>
public static class KnownSchema
{
    private static readonly string[] SqliteTables = ["sqlite_sequence", "sqlite_stat1", "sqlite_stat4", "sqlite_schema", "sqlite_master", "sqlite_temp_master", "sqlite_temp_schema"];

    private static readonly Lazy<HashSet<string>> Known = new(Load);

    /// <summary>Every table and index name the app's migrations or its model create (case-insensitive).</summary>
    public static IReadOnlySet<string> Names => Known.Value;

    /// <summary>True when the database contains an object outside the allow-list.</summary>
    /// <param name="connection">An open (read-only) connection to the archived database.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when unsafe.</returns>
    public static async Task<bool> HasUnknownObjectsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        foreach (var (type, name, table) in await RowsAsync(connection, "SELECT type, name, tbl_name FROM sqlite_master", cancellationToken))
        {
            if (!IsKnown(type, name, table))
            {
                return true;
            }
        }

        var tables = await RowsAsync(connection, "SELECT type, name, schema FROM pragma_table_list WHERE schema = 'main'", cancellationToken);
        return tables.Exists(row => !string.Equals(row.Type, "table", StringComparison.Ordinal));
    }

    /// <summary>True for a <c>table</c> or <c>index</c> row whose names are on the allow-list.</summary>
    /// <param name="type">The <c>sqlite_master.type</c>.</param>
    /// <param name="name">The object name.</param>
    /// <param name="table">The <c>tbl_name</c>.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsKnown(string type, string name, string table) => type switch
    {
        "table" => IsKnownTable(name) && string.Equals(name, table, StringComparison.OrdinalIgnoreCase),
        "index" => IsKnownTable(table) && (Names.Contains(name) || IsAutoIndex(name, table)),
        _ => false,
    };

    private static bool IsKnownTable(string name) => Names.Contains(name) || SqliteTables.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsAutoIndex(string name, string table) => name.StartsWith("sqlite_autoindex_" + table + "_", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<(string Type, string Name, string Table)>> RowsAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string, string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    private static HashSet<string> Load()
    {
        using var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite("Data Source=:memory:").Options);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__EFMigrationsHistory", "__EFMigrationsLock" };
        var assembly = context.GetService<IMigrationsAssembly>();
        foreach (var migration in assembly.Migrations.Values)
        {
            names.UnionWith(assembly.CreateMigration(migration, context.Database.ProviderName!).UpOperations.Select(CreatedName).OfType<string>());
        }

        foreach (var entity in context.Model.GetEntityTypes())
        {
            names.UnionWith(new[] { entity.GetTableName() }.Concat(entity.GetIndexes().Select(index => index.GetDatabaseName())).OfType<string>());
        }

        return names;
    }

    private static string? CreatedName(MigrationOperation operation) => operation switch
    {
        CreateTableOperation create => create.Name,
        CreateIndexOperation index => index.Name,
        RenameTableOperation rename => rename.NewName,
        RenameIndexOperation rename => rename.NewName,
        _ => null,
    };
}
