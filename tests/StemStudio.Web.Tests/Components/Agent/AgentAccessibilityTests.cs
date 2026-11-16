using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>Accessibility of the agent panel (T079d): keyboard, focus, live regions and named controls.</summary>
public sealed class AgentAccessibilityTests : PageTestContext
{
    private const string FocusCall = "Blazor._internal.domWrapper.focus";

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private Cut RenderPanel(ExperienceLevel level = ExperienceLevel.Expert)
    {
        var cut = RenderAtLevel<AgentPanel>(level, _ => { });
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());
        return cut;
    }

    private static void Send(Cut cut)
    {
        Id(cut, "agent-input").Input("Splits verder");
        Id(cut, "agent-send").Click();
        cut.WaitForElement("[data-testid=agent-message-1]");
    }

    [Fact]
    public void Escape_ClosesThePanel()
    {
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Body</h1>"))));
        shell.Find("[data-testid=agent-toggle]").Click();
        var panel = shell.WaitForElement("[data-testid=agent-panel]");

        panel.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        shell.WaitForAssertion(() => shell.FindAll("[data-testid=agent-panel]").ShouldBeEmpty());
        shell.Find("[data-testid=agent-toggle]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task Panel_StaysOpen_AcrossTheOpenMixerNavigation()
    {
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Body</h1>"))));
        shell.Find("[data-testid=agent-toggle]").Click();
        shell.WaitForElement("[data-testid=agent-panel]");
        var host = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<StemStudio.Web.Agent.AgentUiToolHost>(Services);

        await shell.InvokeAsync(() => host.ExecuteAsync(new UiRequestedEventDto
        {
            StepId = AgentData.StepId,
            Tool = "open_mixer",
            Arguments = AgentData.Json($$"""{"trackId":"{{AgentData.TrackId}}"}"""),
            TimeoutSeconds = 15,
        }));

        NavigatedTo.ShouldEndWith($"/tracks/{AgentData.TrackId}/mixer");
        shell.FindAll("[data-testid=agent-panel]").Count.ShouldBe(1);
        Panel.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void OtherKeys_DoNotClose()
    {
        var cut = RenderPanel();
        Panel.Toggle();

        Id(cut, "agent-panel").KeyDown(new KeyboardEventArgs { Key = "a" });

        Panel.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public async Task OpeningThePanel_MovesFocusToItsHeading_AndClosingReturnsIt()
    {
        var cut = RenderPanel();

        cut.Find(".ss-agent-title").GetAttribute("tabindex").ShouldBe("-1");
        JSInterop.Invocations.Count(i => i.Identifier == FocusCall).ShouldBe(1);
        Interop.FocusCaptures.ShouldBe(1);

        await DisposeComponentsAsync();

        Interop.FocusRestores.ShouldBe(1);
    }

    [Fact]
    public void ApprovalCard_FocusesItsHeading_NeverTheApproveButton()
    {
        var cut = RenderPanel();
        Send(cut);
        var before = JSInterop.Invocations.Count(i => i.Identifier == FocusCall);

        AgentEvents.Started().Push(AgentEventTypes.ApprovalRequested, AgentData.Approval());

        cut.WaitForElement("[data-testid=agent-approval-card]");
        cut.WaitForAssertion(() => JSInterop.Invocations.Count(i => i.Identifier == FocusCall).ShouldBe(before + 1));
        cut.Find("#agent-approval-title").GetAttribute("tabindex").ShouldBe("-1");
        Id(cut, "agent-approve").HasAttribute("autofocus").ShouldBeFalse();
    }

    [Fact]
    public void StepStatus_IsAnnouncedInALiveRegion()
    {
        var cut = RenderPanel(ExperienceLevel.Beginner);
        Send(cut);

        AgentEvents.Started()
            .Push(AgentEventTypes.StepCreated, AgentData.Step(1, "start_detail_separation", AgentStepStatusDto.Running, ToolRiskDto.Heavy))
            .Push(AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.StepId, Status = AgentStepStatusDto.WaitingJob, Progress = 40 });

        cut.WaitForAssertion(() => cut.Find(".ss-agent-step-live").TextContent.ShouldBe("start_detail_separation: waiting job"));
        (cut.Find(".ss-agent-step-live").GetAttribute("aria-live"), cut.Find(".ss-agent-step-live").ClassList.Contains("ss-sr-only")).ShouldBe(("polite", true));
    }

    [Fact]
    public void EveryControl_HasAnAccessibleName()
    {
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([AgentData.Summary()]);
        var cut = RenderPanel();
        Send(cut);
        AgentEvents.Started().Push(AgentEventTypes.ApprovalRequested, AgentData.Approval(allowPlan: true));
        cut.WaitForElement("[data-testid=agent-approval-card]");

        foreach (var control in cut.FindAll("button, input, textarea"))
        {
            var name = control.GetAttribute("aria-label") ?? (cut.FindAll($"label[for='{control.Id}']") is [var label, ..] ? label.TextContent : control.TextContent);
            name.Trim().ShouldNotBeNullOrWhiteSpace($"{control.OuterHtml} has no accessible name");
        }
    }

    [Fact]
    public void ToolLog_ShowsArgumentsAndResults_ForExperts()
    {
        var cut = RenderPanel();
        Send(cut);

        AgentEvents.Started()
            .Push(AgentEventTypes.StepCreated, AgentData.Step())
            .Push(AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.StepId, Status = AgentStepStatusDto.Succeeded, ResultSummary = AgentData.Json("""{"count":1}""") });

        cut.WaitForAssertion(() => Id(cut, "agent-tool-log").TextContent.ShouldContain("\"count\":1"));
        Id(cut, "agent-tool-log").TextContent.ShouldContain("search_library");
        Id(cut, "agent-tool-log").TextContent.ShouldContain("Hero of the Day");
    }

    [Fact]
    public void ToolLog_IsHiddenBelowExpert()
    {
        var cut = RenderPanel(ExperienceLevel.Advanced);
        Send(cut);

        AgentEvents.Started().Push(AgentEventTypes.StepCreated, AgentData.Step());

        cut.WaitForElement("[data-testid=agent-step-1]");
        cut.FindAll("[data-testid=agent-tool-log]").ShouldBeEmpty();
    }
}
