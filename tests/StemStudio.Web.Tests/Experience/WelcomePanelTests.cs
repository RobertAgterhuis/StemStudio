using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Web.Components.Experience;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Experience;

/// <summary>First-run welcome panel (T039, FR-011).</summary>
public sealed class WelcomePanelTests : PageTestContext
{
    private ITourLauncher Tours => Services.GetRequiredService<ITourLauncher>();

    private async Task<IRenderedComponent<WelcomePanel>> RenderLoadedAsync()
    {
        await Preferences.LoadAsync();
        return Render<WelcomePanel>();
    }

    [Fact]
    public async Task Shown_WhileNotDismissed_WithTheThreeLevelsTourAndNotNow()
    {
        var panel = await RenderLoadedAsync();

        var region = panel.Find("[data-testid=welcome-panel]");
        region.GetAttribute("data-help-id").ShouldBe("welcome");
        foreach (var id in new[] { "welcome-level-beginner", "welcome-level-advanced", "welcome-level-expert", "welcome-tour-start", "welcome-dismiss" })
        {
            panel.Find($"[data-testid={id}]").TagName.ShouldBe("BUTTON");
        }

        panel.Find("[data-testid=welcome-level-beginner]").TextContent.ShouldContain("I am new to this");
        panel.Find("[data-testid=welcome-level-advanced]").TextContent.ShouldContain("I know my way around");
        panel.Find("[data-testid=welcome-level-expert]").TextContent.ShouldContain("Give me everything");
    }

    [Fact]
    public async Task IsNonModal_ARegionWithoutFocusTrap()
    {
        var panel = await RenderLoadedAsync();

        var region = panel.Find("[data-testid=welcome-panel]");
        region.TagName.ShouldBe("SECTION");
        region.HasAttribute("aria-modal").ShouldBeFalse();
        region.GetAttribute("role").ShouldNotBe("dialog");
        Interop.FocusCaptures.ShouldBe(0);
    }

    [Fact]
    public async Task ImportForm_StaysUsable_WhileTheWelcomeIsShown()
    {
        await Preferences.LoadAsync();

        var page = Render<Import>();

        page.Find("[data-testid=welcome-panel]").ShouldNotBeNull();
        page.Find("[data-testid=import-url-input]").HasAttribute("disabled").ShouldBeFalse();
        page.Find("[data-testid=import-url-submit]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData("welcome-level-beginner", ExperienceLevel.Beginner)]
    [InlineData("welcome-level-advanced", ExperienceLevel.Advanced)]
    [InlineData("welcome-level-expert", ExperienceLevel.Expert)]
    public async Task EachLevelButton_SetsTheLevel_AndDismisses(string button, ExperienceLevel level)
    {
        var panel = await RenderLoadedAsync();

        await panel.Find($"[data-testid={button}]").ClickAsync(new MouseEventArgs());

        Preferences.Current.Level.ShouldBe(level);
        Preferences.Current.Help.WelcomeDismissed.ShouldBeTrue();
        panel.FindAll("[data-testid=welcome-panel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task TourStart_DismissesAndStartsImportBasics()
    {
        var panel = await RenderLoadedAsync();

        await panel.Find("[data-testid=welcome-tour-start]").ClickAsync(new MouseEventArgs());

        await Tours.Received(1).StartAsync("import-basics");
        Preferences.Current.Help.WelcomeDismissed.ShouldBeTrue();
        Preferences.Current.Level.ShouldBeNull();
    }

    [Fact]
    public async Task NotNow_Dismisses_WithoutChoosingALevel()
    {
        var panel = await RenderLoadedAsync();

        await panel.Find("[data-testid=welcome-dismiss]").ClickAsync(new MouseEventArgs());

        Preferences.Current.Help.WelcomeDismissed.ShouldBeTrue();
        Preferences.Current.Level.ShouldBeNull();
        panel.FindAll("[data-testid=welcome-panel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task NotShown_AfterAReloadOfADismissedRecord()
    {
        Store.Snapshot = new(UserPreferences.Default with { Help = HelpProgress.Empty with { WelcomeDismissed = true } }, true);

        var panel = await RenderLoadedAsync();

        panel.FindAll("[data-testid=welcome-panel]").ShouldBeEmpty();
    }

    [Fact]
    public void NotShown_BeforePreferencesAreLoaded()
    {
        Render<WelcomePanel>().FindAll("[data-testid=welcome-panel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReappearsAfterResetHelp()
    {
        Store.Snapshot = new(UserPreferences.Default with { Help = HelpProgress.Empty with { WelcomeDismissed = true } }, true);
        var panel = await RenderLoadedAsync();

        await panel.InvokeAsync(() => Preferences.ResetHelpAsync());

        panel.Find("[data-testid=welcome-panel]").ShouldNotBeNull();
    }
}
