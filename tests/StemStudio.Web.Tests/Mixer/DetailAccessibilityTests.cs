using Bunit;
using NSubstitute;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Accessibility of detail groups (T084): names, keyboard-operable expand, group relationships.</summary>
public sealed class DetailAccessibilityTests : MixerTestContext
{
    private const string ExpandDrums = "[data-testid=expand-drums]";

    public DetailAccessibilityTests() =>
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());

    [Fact]
    public async Task EveryControl_InAnExpandedGroup_HasAnAccessibleName()
    {
        var page = RenderMixer();
        await page.Find(ExpandDrums).ClickAsync(new());

        AssertEveryControlIsNamed(page);

        page.Find("[data-testid=fader-drums-snare]").GetAttribute("aria-label").ShouldBe("drums snare volume");
        page.Find("[data-testid=mute-drums-snare]").GetAttribute("aria-label").ShouldBe("Mute drums snare");
        page.Find("[data-testid=meter-drums-snare]").GetAttribute("aria-label").ShouldBe("drums snare level meter");
        page.Find("[data-testid=strip-drums-snare]").GetAttribute("aria-label").ShouldBe("drums snare channel");
    }

    [Fact]
    public async Task ExpandToggle_IsANativeButton_ThatControlsTheNamedGroup()
    {
        var page = RenderMixer();
        var toggle = page.Find(ExpandDrums);

        toggle.TagName.ShouldBe("BUTTON");
        toggle.GetAttribute("tabindex").ShouldNotBe("-1");
        toggle.GetAttribute("aria-label").ShouldBe("Show detail stems of drums");
        toggle.GetAttribute("aria-controls").ShouldBe("group-drums");

        await toggle.ClickAsync(new());

        page.Find(ExpandDrums).GetAttribute("aria-label").ShouldBe("Hide detail stems of drums");
        var group = page.Find("[data-testid=group-drums]");
        (group.Id, group.GetAttribute("role"), group.GetAttribute("aria-label")).ShouldBe(("group-drums", "group", "drums detail stems"));
    }

    [Fact]
    public async Task ExpandedParentStrip_AnnouncesThatItIsReplaced()
    {
        var page = RenderMixer();
        page.Find("[data-testid=strip-drums]").GetAttribute("aria-label").ShouldBe("drums channel");

        await page.Find(ExpandDrums).ClickAsync(new());

        page.Find("[data-testid=strip-drums]").GetAttribute("aria-label").ShouldBe("drums channel, replaced by its detail stems");
    }

    [Fact]
    public void ImportPage_MentionsDetailSeparationAndItsLimits()
    {
        var help = Render<Import>().Find("[data-testid=stem-limitation-help]").TextContent;

        help.ShouldContain("not reliably possible with current open-source models");
        help.ShouldContain("detail stems");
        help.ShouldContain("quality varies");
    }
}
