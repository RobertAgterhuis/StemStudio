using System.Data.Common;
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
/// <c>schema.migrations</c>, a schema structurally equal to the one this app's migrations create at that level
/// (<see cref="ReferenceSchema"/>: no triggers, views, virtual or unknown tables, columns or indexes), and every stored path
/// inside the layout. Nothing of the archive is migrated or written here.
/// </summary>
public static class ArchivedDatabase
{
    /// <summary>Message of a damaged or foreign database.</summary>
    public const string CorruptText = "The database in the archive is damaged or not a StemStudio library; nothing was changed.";

    /// <summary>Message of a database whose schema differs from the one the migrations create.</summary>
    public const string ObjectsText = "The database in the archive contains triggers, views, tables, columns or indexes that StemStudio never creates; nothing was changed.";

    /// <summary>A context on an archived (staged) database file whose connections run with <c>trusted_schema=OFF</c>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="readOnly">Open read-only.</param>
    /// <returns>The context (caller disposes).</returns>
    public static StemStudioDbContext Context(string path, bool readOnly = false) => new(new DbContextOptionsBuilder<StemStudioDbContext>()
        .UseSqlite(DatabaseSnapshot.StagingConnectionString(path, readOnly))
        .AddInterceptors(UntrustedSchemaInterceptor.Instance)
        .Options);

    /// <summary>Step 7 read-only: integrity, migration history against the manifest, the reference schema of that level.</summary>
    /// <param name="database">The staged database file.</param>
    /// <param name="manifest">The archive's manifest.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>archive-corrupt</c> or <c>archive-unsafe</c>.</exception>
    public static async Task CheckReadOnlyAsync(string database, BackupManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        bool unknown;
        try
        {
            await using var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(database, readOnly: true));
            await connection.OpenAsync(cancellationToken);
            UntrustedSchemaInterceptor.Harden(connection);
            if (!await DatabaseSnapshot.IsIntactAsync(connection, cancellationToken)
                || !(await BackupRun.MigrationsAsync(database, cancellationToken)).SequenceEqual(manifest.Schema.Migrations, StringComparer.Ordinal))
            {
                throw ArchiveReader.Corrupt(CorruptText);
            }

            unknown = !await ReferenceSchema.MatchesAsync(connection, manifest.Schema.Migrations[^1], cancellationToken);
        }
        catch (SqliteException)
        {
            throw ArchiveReader.Corrupt(CorruptText);
        }

        if (unknown)
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

    /// <summary>
    /// Hardens every connection it opens (defence in depth for archived SQL): <c>PRAGMA trusted_schema=OFF</c> and
    /// <c>SQLITE_DBCONFIG_DEFENSIVE</c> (no <c>writable_schema</c>, no direct writes to shadow tables).
    /// </summary>
    private sealed class UntrustedSchemaInterceptor : DbConnectionInterceptor
    {
        public static readonly UntrustedSchemaInterceptor Instance = new();

        private const int DbConfigDefensive = 1010;

        public static void Harden(DbConnection connection)
        {
            if (connection is SqliteConnection sqlite)
            {
                SQLitePCL.raw.sqlite3_db_config(sqlite.Handle, DbConfigDefensive, 1, out _);
            }

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA trusted_schema=OFF";
            command.ExecuteNonQuery();
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Harden(connection);

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Harden(connection);
            return Task.CompletedTask;
        }
    }
}
