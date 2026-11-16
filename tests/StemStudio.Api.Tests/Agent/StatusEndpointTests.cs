using System.Net;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Api.Agent;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T054: <c>GET /api/agent/status</c> and the unavailable state of <c>POST messages</c> (FR-023, SC-005).</summary>
public sealed class StatusEndpointTests : AgentApiTestBase
{
    public static TheoryData<AgentBackendDto, string, string?> NotReady => new()
    {
        { AgentBackendDto.Ollama, "Ollama not reachable at http://localhost:11434", "Start Ollama (ollama serve)" },
        { AgentBackendDto.Ollama, "qwen3.5:9b is not installed in Ollama", "ollama pull qwen3.5:9b (6.6 GB)" },
        { AgentBackendDto.None, "The agent is switched off", "Choose a backend in Settings" },
    };

    [Fact]
    public async Task Status_ReturnsTheProbeOfTheAgentLibrary()
    {
        using var response = await Client.GetAsync(ApiRoutes.AgentStatus, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getAgentStatus");
        (await response.BodyAsync<AgentStatusDto>()).ShouldBeEquivalentTo(AgentStatuses.Ready());
    }

    [Theory]
    [MemberData(nameof(NotReady))]
    public async Task NotReady_IsReportedWith200AndSendIs503(AgentBackendDto backend, string reason, string? fix)
    {
        var status = AgentStatuses.NotReady(backend, reason, fix);
        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(status);
        var conversation = await CreateConversationAsync();

        using var get = await Client.GetAsync(ApiRoutes.AgentStatus, Ct);
        using var send = await SendMessageAsync(conversation.ConversationId);

        await ContractAssert.MatchesAsync(get, "getAgentStatus");
        (await get.BodyAsync<AgentStatusDto>()).ShouldBeEquivalentTo(status);
        send.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(send, "sendAgentMessage");
        var problem = await send.JsonAsync();
        (problem.GetProperty("type").GetString(), problem.GetProperty("status").GetInt32())
            .ShouldBe((AgentProblemTypes.Uri(AgentProblemTypes.BackendUnavailable), 503));
        problem.GetProperty("agentStatus").Deserialize<AgentStatusDto>(ApiFactory.Json).ShouldBeEquivalentTo(status);
        RunService.ReceivedCalls().ShouldBeEmpty();
        (await GetConversationMessagesAsync(conversation.ConversationId)).ShouldBe(0);
    }

    [Fact]
    public async Task AHangingProbe_AnswersWithin3SecondsAsNotReady()
    {
        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<AgentStatusDto>().Task);

        var request = Client.GetAsync(ApiRoutes.AgentStatus, Ct);
        for (var attempt = 0; attempt < 200 && !request.IsCompleted; attempt++)
        {
            Factory.Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.WhenAny(request, Task.Delay(25, Ct));
        }

        using var response = await request;
        await ContractAssert.MatchesAsync(response, "getAgentStatus");
        var status = await response.BodyAsync<AgentStatusDto>();
        (status.Ready, status.Reason, status.Backend).ShouldBe((false, AgentStatusProbe.TimeoutReason, AgentBackendDto.Ollama));
    }

    [Fact]
    public async Task AFailingProbe_IsNotReadyAndTheRestOfTheApiWorks()
    {
        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("connection refused http://localhost:11434"));

        using var status = await Client.GetAsync(ApiRoutes.AgentStatus, Ct);
        using var tracks = await Client.GetAsync(ApiRoutes.Tracks, Ct);
        using var health = await Client.GetAsync(ApiRoutes.Health, Ct);

        var body = await status.BodyAsync<AgentStatusDto>();
        (body.Ready, body.Reason).ShouldBe((false, AgentStatusProbe.FailureReason));
        (tracks.StatusCode, health.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.OK));
    }

    private async Task<int> GetConversationMessagesAsync(Guid conversationId)
    {
        using var response = await Client.GetAsync(ApiRoutes.AgentConversation(conversationId), Ct);
        return (await response.BodyAsync<ConversationDto>()).Messages.Count;
    }
}
