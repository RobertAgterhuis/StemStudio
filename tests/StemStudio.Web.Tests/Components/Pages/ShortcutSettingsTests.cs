using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Components.Pages;

/// <summary>T064: shortcut settings page, pedal setup and the Preferences switch (FR-019, FR-021..FR-023; ui-test-ids 1.13.0).</summary>
public sealed class ShortcutSettingsTests : PageTestContext
{
    private ShortcutPreferences Saved => Services.GetRequiredService<PreferencesState>().Current.Shortcuts;

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> RenderAt(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        await Services.GetRequiredService<PreferencesState>().LoadAsync();
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<ShortcutSettings>());
    }

    private static Task ClickAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string testId) =>
        page.InvokeAsync(() => page.Find($"[data-testid={testId}]").Click());

    private async Task LearnAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string testId, KeyChordData? chord)
    {
        await ClickAsync(page, testId);
        page.Find("[data-testid=shortcut-capture]").ShouldNotBeNull();
        await page.InvokeAsync(() => Interop.CaptureAsync(chord));
    }

    [Fact]
    public async Task Edit_CapturesAChord_SavesIt_AndTheKeyMapIsUpdated()
    {
        var page = await RenderAt();

        await LearnAsync(page, "shortcut-edit-mixer-section-next", new KeyChordData("j", false, false));

        Saved.Overrides["mixer.section.next"].ShouldBe([new ShortcutChord("j", false, false)]);
        page.FindAll("[data-testid=shortcut-capture]").ShouldBeEmpty();
        Interop.CaptureTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        page.Find("[data-testid=shortcut-edit-mixer-section-next]").Closest("tr")!.GetAttribute("data-keys").ShouldBe("j");
    }

    [Fact]
    public async Task AConflict_NamesTheOtherCommand_AndSavesNothing()
    {
        var page = await RenderAt();

        await LearnAsync(page, "shortcut-edit-mixer-section-next", new KeyChordData("l", false, false));

        page.Find("[data-testid=shortcut-conflict]").TextContent.ShouldBe("L is already used by \"Loop on/off\". Change that one first.");
        Saved.Overrides.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("f5", false, "F5 cannot be used: Reloads the page.")]
    [InlineData("tab", false, "Tab cannot be used: Moves the keyboard focus.")]
    [InlineData("g", false, "G cannot be used: Starts the \"G then\" sequences.")]
    public async Task AReservedChord_SaysWhy(string key, bool mod, string message)
    {
        var page = await RenderAt();

        await LearnAsync(page, "shortcut-edit-mixer-stop", new KeyChordData(key, mod, false));

        page.Find("[data-testid=shortcut-conflict]").TextContent.ShouldBe(message);
    }

    [Fact]
    public async Task EscapeOrCancel_EndsTheCaptureWithoutChange()
    {
        var page = await RenderAt();
        await LearnAsync(page, "shortcut-edit-mixer-stop", null);
        page.FindAll("[data-testid=shortcut-capture]").ShouldBeEmpty();

        await ClickAsync(page, "shortcut-edit-mixer-stop");
        await ClickAsync(page, "shortcut-capture-cancel");

        page.FindAll("[data-testid=shortcut-capture]").ShouldBeEmpty();
        Interop.IsActive("capture").ShouldBeFalse();
        Saved.Overrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clear_AndResetAll()
    {
        var page = await RenderAt();

        await ClickAsync(page, "shortcut-clear-mixer-stop");
        Saved.Overrides["mixer.stop"].ShouldBeEmpty();
        page.Find("[data-testid=shortcut-clear-mixer-stop]").Closest("tr")!.GetAttribute("data-keys").ShouldBe(string.Empty);

        await ClickAsync(page, "shortcut-reset-all");
        Saved.Overrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task PedalSetup_PresetLearnAndMode()
    {
        var page = await RenderAt();
        page.Find("[data-testid=pedal-right-key]").TextContent.ShouldBe("PageDown");
        page.Find("[data-testid=pedal-left-key]").TextContent.ShouldBe("PageUp");

        await page.InvokeAsync(() => page.Find("[data-testid=pedal-preset]").Change("next-previous-section"));
        Saved.PedalPreset.ShouldBe("next-previous-section");
        await LearnAsync(page, "pedal-learn-right", new KeyChordData("b", false, false));
        page.Find("[data-testid=pedal-right-key]").TextContent.ShouldBe("B");
        PedalPresets.PedalChords(Saved, "mixer.section.next").ShouldHaveSingleItem().Key.ShouldBe("b");

        await ClickAsync(page, "pedal-mode");
        Saved.PedalMode.ShouldBeTrue();
        page.Find("[data-testid=pedal-mode]").GetAttribute("aria-checked").ShouldBe("true");
    }

    [Fact]
    public async Task LearningAPedalKeyThatIsTaken_IsRefused()
    {
        var page = await RenderAt();

        await LearnAsync(page, "pedal-learn-left", new KeyChordData("l", false, false));

        page.Find("[data-testid=shortcut-conflict]").TextContent.ShouldContain("Loop on/off");
        PedalPresets.PedalChords(Saved, "mixer.section.restart").ShouldBeEmpty();
    }

    [Fact]
    public async Task CustomPreset_HasNoLearnButtons()
    {
        var page = await RenderAt();

        await page.InvokeAsync(() => page.Find("[data-testid=pedal-preset]").Change("custom"));

        page.FindAll("[data-testid=pedal-learn-left]").ShouldBeEmpty();
        page.Find("[data-testid=pedal-setup]").TextContent.ShouldContain("Custom");
    }

    [Fact]
    public async Task TheTestIndicator_ShowsTheLastPedalCommand()
    {
        var page = await RenderAt();
        page.WaitForAssertion(() => Interop.KeyMaps.ContainsKey(ShortcutScope.Mixer).ShouldBeTrue());
        Interop.KeyMaps[ShortcutScope.Mixer].Bindings.Select(b => (b.Command, b.Chords[0].Key)).ShouldBe([("pedal:left", "pageup"), ("pedal:right", "pagedown")]);

        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, "pedal:right"));
        page.Find("[data-testid=pedal-test]").TextContent.ShouldBe("Right pedal pressed: Play / pause");
        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, "pedal:left"));
        page.Find("[data-testid=pedal-test]").TextContent.ShouldBe("Left pedal pressed: Back to start of current section");
    }

    [Fact]
    public async Task Beginner_SeesWhereToSwitch_AndNoEditors()
    {
        var page = await RenderAt(ExperienceLevel.Beginner);

        page.FindAll("[data-testid^=shortcut-edit-]").ShouldBeEmpty();
        page.FindAll("[data-testid=pedal-setup]").ShouldBeEmpty();
        page.Find("[data-testid=shortcut-settings]").TextContent.ShouldContain("Advanced and Expert");
    }

    [Fact]
    public async Task EveryButton_HasANameContainingItsVisibleText()
    {
        var page = await RenderAt(ExperienceLevel.Expert);

        foreach (var button in page.FindAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent).ShouldContain(button.TextContent.Trim());
        }

        page.FindAll("[data-testid^=shortcut-edit-]").Count.ShouldBe(KeyMapDefaults.Catalogue.Commands.Count(c => !c.Reserved));
    }

    [Fact]
    public async Task Dispose_StopsCaptureAndTheTest()
    {
        var page = await RenderAt();
        await ClickAsync(page, "shortcut-edit-mixer-stop");

        await DisposeComponentsAsync();

        Interop.IsActive("capture").ShouldBeFalse();
        Interop.IsActive("shortcuts:Mixer").ShouldBeFalse();
    }
}

