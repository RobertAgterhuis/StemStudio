using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>The settings page's agent section (US4, FR-019, FR-020; ui-test-ids 1.10.0 settings-agent-*).</summary>
public sealed class AgentSettingsTests : PageTestContext
{
    public AgentSettingsTests() => AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private Cut RenderSettings(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var cut = RenderAtLevel<SettingsPage>(level, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-backend]");
        return cut;
    }

    private void ClaudeConfigured(bool acknowledged = false)
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status() with
        {
            Claude = new ClaudeStatusDto { KeyConfigured = true, NoticeAcknowledged = acknowledged, Model = ClaudeModelDto.ClaudeOpus55 },
        });
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings(acknowledged: acknowledged));
    }

    [Fact]
    public void BackendRadioGroup_HasTheThreeBackends_WithTheStoredOneChecked()
    {
        var cut = RenderSettings();

        var group = Id(cut, "settings-agent-backend");
        (group.GetAttribute("role"), group.GetAttribute("data-help-id")).ShouldBe(("radiogroup", "agent-backends"));
        Id(cut, "settings-agent-backend-ollama").HasAttribute("checked").ShouldBeTrue();
        Id(cut, "settings-agent-backend-none").HasAttribute("checked").ShouldBeFalse();
        Id(cut, "settings-agent-backend-claude").HasAttribute("checked").ShouldBeFalse();
    }

    [Fact]
    public void OllamaStatus_ShowsReachabilityVersionAndModels()
    {
        var cut = RenderSettings();

        var status = Id(cut, "settings-agent-ollama-status").TextContent;
        status.ShouldContain("Reachable");
        status.ShouldContain("0.40.16");
        status.ShouldContain("qwen3.5:9b");
    }

    [Fact]
    public void OllamaNotReachable_SaysSo()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status(ready: false) with { Ollama = AgentData.Ollama(reachable: false) });

        var cut = RenderSettings();

        Id(cut, "settings-agent-ollama-status").TextContent.ShouldContain("Not reachable");
    }

    [Fact]
    public void ModelPickers_OfferOnlyToolCapableModels_FromAdvanced()
    {
        var cut = RenderSettings();

        Id(cut, "settings-agent-ollama-model").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["qwen3.5:9b", "qwen3.5:4b"]);
        Id(cut, "settings-agent-ollama-fallback-model").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["qwen3.5:9b", "qwen3.5:4b"]);
        cut.FindAll("[data-testid=settings-agent-claude-model]").ShouldBeEmpty();
    }

    [Fact]
    public void StoredModelThatIsNotPulled_IsStillListed()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings() with { OllamaModel = "qwen3.5:27b" });

        var cut = RenderSettings();

        Id(cut, "settings-agent-ollama-model").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldContain("qwen3.5:27b");
    }

    [Fact]
    public void Beginner_HasNoModelPickers()
    {
        var cut = RenderSettings(ExperienceLevel.Beginner);

        cut.FindAll("[data-testid=settings-agent-ollama-model]").ShouldBeEmpty();
        cut.FindAll("[data-testid=settings-agent-claude-model]").ShouldBeEmpty();
    }

    [Fact]
    public void Expert_SeesTheClaudePickers()
    {
        ClaudeConfigured();

        var cut = RenderSettings(ExperienceLevel.Expert);

        Id(cut, "settings-agent-claude-model").QuerySelectorAll("option").Select(o => o.GetAttribute("value"))
            .ShouldBe(["claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5"]);
        Id(cut, "settings-agent-claude-effort").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["low", "medium", "high"]);
    }

    [Fact]
    public void ClaudeWithoutKey_IsDisabled_AndSaysNotConfigured()
    {
        var cut = RenderSettings();

        Id(cut, "settings-agent-backend-claude").HasAttribute("disabled").ShouldBeTrue();
        var claude = Id(cut, "settings-agent-claude-status");
        claude.GetAttribute("data-key-configured").ShouldBe("false");
        claude.TextContent.ShouldContain("Not configured");
        claude.TextContent.ShouldContain("ANTHROPIC_API_KEY");
    }

    [Fact]
    public void ClaudeWithKey_CanBeChosen_AndShowsThePaidNotice()
    {
        ClaudeConfigured();
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-status").GetAttribute("data-key-configured").ShouldBe("true");
        Id(cut, "settings-agent-backend-claude").HasAttribute("disabled").ShouldBeFalse();
        Id(cut, "settings-agent-backend-claude").Change("claude");

        var notice = Id(cut, "settings-agent-claude-notice").TextContent;
        notice.ShouldContain("paid per use");
        notice.ShouldContain("text only");
        Id(cut, "settings-agent-claude-notice-ack").HasAttribute("checked").ShouldBeFalse();
    }

    [Fact]
    public async Task SavingClaude_NeedsTheAcknowledgement()
    {
        ClaudeConfigured();
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(AgentData.Settings(AgentBackendDto.Claude, true));
        var cut = RenderSettings();
        Id(cut, "settings-agent-backend-claude").Change("claude");

        Id(cut, "settings-agent-save").Click();

        Id(cut, "settings-agent-error").TextContent.ShouldContain("Acknowledge the notice");
        await AgentApi.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>());

        Id(cut, "settings-agent-claude-notice-ack").Change(true);
        Id(cut, "settings-agent-save").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).UpdateSettingsAsync(
            Arg.Is<UpdateAgentSettingsRequest>(r => r.Backend == AgentBackendDto.Claude && r.AcknowledgeClaudeNotice), Arg.Any<CancellationToken>()));
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=settings-agent-error]").ShouldBeEmpty());
        cut.Markup.ShouldContain("Saved");
    }

    [Fact]
    public void AcknowledgedBefore_NeedsNoNewAcknowledgement()
    {
        ClaudeConfigured(acknowledged: true);
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(AgentData.Settings(AgentBackendDto.Claude, true));
        var cut = RenderSettings();
        Id(cut, "settings-agent-backend-claude").Change("claude");

        Id(cut, "settings-agent-claude-notice-ack").HasAttribute("checked").ShouldBeTrue();
        Id(cut, "settings-agent-save").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).UpdateSettingsAsync(Arg.Is<UpdateAgentSettingsRequest>(r => r.Backend == AgentBackendDto.Claude), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void Save_SendsTheChosenModels_AndReloadsThePanelStatus()
    {
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        var cut = RenderSettings();

        Id(cut, "settings-agent-ollama-model").Change("qwen3.5:4b");
        Id(cut, "settings-agent-ollama-fallback-model").Change("qwen3.5:4b");
        Id(cut, "settings-agent-backend-none").Change("none");
        Id(cut, "settings-agent-save").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).UpdateSettingsAsync(
            Arg.Is<UpdateAgentSettingsRequest>(r => r.Backend == AgentBackendDto.None && r.OllamaModel == "qwen3.5:4b" && r.OllamaFallbackModel == "qwen3.5:4b" && !r.AcknowledgeClaudeNotice),
            Arg.Any<CancellationToken>()));
        cut.WaitForAssertion(() => AgentApi.Received(2).GetStatusAsync(Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void ExpertClaudeOptions_AreSaved()
    {
        ClaudeConfigured(acknowledged: true);
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        var cut = RenderSettings(ExperienceLevel.Expert);

        Id(cut, "settings-agent-claude-model").Change("claude-haiku-5-5");
        Id(cut, "settings-agent-claude-effort").Change("high");
        Id(cut, "settings-agent-save").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).UpdateSettingsAsync(
            Arg.Is<UpdateAgentSettingsRequest>(r => r.ClaudeModel == ClaudeModelDto.ClaudeHaiku55 && r.ClaudeEffort == ClaudeEffortDto.High), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void SaveError_IsShown()
    {
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>())
            .Returns<AgentSettingsDto>(_ => throw Problem(System.Net.HttpStatusCode.UnprocessableEntity, "Model unavailable.", "qwen3.5:27b is not pulled or cannot call tools."));
        var cut = RenderSettings();

        Id(cut, "settings-agent-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-error").TextContent.ShouldContain("qwen3.5:27b is not pulled"));
        Id(cut, "settings-agent-error").GetAttribute("role").ShouldBe("alert");
    }

    [Theory]
    [InlineData(true, "Connected")]
    [InlineData(false, "Failed")]
    public void TestConnection_ShowsTheResult(bool ok, string text)
    {
        AgentApi.TestBackendAsync(AgentBackendDto.Ollama, Arg.Any<CancellationToken>())
            .Returns(new BackendTestResultDto { Backend = AgentBackendDto.Ollama, Ok = ok, LatencyMs = 42, Detail = "qwen3.5:9b" });
        var cut = RenderSettings();

        Id(cut, "settings-agent-test").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-test-result").TextContent.ShouldContain(text));
        Id(cut, "settings-agent-test-result").TextContent.ShouldContain("42 ms");
        Id(cut, "settings-agent-test-result").GetAttribute("data-ok").ShouldBe(ok ? "true" : "false");
    }

    [Fact]
    public void TestConnection_IsDisabledForNone_AndShowsAnApiError()
    {
        AgentApi.TestBackendAsync(Arg.Any<AgentBackendDto>(), Arg.Any<CancellationToken>())
            .Returns<BackendTestResultDto>(_ => throw Problem(System.Net.HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached."));
        var cut = RenderSettings();

        Id(cut, "settings-agent-test").Click();
        cut.WaitForAssertion(() => Id(cut, "settings-agent-error").TextContent.ShouldContain("cannot be reached"));

        Id(cut, "settings-agent-backend-none").Change("none");
        Id(cut, "settings-agent-test").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void LoadError_IsShown()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns<AgentSettingsDto>(_ => throw Problem(System.Net.HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached."));

        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Advanced, _ => { });

        cut.WaitForAssertion(() => Id(cut, "settings-agent-error").TextContent.ShouldContain("cannot be reached"));
    }

    [Fact]
    public void Rail_HasTheSettingsLink()
    {
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, b => b.AddMarkupContent(0, "<h1>Body</h1>")));

        shell.Find("[data-testid=nav-settings]").GetAttribute("href").ShouldBe("settings");
    }
}
