using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>Undo, rename/delete and history reload with resume (US5, FR-002, FR-017; ui-test-ids agent-undo, agent-conversation-*).</summary>
public sealed class AgentHistoryTests : PageTestContext
{
    private static readonly Guid UndoRunId = Guid.Parse("b1b2c3d4-0000-4000-8000-00000000000a");

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private Cut RenderPanel(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var cut = RenderAtLevel<AgentPanel>(level, _ => { });
        cut.WaitForElement("[data-testid=agent-undo]");
        return cut;
    }

    private void Stored(AgentRunStatusDto? lastRun, AgentRunDto? snapshot = null)
    {
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([AgentData.Summary(last: lastRun)]);
        AgentApi.GetConversationAsync(AgentData.ConversationId, Arg.Any<CancellationToken>()).Returns(AgentData.Conversation(
            messages: [AgentData.Message(1, MessageRoleDto.User, "Importeer en splits verder")],
            runs: lastRun is { } status ? [AgentData.RunSummary(status)] : []));
        AgentApi.GetRunAsync(AgentData.RunId, Arg.Any<CancellationToken>()).Returns(snapshot ?? AgentData.Run(lastRun ?? AgentRunStatusDto.Completed));
    }

    [Fact]
    public void Undo_IsDisabledWithoutAFinishedRun()
    {
        var cut = RenderPanel();

        Id(cut, "agent-undo").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Undo_IsEnabledAfterAFinishedRun_AndFollowsTheUndoRun()
    {
        Stored(AgentRunStatusDto.Completed);
        AgentApi.UndoAsync(AgentData.ConversationId, Arg.Any<CancellationToken>()).Returns(new AgentRunAcceptedDto { RunId = UndoRunId });
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-undo").HasAttribute("disabled").ShouldBeFalse());

        Id(cut, "agent-undo").Click();
        AgentEvents.Started().Text("Vocals are back at -1.9 dB.");

        cut.WaitForAssertion(() => Id(cut, "agent-streaming").TextContent.ShouldContain("Vocals are back"));
        AgentApi.Received(1).StreamRunEventsAsync(UndoRunId, null, Arg.Any<CancellationToken>());
        Id(cut, "agent-undo").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void NothingToUndo_DisablesUndo_AndSaysSo()
    {
        Stored(AgentRunStatusDto.Completed);
        AgentApi.UndoAsync(AgentData.ConversationId, Arg.Any<CancellationToken>())
            .Returns<AgentRunAcceptedDto>(_ => throw Problem(System.Net.HttpStatusCode.Conflict, "Nothing to undo.", "The agent made no change that can be undone."));
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-undo").HasAttribute("disabled").ShouldBeFalse());

        Id(cut, "agent-undo").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-error").TextContent.ShouldContain("no change that can be undone"));
        Id(cut, "agent-undo").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Reload_OpensTheNewestConversation_AndShowsTheFinishedRun()
    {
        Stored(AgentRunStatusDto.Failed, AgentData.Run(AgentRunStatusDto.Failed) with
        {
            Plan = [new PlanItemDto { Index = 0, Title = "Import the song", Status = PlanItemStatusDto.Failed }],
            Steps = [AgentData.Step(1, "import_url", AgentStepStatusDto.Failed) with { Error = "No audio found." }],
            Failure = new RunFailureDto { Code = AgentFailureCodes.Timeout, Message = "The model did not answer in time." },
        });

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-run-error").TextContent.ShouldContain("did not answer in time"));
        Id(cut, "agent-message-1").TextContent.ShouldContain("Importeer en splits verder");
        Id(cut, "agent-plan-item-0").GetAttribute("data-status").ShouldBe("failed");
        Id(cut, "agent-step-1").GetAttribute("data-tool").ShouldBe("import_url");
        Id(cut, "agent-step-error-1").TextContent.ShouldContain("No audio found.");
        Id(cut, $"agent-conversation-{AgentData.ConversationId}").GetAttribute("aria-current").ShouldBe("true");
        AgentApi.DidNotReceive().StreamRunEventsAsync(Arg.Any<Guid>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Reload_WhileTheAgentIsUnavailable_TheBadgeShowsTheCurrentBackend_NotTheRestoredRuns()
    {
        Stored(AgentRunStatusDto.Completed, AgentData.Run(AgentRunStatusDto.Completed) with { Backend = AgentBackendDto.Fake, Model = "scripted" });
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false, AgentBackendDto.None));

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-message-1").TextContent.ShouldContain("Importeer en splits verder"));
        cut.WaitForAssertion(() => Id(cut, "agent-backend-badge").GetAttribute("data-backend").ShouldBe("none"));
        Id(cut, "agent-backend-badge").GetAttribute("data-model").ShouldBe(string.Empty);
    }

    [Fact]
    public void Reload_ResumesARunningRunsStream()
    {
        Stored(AgentRunStatusDto.WaitingJob);

        var cut = RenderPanel();
        cut.WaitForElement("[data-testid=agent-cancel]");
        AgentEvents.Started().Text("Still splitting");

        cut.WaitForAssertion(() => Id(cut, "agent-streaming").TextContent.ShouldBe("Still splitting"));
        AgentApi.Received(1).StreamRunEventsAsync(AgentData.RunId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resume_DoesNotRepeatUiToolsTheRunAlreadyHas_ButRunsNewOnes()
    {
        var answered = AgentData.Step(1, "navigate", AgentStepStatusDto.Succeeded);
        var waiting = AgentData.Step(2, "navigate", AgentStepStatusDto.AwaitingUi) with { StepId = AgentData.OtherConversationId };
        Stored(AgentRunStatusDto.AwaitingUi, AgentData.Run(AgentRunStatusDto.AwaitingUi) with { Steps = [answered, waiting] });
        var cut = RenderPanel();
        cut.WaitForElement("[data-testid=agent-cancel]");

        AgentEvents
            .Push(AgentEventTypes.UiRequested, new UiRequestedEventDto { StepId = answered.StepId, Tool = "navigate", Arguments = AgentData.Json("""{"page":"tracks"}"""), TimeoutSeconds = 15 })
            .Push(AgentEventTypes.UiRequested, new UiRequestedEventDto { StepId = waiting.StepId, Tool = "navigate", Arguments = AgentData.Json("""{"page":"mixes"}"""), TimeoutSeconds = 15 })
            .Text("Done");

        cut.WaitForAssertion(() => Id(cut, "agent-streaming").TextContent.ShouldBe("Done"));
        await AgentApi.DidNotReceive().SubmitUiResultAsync(AgentData.RunId, answered.StepId, Arg.Any<SubmitUiResultRequest>(), Arg.Any<CancellationToken>());
        await AgentApi.Received(1).SubmitUiResultAsync(AgentData.RunId, waiting.StepId, Arg.Any<SubmitUiResultRequest>(), Arg.Any<CancellationToken>());
        NavigatedTo.ShouldEndWith("/mixes");
    }

    [Fact]
    public void Rename_ChangesTheTitle()
    {
        Stored(AgentRunStatusDto.Completed);
        AgentApi.RenameConversationAsync(AgentData.ConversationId, "Hero practice", Arg.Any<CancellationToken>()).Returns(AgentData.Summary(title: "Hero practice"));
        var cut = RenderPanel();

        Id(cut, $"agent-conversation-rename-{AgentData.ConversationId}").Click();
        var input = cut.Find("input[aria-label='Conversation title']");
        input.GetAttribute("value").ShouldBe("Mix practice");
        input.Input("Hero practice");
        input.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Id(cut, $"agent-conversation-{AgentData.ConversationId}").TextContent.ShouldBe("Hero practice"));
        cut.FindAll("input[aria-label='Conversation title']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_EscapeCancels_AndAnErrorIsShown()
    {
        Stored(AgentRunStatusDto.Completed);
        AgentApi.RenameConversationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ConversationSummaryDto>(_ => throw Problem(System.Net.HttpStatusCode.UnprocessableEntity, "Invalid title.", "The title can have at most 120 characters."));
        var cut = RenderPanel();

        Id(cut, $"agent-conversation-rename-{AgentData.ConversationId}").Click();
        cut.Find("input[aria-label='Conversation title']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        cut.FindAll("input[aria-label='Conversation title']").ShouldBeEmpty();
        await AgentApi.DidNotReceive().RenameConversationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        Id(cut, $"agent-conversation-rename-{AgentData.ConversationId}").Click();
        cut.Find("input[aria-label='Conversation title']").Change("x");
        cut.Find("button[aria-label='Save title']").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-error").TextContent.ShouldContain("at most 120 characters"));
    }

    [Fact]
    public void Delete_AfterConfirmation_RemovesTheConversation_AndClearsTheView()
    {
        Stored(AgentRunStatusDto.Completed);
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var cut = RenderPanel();
        cut.WaitForElement("[data-testid=agent-message-1]");

        Id(cut, $"agent-conversation-delete-{AgentData.ConversationId}").Click();

        cut.WaitForAssertion(() => cut.FindAll($"[data-testid=agent-conversation-{AgentData.ConversationId}]").ShouldBeEmpty());
        cut.FindAll("[data-testid^=agent-message-]").ShouldBeEmpty();
        AgentApi.Received(1).DeleteConversationAsync(AgentData.ConversationId, Arg.Any<CancellationToken>());
        Panel.ConversationId.ShouldBeNull();
    }

    [Fact]
    public void Delete_Declined_KeepsIt()
    {
        Stored(AgentRunStatusDto.Completed);
        var cut = RenderPanel();

        Id(cut, $"agent-conversation-delete-{AgentData.ConversationId}").Click();

        AgentApi.DidNotReceive().DeleteConversationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Id(cut, $"agent-conversation-{AgentData.ConversationId}").ShouldNotBeNull();
    }

    [Fact]
    public void Delete_OfAnotherConversation_KeepsTheOpenOne()
    {
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns([AgentData.Summary(), AgentData.Summary(AgentData.OtherConversationId, "Other")]);
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Panel.ConversationId.ShouldBe(AgentData.ConversationId));

        Id(cut, $"agent-conversation-delete-{AgentData.OtherConversationId}").Click();

        cut.WaitForAssertion(() => cut.FindAll($"[data-testid=agent-conversation-{AgentData.OtherConversationId}]").ShouldBeEmpty());
        Panel.ConversationId.ShouldBe(AgentData.ConversationId);
    }

    [Fact]
    public void Delete_IsDisabledWhileItsRunIsActive()
    {
        Stored(AgentRunStatusDto.Running);

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, $"agent-conversation-delete-{AgentData.ConversationId}").HasAttribute("disabled").ShouldBeTrue());
    }

    [Fact]
    public void Beginner_HasNoRenameOrDelete()
    {
        Stored(AgentRunStatusDto.Completed);

        var cut = RenderPanel(ExperienceLevel.Beginner);

        cut.WaitForElement($"[data-testid=agent-conversation-{AgentData.ConversationId}]");
        cut.FindAll($"[data-testid=agent-conversation-rename-{AgentData.ConversationId}]").ShouldBeEmpty();
        cut.FindAll($"[data-testid=agent-conversation-delete-{AgentData.ConversationId}]").ShouldBeEmpty();
    }
}
