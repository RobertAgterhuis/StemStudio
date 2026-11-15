using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Shell;

/// <summary>Command palette (T060, FR-018, ui-test-ids command-*).</summary>
public sealed class CommandPaletteTests : PageTestContext
{
    public CommandPaletteTests()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.Track()]);
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([TestData.Summary()]);
    }

    private IRenderedComponent<MainLayout> RenderShell(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = level }, true);
        return Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));
    }

    private static IElement Id(IRenderedComponent<MainLayout> shell, string testId) => shell.Find($"[data-testid={testId}]");

    private static async Task<IRenderedComponent<MainLayout>> OpenAsync(IRenderedComponent<MainLayout> shell)
    {
        await Id(shell, "command-palette-open").ClickAsync(new MouseEventArgs());
        shell.WaitForAssertion(() => Id(shell, "command-track-0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"));
        return shell;
    }

    [Fact]
    public async Task Button_OpensAModalDialog_WithACombobox()
    {
        var shell = await OpenAsync(RenderShell());

        var dialog = Id(shell, "command-palette");
        dialog.GetAttribute("role").ShouldBe("dialog");
        dialog.GetAttribute("aria-modal").ShouldBe("true");
        Id(shell, "command-palette-input").GetAttribute("role").ShouldBe("combobox");
        Interop.FocusCaptures.ShouldBe(1);
    }

    [Fact]
    public async Task PaletteCommand_OpensIt_AtEveryLevel()
    {
        var shell = RenderShell(ExperienceLevel.Beginner);

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, "palette.open"));

        shell.WaitForAssertion(() => Id(shell, "command-palette").ShouldNotBeNull());
    }

    [Fact]
    public async Task Lists_Screens_Tracks_Mixes_HelpTopics_AndPageActions()
    {
        var shell = RenderShell();
        using var action = Services.GetRequiredService<PageCommands>().Register("open-mixer", "Open mixer", () => Task.CompletedTask);
        await OpenAsync(shell);

        foreach (var id in new[] { "go-import", "go-tracks", "go-mixes", $"track-{TestData.TrackId}", $"mix-{TestData.MixId}", "help-welcome", "open-mixer" })
        {
            Id(shell, $"command-{id}").GetAttribute("role").ShouldBe("option");
        }

        Id(shell, "command-go-import").GetAttribute("aria-selected").ShouldBe("true");
    }

    [Fact]
    public async Task Typing_FiltersTheCommands()
    {
        var shell = await OpenAsync(RenderShell());

        Id(shell, "command-palette-input").Input("mixes");

        Id(shell, "command-go-mixes").GetAttribute("aria-selected").ShouldBe("true");
        shell.FindAll("[data-testid=command-go-import]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Typing_DoesNotEchoTheQueryIntoTheInput()
    {
        // The browser owns the text: a server-rendered value would overwrite characters typed while a render is in
        // flight (T067: fast typing lost characters in Chromium and Firefox).
        var shell = await OpenAsync(RenderShell());

        Id(shell, "command-palette-input").Input("mixes");

        Id(shell, "command-palette-input").HasAttribute("value").ShouldBeFalse();
    }

    [Fact]
    public async Task ArrowKeys_MoveTheSelection_AndEnterRunsIt()
    {
        var shell = await OpenAsync(RenderShell());
        var input = Id(shell, "command-palette-input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        Id(shell, "command-go-tracks").GetAttribute("aria-selected").ShouldBe("true");
        input.GetAttribute("aria-activedescendant").ShouldBe("command-go-tracks");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        NavigatedTo.ShouldBe("http://localhost/tracks");
        shell.FindAll("[data-testid=command-palette]").ShouldBeEmpty();
    }

    [Fact]
    public async Task ArrowUp_AtTheTop_StaysThere_AndOtherKeysAreIgnored()
    {
        var shell = await OpenAsync(RenderShell());
        var input = Id(shell, "command-palette-input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        Id(shell, "command-go-import").GetAttribute("aria-selected").ShouldBe("true");
    }

    [Fact]
    public async Task Enter_WithoutMatches_DoesNothing()
    {
        var shell = await OpenAsync(RenderShell());
        Id(shell, "command-palette-input").Input("zzzz");

        await Id(shell, "command-palette-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Id(shell, "command-palette").TextContent.ShouldContain("No commands match");
    }

    [Fact]
    public async Task Escape_Closes_AndRestoresFocus()
    {
        var shell = await OpenAsync(RenderShell());

        await Id(shell, "command-palette-input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        shell.FindAll("[data-testid=command-palette]").ShouldBeEmpty();
        Interop.FocusRestores.ShouldBe(1);
    }

    [Fact]
    public async Task ClickingTheBackdrop_Closes()
    {
        var shell = await OpenAsync(RenderShell());

        await shell.Find(".ss-palette-backdrop").ClickAsync(new MouseEventArgs());

        shell.FindAll("[data-testid=command-palette]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("track-0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "http://localhost/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a")]
    [InlineData("mix-6a1f0c9e-2b3d-4e5f-8a9b-0c1d2e3f4a5b", "http://localhost/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/mixer?mixId=6a1f0c9e-2b3d-4e5f-8a9b-0c1d2e3f4a5b")]
    [InlineData("go-import", "http://localhost/")]
    public async Task ClickingAnItem_RunsIt(string command, string uri)
    {
        var shell = await OpenAsync(RenderShell());
        Services.GetRequiredService<NavigationManager>().NavigateTo("mixes");

        await Id(shell, $"command-{command}").ClickAsync(new MouseEventArgs());

        NavigatedTo.ShouldBe(uri);
    }

    [Fact]
    public async Task HelpTopicItems_OpenTheHelpPanel()
    {
        var shell = await OpenAsync(RenderShell());

        await Id(shell, "command-help-welcome").ClickAsync(new MouseEventArgs());

        Id(shell, "help-panel").GetAttribute("data-topic-id").ShouldBe("welcome");
    }

    [Fact]
    public async Task PageActions_RunTheirHandler()
    {
        var shell = RenderShell();
        var ran = false;
        using var action = Services.GetRequiredService<PageCommands>().Register("open-mixer", "Open mixer", () =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        await OpenAsync(shell);

        await Id(shell, "command-open-mixer").ClickAsync(new MouseEventArgs());

        ran.ShouldBeTrue();
    }

    [Fact]
    public async Task ApiFailures_StillShowScreensAndHelp()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<StemStudio.Contracts.Tracks.TrackDto>>(_ => throw Problem(System.Net.HttpStatusCode.ServiceUnavailable, "down"));
        var shell = RenderShell();

        await Id(shell, "command-palette-open").ClickAsync(new MouseEventArgs());

        shell.WaitForAssertion(() => Id(shell, "command-go-import").ShouldNotBeNull());
        shell.FindAll("[data-testid^=command-track-]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("go.import", "http://localhost/")]
    [InlineData("go.tracks", "http://localhost/tracks")]
    [InlineData("go.mixes", "http://localhost/mixes")]
    public async Task GoShortcuts_Navigate(string command, string uri)
    {
        var shell = RenderShell();
        Services.GetRequiredService<NavigationManager>().NavigateTo("tracks/x");

        await shell.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Global, command));

        NavigatedTo.ShouldBe(uri);
    }

    [Fact]
    public void PageCommands_UnregisterOnDispose()
    {
        var commands = Services.GetRequiredService<PageCommands>();
        var changes = 0;
        commands.Changed += () => changes++;

        var registration = commands.Register("a", "A", () => Task.CompletedTask);
        registration.Dispose();
        registration.Dispose();

        commands.Items.ShouldBeEmpty();
        changes.ShouldBe(2);
    }
}
