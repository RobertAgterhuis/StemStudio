using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>Backend unavailable state and the badge (US3, FR-021, FR-023; ui-test-ids agent-unavailable, agent-backend-badge).</summary>
public sealed class AgentUnavailableTests : PageTestContext
{
    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private Cut RenderPanel() => RenderAtLevel<AgentPanel>(ExperienceLevel.Beginner, _ => { });

    [Fact]
    public void NotReady_ShowsTheReasonAndTheFix_AndDisablesTheInput()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false));

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-unavailable").TextContent.ShouldContain("Ollama not reachable at http://host.docker.internal:11434"));
        Id(cut, "agent-unavailable").TextContent.ShouldContain("Start Ollama");
        Id(cut, "agent-unavailable").GetAttribute("role").ShouldBe("status");
        Id(cut, "agent-input").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "agent-send").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Ready_HasNoUnavailableState()
    {
        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());
        cut.FindAll("[data-testid=agent-unavailable]").ShouldBeEmpty();
    }

    [Fact]
    public void BackendNone_SaysTheAgentIsOff()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false, AgentBackendDto.None) with
        {
            Reason = "The agent is turned off.",
            Fix = "Choose Ollama or Claude in Settings.",
        });

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-unavailable").TextContent.ShouldContain("Choose Ollama or Claude in Settings."));
        (Id(cut, "agent-backend-badge").GetAttribute("data-backend"), Id(cut, "agent-backend-badge").GetAttribute("data-model")).ShouldBe(("none", string.Empty));
    }

    [Fact]
    public void NotReady_WithoutReason_StillSaysSo()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false) with { Reason = null, Fix = null });

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-unavailable").TextContent.ShouldContain("The agent is not ready."));
    }

    [Fact]
    public void Fallback_FromTheStatus_IsShownOnTheBadge()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status() with { UsingFallback = true, ActiveModel = "qwen3.5:4b" });

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Id(cut, "agent-backend-badge").GetAttribute("data-fallback").ShouldBe("true"));
        Id(cut, "agent-backend-badge").GetAttribute("data-model").ShouldBe("qwen3.5:4b");
    }

    [Fact]
    public void Status_IsRefreshedEvery10Seconds_WhileThePanelIsOpen()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false), AgentData.Status());
        var cut = RenderPanel();
        cut.WaitForElement("[data-testid=agent-unavailable]");

        Time.Advance(TimeSpan.FromSeconds(9));
        AgentApi.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IAgentApiClient.GetStatusAsync)).ShouldBe(1);
        Time.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => cut.FindAll("[data-testid=agent-unavailable]").ShouldBeEmpty());
        Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task ClosedPanel_StopsRefreshing()
    {
        var cut = RenderPanel();
        cut.WaitForElement("[data-testid=agent-input]");

        await DisposeComponentsAsync();
        Time.Advance(TimeSpan.FromSeconds(30));

        AgentApi.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IAgentApiClient.GetStatusAsync)).ShouldBe(1);
    }

    [Fact]
    public void BackendUnavailableOnSend_ShowsTheStatusOfTheProblem()
    {
        var status = AgentData.Status(ready: false) with { Reason = "Model not pulled", Fix = "ollama pull qwen3.5:9b (6.6 GB)" };
        var error = new ApiError(System.Net.HttpStatusCode.ServiceUnavailable, "The agent backend is not ready.", "Model not pulled", new Dictionary<string, string[]>())
        {
            Type = AgentProblemTypes.Uri(AgentProblemTypes.BackendUnavailable),
            Extensions = new Dictionary<string, System.Text.Json.JsonElement> { ["agentStatus"] = System.Text.Json.JsonSerializer.SerializeToElement(status, System.Text.Json.JsonSerializerOptions.Web) },
        };
        AgentApi.SendMessageAsync(Arg.Any<Guid>(), Arg.Any<SendAgentMessageRequest>(), Arg.Any<CancellationToken>()).Returns<AgentMessageAcceptedDto>(_ => throw new ApiException(error));
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());

        Id(cut, "agent-input").Input("Zoek iets");
        Id(cut, "agent-send").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-unavailable").TextContent.ShouldContain("ollama pull qwen3.5:9b (6.6 GB)"));
        Id(cut, "agent-input").HasAttribute("disabled").ShouldBeTrue();
    }
}
