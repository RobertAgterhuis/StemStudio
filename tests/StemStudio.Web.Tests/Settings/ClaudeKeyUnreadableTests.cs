using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Settings;

/// <summary>Recovery from an unreadable stored key (011 follow-up claude-key US5, CK-FR-011; ui-test-ids 1.21.0).</summary>
public sealed class ClaudeKeyUnreadableTests : PageTestContext
{
    public ClaudeKeyUnreadableTests()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        KeyIs(ClaudeKeyData.Unreadable());
    }

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static bool Has(Cut cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private void KeyIs(ClaudeKeyStatusDto key, AgentStatusDto? status = null) =>
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(status ?? ClaudeKeyData.AgentStatus(key));

    private Cut RenderSettings()
    {
        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Beginner, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");
        return cut;
    }

    private static AgentStatusDto ClaudeNotReady(ClaudeKeyStatusDto key, string reason) => ClaudeKeyData.AgentStatus(key, AgentBackendDto.Claude) with
    {
        Ready = false,
        ActiveModel = null,
        Reason = reason,
        Fix = "Enter the key again in Settings, or switch to Local.",
    };

    [Fact]
    public void Unreadable_ExplainsWhy_AndHowToFixIt()
    {
        var cut = RenderSettings();

        var text = Id(cut, "settings-agent-claude-key-unreadable").TextContent;
        text.ShouldContain("cannot be read on this installation");
        text.ShouldContain("Enter the key again");
        Id(cut, "settings-agent-claude-key-status").TextContent.ShouldContain($"...{ClaudeKeyData.StoredFour}");
    }

    [Fact]
    public void Unreadable_WithoutAReasonFromTheApi_StillExplainsIt()
    {
        KeyIs(ClaudeKeyData.Unreadable() with { Reason = null, Fix = null });

        var text = Id(RenderSettings(), "settings-agent-claude-key-unreadable").TextContent;

        text.ShouldContain("cannot be read on this installation");
        text.ShouldContain("Enter the key again");
    }

    [Fact]
    public void UnreadableBehindTheEnvironmentKey_IsExplainedToo()
    {
        KeyIs(ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Unreadable));

        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-unreadable").TextContent.ShouldContain("Enter the key again");
        Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("environment");
    }

    [Theory]
    [InlineData(StoredClaudeKeyStateDto.Absent)]
    [InlineData(StoredClaudeKeyStateDto.Readable)]
    public void AReadableOrAbsentStoredKey_NeedsNoRecovery(StoredClaudeKeyStateDto stored)
    {
        KeyIs(stored == StoredClaudeKeyStateDto.Absent ? ClaudeKeyData.NotConfigured() : ClaudeKeyData.Stored());

        Has(RenderSettings(), "settings-agent-claude-key-unreadable").ShouldBeFalse();
    }

    [Fact]
    public void EnteringTheKeyAgain_ReplacesTheUnreadableKey()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.Stored(ClaudeKeyData.ReplacementFour));
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-replace").Click();
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Replacement);
        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("stored"));
        Has(cut, "settings-agent-claude-key-unreadable").ShouldBeFalse();
        ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Replacement).ShouldAllBe(form => !cut.Markup.Contains(form, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("unreadable")]
    [InlineData("no-key")]
    public void ClaudeWithoutAUsableKey_OffersSwitchToLocal_InThePanel(string why)
    {
        var key = why == "unreadable" ? ClaudeKeyData.Unreadable() : ClaudeKeyData.NotConfigured();
        KeyIs(key, ClaudeNotReady(key, why == "unreadable" ? ClaudeKeyData.UnreadableReason : "Claude has no key."));
        AgentApi.UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        var panel = RenderAtLevel<AgentPanel>(ExperienceLevel.Beginner, _ => { });
        panel.WaitForElement("[data-testid=agent-unavailable]");

        Id(panel, "agent-unavailable-switch-to-local").Click();

        panel.WaitForAssertion(() => AgentApi.Received(1).UpdateSettingsAsync(
            Arg.Is<UpdateAgentSettingsRequest>(r => r.Backend == AgentBackendDto.Ollama), Arg.Any<CancellationToken>()));
        Id(panel, "agent-unavailable").TextContent.ShouldContain(why == "unreadable" ? "cannot be read on this installation" : "Claude has no key");
    }

    [Fact]
    public void OllamaNotReady_HasNoSwitchToLocal()
    {
        KeyIs(ClaudeKeyData.NotConfigured(), AgentData.Status(ready: false));

        var panel = RenderAtLevel<AgentPanel>(ExperienceLevel.Beginner, _ => { });
        panel.WaitForElement("[data-testid=agent-unavailable]");

        Has(panel, "agent-unavailable-switch-to-local").ShouldBeFalse();
    }
}
