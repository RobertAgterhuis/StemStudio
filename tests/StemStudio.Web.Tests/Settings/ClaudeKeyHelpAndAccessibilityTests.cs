using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Settings;

/// <summary>
/// Help topic <c>agent-claude-key</c> (CK-FR-018) and the accessibility of the key section (CK-SC-006, WCAG 2.2 AA):
/// named controls, live regions, labelled groups and the confirmation's focus and Escape. The axe scan of the
/// rendered page is qa's Playwright suite; these checks keep the structure it scans correct.
/// </summary>
public sealed class ClaudeKeyHelpAndAccessibilityTests : PageTestContext
{
    private const string FocusCall = "Blazor._internal.domWrapper.focus";
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    public ClaudeKeyHelpAndAccessibilityTests()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.AgentStatus(ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Readable)));
    }

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static HelpTopic Topic => Catalog.Topic("agent-claude-key").ShouldNotBeNull();

    private Cut RenderSettings()
    {
        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Beginner, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");
        return cut;
    }

    private static string AccessibleName(Cut cut, IElement control) =>
        control.GetAttribute("aria-label")
        ?? (control.Id is { Length: > 0 } id && cut.FindAll($"label[for='{id}']") is { Count: > 0 } labels ? labels[0].TextContent : null)
        ?? control.Closest("label")?.TextContent
        ?? control.TextContent;

    [Fact]
    public void TheSection_IsTheTopicsAnchor_OnEveryLevel()
    {
        Id(RenderSettings(), "settings-agent-claude-key").GetAttribute("data-help-id").ShouldBe("agent-claude-key");
        Topic.Anchors.ShouldContain("agent-claude-key");
        Topic.Route.ShouldBeNull();
        Topic.Levels.ShouldBeEmpty();
        Topic.Related.ShouldContain("agent-backends");
    }

    [Theory]
    [InlineData("ANTHROPIC_API_KEY")]
    [InlineData("environment variable wins")]
    [InlineData("stored encrypted")]
    [InlineData("last four characters")]
    [InlineData("does not protect")]
    [InlineData("Replace key")]
    [InlineData("Enter the key again")]
    [InlineData("localhost")]
    [InlineData("does not select Claude")]
    public void TheTopic_ExplainsSourcesPrecedenceProtectionAndRecovery(string text) => Topic.Markdown.ShouldContain(text);

    [Fact]
    public void TheTopic_HoldsNoKeyShapedText() => Topic.Markdown.ShouldNotContain(ClaudeKeySentinels.Prefix);

    [Fact]
    public void TheBackendsTopic_NoLongerSaysTheKeyIsNeverStored()
    {
        var backends = Catalog.Topic("agent-backends").ShouldNotBeNull().Markdown;

        backends.ShouldNotContain("never shows or stores the key");
        backends.ShouldContain("agent-claude-key");
    }

    [Fact]
    public void EveryControl_HasAnAccessibleName()
    {
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-replace").Click();
        Id(cut, "settings-agent-claude-key-delete").Click();

        var controls = Id(cut, "settings-agent-claude-key").QuerySelectorAll("input, button");

        controls.Length.ShouldBeGreaterThan(6);
        controls.ShouldAllBe(c => !string.IsNullOrWhiteSpace(AccessibleName(cut, c)));
    }

    [Fact]
    public void GroupsAreLabelled_AndTheStatusIsALiveRegion()
    {
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test-source").GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        Id(cut, "settings-agent-claude-key-status").GetAttribute("aria-live").ShouldBe("polite");
        Id(cut, "settings-agent-claude-key").QuerySelector("h4").ShouldNotBeNull();
    }

    [Fact]
    public void TheConfirmation_TakesTheFocus_OnKeepKey()
    {
        var cut = RenderSettings();
        var before = JSInterop.Invocations.Count(i => i.Identifier == FocusCall);

        Id(cut, "settings-agent-claude-key-delete").Click();

        cut.WaitForAssertion(() => JSInterop.Invocations.Count(i => i.Identifier == FocusCall).ShouldBe(before + 1));
    }

    [Fact]
    public void Escape_KeepsTheKey()
    {
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-delete").Click();

        Id(cut, "settings-agent-claude-key-delete-confirm").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.FindAll("[data-testid=settings-agent-claude-key-delete-confirm]").Count.ShouldBe(1);
        Id(cut, "settings-agent-claude-key-delete-confirm").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[data-testid=settings-agent-claude-key-delete-confirm]").ShouldBeEmpty();
        AgentApi.DidNotReceive().DeleteClaudeKeyAsync(Arg.Any<CancellationToken>());
    }
}
