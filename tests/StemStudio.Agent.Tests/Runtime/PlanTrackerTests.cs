using StemStudio.Agent.Model;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>T026: set_plan and the linking of calls to plan items (FR-007).</summary>
public sealed class PlanTrackerTests
{
    private static PlanItem Item(int index, string? tool, PlanItemStatusDto status) => new() { Index = index, Title = "Step " + index, Tool = tool, Status = status };

    [Fact]
    public void Replace_OnAnEmptyPlan_CreatesPendingItems()
    {
        var plan = PlanTracker.Replace([], [new PlanStep("Find the song", "search_library"), new PlanStep("Open it", null)]);

        plan.Select(item => (item.Index, item.Title, item.Tool, item.Status)).ShouldBe(
        [
            (0, "Find the song", "search_library", PlanItemStatusDto.Pending),
            (1, "Open it", (string?)null, PlanItemStatusDto.Pending),
        ]);
    }

    [Fact]
    public void Replace_KeepsStartedItems_AndReplacesOnlyTheNotStartedOnes()
    {
        IReadOnlyList<PlanItem> current = [Item(0, "a", PlanItemStatusDto.Done), Item(1, "b", PlanItemStatusDto.Running), Item(2, "c", PlanItemStatusDto.Pending)];

        var plan = PlanTracker.Replace(current, [new PlanStep("New", "d")]);

        plan.Select(item => (item.Index, item.Tool, item.Status)).ShouldBe(
        [
            (0, "a", PlanItemStatusDto.Done), (1, "b", PlanItemStatusDto.Running), (2, "d", PlanItemStatusDto.Pending),
        ]);
    }

    [Fact]
    public void Link_TakesTheFirstPendingItemOfTheTool()
    {
        IReadOnlyList<PlanItem> plan = [Item(0, "a", PlanItemStatusDto.Done), Item(1, "b", PlanItemStatusDto.Pending), Item(2, "b", PlanItemStatusDto.Pending)];

        var link = PlanTracker.Link(plan, "b", "Test b");

        link.Index.ShouldBe(1);
        link.Plan[1].Status.ShouldBe(PlanItemStatusDto.Running);
        link.Plan[2].Status.ShouldBe(PlanItemStatusDto.Pending);
    }

    [Fact]
    public void Link_AppendsAnExtraItemForAnUnplannedCall()
    {
        var link = PlanTracker.Link([Item(0, "a", PlanItemStatusDto.Pending)], "z", "Do z");

        link.Index.ShouldBe(1);
        (link.Plan[1].Title, link.Plan[1].Tool, link.Plan[1].Status).ShouldBe(("Do z", "z", PlanItemStatusDto.Running));
    }

    [Fact]
    public void Link_WithoutAPlan_LinksNothing()
    {
        var link = PlanTracker.Link([], "a", "A");

        (link.Index, link.Plan.Count).ShouldBe(((int?)null, 0));
    }

    [Fact]
    public void SetStatus_ChangesOneItem_AndIgnoresNull()
    {
        IReadOnlyList<PlanItem> plan = [Item(0, "a", PlanItemStatusDto.Running)];

        PlanTracker.SetStatus(plan, 0, PlanItemStatusDto.Done)[0].Status.ShouldBe(PlanItemStatusDto.Done);
        PlanTracker.SetStatus(plan, null, PlanItemStatusDto.Done).ShouldBeSameAs(plan);
    }

    [Fact]
    public async Task SetPlan_PublishesThePlan_AndLinksTheCalls()
    {
        var harness = RunnerHarness.Scripted(
            [.. new MetaToolProvider().GetTools(), Echo()],
            Turn(Call("set_plan", """{"steps":[{"title":"Echo it","tool":"echo"}]}""")),
            Turn(Call("echo"), Call("echo")),
            Say("ok"));

        var run = await harness.ExecuteAsync();

        harness.Events.Payload(AgentEventTypes.PlanUpdated).GetProperty("plan")[0].GetProperty("title").GetString().ShouldBe("Echo it");
        harness.Runs.Steps.Select(step => (step.Tool, step.PlanIndex)).ShouldBe([("set_plan", (int?)null), ("echo", 0), ("echo", 1)]);
        run.Plan.Select(item => (item.Tool, item.Status)).ShouldBe([("echo", PlanItemStatusDto.Done), ("echo", PlanItemStatusDto.Done)]);
        harness.Runs.Steps[0].ResultSummary.ShouldBe("""{"accepted":1}""");
        harness.Events.Types.Count(type => type == AgentEventTypes.PlanUpdated).ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task FailedStep_MarksItsPlanItemFailed()
    {
        var boom = Server("boom", _ => throw new StemStudio.Agent.Ports.AgentActionException("No."));
        var harness = RunnerHarness.Scripted(
            [.. new MetaToolProvider().GetTools(), boom],
            Turn(Call("set_plan", """{"steps":[{"title":"Boom","tool":"boom"}]}""")),
            Turn(Call("boom")),
            Say("ok"));

        var run = await harness.ExecuteAsync();

        run.Plan.Single().Status.ShouldBe(PlanItemStatusDto.Failed);
    }

    [Fact]
    public async Task NoPlan_StepsAreStillListed()
    {
        var harness = RunnerHarness.Scripted([Echo()], Turn(Call("echo")), Say("ok"));

        var run = await harness.ExecuteAsync();

        run.Plan.ShouldBeEmpty();
        harness.Runs.Steps.Single().PlanIndex.ShouldBeNull();
        harness.Events.Types.ShouldNotContain(AgentEventTypes.PlanUpdated);
    }

    [Fact]
    public void MetaTools_FollowTheCatalogue()
    {
        var tools = new MetaToolProvider().GetTools().ToList();

        tools.Select(tool => (tool.Name, tool.Kind, tool.Risk, tool.Group)).ShouldBe(
        [
            ("set_plan", ToolKindDto.Meta, ToolRiskDto.Read, ToolGroup.Core), ("wait_for_job", ToolKindDto.Meta, ToolRiskDto.Read, ToolGroup.Core),
        ]);
        ToolArgumentValidator.Validate(tools[0].Schema, """{"steps":[]}""").IsValid.ShouldBeFalse();
        ToolArgumentValidator.Validate(tools[1].Schema, """{"phase":"detail"}""").IsValid.ShouldBeFalse();
        ToolArgumentValidator.Validate(tools[1].Schema, """{"phase":"restyle","jobId":"g1"}""").IsValid.ShouldBeTrue();
    }
}
