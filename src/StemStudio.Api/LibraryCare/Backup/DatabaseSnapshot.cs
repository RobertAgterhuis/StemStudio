using Microsoft.Data.Sqlite;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>
/// Copies the live database with the SQLite online backup API into a staging file (research R-02, ADR-022 Phase 0 P1):
/// readers and writers continue; a busy database is retried with back-off (200 ms .. 3.2 s, 5 attempts). The copy is
/// switched to <c>journal_mode=DELETE</c> (one file) and must pass <c>PRAGMA integrity_check</c>.
/// </summary>
/// <param name="clock">Clock of the back-off.</param>
/// <param name="logger">Logger (no paths).</param>
public sealed partial class DatabaseSnapshot(TimeProvider clock, ILogger logger)
{
    /// <summary>Back-off before each retry of a busy database.</summary>
    public static IReadOnlyList<TimeSpan> RetryDelays { get; } = [.. new[] { 200, 400, 800, 1600, 3200 }.Select(ms => TimeSpan.FromMilliseconds(ms))];

    /// <summary>Message of a database that stayed busy.</summary>
    public const string BusyText = "The database stayed busy; try again.";

    /// <summary>Writes a consistent copy of the live database.</summary>
    /// <param name="liveConnectionString">Connection string of the live database.</param>
    /// <param name="destination">The staging file (its folder is created).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>io-error</c>: busy after every retry, or the copy fails its integrity check.</exception>
    public async Task CreateAsync(string liveConnectionString, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var live = Open(new SqliteConnectionStringBuilder(liveConnectionString) { Pooling = false, DefaultTimeout = 1 }.ToString());
        await live.OpenAsync(cancellationToken);
        await WarnWithoutWalAsync(live, cancellationToken);
        await BackupWithRetriesAsync(live, destination, cancellationToken);
        await FinishCopyAsync(destination, cancellationToken);
    }

    /// <summary>Runs <c>PRAGMA integrity_check</c>.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when the result is <c>ok</c>.</returns>
    public static async Task<bool> IsIntactAsync(SqliteConnection connection, CancellationToken cancellationToken) =>
        string.Equals(await ScalarAsync(connection, "PRAGMA integrity_check", cancellationToken), "ok", StringComparison.Ordinal);

    /// <summary>A connection string for a staging file: no pooling, so the file can be moved and deleted at once.</summary>
    /// <param name="path">The file.</param>
    /// <param name="readOnly">Open read-only.</param>
    /// <returns>The connection string.</returns>
    public static string StagingConnectionString(string path, bool readOnly = false) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Pooling = false,
        Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    private static SqliteConnection Open(string connectionString) => new(connectionString);

    private async Task BackupWithRetriesAsync(SqliteConnection live, string destination, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var copy = Open(StagingConnectionString(destination));
                await copy.OpenAsync(cancellationToken);
                live.BackupDatabase(copy);
                return;
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                File.Delete(destination);
                if (attempt == RetryDelays.Count)
                {
                    throw new LibraryJobException(LibraryJobErrorCodeDto.IoError, BusyText);
                }

                await Task.Delay(RetryDelays[attempt], clock, cancellationToken);
            }
        }
    }

    private static async Task FinishCopyAsync(string destination, CancellationToken cancellationToken)
    {
        await using var copy = Open(StagingConnectionString(destination));
        await copy.OpenAsync(cancellationToken);
        await ScalarAsync(copy, "PRAGMA journal_mode=DELETE", cancellationToken);
        if (!await IsIntactAsync(copy, cancellationToken))
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.IoError, "The database copy is damaged; try again.");
        }
    }

    private async Task WarnWithoutWalAsync(SqliteConnection live, CancellationToken cancellationToken)
    {
        string? mode;
        try
        {
            mode = await ScalarAsync(live, "PRAGMA journal_mode", cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            mode = "busy";
        }

        if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
        {
            LogNotWal(logger, mode ?? "unknown");
        }
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The library database is not in WAL mode ({Mode}); a backup may wait for writers")]
    private static partial void LogNotWal(ILogger logger, string mode);
}
