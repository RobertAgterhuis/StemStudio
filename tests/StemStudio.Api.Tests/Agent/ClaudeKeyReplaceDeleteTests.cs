using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 follow-up claude-key T032: replacing and deleting the stored key (CK-FR-006, CK-FR-015).</summary>
public sealed class ClaudeKeyReplaceDeleteTests : IDisposable
{
    private readonly ClaudeKeyApi _api = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Put_OverAStoredKey_ReplacesCiphertextLastFourAndTimeInOneRow()
    {
        using var first = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);
        var before = await RowAsync();
        _api.Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        using var second = await _api.PutKeyAsync(ClaudeKeySentinels.Replacement);

        var status = await second.BodyAsync<ClaudeKeyStatusDto>();
        (status.LastFour, status.StoredLastFour, status.StoredAt).ShouldBe(("STO2", "STO2", ApiFactory.Start.AddMinutes(5)));
        var after = (await RowAsync()).ShouldNotBeNull();
        after.Ciphertext.ShouldNotBe(before!.Ciphertext);
        (after.LastFour, after.StoredAt).ShouldBe(("STO2", ApiFactory.Start.AddMinutes(5).UtcDateTime));
    }

    [Fact]
    public async Task Delete_RemovesTheStoredKeyAndIsIdempotent()
    {
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);

        using var deleted = await _api.DeleteKeyAsync();
        using var again = await _api.DeleteKeyAsync();

        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(deleted, "deleteClaudeKey");
        (await deleted.BodyAsync<ClaudeKeyStatusDto>()).ShouldBe(ClaudeKeyStatusDto.NotConfigured);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.BodyAsync<ClaudeKeyStatusDto>()).ShouldBe(ClaudeKeyStatusDto.NotConfigured);
        (await RowAsync()).ShouldBeNull();
        _api.Anthropic.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_KeepsTheEnvironmentKeyActive()
    {
        _api.UseEnvironmentKey(ClaudeKeySentinels.Environment);
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);

        using var deleted = await _api.DeleteKeyAsync();

        var status = await deleted.BodyAsync<ClaudeKeyStatusDto>();
        (status.State, status.LastFour, status.Stored).ShouldBe((ClaudeKeyStateDto.Environment, "ENV1", StoredClaudeKeyStateDto.Absent));
    }

    [Fact]
    public async Task Delete_RemovesAnUnreadableKey()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-ck-").FullName;
        try
        {
            using (var first = new ClaudeKeyApi(new() { DataDir = dataDir }))
            using (await first.PutKeyAsync(ClaudeKeySentinels.Stored))
            {
            }

            using var other = new ClaudeKeyApi(new() { DataDir = dataDir, KeyRingDir = dataDir + "-other-keys" });
            (await other.StatusAsync()).State.ShouldBe(ClaudeKeyStateDto.StoredUnreadable);

            using var deleted = await other.DeleteKeyAsync();

            (await deleted.BodyAsync<ClaudeKeyStatusDto>()).ShouldBe(ClaudeKeyStatusDto.NotConfigured);
        }
        finally
        {
            Directory.Delete(dataDir + "-other-keys", true);
            Directory.Delete(dataDir, true);
        }
    }

    [Fact]
    public async Task Delete_KeepsBackendClaudeAndTheAgentThenReportsNoKey()
    {
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);
        using var select = await _api.Client.PutAsJsonAsync(ApiRoutes.AgentSettings, new { backend = "claude", acknowledgeClaudeNotice = true }, Ct);
        select.StatusCode.ShouldBe(HttpStatusCode.OK);
        _api.StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("probe failed"));

        using var deleted = await _api.DeleteKeyAsync();

        (await _api.SettingsAsync()).Backend.ShouldBe(AgentBackendDto.Claude);
        var status = (await _api.Client.GetFromJsonAsync<AgentStatusDto>(ApiRoutes.AgentStatus, ApiFactory.Json, Ct))!;
        (status.Ready, status.Claude.KeyConfigured, status.Claude.Key.State).ShouldBe((false, false, ClaudeKeyStateDto.NotConfigured));
    }

    [Fact]
    public async Task Delete_IsLocalOnly()
    {
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);
        using var request = new HttpRequestMessage(HttpMethod.Delete, ApiRoutes.AgentClaudeKey);
        request.Headers.Add(TestPeerStartup.Header, "192.168.1.20");

        using var refused = await _api.Client.SendAsync(request, Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ContractAssert.MatchesAsync(refused, "deleteClaudeKey");
        (await _api.StatusAsync()).Stored.ShouldBe(StoredClaudeKeyStateDto.Readable);
    }

    private Task<AgentSecretEntity?> RowAsync() => _api.Factory.WithScopeAsync(sp =>
        Task.FromResult(sp.GetRequiredService<IAgentSecretStore>().Find(AgentSecretEntity.ClaudeApiKeyName)));
}
