using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Model;

/// <summary>Skeleton tests (architect, 011 Phase 0 T004): state rules of data-model 4, 6 and 7.</summary>
public sealed class AgentStateRulesTests
{
    [Theory]
    [InlineData(AgentRunStatusDto.Queued, false)]
    [InlineData(AgentRunStatusDto.Running, false)]
    [InlineData(AgentRunStatusDto.AwaitingApproval, false)]
    [InlineData(AgentRunStatusDto.AwaitingUi, false)]
    [InlineData(AgentRunStatusDto.WaitingJob, false)]
    [InlineData(AgentRunStatusDto.Completed, true)]
    [InlineData(AgentRunStatusDto.Failed, true)]
    [InlineData(AgentRunStatusDto.Cancelled, true)]
    [InlineData(AgentRunStatusDto.Interrupted, true)]
    public void RunStatus_IsTerminalOnlyForFinalStates(AgentRunStatusDto status, bool terminal)
    {
        AgentStateRules.IsTerminal(status).ShouldBe(terminal);
    }

    [Theory]
    [InlineData(AgentStepStatusDto.Planned, false)]
    [InlineData(AgentStepStatusDto.AwaitingApproval, false)]
    [InlineData(AgentStepStatusDto.Running, false)]
    [InlineData(AgentStepStatusDto.AwaitingUi, false)]
    [InlineData(AgentStepStatusDto.WaitingJob, false)]
    [InlineData(AgentStepStatusDto.Succeeded, true)]
    [InlineData(AgentStepStatusDto.Failed, true)]
    [InlineData(AgentStepStatusDto.Rejected, true)]
    [InlineData(AgentStepStatusDto.RejectedInvalid, true)]
    [InlineData(AgentStepStatusDto.Skipped, true)]
    [InlineData(AgentStepStatusDto.Expired, true)]
    [InlineData(AgentStepStatusDto.Undone, true)]
    public void StepStatus_IsTerminalOnlyForFinishedSteps(AgentStepStatusDto status, bool terminal)
    {
        AgentStateRules.IsTerminal(status).ShouldBe(terminal);
    }

    [Theory]
    [InlineData(ToolRiskDto.Read, false, false)]
    [InlineData(ToolRiskDto.Change, false, true)]
    [InlineData(ToolRiskDto.Job, false, true)]
    [InlineData(ToolRiskDto.Heavy, true, false)]
    [InlineData(ToolRiskDto.Destructive, true, false)]
    public void Risk_ApprovalRulesFollowAdr020(ToolRiskDto risk, bool always, bool whenTainted)
    {
        AgentStateRules.AlwaysRequiresApproval(risk).ShouldBe(always);
        AgentStateRules.RequiresApprovalWhenTainted(risk).ShouldBe(whenTainted);
    }

    [Fact]
    public void Run_IsTerminalFollowsItsStatus()
    {
        var run = Samples.Run(AgentRunStatusDto.Running);

        run.IsTerminal.ShouldBeFalse();
        (run with { Status = AgentRunStatusDto.Completed }).IsTerminal.ShouldBeTrue();
        run.Usage.ShouldBe(TokenUsage.Zero);
        run.Plan.ShouldBeEmpty();
    }
}
