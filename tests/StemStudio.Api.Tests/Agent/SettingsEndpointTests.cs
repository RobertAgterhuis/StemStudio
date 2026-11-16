using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Api.Agent;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T060: agent settings and the backend test (FR-018, FR-019, SC-006).</summary>
public sealed class SettingsEndpointTests : AgentApiTestBase
{
    private const string SentinelKey = "sk-ant-api03-SENTINEL-0123456789abcdef";

    [Fact]
    public async Task Get_ReturnsTheDefaults()
    {
        using var response = await Client.GetAsync(ApiRoutes.AgentSettings, Ct);

        await ContractAssert.MatchesAsync(response, "getAgentSettings");
        var settings = await response.BodyAsync<AgentSettingsDto>();
        (settings.Backend, settings.OllamaModel, settings.OllamaFallbackModel, settings.ClaudeNoticeAcknowledged, settings.Limits)
            .ShouldBe((AgentBackendDto.Ollama, "qwen3.5:9b", "qwen3.5:4b", false, AgentLimitsDto.Default));
    }

    [Fact]
    public async Task Put_UnchangedModelsNeedNoReachableOllama()
    {
        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentStatuses.NotReady(AgentBackendDto.Ollama, "Ollama not reachable", "Start Ollama"));

        using var none = await PutAsync(new { backend = "none" });
        using var back = await PutAsync(new { backend = "ollama", ollamaModel = "qwen3.5:9b", ollamaThink = true, limits = AgentLimitsDto.Default with { MaxModelTurns = 5 } });

