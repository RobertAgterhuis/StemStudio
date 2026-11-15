using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
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

    [Fact]
    public async Task ModSlash_AtExpert_OpensTheOverlay_WithTheGlobalKeys()
    {
        var shell = RenderShell(ExperienceLevel.Expert);

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "shortcuts.open"));

        var overlay = Id(shell, "shortcut-overlay");
        overlay.GetAttribute("role").ShouldBe("dialog");
        overlay.TextContent.ShouldContain("Command palette");
        overlay.TextContent.ShouldNotContain("Loop on or off");
    }

    [Fact]
    public async Task ModSlash_BelowExpert_DoesNothing()
    {
        var shell = RenderShell(ExperienceLevel.Advanced);

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "shortcuts.open"));

        shell.FindAll("[data-testid=shortcut-overlay]").ShouldBeEmpty();
    }

    [Fact]
    public async Task OnTheMixer_ListsTheMixerKeys()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer");
        var shell = RenderShell(ExperienceLevel.Expert);

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "shortcuts.open"));

        Id(shell, "shortcut-overlay").TextContent.ShouldContain("Loop on or off");
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
