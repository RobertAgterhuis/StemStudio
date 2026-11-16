using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>
/// Dynamic heavy escalation (<see cref="AgentToolDescriptor.HeavyWhen"/>) fails closed: a throwing check makes the step heavy
/// (approval, never covered by a plan grant); cancellation propagates; false keeps the declared risk, true makes it heavy.
/// </summary>
public sealed class EscalationTests
{
    private static readonly Guid TrackA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private readonly List<string> _executed = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ScriptedCall Export() => Call("export", $$"""{"trackId":"{{TrackA}}"}""");

    private AgentToolDescriptor Tool(ToolEscalation heavyWhen) => Server("export", invocation =>
    {
        _executed.Add("export");
        return ToolResult.Of(new JsonObject { ["ok"] = true });
    }, ToolRiskDto.Job, ToolSchema.Of(Param.Uuid("trackId"))) with
    {
        TargetArgument = "trackId",
        HeavyWhen = heavyWhen,
    };

    private static ToolEscalation Throws(Exception exception) => (_, _) => Task.FromException<bool>(exception);

    private static async Task<(AgentRunService Service, Task Execution)> StartAsync(RunnerHarness harness, CancellationToken? token = null)
    {
        await harness.SeedAsync();
        var service = new AgentRunService(harness.Services());
        return (service, service.ExecuteAsync(harness.Run.Id, token ?? Token));
    }

    private static async Task<RunStep> PendingStepAsync(RunnerHarness harness, int count = 1)
    {
        var requested = await harness.Events.WaitForAsync(AgentEventTypes.ApprovalRequested, count);
        var stepId = JsonDocument.Parse(requested.Payload).RootElement.GetProperty("stepId").GetGuid();
        return harness.Runs.Steps.Single(step => step.Id == stepId);
    }

    private static DecideApprovalRequest Decide(RunStep step, ApprovalDecisionDto decision = ApprovalDecisionDto.Approve, ApprovalScopeDto scope = ApprovalScopeDto.Once) =>
        new() { Decision = decision, Scope = scope, ArgumentsHash = step.ArgumentsHash };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrowingCheck_FailsClosed_IntoAnApprovalWithoutTheExceptionText(bool actionException)
    {
        Exception failure = actionException ? new AgentActionException(500, null, "SECRET lookup detail") : new InvalidOperationException("SECRET bug");
        var harness = RunnerHarness.Scripted([Tool(Throws(failure))], Turn(Export()), Say("ok"));
        var (service, execution) = await StartAsync(harness);

        var step = await PendingStepAsync(harness);
        await service.DecideAsync(harness.Run.Id, step.Id, Decide(step, ApprovalDecisionDto.Reject), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        (step.Risk, step.Decision).ShouldBe((ToolRiskDto.Heavy, StepDecisionDto.ApprovalRequired));
        step.DecisionReasons.ShouldContain(AgentDecisionReasons.RiskHeavy);
        _executed.ShouldBeEmpty();
        harness.Events.Events.ShouldAllBe(item => !item.Payload.Contains("SECRET", StringComparison.Ordinal));
        harness.Script.Requests.SelectMany(request => request.Messages).SelectMany(message => message.Contents).OfType<Microsoft.Extensions.AI.FunctionResultContent>()
            .ShouldAllBe(result => !(result.Result ?? string.Empty).ToString()!.Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cancel = new CancellationTokenSource();
        var harness = RunnerHarness.Scripted([Tool((_, _) =>
        {
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        })], Turn(Export()), Say("ok"));
        var (_, execution) = await StartAsync(harness, cancel.Token);

        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        (await harness.CurrentRunAsync())!.Status.ShouldBe(AgentRunStatusDto.Cancelled);
        harness.Events.Types.ShouldNotContain(AgentEventTypes.ApprovalRequested);
    }

    [Fact]
    public async Task FalseKeepsTheDeclaredRisk_TrueMakesItHeavy()
    {
        var small = RunnerHarness.Scripted([Tool((_, _) => Task.FromResult(false))], Turn(Export()), Say("ok"));
        await small.ExecuteAsync();
        small.Runs.Steps.Single().Risk.ShouldBe(ToolRiskDto.Job);
        _executed.Count.ShouldBe(1);

        var large = RunnerHarness.Scripted([Tool((_, _) => Task.FromResult(true))], Turn(Export()), Say("ok"));
        var (service, execution) = await StartAsync(large);
        var step = await PendingStepAsync(large);
        await service.DecideAsync(large.Run.Id, step.Id, Decide(step), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        step.Risk.ShouldBe(ToolRiskDto.Heavy);
        _executed.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AFailedCheck_IsNeverCoveredByAPlanGrant()
    {
        var calls = 0;
        ToolEscalation check = (_, _) => ++calls == 1 ? Task.FromResult(true) : Task.FromException<bool>(new AgentActionException("down"));
        var harness = RunnerHarness.Scripted(
            [.. new MetaToolProvider().GetTools(), Tool(check)],
            Turn(Call("set_plan", """{"steps":[{"title":"A","tool":"export"},{"title":"A again","tool":"export"}]}""")),
            Turn(Export()),
            Turn(Export()),
            Say("ok"));
        var (service, execution) = await StartAsync(harness);

        var first = await PendingStepAsync(harness);
        await service.DecideAsync(harness.Run.Id, first.Id, Decide(first, scope: ApprovalScopeDto.PlanHeavy), Token);
        var second = await PendingStepAsync(harness, 2);
        await service.DecideAsync(harness.Run.Id, second.Id, Decide(second), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        second.Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
        _executed.Count.ShouldBe(2);
    }
}