/// <summary>T064: the Preferences switch "Single-key shortcuts" at every level and the link to the settings.</summary>
public sealed class SingleKeyPreferenceTests : PageTestContext
{
    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Expert)]
    public async Task TheSwitch_TurnsSingleKeysOff_AndTheLinkOpensTheSettings(ExperienceLevel level)
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = level }, true);
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));
        await shell.InvokeAsync(() => shell.Find("[data-testid=preferences-open]").Click());

        var toggle = shell.Find("[data-testid=pref-single-key]");
        toggle.GetAttribute("aria-checked").ShouldBe("true");
        await shell.InvokeAsync(() => toggle.Click());

        Services.GetRequiredService<PreferencesState>().Current.Shortcuts.SingleKey.ShouldBeFalse();
        shell.Find("[data-testid=pref-single-key]").GetAttribute("aria-checked").ShouldBe("false");
        Interop.KeyMaps[ShortcutScope.Global].SingleKey.ShouldBeFalse();
        shell.Find("[data-testid=pref-shortcuts-open]").GetAttribute("href").ShouldBe("settings/shortcuts");
        await shell.InvokeAsync(() => shell.Find("[data-testid=pref-shortcuts-open]").Click());
        shell.FindAll("[data-testid=preferences-panel]").ShouldBeEmpty();
    }
}
