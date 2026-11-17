using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T031: consistent database snapshot with the online backup API (research R-02, R-04 Phase 0, FR-002).</summary>
public sealed class DatabaseSnapshotTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-snapshot-");
    private readonly ListLoggerProvider _logs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Live => Path.Combine(_root.FullName, "live.db");

    private string Copy => Path.Combine(_root.FullName, "staging", "stemstudio.db");

    [Fact]
    public async Task Snapshot_WhileAWriterLoops_NoWriteFailsAndTheCopyIsConsistent()
    {
        await CreateLiveAsync("wal");
        using var stop = new CancellationTokenSource();
        var writer = WriteLoopAsync(stop.Token);

        await Snapshot(TimeProvider.System).CreateAsync(ConnectionString(Live), Copy, Ct);
        await Task.Delay(100, Ct);
        await stop.CancelAsync();
        var (writes, failures) = await writer;

        (writes > 0, failures).ShouldBe((true, 0));
        (await ScalarAsync(Copy, "PRAGMA integrity_check")).ShouldBe("ok");
        (await ScalarAsync(Copy, "PRAGMA journal_mode")).ShouldBe("delete");
        long.Parse((await ScalarAsync(Copy, "SELECT count(*) FROM Items"))!, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
        File.Exists(Copy + "-wal").ShouldBeFalse();
        _logs.Entries.ShouldNotContain(entry => entry.Message.Contains("WAL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Snapshot_RetriesWhileAWriterHoldsTheDatabase_AndWarnsWithoutWal()
    {
        await CreateLiveAsync("delete");
        await using var writer = await OpenAsync(Live);
        await ExecuteAsync(writer, "BEGIN EXCLUSIVE; INSERT INTO Items (Value) VALUES ('held');");
        var release = Task.Run(async () =>
        {
            await Task.Delay(300, Ct);
            await ExecuteAsync(writer, "COMMIT;");
        }, Ct);

        await Snapshot(TimeProvider.System).CreateAsync(ConnectionString(Live), Copy, Ct);
        await release;

        (await ScalarAsync(Copy, "PRAGMA integrity_check")).ShouldBe("ok");
        _logs.Entries.ShouldContain(entry => entry.Message.Contains("not in WAL mode", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Snapshot_FailsWithIoErrorWhenTheDatabaseStaysBusy()
    {
        await CreateLiveAsync("delete");
        await using var writer = await OpenAsync(Live);
        await ExecuteAsync(writer, "BEGIN EXCLUSIVE; INSERT INTO Items (Value) VALUES ('held');");
        var clock = new FakeTimeProvider();

        var snapshot = Snapshot(clock).CreateAsync(ConnectionString(Live), Copy, Ct);
        while (!snapshot.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10, Ct);
        }

        var error = await Should.ThrowAsync<LibraryJobException>(() => snapshot);
        (error.Code, error.Message).ShouldBe((LibraryJobErrorCodeDto.IoError, "The database stayed busy; try again."));
        File.Exists(Copy).ShouldBeFalse();
        DatabaseSnapshot.RetryDelays.Sum(delay => delay.TotalMilliseconds).ShouldBe(6200);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _logs.Dispose();
        _root.Delete(recursive: true);
    }

    private static string ConnectionString(string path) => new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();

    private static async Task<SqliteConnection> OpenAsync(string path)
    {
        var connection = new SqliteConnection(ConnectionString(path));
        await connection.OpenAsync(Ct);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<string?> ScalarAsync(string path, string sql)
    {
        await using var connection = await OpenAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    private DatabaseSnapshot Snapshot(TimeProvider clock) => new(clock, _logs.CreateLogger("snapshot"));

    private async Task CreateLiveAsync(string journalMode)
    {
        await using var connection = await OpenAsync(Live);
        await ExecuteAsync(connection, $"PRAGMA journal_mode={journalMode}; CREATE TABLE Items (Id INTEGER PRIMARY KEY, Value TEXT); INSERT INTO Items (Value) VALUES ('a');");
    }

    private async Task<(int Writes, int Failures)> WriteLoopAsync(CancellationToken stop)
    {
        var (writes, failures) = (0, 0);
        await using var connection = await OpenAsync(Live);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await ExecuteAsync(connection, "INSERT INTO Items (Value) VALUES ('loop');");
                writes++;
            }
            catch (SqliteException)
            {
                failures++;
            }

            await Task.Yield();
        }

        return (writes, failures);
    }
}
