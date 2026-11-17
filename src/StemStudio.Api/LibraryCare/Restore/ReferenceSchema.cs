using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// The structural schema check of an archived database (security review 013 item 2): its schema must equal, object by
/// object, the schema this app's own migrations produce at the archive's migration level. The reference is built once per
/// level by migrating an empty temporary database and described through pragmas, so formatting does not matter but
/// structure does: every <c>sqlite_master</c> object (type, name, table; no triggers or views exist in the reference),
/// every column (<c>pragma_table_xinfo</c>: type, not null, default, key, hidden/generated), the number of CHECK
/// constraints, foreign keys, every index (<c>pragma_index_list</c> / <c>pragma_index_xinfo</c>: unique, origin, partial,
/// columns, expression columns) with the WHERE clause of a partial index, and the <c>pragma_table_list</c> types (no
/// virtual or shadow tables). Only SQLite's own <c>sqlite_sequence</c> and <c>sqlite_stat*</c> tables and EF Core's
/// <c>__EFMigrationsLock</c> (present only when the database was migrated by EF, not by a SQL script) are ignored.
/// </summary>
public static partial class ReferenceSchema
{
    private const string Ignored = "('sqlite_sequence', 'sqlite_stat1', 'sqlite_stat4', '__EFMigrationsLock')";

    private const string ObjectsSql = "SELECT type, name, tbl_name, coalesce(sql, '') FROM sqlite_master WHERE name NOT IN " + Ignored;

    private const string ListSql = "SELECT schema, name, type FROM pragma_table_list WHERE schema = 'main' AND name NOT IN " + Ignored;

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> References = new(StringComparer.Ordinal);

    /// <summary>True when the archived schema equals the reference of its last migration.</summary>
    /// <param name="connection">An open (read-only) connection to the archived database.</param>
    /// <param name="lastMigration">The archive's last migration (already checked against its history).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when equal.</returns>
    /// <exception cref="LibraryJobException"><c>archive-incompatible</c> when this app does not know the migration.</exception>
    public static async Task<bool> MatchesAsync(SqliteConnection connection, string lastMigration, CancellationToken cancellationToken)
    {
        var reference = await ForAsync(lastMigration, cancellationToken);
        return reference.SequenceEqual(await DescribeAsync(connection, cancellationToken), StringComparer.Ordinal);
    }

    /// <summary>The structural description of a database, one sorted line per fact.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lines.</returns>
    public static async Task<IReadOnlyList<string>> DescribeAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var lines = new List<string>();
        foreach (var row in await RowsAsync(connection, ObjectsSql, null, cancellationToken))
        {
            lines.Add($"object|{row[0]}|{row[1]}|{row[2]}|{Clauses(row[0], row[3])}");
            lines.AddRange(await DetailsAsync(connection, row[0], row[1], cancellationToken));
        }

        lines.AddRange((await RowsAsync(connection, ListSql, null, cancellationToken)).Select(row => "list|" + string.Join('|', row)));
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static async Task<IEnumerable<string>> DetailsAsync(SqliteConnection connection, string type, string name, CancellationToken cancellationToken) => type switch
    {
        "table" => Prefixed("column|" + name, await RowsAsync(connection, "SELECT name, type, \"notnull\", coalesce(dflt_value, ''), pk, hidden FROM pragma_table_xinfo($name)", name, cancellationToken))
            .Concat(Prefixed("key|" + name, await RowsAsync(connection, "SELECT \"table\", \"from\", coalesce(\"to\", ''), on_update, on_delete FROM pragma_foreign_key_list($name)", name, cancellationToken)))
            .Concat(Prefixed("index|" + name, await RowsAsync(connection, "SELECT name, \"unique\", origin, partial FROM pragma_index_list($name)", name, cancellationToken))),
        "index" => Prefixed("indexcolumn|" + name, await RowsAsync(connection, "SELECT seqno, cid, coalesce(name, ''), \"desc\", coalesce(coll, ''), \"key\" FROM pragma_index_xinfo($name)", name, cancellationToken)),
        _ => [],
    };

    private static IEnumerable<string> Prefixed(string prefix, List<string[]> rows) => rows.Select(row => prefix + "|" + string.Join('|', row));

    /// <summary>The clauses pragmas do not show: CHECK constraints of a table, the WHERE clause of an index.</summary>
    private static string Clauses(string type, string sql)
    {
        var plain = Whitespace().Replace(Quoted().Replace(sql, "q"), " ").ToUpperInvariant();
        return type switch
        {
            "table" => "checks=" + CheckKeyword().Count(plain),
            "index" when plain.IndexOf(" WHERE ", StringComparison.Ordinal) is var at and >= 0 => "where=" + plain[at..].Trim(),
            _ => string.Empty,
        };
    }

    private static async Task<IReadOnlyList<string>> ForAsync(string lastMigration, CancellationToken cancellationToken)
    {
        if (References.TryGetValue(lastMigration, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(Path.GetTempPath(), "stemstudio-schema-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using (var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(DatabaseSnapshot.StagingConnectionString(path)).Options))
            {
                if (!context.Database.GetMigrations().Contains(lastMigration, StringComparer.Ordinal))
                {
                    throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveIncompatible, Archive.ManifestRules.IncompatibleText);
                }

                await context.GetService<IMigrator>().MigrateAsync(lastMigration, cancellationToken);
            }

            await using var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(path, readOnly: true));
            await connection.OpenAsync(cancellationToken);
            return References.GetOrAdd(lastMigration, await DescribeAsync(connection, cancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<List<string[]>> RowsAsync(SqliteConnection connection, string sql, string? name, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (name is not null)
        {
            command.Parameters.AddWithValue("$name", name);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<string[]>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)!)]);
        }

        return rows;
    }

    [GeneratedRegex("\"[^\"]*\"|'[^']*'|`[^`]*`|\\[[^\\]]*\\]|--[^\\n]*|/\\*.*?\\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Whitespace();

    [GeneratedRegex("\\bCHECK\\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CheckKeyword();
}
