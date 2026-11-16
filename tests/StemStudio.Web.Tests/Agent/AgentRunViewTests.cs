using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>The run view applies every event type of DELTA.md section 3 and restores a run snapshot.</summary>
public sealed class AgentRunViewTests
{
    private static AgentEvent Event(int id, string type, object data) => new(id, type, JsonSerializer.SerializeToElement(data, JsonSerializerOptions.Web));

    [Fact]
    public void ApprovalRequested_ThenResolvedForAnotherStep_KeepsTheCard_ThenResolvedClosesIt()
    {
        var view = AgentRunView.Start(AgentData.RunId).Apply(Event(1, AgentEventTypes.ApprovalRequested, AgentData.Approval()));

        var other = view.Apply(Event(2, AgentEventTypes.ApprovalResolved, new ApprovalStateDto { StepId = Guid.NewGuid(), Status = ApprovalStatusDto.Approved, Scope = ApprovalScopeDto.Once }));
        var resolved = other.Apply(Event(3, AgentEventTypes.ApprovalResolved, new ApprovalStateDto { StepId = AgentData.StepId, Status = ApprovalStatusDto.Rejected, Scope = ApprovalScopeDto.Once }));

        (view.Approval?.StepId, other.Approval?.StepId, resolved.Approval, resolved.LastEventId).ShouldBe((AgentData.StepId, AgentData.StepId, null, 3));
    }

    [Fact]
    public void UsageUiRequestAndUnknownEvents_UpdateOnlyWhatTheyCarry()
    {
        var view = AgentRunView.Start(AgentData.RunId)
            .Apply(Event(5, AgentEventTypes.UsageUpdated, new TokenUsageDto { InputTokens = 1200, OutputTokens = 80, CachedInputTokens = 900 }))
            .Apply(Event(4, AgentEventTypes.UiRequested, new { stepId = AgentData.StepId }))
            .Apply(Event(6, "something.new", new { }));

        (view.Usage!.InputTokens, view.LastEventId, view.Ended).ShouldBe((1200L, 6, false));
    }

    [Theory]
    [InlineData(AgentEventTypes.RunCompleted, false)]
    [InlineData(AgentEventTypes.RunCancelled, false)]
    [InlineData(AgentEventTypes.RunInterrupted, true)]
    public void TerminalEvents_EndTheRun_AndCloseTheCard(string type, bool interrupted)
    {
        var view = AgentRunView.Start(AgentData.RunId).Apply(Event(1, AgentEventTypes.ApprovalRequested, AgentData.Approval()))
            .Apply(Event(2, type, new { runId = AgentData.RunId, messageId = AgentData.MessageId }));

        (view.Ended, view.Interrupted, view.Approval).ShouldBe((true, interrupted, null));
    }

    [Fact]
    public void StepUpdate_KeepsEarlierValues_AndRecreatingAStepReplacesIt()
    {
        var step = AgentData.Step() with { Progress = 10, Error = "old", ResultSummary = AgentData.Json("""{"a":1}""") };
        var view = AgentRunView.Start(AgentData.RunId)
            .Apply(Event(1, AgentEventTypes.StepCreated, step))
            .Apply(Event(2, AgentEventTypes.StepCreated, step))
            .Apply(Event(3, AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.StepId, Status = AgentStepStatusDto.Succeeded }));

        var only = view.Steps.ShouldHaveSingleItem();
        (only.Status, only.Progress, only.Error, only.ResultSummary!.Value.GetProperty("a").GetInt32()).ShouldBe((AgentStepStatusDto.Succeeded, 10, "old", 1));
    }

    [Theory]
    [InlineData(AgentRunStatusDto.Running, false, false)]
    [InlineData(AgentRunStatusDto.Completed, true, false)]
    [InlineData(AgentRunStatusDto.Failed, true, false)]
    [InlineData(AgentRunStatusDto.Cancelled, true, false)]
    [InlineData(AgentRunStatusDto.Interrupted, true, true)]
    public void Snapshot_RestoresTheRun(AgentRunStatusDto status, bool ended, bool interrupted)
    {
        var run = AgentData.Run(status) with { Plan = [new PlanItemDto { Index = 0, Title = "Search", Status = PlanItemStatusDto.Done }], Steps = [AgentData.Step()], PendingApproval = AgentData.Approval() };

        var view = AgentRunView.FromSnapshot(run);

        (view.Ended, view.Interrupted, view.Plan.Count, view.Steps.Count, view.Approval?.StepId, view.Model).ShouldBe((ended, interrupted, 1, 1, AgentData.StepId, "qwen3.5:9b"));
    }

    [Theory]
    [InlineData("abc", "abcd", false, "abcd")]
    [InlineData("abc", "abc\n", true, "abc")]
    [InlineData("abc", "a\nbc", true, "abc")]
    [InlineData("", "\n", true, "")]
    [InlineData("abc", "ab", false, "ab")]
    public void TryStripEnter_RemovesOnlyTheInsertedNewLine(string previous, string current, bool stripped, string text)
    {
        AgentWire.TryStripEnter(previous, current, out var result).ShouldBe(stripped);
        result.ShouldBe(text);
    }
}
