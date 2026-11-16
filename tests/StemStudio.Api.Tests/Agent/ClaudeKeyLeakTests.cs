using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 follow-up claude-key T027 [sec] (CK-SC-001): a key saved through the API never appears - in any of the
/// <see cref="ClaudeKeySentinels.SearchForms"/> or as raw UTF-16 - in any response, any log line or log scope at Trace, the SSE
/// stream of a Claude run, or the SQLite files that exist (<c>stemstudio.db</c> and, depending on the journal mode, its
/// <c>-journal</c>/<c>-wal</c>/<c>-shm</c>). The Claude run talks to a local stub that answers every request with an error;
/// Anthropic is never contacted. Positive controls prove each channel was really exercised (quality-gate R2, qa F1, F2).
/// </summary>
public sealed class ClaudeKeyLeakTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StoredSentinel_NeverLeaks()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-leak-").FullName;
        try
        {
            var bodies = await ExerciseAsync(dataDir);
            SqliteConnection.ClearAllPools();

            await ShouldNotBeInTheDatabaseFilesAsync(dataDir, "after shutdown");
            bodies.ShouldNotBeEmpty();
            bodies.ForEach(body => ShouldNotContain(Encoding.UTF8.GetBytes(body), "response"));
        }
        finally
        {
            Directory.Delete(dataDir, true);
        }
    }

    private static async Task<List<string>> ExerciseAsync(string dataDir)
    {
        using var api = new ClaudeKeyApi(new() { DataDir = dataDir, TraceLogs = true });
        var bodies = new List<string>();
        using (var stored = await api.PutKeyAsync(ClaudeKeySentinels.Stored))
        {
            stored.StatusCode.ShouldBe(HttpStatusCode.OK);
            bodies.Add(await stored.Content.ReadAsStringAsync(Ct));
        }

        foreach (var route in new[] { ApiRoutes.AgentClaudeKey, ApiRoutes.AgentStatus, ApiRoutes.AgentSettings })
        {
            bodies.Add(await api.Client.GetStringAsync(route, Ct));
        }

        bodies.AddRange(await ClaudeRunAsync(api));
        LogsShouldBeCleanAtTrace(api);
        await ShouldNotBeInTheDatabaseFilesAsync(dataDir, "while running");
        return bodies;
    }

    private static async Task<List<string>> ClaudeRunAsync(ClaudeKeyApi api)
    {
        using var select = await api.Client.PutAsJsonAsync(ApiRoutes.AgentSettings, new { backend = "claude", acknowledgeClaudeNotice = true }, Ct);
        select.StatusCode.ShouldBe(HttpStatusCode.OK);
        var run = await api.RunToTheEndAsync();

        // Positive control (R2): the run really decrypted the stored key and sent it to the stand-in, and failed on its error.
        api.AnthropicKeys.Distinct().ShouldBe([ClaudeKeySentinels.Stored]);
        run.Failure.ShouldNotBeNull().Code.ShouldBe(AgentFailureCodes.BackendUnavailable);

        // The terminal event is published right after the run row ends; the replay of an ended run closes the stream.
        var events = string.Empty;
        await Eventually.WaitUntilAsync(async () => (events = await api.Client.GetStringAsync(ApiRoutes.AgentRunEvents(run.RunId), Ct)).Contains("event: run.", StringComparison.Ordinal));
        return [events, await api.Client.GetStringAsync(ApiRoutes.AgentRun(run.RunId), Ct)];
    }

    private static void LogsShouldBeCleanAtTrace(ClaudeKeyApi api)
    {
        var entries = api.Factory.Logs.Entries;

        // Positive control (F1): the Trace filter really applied, so the search covers the most verbose output.
        entries.ShouldContain(entry => entry.Level == LogLevel.Trace);
        entries.Select(entry => entry.Message + entry.Exception).Concat(api.Factory.Logs.Scopes).ToList()
            .ForEach(line => ShouldNotContain(Encoding.UTF8.GetBytes(line), "log"));
    }

    // F2: Microsoft.Data.Sqlite keeps SQLite's default rollback journal (no WAL is configured in src), so -wal/-shm never exist and
    // the -journal file lives only during a write. The test records the mode, requires -wal when WAL is ever enabled, proves the
    // scanned database holds the key row, and reads every stemstudio.db* file with FileShare.ReadWrite while the host still runs.
    private static async Task ShouldNotBeInTheDatabaseFilesAsync(string dataDir, string when)
    {
        var database = Path.Combine(dataDir, "stemstudio.db");
        var (mode, rows) = await JournalModeAndKeyRowsAsync(database);
        var files = Directory.EnumerateFiles(dataDir, "stemstudio.db*").ToList();

        rows.ShouldBe(1, "the scanned database must hold the stored key's row");
        mode.ShouldBeOneOf("delete", "wal");
        if (mode == "wal")
        {
            files.ShouldContain(database + "-wal");
        }

        files.ShouldContain(database);
        foreach (var file in files)
        {
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, Ct);
            ShouldNotContain(copy.ToArray(), $"{Path.GetFileName(file)} ({when}, journal mode {mode})");
        }
    }

    private static async Task<(string Mode, long Rows)> JournalModeAndKeyRowsAsync(string database)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = (string)(await command.ExecuteScalarAsync(Ct))!;
        command.CommandText = "SELECT COUNT(*) FROM AgentSecrets WHERE LastFour = 'STO1';";
        return (mode, (long)(await command.ExecuteScalarAsync(Ct))!);
    }

    private static void ShouldNotContain(byte[] haystack, string where)
    {
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            haystack.AsSpan().IndexOf(Encoding.UTF8.GetBytes(form)).ShouldBe(-1, $"a search form of the stored sentinel was found in {where}");
        }

        haystack.AsSpan().IndexOf(Encoding.Unicode.GetBytes(ClaudeKeySentinels.Stored)).ShouldBe(-1, $"UTF-16 sentinel found in {where}");
    }
}
