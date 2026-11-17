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
/// <c>schema.migrations</c> (every id known to this app, else <c>archive-incompatible</c>), only tables and indexes with
/// ordinary, known tables (<see cref="KnownSchema"/>), and every stored path inside the layout. Nothing of the archive is
/// migrated or written here; restore then rebuilds the database from the migrations (<see cref="DatabaseRebuild"/>).
/// </summary>
public static class ArchivedDatabase
{
    /// <summary>Message of a damaged or foreign database.</summary>
    public const string CorruptText = "The database in the archive is damaged or not a StemStudio library; nothing was changed.";

    /// <summary>Message of a database whose schema differs from the one the migrations create.</summary>
    public const string ObjectsText = "The database in the archive contains triggers, views or tables that StemStudio never creates; nothing was changed.";

    /// <summary>A context on an archived (staged) database file whose connections run with <c>trusted_schema=OFF</c>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="readOnly">Open read-only.</param>
    /// <returns>The context (caller disposes).</returns>
    public static StemStudioDbContext Context(string path, bool readOnly = false) => new(new DbContextOptionsBuilder<StemStudioDbContext>()
        .UseSqlite(DatabaseSnapshot.StagingConnectionString(path, readOnly))
        .AddInterceptors(UntrustedSchemaInterceptor.Instance)
        .Options);

    /// <summary>A hardened connection on an archived database file (<c>trusted_schema=OFF</c>, defensive mode).</summary>
    /// <param name="path">The file.</param>
    /// <param name="readOnly">Open read-only.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The open connection (caller disposes).</returns>
    public static async Task<SqliteConnection> OpenAsync(string path, bool readOnly, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(path, readOnly));
        try
        {
            await connection.OpenAsync(cancellationToken);
            UntrustedSchemaInterceptor.Harden(connection);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Step 7 read-only: integrity, migration history against the manifest, known tables and indexes only.</summary>
    /// <param name="database">The staged database file.</param>
    /// <param name="manifest">The archive's manifest.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>archive-corrupt</c> or <c>archive-unsafe</c>.</exception>
    public static async Task CheckReadOnlyAsync(string database, BackupManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Schema.Migrations.Any(id => !KnownSchema.Migrations.Contains(id)))
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveIncompatible, ManifestRules.IncompatibleText);
        }

        bool unsafeObjects;
        try
        {
            await using var connection = await OpenAsync(database, readOnly: true, cancellationToken);
            if (!await DatabaseSnapshot.IsIntactAsync(connection, cancellationToken)
                || !(await BackupRun.MigrationsAsync(database, cancellationToken)).SequenceEqual(manifest.Schema.Migrations, StringComparer.Ordinal))
            {
                throw ArchiveReader.Corrupt(CorruptText);
            }

            unsafeObjects = await KnownSchema.HasUnsafeObjectsAsync(connection, cancellationToken);
        }
        catch (SqliteException)
        {
            throw ArchiveReader.Corrupt(CorruptText);
        }

        if (unsafeObjects)
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
