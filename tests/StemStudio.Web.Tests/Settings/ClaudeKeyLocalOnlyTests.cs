using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Agent;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Settings;

/// <summary>
/// Key entry only on the computer itself (011 follow-up claude-key US6, CK-FR-012; ui-test-ids 1.21.0
/// settings-agent-claude-key-local-only) [sec].
/// </summary>
public sealed class ClaudeKeyLocalOnlyTests : PageTestContext
{
    private const string Notice = "The Claude key can only be entered on the computer StemStudio runs on";

    public ClaudeKeyLocalOnlyTests() => AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static bool Has(Cut cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private void OpenedUnder(string baseUri) => Services.AddScoped(_ => new PageOrigin(new TestNavigationManager(baseUri)));

    private Cut RenderSettings()
    {
        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Advanced, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");
        return cut;
    }

    [Fact]
    public void OnTheComputer_ThereIsNoNotice_AndTheFieldIsEnabled()
    {
        var cut = RenderSettings();

        Has(cut, "settings-agent-claude-key-local-only").ShouldBeFalse();
        Id(cut, "settings-agent-claude-key-input").HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData("http://192.168.1.20:8080/")]
    [InlineData("http://web:8080/")]
    public void FromAnotherHostName_ShowsTheNotice_AndDisablesTheControls(string baseUri)
    {
        OpenedUnder(baseUri);

        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-local-only").TextContent.ShouldContain(Notice);
        Id(cut, "settings-agent-claude-key-input").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("not-configured");
    }

    [Fact]
    public void ALocalOnlyAnswer_ShowsTheSameNotice_InsteadOfAnError()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClaudeKeyStatusDto>(_ => throw ClaudeKeyData.Problem(HttpStatusCode.Forbidden, AgentProblemTypes.LocalOnly, detail: ClaudeKeySentinels.Stored));
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-local-only").TextContent.ShouldContain(Notice));
        Has(cut, "settings-agent-claude-key-error").ShouldBeFalse();
        Id(cut, "settings-agent-claude-key-input").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeTrue();
        cut.Markup.ShouldNotContain(ClaudeKeySentinels.Stored);
    }

    [Fact]
    public void AnotherForbidden_IsAnError_NotTheNotice()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClaudeKeyStatusDto>(_ => throw ClaudeKeyData.Problem(HttpStatusCode.Forbidden, "forbidden"));
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-error").TextContent.ShouldContain("(403)"));
        Has(cut, "settings-agent-claude-key-local-only").ShouldBeFalse();
    }
}
