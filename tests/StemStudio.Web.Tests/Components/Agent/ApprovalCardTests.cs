using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>The confirmation card (US2, FR-012, FR-013; ui-test-ids 1.10.0 agent-approval-*).</summary>
public sealed class ApprovalCardTests : PageTestContext
{
    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    [Fact]
    public void Card_ShowsToolRiskReasonsEffectArgumentsUndoAndEstimate()
    {
        var cut = Request(AgentData.Approval(reasons: [AgentDecisionReasons.RiskHeavy, AgentDecisionReasons.Tainted]));

        var card = Id(cut, "agent-approval-card");
        (card.GetAttribute("data-tool"), card.GetAttribute("data-risk"), card.GetAttribute("data-reasons"))
            .ShouldBe(("start_detail_separation", "heavy", "risk-heavy,tainted"));
        card.GetAttribute("data-help-id").ShouldBe("agent-approvals");
        Id(cut, "agent-approval-effect").TextContent.ShouldBe("Splits Hero of the Day into 6 detail stems");
        Id(cut, "agent-approval-arguments").TextContent.ShouldContain($"\"trackId\": \"{AgentData.TrackId}\"");
        card.TextContent.ShouldContain("Proposed after reading external content");
        card.TextContent.ShouldContain("Long job");
        card.TextContent.ShouldContain("Can be undone");
        var estimate = Id(cut, "agent-approval-estimate");
        (estimate.GetAttribute("data-minutes"), estimate.GetAttribute("data-device")).ShouldBe(("6.5", "gpu"));
        estimate.TextContent.ShouldContain("About 7 minutes on the graphics card");
        cut.FindAll("[data-testid=agent-approve-plan]").ShouldBeEmpty();
    }

    [Fact]
    public void DestructiveCard_HasNoEstimate_AndSaysItCannotBeUndone()
    {
        var cut = Request(AgentData.Approval(ToolRiskDto.Destructive, reasons: [AgentDecisionReasons.RiskDestructive]) with { Undoable = false, Tool = "delete_track" });

        cut.FindAll("[data-testid=agent-approval-estimate]").ShouldBeEmpty();
        var card = Id(cut, "agent-approval-card");
        card.TextContent.ShouldContain("Cannot be undone");
        card.TextContent.ShouldContain("Deletes or overwrites without undo");
        card.TextContent.ShouldNotContain("external content");
    }

    [Theory]
    [InlineData("cpu", "cpu", "on the processor")]
    [InlineData(null, "", "")]
    public void HeavyCard_OnTheProcessorOrWithoutADevice(string? device, string attribute, string text)
    {
        var cut = Request(AgentData.Approval() with { Device = device, EstimatedMinutes = 0.4 });

        var estimate = Id(cut, "agent-approval-estimate");
        (estimate.GetAttribute("data-device"), estimate.GetAttribute("data-minutes")).ShouldBe((attribute, "0.4"));
        estimate.TextContent.ShouldContain("About 1 minute");
        estimate.TextContent.ShouldContain(text);
    }

    [Fact]
    public void HeavyCard_WithoutAnEstimate_SaysSo()
    {
        var cut = Request(AgentData.Approval() with { EstimatedMinutes = null });

        Id(cut, "agent-approval-estimate").GetAttribute("data-minutes").ShouldBe(string.Empty);
        Id(cut, "agent-approval-estimate").TextContent.ShouldContain("Duration unknown");
    }

    [Theory]
    [InlineData(AgentDecisionReasons.UrlNotFromUser, "The link does not appear in your own messages")]
    [InlineData(AgentDecisionReasons.UndoCompensation, "Removes what an earlier step created")]
    [InlineData("new-reason", "new-reason")]
    public void OtherReasons_AreReadable(string reason, string text)
    {
        var cut = Request(AgentData.Approval(ToolRiskDto.Change, reasons: [reason]));

        Id(cut, "agent-approval-card").TextContent.ShouldContain(text);
    }

    [Fact]
    public async Task Approve_PostsOnceWithTheArgumentsHash_AndApproveIsNotFocused()
    {
        var cut = Request(AgentData.Approval());

        Id(cut, "agent-approve").Click();

        await AgentApi.Received(1).DecideApprovalAsync(AgentData.RunId, AgentData.StepId,
            Arg.Is<DecideApprovalRequest>(r => r.Decision == ApprovalDecisionDto.Approve && r.Scope == ApprovalScopeDto.Once && r.ArgumentsHash == "sha256:abc"),
            Arg.Any<CancellationToken>());
        Id(cut, "agent-approve").HasAttribute("autofocus").ShouldBeFalse();
    }

    [Fact]
    public async Task ApprovePlan_ExistsOnlyWhenAllowed_AndPostsPlanHeavy()
    {
        var cut = Request(AgentData.Approval(allowPlan: true));

        Id(cut, "agent-approve-plan").Click();

        await AgentApi.Received(1).DecideApprovalAsync(AgentData.RunId, AgentData.StepId,
            Arg.Is<DecideApprovalRequest>(r => r.Decision == ApprovalDecisionDto.Approve && r.Scope == ApprovalScopeDto.PlanHeavy), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_PostsRejectWithoutAScope()
    {
        var cut = Request(AgentData.Approval());

        Id(cut, "agent-reject").Click();

        await AgentApi.Received(1).DecideApprovalAsync(AgentData.RunId, AgentData.StepId,
            Arg.Is<DecideApprovalRequest>(r => r.Decision == ApprovalDecisionDto.Reject && r.Scope == null && r.ArgumentsHash == "sha256:abc"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Card_ClosesOnApprovalResolved()
    {
        var cut = Request(AgentData.Approval());

        AgentEvents.Push(AgentEventTypes.ApprovalResolved, new ApprovalStateDto { StepId = AgentData.StepId, Status = ApprovalStatusDto.Approved, Scope = ApprovalScopeDto.Once });

        cut.WaitForAssertion(() => cut.FindAll("[data-testid=agent-approval-card]").ShouldBeEmpty());
    }

    [Fact]
    public void ExpiredApproval_ClosesTheCard_AndShowsTheProblem()
    {
        AgentApi.DecideApprovalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DecideApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns<ApprovalStateDto>(_ => throw Problem(System.Net.HttpStatusCode.Conflict, "The approval is no longer pending.", "The run has ended."));
        var cut = Request(AgentData.Approval());

        Id(cut, "agent-approve").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-error").TextContent.ShouldContain("The run has ended."));
        cut.FindAll("[data-testid=agent-approval-card]").ShouldBeEmpty();
    }

    [Fact]
    public void Beginner_SeesTheCardToo()
    {
        var cut = Request(AgentData.Approval(), ExperienceLevel.Beginner);

        Id(cut, "agent-approval-card").ShouldNotBeNull();
    }

    private Cut Request(ApprovalRequestDto approval, ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var cut = RenderAtLevel<StemStudio.Web.Components.Agent.AgentPanel>(level, _ => { });
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());
        Id(cut, "agent-input").Input("Splits verder");
        Id(cut, "agent-send").Click();
        AgentEvents.Started().Push(AgentEventTypes.ApprovalRequested, approval);
        cut.WaitForElement("[data-testid=agent-approval-card]");
        return cut;
    }
}
