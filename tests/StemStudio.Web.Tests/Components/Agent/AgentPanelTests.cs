using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>The agent panel (US1, FR-001, FR-004, FR-007; ui-test-ids 1.10.0).</summary>
public sealed class AgentPanelTests : PageTestContext
{
    private static RenderFragment Body => b => b.AddMarkupContent(0, "<h1>Page body</h1>");

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private Cut RenderPanel(ExperienceLevel level = ExperienceLevel.Advanced) =>
        RenderAtLevel<AgentPanel>(level, _ => { });

    private static void Type(Cut cut, string text) => Id(cut, "agent-input").Input(text);

    [Fact]
    public void Toggle_IsInTheLayout_AndOpensAndClosesThePanel()
    {
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, Body));

        var toggle = shell.Find("[data-testid=agent-toggle]");
        toggle.GetAttribute("aria-expanded").ShouldBe("false");
        shell.FindAll("[data-testid=agent-panel]").ShouldBeEmpty();

        toggle.Click();

        var panel = shell.WaitForElement("[data-testid=agent-panel]");
        (panel.TagName, panel.GetAttribute("aria-label"), panel.GetAttribute("data-help-id")).ShouldBe(("ASIDE", "Composer agent", "agent-panel"));
        shell.Find("[data-testid=agent-toggle]").GetAttribute("aria-expanded").ShouldBe("true");

        shell.Find("[data-testid=agent-toggle]").Click();

        shell.WaitForAssertion(() => shell.FindAll("[data-testid=agent-panel]").ShouldBeEmpty());
    }

    [Fact]
    public void NotReady_DisablesInputAndSend()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false));

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeTrue());
        Id(cut, "agent-send").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Send_IsDisabledWhileTheInputIsEmpty()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());

        Id(cut, "agent-send").HasAttribute("disabled").ShouldBeTrue();
        Type(cut, "   ");
        Id(cut, "agent-send").HasAttribute("disabled").ShouldBeTrue();
        Type(cut, "Zoek Hero of the Day");
        Id(cut, "agent-send").HasAttribute("disabled").ShouldBeFalse();
        Id(cut, "agent-input").GetAttribute("maxlength").ShouldBe("4000");
    }

    [Fact]
    public async Task Send_CreatesAConversation_AndPostsTheTextWithThePageContext()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{AgentData.TrackId}/mixer?mixId={AgentData.MixId}");
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());

        Type(cut, "Zet de zang 3 dB zachter");
        Id(cut, "agent-send").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-message-1").TextContent.ShouldContain("Zet de zang 3 dB zachter"));
        await AgentApi.Received(1).CreateConversationAsync(null, Arg.Any<CancellationToken>());
        await AgentApi.Received(1).SendMessageAsync(
            AgentData.ConversationId,
            Arg.Is<SendAgentMessageRequest>(r => r.Text == "Zet de zang 3 dB zachter" && r.Context.Page == "mixer" && r.Context.TrackId == AgentData.TrackId && r.Context.MixId == AgentData.MixId),
            Arg.Any<CancellationToken>());
        Id(cut, "agent-message-1").GetAttribute("data-role").ShouldBe("user");
        Id(cut, "agent-input").GetAttribute("value").ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task Enter_Sends_AndShiftEnterKeepsTheNewLine()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());

        Id(cut, "agent-input").KeyDown(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });
        Type(cut, "line one\n");
        Id(cut, "agent-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Type(cut, "line one\nline two\n");

        cut.WaitForAssertion(() => Id(cut, "agent-message-1").TextContent.ShouldContain("line one\nline two"));
        await AgentApi.Received(1).SendMessageAsync(Arg.Any<Guid>(), Arg.Is<SendAgentMessageRequest>(r => r.Text == "line one\nline two"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StreamedText_ShowsInALiveRegion_ThenTheStoredAnswer()
    {
        AgentApi.GetConversationAsync(AgentData.ConversationId, Arg.Any<CancellationToken>()).Returns(AgentData.Conversation(messages:
        [
            AgentData.Message(1, MessageRoleDto.User, "Zoek Hero of the Day"),
            AgentData.Message(2, MessageRoleDto.Assistant, "I found Hero of the Day."),
        ]));
        var cut = SendCommand("Zoek Hero of the Day");

        AgentEvents.Started().Text("I found ").Text("Hero of the Day.");

        cut.WaitForAssertion(() => Id(cut, "agent-streaming").TextContent.ShouldBe("I found Hero of the Day."));
        Id(cut, "agent-streaming").GetAttribute("aria-live").ShouldBe("polite");
        Id(cut, "agent-cancel").ShouldNotBeNull();

        AgentEvents.Completed();

        cut.WaitForAssertion(() => Id(cut, "agent-message-2").GetAttribute("data-role").ShouldBe("assistant"));
        Id(cut, "agent-message-2").TextContent.ShouldContain("I found Hero of the Day.");
        cut.FindAll("[data-testid=agent-streaming]").ShouldBeEmpty();
        cut.FindAll("[data-testid=agent-cancel]").ShouldBeEmpty();
    }

    [Fact]
    public void Plan_ShowsEveryItemWithItsStatus()
    {
        var cut = SendCommand("Importeer en splits verder");
        cut.FindAll("[data-testid=agent-plan]").ShouldBeEmpty();

        AgentEvents.Started().Push(AgentEventTypes.PlanUpdated, new PlanUpdatedEventDto
        {
            Plan =
            [
                new PlanItemDto { Index = 0, Title = "Import the song", Tool = "import_url", Status = PlanItemStatusDto.Done },
                new PlanItemDto { Index = 1, Title = "Split further", Tool = "start_detail_separation", Status = PlanItemStatusDto.Running },
                new PlanItemDto { Index = 2, Title = "Open the mixer", Status = PlanItemStatusDto.Pending },
            ],
        });

        cut.WaitForAssertion(() => Id(cut, "agent-plan-item-2").GetAttribute("data-status").ShouldBe("pending"));
        Id(cut, "agent-plan-item-0").GetAttribute("data-status").ShouldBe("done");
        Id(cut, "agent-plan-item-1").GetAttribute("data-status").ShouldBe("running");
        Id(cut, "agent-plan-item-1").TextContent.ShouldContain("Split further");
        Id(cut, "agent-plan").TagName.ShouldBe("OL");
    }

    [Fact]
    public void Steps_ShowToolStatusRiskProgressAndError()
    {
        var cut = SendCommand("Splits verder");

        AgentEvents.Started()
            .Push(AgentEventTypes.StepCreated, AgentData.Step(1, "start_detail_separation", AgentStepStatusDto.Running, ToolRiskDto.Heavy))
            .Push(AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.StepId, Status = AgentStepStatusDto.WaitingJob, Progress = 40 })
            .Push(AgentEventTypes.StepCreated, AgentData.Step(2, "open_mixer", AgentStepStatusDto.Running) with { StepId = AgentData.OtherConversationId })
            .Push(AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.OtherConversationId, Status = AgentStepStatusDto.Failed, Error = "The mixer of Hero of the Day is not open" });

        cut.WaitForAssertion(() => Id(cut, "agent-step-error-2").TextContent.ShouldContain("The mixer of Hero of the Day is not open"));
        var step = Id(cut, "agent-step-1");
        (step.GetAttribute("data-tool"), step.GetAttribute("data-status"), step.GetAttribute("data-risk")).ShouldBe(("start_detail_separation", "waiting_job", "heavy"));
        Id(cut, "agent-step-progress-1").GetAttribute("aria-valuenow").ShouldBe("40");
        Id(cut, "agent-step-2").GetAttribute("data-status").ShouldBe("failed");
    }

    [Fact]
    public void Beginner_SeesNoStepRows_ButProgressAndErrors()
    {
        var cut = SendCommand("Splits verder", ExperienceLevel.Beginner);

        AgentEvents.Started()
            .Push(AgentEventTypes.StepCreated, AgentData.Step(1, "start_detail_separation", AgentStepStatusDto.WaitingJob, ToolRiskDto.Heavy) with { Progress = 15 })
            .Push(AgentEventTypes.StepCreated, AgentData.Step(2, "open_mixer", AgentStepStatusDto.Failed) with { StepId = AgentData.OtherConversationId, Error = "Not open" });

        cut.WaitForAssertion(() => Id(cut, "agent-step-error-2").TextContent.ShouldContain("Not open"));
        Id(cut, "agent-step-progress-1").GetAttribute("aria-valuenow").ShouldBe("15");
        cut.FindAll("[data-testid=agent-step-1]").ShouldBeEmpty();
    }

    [Fact]
    public void RunFailure_ShowsTheMessageAndTheFix()
    {
        var cut = SendCommand("Zoek iets");

        AgentEvents.Started().Failed("The model did not answer in time.", "Try a shorter command");

        cut.WaitForAssertion(() => Id(cut, "agent-run-error").TextContent.ShouldContain("The model did not answer in time."));
        Id(cut, "agent-run-error").TextContent.ShouldContain("Try a shorter command");
        Id(cut, "agent-run-error").GetAttribute("role").ShouldBe("alert");
        cut.FindAll("[data-testid=agent-cancel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_CancelsTheActiveRun()
    {
        AgentApi.CancelRunAsync(AgentData.RunId, false, Arg.Any<CancellationToken>()).Returns(AgentData.Run(AgentRunStatusDto.Cancelled));
        var cut = SendCommand("Zoek iets");
        AgentEvents.Started();
        cut.WaitForElement("[data-testid=agent-cancel]");

        Id(cut, "agent-cancel").Click();

        await AgentApi.Received(1).CancelRunAsync(AgentData.RunId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Badge_ShowsBackendModelAndFallback_AndFollowsModelChanges()
    {
        var cut = SendCommand("Splits verder");
        cut.WaitForAssertion(() => Id(cut, "agent-backend-badge").GetAttribute("data-model").ShouldBe("qwen3.5:9b"));
        Id(cut, "agent-backend-badge").GetAttribute("data-backend").ShouldBe("ollama");
        Id(cut, "agent-backend-badge").GetAttribute("data-fallback").ShouldBe("false");

        AgentEvents.Started().Push(AgentEventTypes.ModelChanged, new ModelChangedEventDto { Model = "qwen3.5:4b", UsingFallback = true, Reason = "A GPU job started" });

        cut.WaitForAssertion(() => Id(cut, "agent-backend-badge").GetAttribute("data-fallback").ShouldBe("true"));
        Id(cut, "agent-backend-badge").GetAttribute("data-model").ShouldBe("qwen3.5:4b");
        Id(cut, "agent-backend-badge").GetAttribute("title").ShouldBe("A GPU job started");
    }

    [Fact]
    public void ConversationList_ListsNewestFirst_AndMarksTheOpenOne()
    {
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns(
            [AgentData.Summary(AgentData.OtherConversationId, "Newest"), AgentData.Summary(AgentData.ConversationId, "Older")]);
        AgentApi.GetConversationAsync(AgentData.ConversationId, Arg.Any<CancellationToken>()).Returns(AgentData.Conversation(messages:
            [AgentData.Message(1, MessageRoleDto.User, "Earlier command")]));
        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-conversation-list").QuerySelectorAll("button[data-testid^=agent-conversation-]").Length.ShouldBe(2));
        var entries = Id(cut, "agent-conversation-list").QuerySelectorAll("button[data-testid^=agent-conversation-]");
        entries[0].TextContent.ShouldContain("Newest");

        Id(cut, $"agent-conversation-{AgentData.ConversationId}").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-message-1").TextContent.ShouldContain("Earlier command"));
        Id(cut, $"agent-conversation-{AgentData.ConversationId}").GetAttribute("aria-current").ShouldBe("true");
        Id(cut, $"agent-conversation-{AgentData.OtherConversationId}").HasAttribute("aria-current").ShouldBeFalse();
    }

    [Fact]
    public async Task NewConversation_CreatesOne_AndClearsTheView()
    {
        var cut = SendCommand("Zoek iets");
        AgentEvents.Started().Completed();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=agent-cancel]").ShouldBeEmpty());
        AgentApi.CreateConversationAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(AgentData.Conversation(AgentData.OtherConversationId));

        Id(cut, "agent-new-conversation").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid^=agent-message-]").ShouldBeEmpty());
        await AgentApi.Received(2).CreateConversationAsync(null, Arg.Any<CancellationToken>());
        Panel.ConversationId.ShouldBe(AgentData.OtherConversationId);
    }

    [Fact]
    public void SendFailure_ShowsTheErrorAndKeepsTheText()
    {
        AgentApi.SendMessageAsync(Arg.Any<Guid>(), Arg.Any<SendAgentMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns<AgentMessageAcceptedDto>(_ => throw Problem(System.Net.HttpStatusCode.Conflict, "A run is active.", "Wait for the current run or cancel it."));
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());

        Type(cut, "Zoek iets");
        Id(cut, "agent-send").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-error").TextContent.ShouldContain("Wait for the current run or cancel it."));
        Id(cut, "agent-input").GetAttribute("value").ShouldBe("Zoek iets");
        cut.FindAll("[data-testid^=agent-message-]").ShouldBeEmpty();
    }

    [Fact]
    public void LostStream_ShowsTheRunSnapshotError()
    {
        AgentApi.GetRunAsync(AgentData.RunId, Arg.Any<CancellationToken>()).Returns(AgentData.Run(AgentRunStatusDto.Failed) with
        {
            Failure = new RunFailureDto { Code = AgentFailureCodes.Internal, Message = "The run stopped." },
        });
        var cut = SendCommand("Zoek iets");

        AgentEvents.Fail(new StemStudio.Web.Api.ApiException(StemStudio.Web.Api.ApiError.Unreachable));

        cut.WaitForAssertion(() => Id(cut, "agent-run-error").TextContent.ShouldContain("The run stopped."));
    }

    private Cut SendCommand(string text, ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var cut = RenderPanel(level);
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());
        Type(cut, text);
        Id(cut, "agent-send").Click();
        cut.WaitForAssertion(() => Id(cut, "agent-message-1").ShouldNotBeNull());
        return cut;
    }
}