        await ContractAssert.MatchesAsync(back, "updateAgentSettings");
        var settings = await back.BodyAsync<AgentSettingsDto>();
        (settings.Backend, settings.OllamaThink, settings.Limits.MaxModelTurns, settings.UpdatedAt).ShouldBe((AgentBackendDto.Ollama, true, 5, ApiFactory.Start));
        (await (await Client.GetAsync(ApiRoutes.AgentSettings, Ct)).BodyAsync<AgentSettingsDto>()).Limits.MaxModelTurns.ShouldBe(5);
    }

    [Fact]
    public async Task Put_ChangedModelsMustExistWithTools()
    {
        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentStatuses.Ready() with
        {
            Ollama = AgentStatuses.Ready().Ollama with
            {
                Models = [new OllamaModelInfoDto { Name = "qwen3.5:4b", SizeBytes = 1, Tools = true }, new OllamaModelInfoDto { Name = "gemma:2b", SizeBytes = 1, Tools = false }],
            },
        });

        using var missing = await PutAsync(new { ollamaModel = "llama9:70b" });
        using var noTools = await PutAsync(new { ollamaFallbackModel = "gemma:2b" });
        using var ok = await PutAsync(new { ollamaModel = "qwen3.5:4b" });

        foreach (var response in new[] { missing, noTools })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            await ContractAssert.MatchesAsync(response, "updateAgentSettings");
            (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.ModelUnavailable));
        }

        (await ok.BodyAsync<AgentSettingsDto>()).OllamaModel.ShouldBe("qwen3.5:4b");
    }

    [Fact]
    public async Task Put_ClaudeWithoutAKey_Is409()
    {
        using var response = await PutAsync(new { backend = "claude", acknowledgeClaudeNotice = true });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "updateAgentSettings");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.ClaudeNotConfigured));
    }

    [Fact]
    public async Task Put_ClaudeNeedsTheAcknowledgedNotice()
    {
        UseKey(SentinelKey);

        using var withoutNotice = await PutAsync(new { backend = "claude" });
        using var acknowledged = await PutAsync(new { backend = "claude", claudeModel = "claude-sonnet-5-5", claudeEffort = "high", acknowledgeClaudeNotice = true });
        using var later = await PutAsync(new { backend = "claude" });

        withoutNotice.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await withoutNotice.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.NoticeNotAcknowledged));
        await ContractAssert.MatchesAsync(withoutNotice, "updateAgentSettings");
        var settings = await acknowledged.BodyAsync<AgentSettingsDto>();
        (settings.Backend, settings.ClaudeModel, settings.ClaudeEffort, settings.ClaudeNoticeAcknowledged)
            .ShouldBe((AgentBackendDto.Claude, ClaudeModelDto.ClaudeSonnet55, ClaudeEffortDto.High, true));
        later.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("""{"backend":"fake"}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"ollamaModel":""}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"limits":{"maxModelTurns":31,"maxToolCalls":20,"maxInvalidCalls":3,"turnTimeoutSeconds":120,"claudeTurnTimeoutSeconds":180,"claudeMaxInputTokensPerRun":60000,"claudeMaxOutputTokensPerRun":8000,"maxJobWaitMinutes":45}}""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{"limits":{"maxModelTurns":12,"maxToolCalls":20,"maxInvalidCalls":3,"turnTimeoutSeconds":5,"claudeTurnTimeoutSeconds":180,"claudeMaxInputTokensPerRun":60000,"claudeMaxOutputTokensPerRun":8000,"maxJobWaitMinutes":45}}""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{"limits":{"maxModelTurns":12,"maxToolCalls":20,"maxInvalidCalls":3,"turnTimeoutSeconds":120,"claudeTurnTimeoutSeconds":180,"claudeMaxInputTokensPerRun":60000,"claudeMaxOutputTokensPerRun":8000,"maxJobWaitMinutes":181}}""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("not json", HttpStatusCode.BadRequest)]
    public async Task Put_RejectsInvalidValues(string body, HttpStatusCode expected)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await Client.PutAsync(ApiRoutes.AgentSettings, content, Ct);

        response.StatusCode.ShouldBe(expected);
        await ContractAssert.MatchesAsync(response, "updateAgentSettings");
    }

    [Fact]
    public async Task Test_ProbesTheChosenBackend()
    {
        StatusService.TestAsync(AgentBackendDto.Ollama, Arg.Any<CancellationToken>())
            .Returns(new BackendTestResultDto { Backend = AgentBackendDto.Ollama, Ok = true, LatencyMs = 42, Detail = "qwen3.5:9b answered" });

        using var ok = await Client.PostAsJsonAsync(ApiRoutes.AgentSettingsTest, new { backend = "ollama" }, Ct);
        using var none = await Client.PostAsJsonAsync(ApiRoutes.AgentSettingsTest, new { backend = "none" }, Ct);

        await ContractAssert.MatchesAsync(ok, "testAgentBackend");
        (await ok.BodyAsync<BackendTestResultDto>()).LatencyMs.ShouldBe(42);
        none.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(none, "testAgentBackend");
    }

    [Fact]
    public async Task Test_AFailingProbeIsNotOkWithAFixedDetail()
    {
        StatusService.TestAsync(AgentBackendDto.Ollama, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("http://internal:11434 refused"));

        using var response = await Client.PostAsJsonAsync(ApiRoutes.AgentSettingsTest, new { backend = "ollama" }, Ct);

        await ContractAssert.MatchesAsync(response, "testAgentBackend");
        var result = await response.BodyAsync<BackendTestResultDto>();
        (result.Ok, result.Detail).ShouldBe((false, AgentStatusProbe.FailureReason));
    }

    [Fact]
    public async Task Put_LimitsStayEditableWithClaudeAfterTheKeyWasRemoved()
    {
        UseKey(SentinelKey);
        using var claude = await PutAsync(new { backend = "claude", acknowledgeClaudeNotice = true });
        UseKey(null);

        using var limits = await PutAsync(new { limits = AgentLimitsDto.Default with { MaxToolCalls = 7 } });
        using var again = await PutAsync(new { backend = "claude" });

        (await limits.BodyAsync<AgentSettingsDto>()).Limits.MaxToolCalls.ShouldBe(7);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_TrimsModelNames()
    {
        using var response = await PutAsync(new { ollamaFallbackModel = "  qwen3.5:9b  " });

        (await response.BodyAsync<AgentSettingsDto>()).OllamaFallbackModel.ShouldBe("qwen3.5:9b");
    }

    [Fact]
    public async Task TheKey_NeverAppearsInResponsesLogsOrTheDatabase()
    {
        UseKey(SentinelKey);
        StatusService.TestAsync(AgentBackendDto.Claude, Arg.Any<CancellationToken>())
            .Returns(new BackendTestResultDto { Backend = AgentBackendDto.Claude, Ok = true, LatencyMs = 1, Detail = "claude-opus-5-5 is available" });

        var bodies = new List<string>();
        foreach (var response in new[]
        {
            await PutAsync(new { backend = "claude", acknowledgeClaudeNotice = true }),
            await Client.GetAsync(ApiRoutes.AgentSettings, Ct),
            await Client.GetAsync(ApiRoutes.AgentStatus, Ct),
            await Client.PostAsJsonAsync(ApiRoutes.AgentSettingsTest, new { backend = "claude" }, Ct),
        })
        {
            using (response)
            {
                bodies.Add(await response.Content.ReadAsStringAsync(Ct));
            }
        }

        bodies.ShouldAllBe(body => !body.Contains("SENTINEL"));
        Factory.Logs.Entries.ShouldAllBe(entry => !entry.Message.Contains("SENTINEL"));
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var database = await File.ReadAllBytesAsync(Path.Combine(Factory.DataDir, "stemstudio.db"), Ct);
        System.Text.Encoding.UTF8.GetString(database).ShouldNotContain("SENTINEL");
    }

    private void UseKey(string? key) => Environment.GetVariable("ANTHROPIC_API_KEY").Returns(key);

    private Task<HttpResponseMessage> PutAsync(object body) => Client.PutAsJsonAsync(ApiRoutes.AgentSettings, body, Ct);
}
