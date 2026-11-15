using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Expandable detail stem groups in the mixer (FR-012, FR-013, US2).</summary>
public sealed class StemGroupTests : MixerTestContext
{
    private const string ExpandDrums = "[data-testid=expand-drums]";
    private static readonly string[] DrumKeys = ["drums-kick", "drums-snare", "drums-rest"];

    public StemGroupTests() =>
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());

    private async Task<IRenderedComponent<MixerPage>> ExpandedDrumsAsync()
    {
        var page = RenderMixer();
        await page.Find(ExpandDrums).ClickAsync(new());
        return page;
    }

    [Fact]
    public void ExpandButtons_OnlyForSplitParents_Collapsed()
    {
        var page = RenderMixer();

        page.FindAll("[data-testid^=expand-]").Select(e => e.GetAttribute("data-testid")).ShouldBe(["expand-vocals", "expand-drums"]);
        page.Find(ExpandDrums).GetAttribute("aria-expanded").ShouldBe("false");
        page.FindAll("[data-testid^=group-]").ShouldBeEmpty();
        page.Find("[data-testid=strip-drums]").GetAttribute("data-expanded").ShouldBe("false");
        page.Find("[data-testid=strip-bass]").HasAttribute("data-expanded").ShouldBeFalse();
    }

    [Fact]
    public void TrackWithoutDetailStems_HasNoExpandButtons()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());

        RenderMixer().FindAll("[data-testid^=expand-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Engine_GetsDetailMetersAndChannels_AtLoad()
    {
        RenderMixer();

        await Engine.Received(1).LoadTrackAsync(Arg.Is<TrackDto>(t => t.Detail.Status == DetailPhaseStatusDto.Completed));
        await Engine.Received(1).AttachMeterAsync("drums.snare", "meter-drums-snare");
        await Engine.Received(1).AttachMeterAsync("vocals.rest", "meter-vocals-rest");
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto("drums.kick", 0.8, 0, false, false));
        await Engine.Received(12).SetChannelAsync(Arg.Any<ChannelSettingsDto>());
    }

    [Fact]
    public async Task Expand_CallsSetExpanded_AndShowsGroupStripsWithAllControls()
    {
        var page = await ExpandedDrumsAsync();

        await Engine.Received(1).SetExpandedAsync("drums", true);
        page.Find(ExpandDrums).GetAttribute("aria-expanded").ShouldBe("true");
        page.Find("[data-testid=strip-drums]").GetAttribute("data-expanded").ShouldBe("true");
        page.Find("[data-testid=strip-drums]").ClassList.ShouldContain("ss-strip-silent");
        var group = page.Find("[data-testid=group-drums]");
        group.QuerySelectorAll("[data-testid^=strip-]").Select(e => e.GetAttribute("data-testid")).ShouldBe(DrumKeys.Select(k => $"strip-{k}"));
        foreach (var key in DrumKeys)
        {
            AssertControls(page, key);
        }

        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task DetailControls_CallSetChannelWithDetailIds()
    {
        var page = await ExpandedDrumsAsync();
        Engine.ClearReceivedCalls();

        SetValue(page.Find("[data-testid=fader-drums-snare]"), 50);
        SetValue(page.Find("[data-testid=pan-drums-snare]"), 20);
        await page.Find("[data-testid=mute-drums-snare]").ClickAsync(new());
        await page.Find("[data-testid=solo-drums-kick]").ClickAsync(new());

        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto("drums.snare", 0.5, 0, false, false));
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto("drums.snare", 0.5, 0.2, true, false));
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto("drums.kick", 0.8, 0, false, true));
        page.Find("[data-testid=strip-drums-kick]").ClassList.ShouldNotContain("ss-strip-silent");
        page.Find("[data-testid=strip-drums-rest]").ClassList.ShouldContain("ss-strip-silent");
        page.Find("[data-testid=strip-vocals]").ClassList.ShouldContain("ss-strip-silent");
    }

    [Fact]
    public async Task Collapse_HidesTheGroup_AndKeepsDetailSettings()
    {
        var page = await ExpandedDrumsAsync();
        await page.Find("[data-testid=mute-drums-snare]").ClickAsync(new());

        await page.Find(ExpandDrums).ClickAsync(new());

        await Engine.Received(1).SetExpandedAsync("drums", false);
        page.FindAll("[data-testid=group-drums]").ShouldBeEmpty();
        page.Find("[data-testid=strip-drums]").GetAttribute("data-expanded").ShouldBe("false");
        await page.Find(ExpandDrums).ClickAsync(new());
        page.Find("[data-testid=mute-drums-snare]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task RejectedExpand_ShowsTheError_AndStaysCollapsed()
    {
        Engine.SetExpandedAsync("drums", true).ThrowsAsync(new AudioEngineException("Failed to load stem \"drums.snare\": HTTP 404"));

        var page = await ExpandedDrumsAsync();

        page.Find("[data-testid=mixer-engine-error]").TextContent.ShouldBe("Failed to load stem \"drums.snare\": HTTP 404");
        page.Find(ExpandDrums).HasAttribute("disabled").ShouldBeFalse();
        page.Find(ExpandDrums).GetAttribute("aria-expanded").ShouldBe("false");
        page.FindAll("[data-testid=group-drums]").ShouldBeEmpty();
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    private static void AssertControls(IRenderedComponent<MixerPage> page, string key)
    {
        page.Find($"[data-testid=fader-{key}]").GetAttribute("aria-valuenow").ShouldBe("80");
        page.Find($"[data-testid=pan-{key}]").GetAttribute("aria-valuenow").ShouldBe("0");
        page.Find($"[data-testid=mute-{key}]").GetAttribute("aria-pressed").ShouldBe("false");
        page.Find($"[data-testid=solo-{key}]").GetAttribute("aria-pressed").ShouldBe("false");
        page.Find($"[data-testid=meter-{key}]").Id.ShouldBe($"meter-{key}");
    }
}
