using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using NSubstitute;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>T041: approvals suspend and resume the run, bound to the exact arguments (FR-012, FR-015).</summary>
public sealed class ApprovalFlowTests
{
    private static readonly Guid TrackA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TrackB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private readonly List<string> _executed = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private AgentToolDescriptor Heavy(string name = "render") => Server(name, invocation =>
    {
        _executed.Add($"{name}:{invocation.Text("trackId")}");
        return ToolResult.Of(new JsonObject { ["ok"] = true });
    }, ToolRiskDto.Heavy, ToolSchema.Of(Param.Uuid("trackId"))) with
    {
        TargetArgument = "trackId",
        UndoKind = UndoKindDto.Compensate,
        Estimator = (_, _) => Task.FromResult(new ToolEstimate("Render a track", 4, "GPU (RTX 4070)")),
    };

    private static ScriptedCall Render(Guid track, string name = "render") => Call(name, $$"""{"trackId":"{{track}}"}""");

    private static DecideApprovalRequest Approve(RunStep step, ApprovalScopeDto scope = ApprovalScopeDto.Once) =>
        new() { Decision = ApprovalDecisionDto.Approve, Scope = scope, ArgumentsHash = step.ArgumentsHash };

    private static async Task<(AgentRunService Service, Task Execution)> StartAsync(RunnerHarness harness)
    {
        await harness.SeedAsync();
        var service = new AgentRunService(harness.Services());
        return (service, service.ExecuteAsync(harness.Run.Id, Token));
    }

    private static async Task<RunStep> PendingStepAsync(RunnerHarness harness, int count = 1)
    {
        var requested = await harness.Events.WaitForAsync(AgentEventTypes.ApprovalRequested, count);
        var stepId = System.Text.Json.JsonDocument.Parse(requested.Payload).RootElement.GetProperty("stepId").GetGuid();
        return harness.Runs.Steps.Single(step => step.Id == stepId);
    }

