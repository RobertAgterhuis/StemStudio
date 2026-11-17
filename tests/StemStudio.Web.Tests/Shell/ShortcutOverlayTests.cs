using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Shell;

/// <summary>Shortcut overlay (T060, FR-017, design-spec.md 6).</summary>
public sealed class ShortcutOverlayTests : PageTestContext
{
    private IRenderedComponent<MainLayout> RenderShell(ExperienceLevel level)
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = level }, true);
        return Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));
    }

    private static IElement Id(IRenderedComponent<MainLayout> shell, string testId) => shell.Find($"[data-testid={testId}]");

    private async Task<IElement> OpenAsync(IRenderedComponent<MainLayout> shell)
    {
        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "shortcuts.open"));
        return Id(shell, "shortcut-overlay");
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public async Task ModSlash_OpensTheOverlay_AtEveryLevel_WithTheGlobalKeys(ExperienceLevel level)
    {
        var shell = RenderShell(level);

        var overlay = await OpenAsync(shell);

        overlay.GetAttribute("role").ShouldBe("dialog");
        Id(shell, "shortcut-row-palette-open").GetAttribute("data-keys").ShouldBe("mod+k");
        Id(shell, "shortcut-row-go-import").GetAttribute("data-keys").ShouldBe("g i");
        Id(shell, "shortcut-row-go-import").TextContent.ShouldContain("G then I");
        overlay.QuerySelector("[data-testid=shortcut-group-transport]").ShouldBeNull();
    }

    [Fact]
    public async Task OnTheMixer_ListsTheMixerGroupsAndRows_FromTheKeyMap()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer");
        var shell = RenderShell(ExperienceLevel.Expert);

        var overlay = await OpenAsync(shell);

        overlay.QuerySelectorAll("table").Select(t => t.GetAttribute("data-testid"))
            .ShouldBe(["shortcut-group-transport", "shortcut-group-loop-sections", "shortcut-group-speed", "shortcut-group-channels",
                "shortcut-group-setlist", "shortcut-group-score", "shortcut-group-navigation"]);
        Id(shell, "shortcut-row-mixer-play-pause").GetAttribute("data-keys").ShouldBe("space|pagedown");
        Id(shell, "shortcut-row-mixer-play-pause").TextContent.ShouldContain("Space or PageDown (pedal)");
        Id(shell, "shortcut-row-mixer-seek--5").GetAttribute("data-keys").ShouldBe("arrowleft");
        Id(shell, "shortcut-row-mixer-practice-reset").GetAttribute("data-keys").ShouldBe("0");
        Id(shell, "shortcut-row-mixer-countin-toggle").GetAttribute("data-keys").ShouldBe("shift+k");
        Id(shell, "shortcut-row-setlist-next").GetAttribute("data-keys").ShouldBe("mod+arrowright");
        Id(shell, "shortcut-group-speed").TextContent.ShouldContain("Speed +5 %");
    }

    [Fact]
    public async Task RemappedAndPedalKeys_AreShown_AndReservedRowsHidden()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer");
        var overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>>
        {
            ["mixer.section.next"] = [new ShortcutChord("j"), new ShortcutChord("b", Layer: KeyLayer.Pedal)],
            ["mixer.stop"] = [],
        };
        Store.Snapshot = new(UserPreferences.Default with { Level = ExperienceLevel.Advanced, Shortcuts = ShortcutPreferences.Default with { Overrides = overrides, SingleKey = false } }, true);
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));

        var overlay = await OpenAsync(shell);

        Id(shell, "shortcut-row-mixer-section-next").GetAttribute("data-keys").ShouldBe("j|b");
        Id(shell, "shortcut-row-mixer-stop").GetAttribute("data-keys").ShouldBe(string.Empty);
        Id(shell, "shortcut-row-mixer-stop").TextContent.ShouldContain("-");
        overlay.TextContent.ShouldContain("Single-key shortcuts are off");
        shell.FindAll("[data-testid=shortcut-row-mixer-record-toggle]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task OnTheLibrary_ListsTheLibrarySearchKey()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("library");
        var shell = RenderShell(ExperienceLevel.Beginner);

        await OpenAsync(shell);

        Id(shell, "shortcut-row-library-search-focus").GetAttribute("data-keys").ShouldBe("/");
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    public async Task HelpShortcuts_InTheHelpPanel_OpensItAtEveryLevel(ExperienceLevel level)
    {
        var shell = RenderShell(level);
        await Services.GetRequiredService<HelpContext>().OpenAsync("nope");
        shell.Render();

        await Id(shell, "help-shortcuts").ClickAsync(new MouseEventArgs());

        Id(shell, "shortcut-overlay").ShouldNotBeNull();
    }

    [Fact]
    public async Task HelpShortcuts_UnderAShownTopic_OpensIt()
    {
        // design-spec.md 4.7: the shortcut link stays below the topic, so it is reachable from every screen topic (T067).
        var shell = RenderShell(ExperienceLevel.Advanced);
        await Services.GetRequiredService<HelpContext>().OpenAsync("welcome");
        shell.Render();
        Id(shell, "help-topic").ShouldNotBeNull();

        await Id(shell, "help-shortcuts").ClickAsync(new MouseEventArgs());

        Id(shell, "shortcut-overlay").ShouldNotBeNull();
    }

    [Fact]
    public async Task Close_AndEscape_CloseIt()
    {
        var shell = RenderShell(ExperienceLevel.Expert);
        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "shortcuts.open"));

        await Id(shell, "shortcut-overlay-close").ClickAsync(new MouseEventArgs());
        shell.FindAll("[data-testid=shortcut-overlay]").ShouldBeEmpty();

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "shortcuts.open"));
        await Id(shell, "shortcut-overlay").KeyDownAsync(new KeyboardEventArgs { Key = "x" });
        Id(shell, "shortcut-overlay").ShouldNotBeNull();
        await Id(shell, "shortcut-overlay").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        shell.FindAll("[data-testid=shortcut-overlay]").ShouldBeEmpty();
    }

    [Fact]
    public void Expert_HasAShortcutsButton_AndKbdHints()
    {
        var shell = RenderShell(ExperienceLevel.Expert);

        shell.Find("[data-help-id=shortcut-overlay]").TagName.ShouldBe("BUTTON");
        Id(shell, "command-palette-open").QuerySelector("kbd").ShouldNotBeNull();
    }

    [Fact]
    public void Advanced_HasNoShortcutsButton_NorKbdHints()
    {
        var shell = RenderShell(ExperienceLevel.Advanced);

        shell.FindAll("[data-help-id=shortcut-overlay]").ShouldBeEmpty();
        Id(shell, "command-palette-open").QuerySelector("kbd").ShouldBeNull();
    }
}
