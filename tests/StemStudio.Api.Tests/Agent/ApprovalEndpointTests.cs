using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T045: <c>POST runs/{runId}/approvals/{stepId}</c> (FR-015).</summary>
public sealed class ApprovalEndpointTests : AgentApiTestBase
{
    private static readonly string Hash = new('a', 64);

    [Theory]
    [InlineData(ApprovalDecisionDto.Approve, ApprovalScopeDto.Once, ApprovalStatusDto.Approved)]
    [InlineData(ApprovalDecisionDto.Approve, ApprovalScopeDto.PlanHeavy, ApprovalStatusDto.Approved)]
    [InlineData(ApprovalDecisionDto.Reject, ApprovalScopeDto.Once, ApprovalStatusDto.Rejected)]
    public async Task Decide_PassesTheDecisionToTheAgentAndReturnsTheState(ApprovalDecisionDto decision, ApprovalScopeDto scope, ApprovalStatusDto status)
    {
        var (runId, stepId) = await PendingStepAsync();
        var state = new ApprovalStateDto { StepId = stepId, Status = status, Scope = scope, DecidedAt = ApiFactory.Start };
        RunService.DecideAsync(runId, stepId, Arg.Any<DecideApprovalRequest>(), Arg.Any<CancellationToken>()).Returns(state);

        using var response = await DecideAsync(runId, stepId, new { decision = Name(decision), scope = Name(scope), argumentsHash = Hash });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "decideAgentApproval");
        (await response.BodyAsync<ApprovalStateDto>()).ShouldBe(state);
        await RunService.Received(1).DecideAsync(runId, stepId,
            Arg.Is<DecideApprovalRequest>(r => r.Decision == decision && r.Scope == scope && r.ArgumentsHash == Hash), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Decide_IsIdempotent_TheStoredDecisionComesBack()
    {
        var (runId, stepId) = await PendingStepAsync();
        var stored = new ApprovalStateDto { StepId = stepId, Status = ApprovalStatusDto.Approved, Scope = ApprovalScopeDto.Once, DecidedAt = ApiFactory.Start };
        RunService.DecideAsync(runId, stepId, Arg.Any<DecideApprovalRequest>(), Arg.Any<CancellationToken>()).Returns(stored);

        using var first = await DecideAsync(runId, stepId, new { decision = "approve", argumentsHash = Hash });
        using var second = await DecideAsync(runId, stepId, new { decision = "reject", argumentsHash = Hash });

        (await second.BodyAsync<ApprovalStateDto>()).ShouldBe(await first.BodyAsync<ApprovalStateDto>());
    }

    [Theory]
    [InlineData(AgentProblemTypes.ApprovalNotPending)]
    [InlineData(AgentProblemTypes.ArgumentsChanged)]
    public async Task Decide_MapsAgentConflictsTo409(string code)
    {
        var (runId, stepId) = await PendingStepAsync();
        RunService.DecideAsync(runId, stepId, Arg.Any<DecideApprovalRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AgentConflictException(code, "The approval cannot be decided."));

        using var response = await DecideAsync(runId, stepId, new { decision = "approve", argumentsHash = Hash });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "decideAgentApproval");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(code));
    }

    [Fact]
    public async Task Decide_UnknownRunOrStep_Is404()
    {
        var (runId, _) = await PendingStepAsync();

        using var unknownRun = await DecideAsync(Guid.NewGuid(), Guid.NewGuid(), new { decision = "approve", argumentsHash = Hash });
        using var unknownStep = await DecideAsync(runId, Guid.NewGuid(), new { decision = "approve", argumentsHash = Hash });

        new[] { unknownRun, unknownStep }.ShouldAllBe(r => r.StatusCode == HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknownStep, "decideAgentApproval");
        RunService.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IAgentRunService.DecideAsync)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{"decision":"approve","argumentsHash":"short"}""")]
    [InlineData("""{"decision":"approve"}""")]
    [InlineData("""{"decision":"maybe","argumentsHash":"aaaa"}""")]
    [InlineData("not json")]
    public async Task Decide_RejectsInvalidBodiesWith400(string body)
    {
        var (runId, stepId) = await PendingStepAsync();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await Client.PostAsync(ApiRoutes.AgentApproval(runId, stepId), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(response, "decideAgentApproval");
    }

    [Fact]
    public async Task Resolved_IsBroadcastToEverySubscriber()
    {
        var (runId, stepId) = await PendingStepAsync();
        var state = new ApprovalStateDto { StepId = stepId, Status = ApprovalStatusDto.Rejected, Scope = ApprovalScopeDto.Once, DecidedAt = ApiFactory.Start };
        RunService.DecideAsync(runId, stepId, Arg.Any<DecideApprovalRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Publish(runId, AgentEventTypes.ApprovalResolved, state);
            await Publish(runId, AgentEventTypes.RunCompleted, new RunCompletedEventDto { RunId = runId, MessageId = Guid.NewGuid() });
            return state;
        });
        var first = OpenStreamAsync(runId);
        var second = OpenStreamAsync(runId);
        await Task.WhenAll(first, second);

        using var response = await DecideAsync(runId, stepId, new { decision = "reject", argumentsHash = Hash });

        foreach (var stream in new[] { await first, await second })
        {
            (await stream.NextEventAsync()).Event.ShouldBe(AgentEventTypes.ApprovalResolved);
            (await stream.NextEventAsync()).Event.ShouldBe(AgentEventTypes.RunCompleted);
        }
    }

    private async Task<SseReader> OpenStreamAsync(Guid runId)
    {
        var response = await Client.GetAsync(ApiRoutes.AgentRunEvents(runId), HttpCompletionOption.ResponseHeadersRead, Ct);
        return new SseReader(new StreamReader(await response.Content.ReadAsStreamAsync(Ct)));
    }

    private Task<RunEvent> Publish<T>(Guid runId, string type, T payload) =>
        Service<IRunEventSink>().PublishAsync(runId, type, JsonSerializer.Serialize(payload, ApiFactory.Json), Ct);

    private Task<HttpResponseMessage> DecideAsync(Guid runId, Guid stepId, object body) =>
        Client.PostAsJsonAsync(ApiRoutes.AgentApproval(runId, stepId), body, Ct);

    private async Task<(Guid RunId, Guid StepId)> PendingStepAsync()
    {
        var conversation = await CreateConversationAsync();
        using var response = await SendMessageAsync(conversation.ConversationId);
        var runId = (await response.BodyAsync<AgentMessageAcceptedDto>()).RunId;
        var step = AgentRecords.Step(runId, 1, AgentStepStatusDto.AwaitingApproval);
        await Service<IRunStore>().SaveStepAsync(step, Ct);
        await Service<IRunStore>().SaveApprovalAsync(AgentRecords.Approval(step.Id, runId), Ct);
        return (runId, step.Id);
    }

    private static string Name<T>(T value) => JsonSerializer.Serialize(value).Trim('"');
}
