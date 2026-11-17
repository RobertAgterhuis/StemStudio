using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// The checks of an archived database that restore and verify share (data-model 3.4 steps 7 and 8): opened read-only with
/// <c>PRAGMA trusted_schema=OFF</c>, <c>integrity_check</c> = <c>ok</c>, <c>__EFMigrationsHistory</c> equal to
/// <c>schema.migrations</c>, no SQL objects that run code (triggers, views, virtual tables: the known migrations create
/// none), and every stored path inside the layout. Nothing is migrated or written here.
/// </summary>
public static class ArchivedDatabase
{
    /// <summary>Message of a damaged or foreign database.</summary>
    public const string CorruptText = "The database in the archive is damaged or not a StemStudio library; nothing was changed.";

    /// <summary>Message of a database with triggers or views.</summary>
    public const string ObjectsText = "The database in the archive contains triggers or views that StemStudio never creates; nothing was changed.";

    private const string ExecutableObjectsSql =
        "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('trigger', 'view') OR (type = 'table' AND sql LIKE 'CREATE VIRTUAL TABLE%')";

    /// <summary>A context on an archived (staged) database file whose connections run with <c>trusted_schema=OFF</c>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="readOnly">Open read-only.</param>
    /// <returns>The context (caller disposes).</returns>
    public static StemStudioDbContext Context(string path, bool readOnly = false) => new(new DbContextOptionsBuilder<StemStudioDbContext>()
        .UseSqlite(DatabaseSnapshot.StagingConnectionString(path, readOnly))
        .AddInterceptors(UntrustedSchemaInterceptor.Instance)
        .Options);

    /// <summary>Step 7 read-only: integrity, migration history against the manifest, no triggers or views.</summary>
    /// <param name="database">The staged database file.</param>
    /// <param name="manifest">The archive's manifest.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>archive-corrupt</c> or <c>archive-unsafe</c>.</exception>
    public static async Task CheckReadOnlyAsync(string database, BackupManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        long objects;
        try
        {
            await using var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(database, readOnly: true));
            await connection.OpenAsync(cancellationToken);
            await UntrustedSchemaInterceptor.ApplyAsync(connection, cancellationToken);
            if (!await DatabaseSnapshot.IsIntactAsync(connection, cancellationToken)
                || !(await BackupRun.MigrationsAsync(database, cancellationToken)).SequenceEqual(manifest.Schema.Migrations, StringComparer.Ordinal))
            {
                throw ArchiveReader.Corrupt(CorruptText);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = ExecutableObjectsSql;
            objects = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            throw ArchiveReader.Corrupt(CorruptText);
        }

        if (objects > 0)
        {
            throw ArchiveReader.Unsafe(ObjectsText);
        }
    }

    /// <summary>Step 8 on a read-only context of the staged file (verify).</summary>
    /// <param name="database">The staged database file.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static async Task CheckStoredPathsAsync(string database, CancellationToken cancellationToken)
    {
        await using var context = Context(database, readOnly: true);
        await CheckStoredPathsAsync(context, cancellationToken);
    }

    /// <summary>Step 8: every stored path inside the layout; unreadable rows count as a damaged database.</summary>
    /// <param name="context">A context on the staged database.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>archive-unsafe</c> or <c>archive-corrupt</c>.</exception>
    public static async Task CheckStoredPathsAsync(StemStudioDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await StoredPaths.CheckAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is SqliteException or JsonException or InvalidOperationException)
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveCorrupt, CorruptText, exception);
        }
    }

    /// <summary>Runs <c>PRAGMA trusted_schema=OFF</c> on every connection it opens (defence in depth for archived SQL).</summary>
    private sealed class UntrustedSchemaInterceptor : DbConnectionInterceptor
    {
        public static readonly UntrustedSchemaInterceptor Instance = new();

        public static async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA trusted_schema=OFF";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA trusted_schema=OFF";
            command.ExecuteNonQuery();
        }

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) =>
            ApplyAsync(connection, cancellationToken);
    }
}
