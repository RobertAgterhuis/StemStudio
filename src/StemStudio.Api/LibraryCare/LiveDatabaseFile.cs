using Microsoft.Data.Sqlite;

namespace StemStudio.Api.LibraryCare;

/// <summary>
/// The live database file is created only at start-up (013 CTO E2E finding): every other connection opens it with
/// <c>Mode=ReadWrite</c>, so a connection opened while a restore has moved the database aside fails instead of creating an
/// empty database that would take the real one's place.
/// </summary>
public static class LiveDatabaseFile
{
    /// <summary>The live connection string: a file database on the default create mode is opened read-write only.</summary>
    /// <param name="configured">The configured connection string.</param>
    /// <returns>The connection string the application uses.</returns>
    public static string ForLive(string configured)
    {
        var builder = new SqliteConnectionStringBuilder(configured);
        if (IsFile(builder) && builder.Mode == SqliteOpenMode.ReadWriteCreate)
        {
            builder.Mode = SqliteOpenMode.ReadWrite;
        }

        return builder.ToString();
    }

    /// <summary>Creates the empty database file of a read-write connection string when it does not exist (first start).</summary>
    /// <param name="liveConnectionString">The live connection string.</param>
    /// <returns>True when the file was created.</returns>
    public static bool EnsureCreated(string liveConnectionString)
    {
        var builder = new SqliteConnectionStringBuilder(liveConnectionString);
        if (!IsFile(builder) || builder.Mode != SqliteOpenMode.ReadWrite || File.Exists(builder.DataSource))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(builder.DataSource))!);
        builder.Mode = SqliteOpenMode.ReadWriteCreate;
        builder.Pooling = false;
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return true;
    }

    /// <summary>
    /// Puts the live file database in WAL mode (persistent in the file). EF Core only does this when it creates the database
    /// itself, which <see cref="EnsureCreated"/> pre-empts, and a restored library comes from a snapshot in
    /// <c>journal_mode=DELETE</c>. Without WAL a reader's shared lock makes a concurrent writer's commit fail with
    /// "database is locked" (flake round 022/023).
    /// </summary>
    /// <param name="liveConnectionString">The live connection string.</param>
    /// <returns>True when the file database is in WAL mode now; false for a non-file database.</returns>
    public static bool UseWal(string liveConnectionString)
    {
        var builder = new SqliteConnectionStringBuilder(liveConnectionString);
        if (!IsFile(builder))
        {
            return false;
        }

        builder.Pooling = false;
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        return string.Equals(command.ExecuteScalar() as string, "wal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFile(SqliteConnectionStringBuilder builder) =>
        builder.Mode != SqliteOpenMode.Memory
        && !string.IsNullOrEmpty(builder.DataSource)
        && !string.Equals(builder.DataSource, ":memory:", StringComparison.Ordinal)
        && !builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
}
