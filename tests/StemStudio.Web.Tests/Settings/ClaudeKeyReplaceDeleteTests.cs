using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Agent;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Settings;

/// <summary>Replace or delete the stored Claude key (011 follow-up claude-key US3, CK-FR-014, CK-FR-016; ui-test-ids 1.21.0).</summary>
public sealed class ClaudeKeyReplaceDeleteTests : PageTestContext
{
    private const string KeyChanged = "Key changed - applies to the next command";

    public ClaudeKeyReplaceDeleteTests()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        KeyIs(ClaudeKeyData.Stored());
    }

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static bool Has(Cut cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private void KeyIs(ClaudeKeyStatusDto key) => AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.AgentStatus(key));

    private Cut RenderSettings()
    {
        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Advanced, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");
        return cut;
    }

    private Cut Replacing()
    {
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-replace").Click();
        return cut;
    }

    private Cut Confirming()
    {
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-delete").Click();
        return cut;
    }

    [Fact]
    public void StoredKey_OffersReplaceAndDelete_WithoutTheField()
    {
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-replace").TextContent.ShouldContain("Replace key");
        Id(cut, "settings-agent-claude-key-delete").TextContent.ShouldContain("Delete key");
        Has(cut, "settings-agent-claude-key-input").ShouldBeFalse();
        Has(cut, "settings-agent-claude-key-cancel").ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-configured")]
    [InlineData("environment")]
    public void WithoutAStoredKey_ThereIsNothingToReplaceOrDelete(string name)
    {
        KeyIs(name == "environment" ? ClaudeKeyData.Environment() : ClaudeKeyData.NotConfigured());

        var cut = RenderSettings();

        Has(cut, "settings-agent-claude-key-replace").ShouldBeFalse();
        Has(cut, "settings-agent-claude-key-delete").ShouldBeFalse();
    }

    [Theory]
    [InlineData(StoredClaudeKeyStateDto.Readable)]
    [InlineData(StoredClaudeKeyStateDto.Unreadable)]
    public void AnOverriddenOrUnreadableStoredKey_CanBeReplacedAndDeleted(StoredClaudeKeyStateDto stored)
    {
        KeyIs(stored == StoredClaudeKeyStateDto.Unreadable ? ClaudeKeyData.Unreadable() : ClaudeKeyData.Environment(stored));

        var cut = RenderSettings();

        Has(cut, "settings-agent-claude-key-replace").ShouldBeTrue();
        Has(cut, "settings-agent-claude-key-delete").ShouldBeTrue();
    }

    [Fact]
    public void Replace_ShowsAnEmptyField_SaveNewKey_AndCancel()
    {
        var cut = Replacing();

        (Id(cut, "settings-agent-claude-key-input").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        Id(cut, "settings-agent-claude-key-save").TextContent.ShouldContain("Save new key");
        Id(cut, "settings-agent-claude-key-cancel").TextContent.ShouldContain("Cancel");
        Has(cut, "settings-agent-claude-key-replace").ShouldBeFalse();
    }

    [Fact]
    public void Cancel_HidesTheField_AndEmptiesIt()
    {
        var cut = Replacing();
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Replacement);

        Id(cut, "settings-agent-claude-key-cancel").Click();

        Has(cut, "settings-agent-claude-key-input").ShouldBeFalse();
        Id(cut, "settings-agent-claude-key-replace").Click();
        Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeTrue();
        cut.Markup.ShouldNotContain(ClaudeKeySentinels.Replacement);
        AgentApi.DidNotReceive().StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveNewKey_ReplacesIt_AndShowsTheNewLastFour()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.Stored(ClaudeKeyData.ReplacementFour));
        var cut = Replacing();
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Replacement);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").TextContent.ShouldContain($"...{ClaudeKeyData.ReplacementFour}"));
        AgentApi.Received(1).StoreClaudeKeyAsync(ClaudeKeySentinels.Replacement, Arg.Any<CancellationToken>());
        Has(cut, "settings-agent-claude-key-input").ShouldBeFalse();
        Has(cut, "settings-agent-claude-key-replace").ShouldBeTrue();
        cut.Markup.ShouldNotContain(ClaudeKeySentinels.Replacement);
    }

    [Fact]
    public void AFailedReplace_KeepsTheEmptiedFieldOpen()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClaudeKeyStatusDto>(_ => throw ClaudeKeyData.Problem(HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, reason: ClaudeKeyInvalidReasons.Length));
        var cut = Replacing();
        Id(cut, "settings-agent-claude-key-input").Input("short");

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Has(cut, "settings-agent-claude-key-error").ShouldBeTrue());
        (Id(cut, "settings-agent-claude-key-input").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        Has(cut, "settings-agent-claude-key-cancel").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-status").TextContent.ShouldContain($"...{ClaudeKeyData.StoredFour}");
    }

    [Fact]
    public void Delete_AsksForConfirmation_InAnAlertDialog()
    {
        var cut = Confirming();

        var dialog = Id(cut, "settings-agent-claude-key-delete-confirm");
        dialog.GetAttribute("role").ShouldBe("alertdialog");
        cut.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldContain("Delete the stored Claude key?");
        cut.Find($"#{dialog.GetAttribute("aria-describedby")}").TextContent.ShouldContain("does not change the selected backend");
        Has(cut, "settings-agent-claude-key-delete-confirm-yes").ShouldBeTrue();
        Has(cut, "settings-agent-claude-key-delete-confirm-no").ShouldBeTrue();
    }

    [Fact]
    public void No_KeepsTheKey()
    {
        var cut = Confirming();

        Id(cut, "settings-agent-claude-key-delete-confirm-no").Click();

        Has(cut, "settings-agent-claude-key-delete-confirm").ShouldBeFalse();
        Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("stored");
        AgentApi.DidNotReceive().DeleteClaudeKeyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Yes_DeletesTheKey_WithoutSwitchingTheBackend()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings(AgentBackendDto.Claude, true));
        AgentApi.DeleteClaudeKeyAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.NotConfigured());
        var cut = Confirming();

        Id(cut, "settings-agent-claude-key-delete-confirm-yes").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("not-configured"));
        AgentApi.Received(1).DeleteClaudeKeyAsync(Arg.Any<CancellationToken>());
        Has(cut, "settings-agent-claude-key-delete-confirm").ShouldBeFalse();
        Has(cut, "settings-agent-claude-key-delete").ShouldBeFalse();
        Has(cut, "settings-agent-claude-key-input").ShouldBeTrue();
        Id(cut, "settings-agent-claude-status").GetAttribute("data-key-configured").ShouldBe("false");
        Id(cut, "settings-agent-backend-claude").HasAttribute("checked").ShouldBeTrue();
        AgentApi.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AFailedDelete_ShowsTheError_AndKeepsTheStatus()
    {
        AgentApi.DeleteClaudeKeyAsync(Arg.Any<CancellationToken>())
            .Returns<ClaudeKeyStatusDto>(_ => throw ClaudeKeyData.Problem(HttpStatusCode.InternalServerError, "internal"));
        var cut = Confirming();

        Id(cut, "settings-agent-claude-key-delete-confirm-yes").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-error").TextContent.ShouldContain("(500)"));
        Has(cut, "settings-agent-claude-key-delete-confirm").ShouldBeFalse();
        Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("stored");
    }

    [Fact]
    public void LocalOnly_DisablesReplaceAndDelete()
    {
        Services.AddScoped(_ => new PageOrigin(new TestNavigationManager("http://192.168.1.20:8080/")));

        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-replace").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-delete").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task AKeyChange_DuringAnActiveRun_IsNotedInThePanel_UntilTheRunEnds()
    {
        AgentApi.DeleteClaudeKeyAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.NotConfigured());
        await Panel.LoadAsync();
        (await Panel.SendAsync("Make the vocals quieter")).ShouldBeTrue();
        var panel = RenderAtLevel<AgentPanel>(ExperienceLevel.Advanced, _ => { });
        var cut = Confirming();

        Id(cut, "settings-agent-claude-key-delete-confirm-yes").Click();

        panel.WaitForAssertion(() => panel.Markup.ShouldContain(KeyChanged));
        panel.Find(".ss-agent-key-changed").GetAttribute("role").ShouldBe("status");
        Has(panel, "agent-cancel").ShouldBeTrue();
        AgentEvents.Started().Completed();
        AgentEvents.End();
        await Panel.Following;
        panel.WaitForAssertion(() => panel.Markup.ShouldNotContain(KeyChanged));
    }

    [Fact]
    public async Task AKeyChange_WithoutARun_IsNotNoted()
    {
        AgentApi.DeleteClaudeKeyAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.NotConfigured());
        await Panel.LoadAsync();
        var panel = RenderAtLevel<AgentPanel>(ExperienceLevel.Advanced, _ => { });
        var cut = Confirming();

        Id(cut, "settings-agent-claude-key-delete-confirm-yes").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("not-configured"));
        panel.Markup.ShouldNotContain(KeyChanged);
        Panel.KeyChangedDuringRun.ShouldBeFalse();
    }
}
