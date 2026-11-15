using Bunit;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Web.Components.Design;
using StemStudio.Web.Components.Mixer;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>The mixer console layout (T033, FR-005, FR-030; design-spec.md 4.5).</summary>
public sealed class MixerDesignTests : MixerTestContext
{
    private static readonly string[] MixerIds =
    [
        "fader-vocals", "pan-vocals", "mute-vocals", "solo-vocals", "meter-vocals", "meter-master", "master-fader",
        "transport-play", "transport-pause", "transport-stop", "transport-seek", "transport-position", "transport-state",
        "loop-start", "loop-end", "loop-toggle", "loop-set-a", "loop-set-b", "metronome-toggle", "metronome-volume",
        "metronome-bpm", "metronome-offset", "metronome-beats-per-bar", "metronome-reset", "mix-save", "mix-save-as",
    ];

    [Fact]
    public void Console_HasHeader_Panels_Strips_PinnedMaster_AndTransportDock()
    {
        var page = RenderMixer();

        var console = page.Find(".ss-console");
        console.QuerySelector("header.ss-console-head h1").ShouldNotBeNull();
        console.QuerySelector("header.ss-console-head [data-testid=mix-save]").ShouldNotBeNull();
        console.QuerySelector(".ss-console-panels [data-testid=loop-toggle]").ShouldNotBeNull();
        console.QuerySelector(".ss-console-panels [data-testid=metronome-toggle]").ShouldNotBeNull();
        console.QuerySelector(".ss-console-strips [data-testid=strip-vocals]").ShouldNotBeNull();
        console.QuerySelector(".ss-console-master [data-testid=master-fader]").ShouldNotBeNull();
        console.QuerySelector(".ss-console-strips [data-testid=master-fader]").ShouldBeNull();
        console.QuerySelector(".ss-transport-dock [data-testid=transport-play]").ShouldNotBeNull();
    }

    [Fact]
    public void EveryReleasedMixerId_IsRendered_AtAdvanced()
    {
        var page = RenderMixer();

        foreach (var id in MixerIds.Concat(StemNames.SixStem.Select(s => $"strip-{s}")))
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }
    }

    [Fact]
    public void Strips_CarryTheirStemColourTab()
    {
        var page = RenderMixer();

        foreach (var stem in StemNames.SixStem)
        {
            var tab = page.Find($"[data-testid=strip-{stem}] .ss-stem-tab");
            tab.GetAttribute("style").ShouldBe($"--stem-color: {StemColor.Var(stem)}");
            tab.TextContent.Trim().ShouldBe(stem);
        }
    }

    [Fact]
    public void DetailStrips_UseTheParentColour_WithAPatternPerDetail()
    {
        var strip = Render<ChannelStrip>(p => p.Add(s => s.Channel, new ChannelState("drums.snare")).Add(s => s.Pattern, 1));

        var tab = strip.Find(".ss-stem-tab");
        tab.GetAttribute("style").ShouldBe($"--stem-color: {StemColor.Var("drums")}");
        tab.ClassList.ShouldContain("ss-pattern-dashed");
        tab.TextContent.Trim().ShouldBe("snare");
    }

    [Fact]
    public void ExpandedGroup_PassesPatternsToItsDetailStrips()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());
        var page = RenderMixer();

        page.Find("[data-testid=expand-drums]").Click();

        page.WaitForAssertion(() => page.Find("[data-testid=strip-drums-snare] .ss-stem-tab").ClassList.ShouldContain("ss-pattern-dashed"));
        page.Find("[data-testid=strip-drums-kick] .ss-stem-tab").ClassList.ShouldContain("ss-pattern-solid");
    }

    [Fact]
    public void Meters_HaveNoInlineStyle_AndCssDrawsThemFromMeterLevelByTransform()
    {
        var page = RenderMixer();

        foreach (var meter in page.FindAll("[data-testid^=meter-]"))
        {
            meter.HasAttribute("style").ShouldBeFalse(meter.GetAttribute("data-testid"));
            meter.QuerySelector(".ss-meter-fill").ShouldNotBeNull();
        }

        var css = RepoPaths.ReadWeb("wwwroot/css/components/mixer.css");
        css.ShouldContain("scaleY(calc(1 - var(--meter-level, 0)))");
        css.ShouldNotContain("height: calc(var(--meter-level");
    }

    [Fact]
    public void MuteAndSolo_UseTheTargetSizedToggleClass()
    {
        var page = RenderMixer();

        page.Find("[data-testid=mute-vocals]").ClassList.ShouldContain("ss-strip-toggle");
        page.Find("[data-testid=solo-vocals]").ClassList.ShouldContain("ss-strip-toggle");
        RepoPaths.ReadWeb("wwwroot/css/components/mixer.css").ShouldContain("min-width: var(--ss-target-min)");
    }

    [Fact]
    public void TransportDock_ReservesScrollPadding_SoFocusIsNeverHidden()
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/mixer.css");

        css.ShouldContain("html:has(.ss-transport-dock)");
        css.ShouldContain("scroll-padding-bottom: var(--ss-transport-height)");
    }

    [Fact]
    public void MasterStrip_HasAnInkTab()
    {
        var page = RenderMixer();

        page.Find(".ss-console-master .ss-master-tab").TextContent.Trim().ShouldBe("Master");
    }
}
