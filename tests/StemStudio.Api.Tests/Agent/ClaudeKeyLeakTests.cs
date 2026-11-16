using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.Sqlite;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 follow-up claude-key T027 [sec] (CK-SC-001): a key saved through the API never appears - as text, base64 or UTF-16 - in
/// any response, any log line at Trace, the SSE stream of a Claude run, or the SQLite files (<c>stemstudio.db</c>, <c>-wal</c>,
/// <c>-shm</c>). The Claude run talks to a local stub that answers every request with an error; Anthropic is never contacted.
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

            var files = Directory.EnumerateFiles(dataDir, "stemstudio.db*").ToList();
            files.ShouldContain(file => file.EndsWith("stemstudio.db", StringComparison.Ordinal));
            foreach (var file in files)
            {
                ShouldNotContain(await File.ReadAllBytesAsync(file, Ct), file);
            }

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
        var logs = api.Factory.Logs.Entries.Select(entry => entry.Message + entry.Exception).ToList();
        logs.ShouldNotBeEmpty();
        logs.ForEach(line => ShouldNotContain(Encoding.UTF8.GetBytes(line), "log"));
        return bodies;
    }

    private static async Task<List<string>> ClaudeRunAsync(ClaudeKeyApi api)
    {
        using var select = await api.Client.PutAsJsonAsync(ApiRoutes.AgentSettings, new { backend = "claude", acknowledgeClaudeNotice = true }, Ct);
        select.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var created = await api.Client.PostAsJsonAsync(ApiRoutes.AgentConversations, new CreateConversationRequest(), Ct);
        var conversation = await created.BodyAsync<ConversationDto>();
        using var sent = await api.Client.PostAsJsonAsync(ApiRoutes.AgentMessages(conversation.ConversationId),
            new SendAgentMessageRequest { Text = "Mute the drums", Context = new PageContextDto { Page = "mixer" } }, Ct);
        sent.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var runId = (await sent.BodyAsync<AgentMessageAcceptedDto>()).RunId;

        await Eventually.WaitUntilAsync(async () => (await RunAsync(api, runId)).EndedAt is not null);

        // The terminal event is published right after the run row ends; the replay of an ended run closes the stream.
        var events = string.Empty;
        await Eventually.WaitUntilAsync(async () => (events = await api.Client.GetStringAsync(ApiRoutes.AgentRunEvents(runId), Ct)).Contains("event: run.", StringComparison.Ordinal));
        return [events, await api.Client.GetStringAsync(ApiRoutes.AgentRun(runId), Ct)];
    }

    private static async Task<AgentRunDto> RunAsync(ClaudeKeyApi api, Guid runId) =>
        (await api.Client.GetFromJsonAsync<AgentRunDto>(ApiRoutes.AgentRun(runId), ApiFactory.Json, Ct))!;

    private static void ShouldNotContain(byte[] haystack, string where)
    {
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            haystack.AsSpan().IndexOf(Encoding.UTF8.GetBytes(form)).ShouldBe(-1, $"{form} found in {where}");
        }

        haystack.AsSpan().IndexOf(Encoding.Unicode.GetBytes(ClaudeKeySentinels.Stored)).ShouldBe(-1, $"UTF-16 sentinel found in {where}");
    }
}
