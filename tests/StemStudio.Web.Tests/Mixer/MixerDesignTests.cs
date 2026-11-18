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
        var karaoke = RepoPaths.ReadWeb("wwwroot/css/components/karaoke.css");

        // The padding follows the dock's measured height (karaoke banner + count-in + transport, 012 integration),
        // with the fixed transport height as the fallback before the script has measured it.
        css.ShouldContain("html:has(.ss-transport-dock)");
        css.ShouldContain("scroll-padding-bottom: calc(var(--ss-dock-inset, var(--ss-transport-height)) + var(--ss-space-2))");
        karaoke.ShouldContain("scroll-padding-bottom: calc(var(--ss-dock-inset, calc(var(--ss-transport-height) + 9rem)) + var(--ss-space-2))");
    }

    [Fact]
    public void MixerDock_IsAFixedBar_AndTheContentReservesItsInset()
    {
        var karaoke = RepoPaths.ReadWeb("wwwroot/css/components/karaoke.css");
        var mixer = RepoPaths.ReadWeb("wwwroot/css/components/mixer.css");

        // 014 integration (WCAG 2.5.8, axe target-size): a sticky dock is part of the page flow, so a target that
        // scrolled half under it counted as overlapped by the dock's controls. The dock is a fixed bar beside the rail
        // (and left of a docked help or agent panel), and the content ends the measured dock inset higher instead.
        var dock = RepoPaths.CssRule(karaoke, ".ss-mixer-dock {");
        dock.ShouldContain("position: fixed;");
        dock.ShouldContain("left: calc(var(--ss-rail-width) + 1px);");
        dock.ShouldNotContain("position: sticky");
        karaoke.ShouldContain(".ss-shell-help-open .ss-mixer-dock,");
        RepoPaths.CssRule(karaoke, ".ss-shell-agent-open .ss-mixer-dock {").ShouldContain("right: var(--ss-help-width);");
        RepoPaths.CssRule(mixer, ".ss-main:has(.ss-mixer-dock) {")
            .ShouldContain("padding-bottom: calc(var(--ss-dock-inset, calc(var(--ss-transport-height) + 9rem)) + var(--ss-space-4));");
    }

    [Fact]
    public void DockInsetScript_MeasuresTheStickyDock_AndIsLoadedByTheApp()
    {
        var script = RepoPaths.ReadWeb("wwwroot/js/dock-inset.js");
        var app = RepoPaths.ReadWeb("Components/App.razor");

        script.ShouldContain("new ResizeObserver(");
        script.ShouldContain("'.ss-mixer-dock, .ss-transport-dock'");
        script.ShouldContain("setProperty('--ss-dock-inset'");
        script.ShouldNotContain("setTimeout");
        app.ShouldContain("<script src=\"js/dock-inset.js\"></script>");
    }

    [Fact]
    public void MasterStrip_HasAnInkTab()
    {
        var page = RenderMixer();

        page.Find(".ss-console-master .ss-master-tab").TextContent.Trim().ShouldBe("Master");
    }
}
