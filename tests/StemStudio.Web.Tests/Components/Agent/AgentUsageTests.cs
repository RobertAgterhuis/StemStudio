using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>Claude usage and "Switch to local" on Claude failures (T066, amendment; ui-test-ids agent-usage, agent-switch-to-local).</summary>
public sealed class AgentUsageTests : PageTestContext
{
    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private Cut Run(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var cut = RenderAtLevel<AgentPanel>(level, _ => { });
        cut.WaitForAssertion(() => Id(cut, "agent-input").HasAttribute("disabled").ShouldBeFalse());
        Id(cut, "agent-input").Input("Zoek iets");
        Id(cut, "agent-send").Click();
        cut.WaitForElement("[data-testid=agent-message-1]");
        return cut;
    }

    private static TokenUsageDto Usage => new() { InputTokens = 12_345, OutputTokens = 678, CachedInputTokens = 9_000 };

    [Fact]
    public void ClaudeRun_ShowsTheTokenUsage_FromAdvanced()
    {
        var cut = Run();

        AgentEvents.Started(AgentBackendDto.Claude, "claude-opus-5-5").Push(AgentEventTypes.UsageUpdated, Usage);

        cut.WaitForAssertion(() => Id(cut, "agent-usage").TextContent.ShouldContain("12,345 input"));
        Id(cut, "agent-usage").TextContent.ShouldContain("9,000 cached");
        Id(cut, "agent-usage").TextContent.ShouldContain("678 output");
    }

    [Fact]
    public void Beginner_SeesNoUsage()
    {
        var cut = Run(ExperienceLevel.Beginner);

        AgentEvents.Started(AgentBackendDto.Claude, "claude-opus-5-5").Push(AgentEventTypes.UsageUpdated, Usage).Text("x");

        cut.WaitForElement("[data-testid=agent-streaming]");
        cut.FindAll("[data-testid=agent-usage]").ShouldBeEmpty();
    }

    [Fact]
    public void OllamaRun_ShowsNoUsage()
    {
        var cut = Run();

        AgentEvents.Started().Push(AgentEventTypes.UsageUpdated, Usage).Text("x");

        cut.WaitForElement("[data-testid=agent-streaming]");
        cut.FindAll("[data-testid=agent-usage]").ShouldBeEmpty();
    }

    [Fact]
    public void ClaudeFailure_OffersSwitchToLocal_WhichSelectsOllama()
    {
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        var cut = Run();
        AgentEvents.Started(AgentBackendDto.Claude, "claude-opus-5-5").Failed("Claude rejected the key.", "Check ANTHROPIC_API_KEY", AgentFailureCodes.BackendUnavailable);
        cut.WaitForElement("[data-testid=agent-run-error]");

        cut.Find("[data-testid=agent-run-error] [data-testid=agent-switch-to-local]").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).UpdateSettingsAsync(
            Arg.Is<UpdateAgentSettingsRequest>(r => r.Backend == AgentBackendDto.Ollama && !r.AcknowledgeClaudeNotice), Arg.Any<CancellationToken>()));
        // Once on open, once when the run ended, once after the switch.
        cut.WaitForAssertion(() => AgentApi.Received(3).GetStatusAsync(Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void SwitchToLocal_Failure_IsShown()
    {
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>())
            .Returns<AgentSettingsDto>(_ => throw Problem(System.Net.HttpStatusCode.UnprocessableEntity, "Model unavailable.", "qwen3.5:9b is not pulled."));
        var cut = Run();
        AgentEvents.Started(AgentBackendDto.Claude, "claude-opus-5-5").Failed("Rate limited.");
        cut.WaitForElement("[data-testid=agent-switch-to-local]");

        Id(cut, "agent-switch-to-local").Click();

        cut.WaitForAssertion(() => Id(cut, "agent-error").TextContent.ShouldContain("qwen3.5:9b is not pulled."));
    }

    [Fact]
    public void OllamaFailure_HasNoSwitchToLocal()
    {
        var cut = Run();

        AgentEvents.Started().Failed("Ollama stopped.");

        cut.WaitForElement("[data-testid=agent-run-error]");
        cut.FindAll("[data-testid=agent-switch-to-local]").ShouldBeEmpty();
    }
}