    [Fact]
    public async Task ApprovalRequiredStep_SuspendsTheRun_AndPublishesTheCard()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Say("ok"));
        var (service, execution) = await StartAsync(harness);

        var step = await PendingStepAsync(harness);

        _executed.ShouldBeEmpty();
        step.Status.ShouldBe(AgentStepStatusDto.AwaitingApproval);
        (step.EstimatedMinutes, step.Decision).ShouldBe((4.0, StepDecisionDto.ApprovalRequired));
        (await harness.CurrentRunAsync())!.Status.ShouldBe(AgentRunStatusDto.AwaitingApproval);
        var approval = (await harness.Runs.GetApprovalAsync(step.Id, Token))!;
        (approval.Status, approval.ArgumentsHash, approval.RunId).ShouldBe((ApprovalStatusDto.Pending, step.ArgumentsHash, harness.Run.Id));
        var card = harness.Events.Payload(AgentEventTypes.ApprovalRequested);
        card.GetProperty("effect").GetString().ShouldBe("Render a track");
        card.GetProperty("device").GetString().ShouldBe("gpu");
        card.GetProperty("undoable").GetBoolean().ShouldBeTrue();
        card.GetProperty("allowPlanScope").GetBoolean().ShouldBeFalse();
        card.GetProperty("reasons")[0].GetString().ShouldBe(AgentDecisionReasons.RiskHeavy);
        card.GetProperty("arguments").GetProperty("trackId").GetString().ShouldBe(TrackA.ToString());

        await service.DecideAsync(harness.Run.Id, step.Id, Approve(step), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task Approve_ExecutesTheStep_AndResolvesTheCard()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Say("ok"));
        var (service, execution) = await StartAsync(harness);
        var step = await PendingStepAsync(harness);

        var state = await service.DecideAsync(harness.Run.Id, step.Id, Approve(step), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        (state.Status, state.Scope, state.DecidedAt).ShouldBe((ApprovalStatusDto.Approved, ApprovalScopeDto.Once, RunnerHarness.Start));
        _executed.ShouldBe([$"render:{TrackA}"]);
        harness.Runs.Steps.Single().Status.ShouldBe(AgentStepStatusDto.Succeeded);
        harness.Events.Payload(AgentEventTypes.ApprovalResolved).GetProperty("status").GetString().ShouldBe("approved");
        (await harness.CurrentRunAsync())!.Status.ShouldBe(AgentRunStatusDto.Completed);
    }

    [Fact]
    public async Task ChangedHash_IsRefused_AndTheApprovalStaysPending()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Say("ok"));
        var (service, execution) = await StartAsync(harness);
        var step = await PendingStepAsync(harness);

        var conflict = await Should.ThrowAsync<AgentConflictException>(() =>
            service.DecideAsync(harness.Run.Id, step.Id, new DecideApprovalRequest { Decision = ApprovalDecisionDto.Approve, ArgumentsHash = new string('0', 64) }, Token));

        conflict.ProblemCode.ShouldBe(AgentProblemTypes.ArgumentsChanged);
        (await harness.Runs.GetApprovalAsync(step.Id, Token))!.Status.ShouldBe(ApprovalStatusDto.Pending);
        _executed.ShouldBeEmpty();
        await service.DecideAsync(harness.Run.Id, step.Id, Approve(step), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task Reject_ReturnsRejectedToTheModel_AndSkipsThePlanItem()
    {
        var harness = RunnerHarness.Scripted(
            [.. new MetaToolProvider().GetTools(), Heavy()],
            Turn(Call("set_plan", """{"steps":[{"title":"Render","tool":"render"}]}""")),
            Turn(Render(TrackA)),
            Say("Not rendered."));
        var (service, execution) = await StartAsync(harness);
        var step = await PendingStepAsync(harness);

        var state = await service.DecideAsync(harness.Run.Id, step.Id, new DecideApprovalRequest { Decision = ApprovalDecisionDto.Reject, ArgumentsHash = step.ArgumentsHash }, Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        state.Status.ShouldBe(ApprovalStatusDto.Rejected);
        _executed.ShouldBeEmpty();
        harness.Runs.Steps.Single(s => s.Tool == "render").Status.ShouldBe(AgentStepStatusDto.Rejected);
        var result = harness.Script.Requests[2].Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Last();
        result.Result!.ToString()!.ShouldContain("The user rejected this step.");
        (await harness.CurrentRunAsync())!.Plan.Single().Status.ShouldBe(PlanItemStatusDto.Skipped);
    }

    [Fact]
    public async Task SecondDecision_ReturnsTheStoredOne()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Say("ok"));
        var (service, execution) = await StartAsync(harness);
        var step = await PendingStepAsync(harness);

        await service.DecideAsync(harness.Run.Id, step.Id, Approve(step), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);
        var second = await service.DecideAsync(harness.Run.Id, step.Id, new DecideApprovalRequest { Decision = ApprovalDecisionDto.Reject, ArgumentsHash = step.ArgumentsHash }, Token);

        second.Status.ShouldBe(ApprovalStatusDto.Approved);
        _executed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UnknownStep_IsNotFound()
    {
        var harness = RunnerHarness.Scripted([Heavy()]);
        var service = new AgentRunService(harness.Services());

        await Should.ThrowAsync<KeyNotFoundException>(() => service.DecideAsync(harness.Run.Id, Guid.NewGuid(), new DecideApprovalRequest { Decision = ApprovalDecisionDto.Approve, ArgumentsHash = "x" }, Token));
    }

    [Fact]
    public async Task PendingApproval_ExpiresAfter24Hours()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Say("It expired."));
        var (service, execution) = await StartAsync(harness);
        var step = await PendingStepAsync(harness);

        for (var i = 0; i < 200 && !execution.IsCompleted; i++)
        {
            await Task.Delay(5, Token);
            harness.Time.Advance(TimeSpan.FromHours(25));
        }

        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);
        harness.Runs.Steps.Single().Status.ShouldBe(AgentStepStatusDto.Expired);
        (await harness.Runs.GetApprovalAsync(step.Id, Token))!.Status.ShouldBe(ApprovalStatusDto.Expired);
        var late = await Should.ThrowAsync<AgentConflictException>(() => service.DecideAsync(harness.Run.Id, step.Id, Approve(step), Token));
        late.ProblemCode.ShouldBe(AgentProblemTypes.ApprovalNotPending);
        _executed.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlanHeavyScope_CoversOnlyLaterStepsOfTheSameToolAndTarget()
    {
        var harness = RunnerHarness.Scripted(
            [.. new MetaToolProvider().GetTools(), Heavy(), Heavy("encode")],
            Turn(Call("set_plan", """{"steps":[{"title":"A","tool":"render"},{"title":"A again","tool":"render"},{"title":"B","tool":"render"},{"title":"E","tool":"encode"}]}""")),
            Turn(Render(TrackA)),
            Turn(Render(TrackA)),
            Turn(Render(TrackB)),
            Say("ok"));
        var (service, execution) = await StartAsync(harness);

        var first = await PendingStepAsync(harness);
        harness.Events.Payload(AgentEventTypes.ApprovalRequested).GetProperty("allowPlanScope").GetBoolean().ShouldBeTrue();
        await service.DecideAsync(harness.Run.Id, first.Id, Approve(first, ApprovalScopeDto.PlanHeavy), Token);
        var third = await PendingStepAsync(harness, 2);
        await service.DecideAsync(harness.Run.Id, third.Id, Approve(third), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        _executed.ShouldBe([$"render:{TrackA}", $"render:{TrackA}", $"render:{TrackB}"]);
        harness.Runs.Steps.Where(step => step.Tool == "render").Select(step => step.Decision)
            .ShouldBe([StepDecisionDto.ApprovalRequired, StepDecisionDto.Auto, StepDecisionDto.ApprovalRequired]);
    }

    [Fact]
    public async Task PlanScope_IsIgnoredWhenTheCardDidNotAllowIt()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Turn(Render(TrackA)), Say("ok"));
        var (service, execution) = await StartAsync(harness);

        var first = await PendingStepAsync(harness);
        var state = await service.DecideAsync(harness.Run.Id, first.Id, Approve(first, ApprovalScopeDto.PlanHeavy), Token);
        var second = await PendingStepAsync(harness, 2);
        await service.DecideAsync(harness.Run.Id, second.Id, Approve(second), Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        state.Scope.ShouldBe(ApprovalScopeDto.Once);
        _executed.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CancelWhileAwaitingApproval_CancelsTheRunAndExpiresTheApproval()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Turn(Render(TrackA)), Say("never"));
        var (service, execution) = await StartAsync(harness);
        var step = await PendingStepAsync(harness);

        await service.CancelAsync(harness.Run.Id, cancelJobs: false, Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        (await harness.CurrentRunAsync())!.Status.ShouldBe(AgentRunStatusDto.Cancelled);
        (await harness.Runs.GetApprovalAsync(step.Id, Token))!.Status.ShouldBe(ApprovalStatusDto.Expired);
        harness.Runs.Steps.Single().Status.ShouldBe(AgentStepStatusDto.Expired);
        var ended = await Should.ThrowAsync<AgentConflictException>(() => service.CancelAsync(harness.Run.Id, false, Token));
        ended.ProblemCode.ShouldBe(AgentProblemTypes.RunEnded);
    }

    [Fact]
    public async Task CancelWithJobs_CancelsTheJobsTheRunStarted()
    {
        var detail = new Detail002ToolProvider().GetTools().First(tool => tool.Name == "start_detail_separation");
        var harness = RunnerHarness.Scripted([detail, Heavy()], Turn(Call("start_detail_separation", $$"""{"trackId":"{{TrackA}}"}""")), Turn(Render(TrackB)), Say("never"));
        harness.Actions.Tracks.GetTrackAsync(TrackA, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackA));
        harness.Actions.Tracks.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(new StemStudio.Contracts.Health.ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Cpu });
        harness.Actions.Tracks.StartDetailAsync(TrackA, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackA).WithDetail(DetailPhaseStatusDto.Queued));
        var (service, execution) = await StartAsync(harness);
        var first = await PendingStepAsync(harness);
        await service.DecideAsync(harness.Run.Id, first.Id, Approve(first), Token);
        await PendingStepAsync(harness, 2);

        await service.CancelAsync(harness.Run.Id, cancelJobs: true, Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        await harness.Actions.Tracks.Received(1).CancelDetailAsync(TrackA, Arg.Any<CancellationToken>());
        harness.Events.Payload(AgentEventTypes.RunCancelled).GetProperty("cancelledJobs")[0].GetString().ShouldBe($"detail:{TrackA}");
    }

    [Fact]
    public async Task CancelOfAQueuedRun_EndsItDirectly()
    {
        var harness = RunnerHarness.Scripted([Heavy()], Say("never"));
        await harness.SeedAsync();
        var service = new AgentRunService(harness.Services());

        await service.CancelAsync(harness.Run.Id, false, Token);

        (await harness.CurrentRunAsync())!.Status.ShouldBe(AgentRunStatusDto.Cancelled);
        harness.Events.Types.ShouldBe([AgentEventTypes.RunCancelled]);
        await Should.ThrowAsync<KeyNotFoundException>(() => service.CancelAsync(Guid.NewGuid(), false, Token));
    }
}
