using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Mixer controls per level (T043, FR-012..FR-014, SC-010; ui-test-ids R7/R8).</summary>
public sealed class MixerLevelTests : MixerTestContext
{
    private static readonly string[] HiddenAtBeginner =
        ["loop-start", "loop-end", "metronome-bpm", "metronome-offset", "metronome-beats-per-bar", "metronome-reset"];

    private static readonly string[] KeptAtBeginner =
    [
        "fader-vocals", "mute-vocals", "solo-vocals", "meter-vocals", "meter-master", "master-fader", "transport-play", "transport-pause",
        "transport-stop", "transport-seek", "transport-position", "loop-set-a", "loop-set-b", "loop-toggle", "metronome-toggle",
        "metronome-volume", "mix-save", "mix-save-as",
    ];

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, Context(level))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));

    private static ExperienceContext Context(ExperienceLevel level) => new(level, DensityPreference.Comfortable);

    private static void SwitchTo(IRenderedComponent<CascadingValue<ExperienceContext>> page, ExperienceLevel level) =>
        page.Render(p => p.Add(c => c.Value, Context(level)));

    [Fact]
    public void Beginner_HidesExactlyTheR8Controls()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        foreach (var id in HiddenAtBeginner)
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }

        page.FindAll("[data-testid^=pan-]:not([data-testid^=pan-marker])").ShouldBeEmpty();
        foreach (var id in KeptAtBeginner)
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void AdvancedAndExpert_ShowTheR8Controls(ExperienceLevel level)
    {
        var page = RenderAt(level);

        foreach (var id in HiddenAtBeginner.Append("pan-vocals"))
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }
    }

    [Fact]
    public void Beginner_HidesDetailPans_Too()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());
        var page = RenderAt(ExperienceLevel.Beginner);

        page.Find("[data-testid=expand-drums]").Click();

        page.WaitForAssertion(() => page.Find("[data-testid=fader-drums-snare]").ShouldNotBeNull());
        page.FindAll("[data-testid=pan-drums-snare]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-30, "Panned L 30")]
    [InlineData(45, "Panned R 45")]
    public void PanMarker_ShowsAPanSetAtAHigherLevel_ReadOnly(int pan, string text)
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.Find("[data-testid=pan-vocals]").Input(pan.ToString(System.Globalization.CultureInfo.InvariantCulture));

        SwitchTo(page, ExperienceLevel.Beginner);

        var marker = page.Find("[data-testid=pan-marker-vocals]");
        marker.TextContent.Trim().ShouldBe(text);
        marker.TagName.ShouldNotBe("INPUT");
        page.FindAll("[data-testid=pan-marker-drums]").ShouldBeEmpty();
    }

    [Fact]
    public void PanMarker_IsBeginnerOnly()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.Find("[data-testid=pan-vocals]").Input("-30");

        page.FindAll("[data-testid=pan-marker-vocals]").ShouldBeEmpty();
    }

    [Fact]
    public void SwitchingLevels_DoesNotTouchTheEngine_AndKeepsMixerState()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.Find("[data-testid=fader-vocals]").Input("40");
        page.Find("[data-testid=pan-vocals]").Input("-30");
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        Engine.ClearReceivedCalls();

        SwitchTo(page, ExperienceLevel.Beginner);
        SwitchTo(page, ExperienceLevel.Expert);

        Engine.ReceivedCalls().ShouldBeEmpty();
        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-valuenow").ShouldBe("40");
        page.Find("[data-testid=pan-vocals]").GetAttribute("aria-valuenow").ShouldBe("-30");
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public void TypedLoopValues_StayApplied_WhenHiddenAtBeginner()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.Find("[data-testid=loop-start]").Change("10");
        page.Find("[data-testid=loop-end]").Change("20");

        SwitchTo(page, ExperienceLevel.Beginner);
        SwitchTo(page, ExperienceLevel.Advanced);

        page.Find("[data-testid=loop-start]").GetAttribute("value").ShouldBe("10");
        Engine.Received().SetLoopAsync(new LoopRegionDto(10, 20, false));
    }
}
