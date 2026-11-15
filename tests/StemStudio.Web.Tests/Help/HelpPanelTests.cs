using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Help;

/// <summary>The help panel in the shell (T048, FR-022..FR-024).</summary>
public sealed class HelpPanelTests : HelpTestContext
{
    private IRenderedComponent<MainLayout> RenderShell(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = level }, true);
        return Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));
    }

    private static IElement Id(IRenderedComponent<MainLayout> shell, string testId) => shell.Find($"[data-testid={testId}]");

    [Fact]
    public async Task HelpToggle_OpensTheScreenTopic_AndFlipsAriaExpanded()
    {
        var shell = RenderShell();

        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        var panel = Id(shell, "help-panel");
        panel.TagName.ShouldBe("ASIDE");
        panel.GetAttribute("aria-label").ShouldBe("Help");
        panel.GetAttribute("data-topic-id").ShouldBe("import-screen");
        Id(shell, "help-topic-title").TextContent.ShouldBe("Import a song");
        Id(shell, "help-toggle").GetAttribute("aria-expanded").ShouldBe("true");
        Id(shell, "app-shell").ClassList.ShouldContain("ss-shell-help-open");
    }

    [Fact]
    public async Task NavHelp_OpensThePanel()
    {
        var shell = RenderShell();

        await Id(shell, "nav-help").ClickAsync(new MouseEventArgs());

        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("import-screen");
    }

    [Fact]
    public async Task HelpOpenCommand_UsesTheFocusedControl()
    {
        var shell = RenderShell();
        Interop.FocusedHelpId = "mixer-pan";

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "help.open"));

        shell.WaitForAssertion(() => Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("mixer-pan"));
    }

    [Fact]
    public async Task UnknownCommands_AreIgnored()
    {
        var shell = RenderShell();

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "something.else"));

        shell.FindAll("[data-testid=help-panel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task HelpClose_ClosesAndRestoresFocus()
    {
        var shell = RenderShell();
        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        await Id(shell, "help-close").ClickAsync(new MouseEventArgs());

        shell.FindAll("[data-testid=help-panel]").ShouldBeEmpty();
        Interop.FocusRestores.ShouldBe(1);
    }

    [Fact]
    public async Task Escape_InThePanel_Closes()
    {
        var shell = RenderShell();
        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        await Id(shell, "help-panel").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Id(shell, "help-panel").ShouldNotBeNull();
        await Id(shell, "help-panel").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        shell.FindAll("[data-testid=help-panel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task TopicView_HasArticleTitleBody_AndRelatedLinks()
    {
        var shell = RenderShell();
        await Help.OpenAsync("mixer-pan");
        shell.Render();

        Id(shell, "help-topic").TagName.ShouldBe("ARTICLE");
        Id(shell, "help-topic-body").InnerHtml.ShouldContain("Pan places a stem");
        await Id(shell, "help-related-mixer-screen").ClickAsync(new MouseEventArgs());

        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("mixer-screen");
        await Id(shell, "help-back").ClickAsync(new MouseEventArgs());
        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("mixer-pan");
    }

    [Fact]
    public async Task HelpLinksInTheBody_AreInterceptedAndOpenInThePanel()
    {
        var shell = RenderShell();
        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        await shell.InvokeAsync(() => Services.GetRequiredService<NavigationManager>().NavigateTo("/help/stem"));

        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("stem");
        Services.GetRequiredService<NavigationManager>().Uri.ShouldNotContain("/help/");
    }

    [Fact]
    public async Task GatedTopic_AboveTheLevel_ShowsALevelNoteAndSwitch()
    {
        var shell = RenderShell(ExperienceLevel.Beginner);
        await Help.OpenAsync("mixer-pan");
        shell.Render();

        Id(shell, "help-level-note").TextContent.ShouldContain("Available at Advanced level");
        await Id(shell, "help-level-switch").ClickAsync(new MouseEventArgs());

        Preferences.Current.Level.ShouldBe(ExperienceLevel.Advanced);
        shell.FindAll("[data-testid=help-level-note]").ShouldBeEmpty();
    }

    [Fact]
    public async Task BeginnerOnlyTopic_AtAdvanced_OffersBeginner()
    {
        var shell = RenderShell();
        await Help.OpenAsync("journey");
        shell.Render();

        Id(shell, "help-level-note").TextContent.ShouldContain("Available at Beginner level");
    }

    [Fact]
    public async Task VisibleFeature_HasNoLevelNote()
    {
        var shell = RenderShell();
        await Help.OpenAsync("mixer-pan");
        shell.Render();

        shell.FindAll("[data-testid=help-level-note]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Index_ListsTopicsPerArea()
    {
        var shell = RenderShell();
        await Help.OpenAsync("nope");
        shell.Render();

        var index = Id(shell, "help-index");
        index.QuerySelectorAll("h3").Select(h => h.TextContent).ShouldBe(["Getting started", "Import", "Tracks", "Mixer", "Glossary"]);
        await index.QuerySelector("[data-help-topic=stem]")!.ClickAsync(new MouseEventArgs());
        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("stem");
    }

    [Fact]
    public async Task Search_ListsResults_AndOpensOne()
    {
        var shell = RenderShell();
        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        Id(shell, "help-search-input").Input("stereo");

        var result = Id(shell, "help-result-mixer-pan");
        result.TextContent.ShouldContain("Pan");
        await result.ClickAsync(new MouseEventArgs());
        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("mixer-pan");
        shell.FindAll("[data-testid^=help-result-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_WithoutHits_SaysSo()
    {
        var shell = RenderShell();
        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        Id(shell, "help-search-input").Input("zzzz");

        Id(shell, "help-panel").TextContent.ShouldContain("No help topics match");
    }

    [Fact]
    public async Task Panel_FollowsNavigation()
    {
        var shell = RenderShell();
        await Id(shell, "help-toggle").ClickAsync(new MouseEventArgs());

        await shell.InvokeAsync(() => GoTo("tracks"));

        shell.WaitForAssertion(() => Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("tracks-library"));
    }
}
