using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Preset controls in the mixer (T045, FR-016): chip row at Beginner, menu at Advanced/Expert.</summary>
public sealed class PracticePresetsComponentTests : MixerTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));

    [Fact]
    public void Beginner_ShowsAChipRowWithEveryAvailablePreset()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        var row = page.Find("[data-testid=mixer-presets]");
        row.TagName.ShouldNotBe("BUTTON");
        row.QuerySelectorAll("[data-testid^=preset-]").Select(b => b.GetAttribute("data-testid"))
            .ShouldBe(["preset-play-along-drums", "preset-play-along-bass", "preset-play-along-guitar", "preset-play-along-piano",
                "preset-karaoke", "preset-focus-vocals", "preset-reset"]);
        row.GetAttribute("data-help-id").ShouldBe("mixer-presets");
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void AdvancedAndExpert_ShowAMenuButton_WithItemsOnlyWhenOpen(ExperienceLevel level)
    {
        var page = RenderAt(level);

        var button = page.Find("[data-testid=mixer-presets]");
        button.TagName.ShouldBe("BUTTON");
        button.GetAttribute("aria-expanded").ShouldBe("false");
        button.GetAttribute("aria-haspopup").ShouldBe("menu");
        page.FindAll("[data-testid^=preset-]").ShouldBeEmpty();

        button.Click();

        page.Find("[data-testid=mixer-presets]").GetAttribute("aria-expanded").ShouldBe("true");
        page.Find("[role=menu]").QuerySelectorAll("[role=menuitem]").Count.ShouldBe(7);
    }

    [Fact]
    public async Task ChoosingAPreset_UsesTheEngineLikeManualChanges_AndMarksTheMixDirty()
    {
        var page = RenderAt(ExperienceLevel.Beginner);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=preset-play-along-drums]").ClickAsync(new MouseEventArgs());

        await Engine.Received(6).SetChannelAsync(Arg.Any<ChannelSettingsDto>());
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums" && c.Mute));
        await Engine.Received(1).SetMetronomeAsync(Arg.Is<MetronomeSettingsDto>(m => m.Enabled));
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        page.Find("[data-testid=mute-drums]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task ChoosingFromTheMenu_ClosesIt()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.Find("[data-testid=mixer-presets]").Click();

        await page.Find("[data-testid=preset-karaoke]").ClickAsync(new MouseEventArgs());

        page.FindAll("[role=menu]").ShouldBeEmpty();
        page.Find("[data-testid=mute-vocals]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task Escape_ClosesTheMenu()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.Find("[data-testid=mixer-presets]").Click();

        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[role=menu]").ShouldBeEmpty();
        page.Find("[data-testid=mixer-presets]").Click();
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "x" });
        page.FindAll("[role=menu]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task Presets_AreAnnouncedThroughTheToast()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        await page.Find("[data-testid=preset-focus-vocals]").ClickAsync(new MouseEventArgs());

        Services.GetService(typeof(StemStudio.Web.Services.ToastService)).ShouldBeOfType<StemStudio.Web.Services.ToastService>()
            .Message.ShouldBe("Focus: vocals applied");
    }
}
