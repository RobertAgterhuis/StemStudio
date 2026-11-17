using Microsoft.Data.Sqlite;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// Recognises an item that something created at a live path while the switch had moved the real one aside (013 CTO E2E
/// finding: a SQLite connection on the default create mode made an empty database). Only such provably fresh, empty items
/// may be removed; anything else is treated as real data and is never deleted.
/// </summary>
public static class FreshItems
{
    /// <summary>The live database's companion files.</summary>
    public static IReadOnlyList<string> DatabaseCompanions { get; } = ["stemstudio.db-wal", "stemstudio.db-shm"];

    /// <summary>
    /// True for an empty folder, a 0-byte file, an <c>-shm</c> file whose <c>-wal</c> is absent or empty, and a SQLite
    /// database (with its <c>-wal</c>) that has no table at all.
    /// </summary>
    /// <param name="path">An existing path.</param>
    /// <returns>True when removing it cannot lose data.</returns>
    public static bool IsFreshEmpty(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Directory.Exists(path))
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        var file = new FileInfo(path);
        return file.Exists && (file.Length == 0 || HoldsNoData(path));
    }

    /// <summary>Removes a fresh, empty item (closing idle pooled connections first so the file is not held open).</summary>
    /// <param name="path">The path.</param>
    /// <exception cref="IOException">The item is still held open.</exception>
    public static void Remove(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: false);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static bool HoldsNoData(string path) =>
        path.EndsWith("-shm", StringComparison.Ordinal) ? IsAbsentOrEmpty(path[..^4] + "-wal") : path.EndsWith(".db", StringComparison.Ordinal) && HasNoTables(path);

    private static bool IsAbsentOrEmpty(string path) => !File.Exists(path) || new FileInfo(path).Length == 0;

    private static bool HasNoTables(string path)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master";
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0;
        }
        catch (SqliteException)
        {
            return false;
        }
    }
}

/// <summary>
/// One move of the switch or its rollback: clears the named fresh, empty items at the destination, then renames, retried
/// <see cref="LibrarySwitcher.MoveAttempts"/> times (idle pooled connections are closed between attempts). A source that
/// disappeared counts as moved. Refuses (returns false) at once when an item to clear holds data.
/// </summary>
/// <param name="files">File seam.</param>
/// <param name="retryDelay">Delay between attempts.</param>
public sealed class JournalMover(ILibraryFiles files, TimeSpan retryDelay)
{
    /// <summary>Moves <paramref name="source"/> to <paramref name="destination"/>.</summary>
    /// <param name="source">Existing item.</param>
    /// <param name="destination">Target path.</param>
    /// <param name="clear">Items to remove first when they exist (each must be fresh and empty).</param>
    /// <returns>True when moved; false when an item to clear holds data or the move kept failing.</returns>
    public async Task<bool> MoveAsync(string source, string destination, IReadOnlyList<string> clear)
    {
        ArgumentNullException.ThrowIfNull(clear);
        if (!clear.Where(Path.Exists).All(FreshItems.IsFreshEmpty))
        {
            return false;
        }

        for (var attempt = 0; attempt < LibrarySwitcher.MoveAttempts; attempt++)
        {
            // A -wal or -shm file disappears when the last connection closes; nothing to move.
            if (!Path.Exists(source) || TryMove(source, destination, clear))
            {
                return true;
            }

            await Task.Delay(retryDelay);
        }

        return false;
    }

    private bool TryMove(string source, string destination, IReadOnlyList<string> clear)
    {
        try
        {
            foreach (var item in clear.Where(Path.Exists))
            {
                FreshItems.Remove(item);
            }

            files.Move(source, destination);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An idle pooled connection opened during the switch may hold the file; close it before the next attempt.
            SqliteConnection.ClearAllPools();
            return false;
        }
    }
}
