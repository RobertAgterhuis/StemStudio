using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using StemStudio.Agent.Backends;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 follow-up claude-key T042 (CK-FR-011, CK-SC-004): the library moves to a host with another key ring (restore on another
/// computer, lost key volume). StemStudio starts, everything else works, Claude says why it cannot run, and saving the key again
/// repairs it.
/// </summary>
public sealed class ClaudeKeyUnreadableTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("stemstudio-unreadable-").FullName;

    private string OtherKeyRing => _dataDir + "-other-keys";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(OtherKeyRing, true);
        Directory.Delete(_dataDir, true);
    }

    [Fact]
    public async Task OtherKeyRing_StartsAndReportsStoredUnreadableWithAFix()
    {
        await StoreAndSelectClaudeAsync();
        using var api = OtherHost();

        var status = await api.StatusAsync();

        (status.State, status.Configured, status.Stored, status.StoredLastFour, status.StoreAvailable)
            .ShouldBe((ClaudeKeyStateDto.StoredUnreadable, false, StoredClaudeKeyStateDto.Unreadable, "STO1", true));
        (status.Reason, status.Fix).ShouldBe((ClaudeKeyProvider.UnreadableReason, ClaudeKeyProvider.UnreadableFix));
        api.Factory.Logs.For(nameof(ProtectedClaudeKeyStore)).Count(entry => entry.Message.Contains("cannot be decrypted", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Theory]
    [InlineData(ApiRoutes.Health)]
    [InlineData(ApiRoutes.Tracks)]
    [InlineData(ApiRoutes.Mixes)]
    [InlineData(ApiRoutes.AgentSettings)]
    [InlineData(ApiRoutes.AgentConversations)]
    [InlineData(ApiRoutes.AgentStatus)]
    public async Task OtherKeyRing_LeavesEveryOtherEndpointWorking(string route)
    {
        await StoreAndSelectClaudeAsync();
        using var api = OtherHost();

        using var response = await api.Client.GetAsync(route, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtherKeyRing_ClaudeRunFailsBackendUnavailableWithTheReason()
    {
        await StoreAndSelectClaudeAsync();
        using var api = OtherHost();

        var run = await RunToTheEndAsync(api);

        run.Status.ShouldBe(AgentRunStatusDto.Failed);
        run.Failure.ShouldNotBeNull().Code.ShouldBe(AgentFailureCodes.BackendUnavailable);
        run.Failure.Message.ShouldContain(ClaudeKeyProvider.UnreadableReason);
        api.Anthropic.Requests.ShouldBeEmpty();
        (await api.SettingsAsync()).Backend.ShouldBe(AgentBackendDto.Claude);
    }

    [Fact]
    public async Task OtherKeyRing_SavingTheKeyAgainReplacesTheUnreadableRow()
    {
        await StoreAndSelectClaudeAsync();
        using var api = OtherHost();

        using var response = await api.PutKeyAsync(ClaudeKeySentinels.Replacement);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "storeClaudeKey");
        var status = await response.BodyAsync<ClaudeKeyStatusDto>();
        (status.State, status.Stored, status.LastFour, status.Reason, status.Fix)
            .ShouldBe((ClaudeKeyStateDto.Stored, StoredClaudeKeyStateDto.Readable, "STO2", (string?)null, (string?)null));
    }

    private async Task StoreAndSelectClaudeAsync()
    {
        using var first = new ClaudeKeyApi(new() { DataDir = _dataDir });
        using var stored = await first.PutKeyAsync(ClaudeKeySentinels.Stored);
        using var select = await first.Client.PutAsJsonAsync(ApiRoutes.AgentSettings, new { backend = "claude", acknowledgeClaudeNotice = true }, Ct);
        select.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private ClaudeKeyApi OtherHost() => new(new() { DataDir = _dataDir, KeyRingDir = OtherKeyRing });

    private static async Task<AgentRunDto> RunToTheEndAsync(ClaudeKeyApi api)
    {
        using var created = await api.Client.PostAsJsonAsync(ApiRoutes.AgentConversations, new CreateConversationRequest(), Ct);
        var conversation = await created.BodyAsync<ConversationDto>();
        using var sent = await api.Client.PostAsJsonAsync(ApiRoutes.AgentMessages(conversation.ConversationId),
            new SendAgentMessageRequest { Text = "Mute the drums", Context = new PageContextDto { Page = "mixer" } }, Ct);
        sent.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var runId = (await sent.BodyAsync<AgentMessageAcceptedDto>()).RunId;
        AgentRunDto? run = null;
        await Eventually.WaitUntilAsync(async () =>
            (run = await api.Client.GetFromJsonAsync<AgentRunDto>(ApiRoutes.AgentRun(runId), ApiFactory.Json, Ct))!.EndedAt is not null);
        return run!;
    }
}
