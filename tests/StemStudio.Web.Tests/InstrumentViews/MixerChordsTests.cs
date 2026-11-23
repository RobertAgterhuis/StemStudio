using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Chords in the mixer page (T070, FR-013): the lane in the scrolling content, the chords on the karaoke banner in the dock.</summary>
public sealed class MixerChordsTests : MixerTestContext
{
    public MixerChordsTests()
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Api.GetChordsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords());
    }

    [Fact]
    public void ReadyChords_GoOnTheBanner_WhichIsMarked()
    {
        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-banner]").GetAttribute("data-chords").ShouldBe("true"));
        Views.Received(1).SetKaraokeChordsAsync(Arg.Is<ChordChartData>(c => c.Segments.Count == 2));
        Views.Received(1).AttachChordLaneAsync("chord-lane-host", Arg.Any<ChordChartData>(), Arg.Any<ChordLaneOptions>());
    }

    [Fact]
    public void TheLane_LivesInTheScrollingContent_NeverInTheDock()
    {
        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=chord-lane]"));
        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=chord-lane]").ShouldBeNull();
        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=chord-lane-toggle]").ShouldBeNull();
    }

    [Fact]
    public void StaleChords_StayOnTheBanner()
    {
        Api.GetChordsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.StaleEditedChords());

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-banner]").GetAttribute("data-chords").ShouldBe("true"));
        Views.Received(1).SetKaraokeChordsAsync(Arg.Is<ChordChartData>(c => c.Segments.Count == 2));
    }

    [Fact]
    public void WithoutReadyChords_TheBannerHasNoChords()
    {
        Api.GetChordsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords(AnalysisStatusDto.None));

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-banner]"));
        page.Find("[data-testid=karaoke-banner]").HasAttribute("data-chords").ShouldBeFalse();
        Views.DidNotReceive().SetKaraokeChordsAsync(Arg.Any<ChordChartData>());
    }

    [Fact]
    public void EngineError_OnBannerChords_IsShown_AndTheFlagStaysOff()
    {
        Views.SetKaraokeChordsAsync(Arg.Any<ChordChartData?>()).Returns(Task.FromException(new AudioEngineException("Invalid chord chart: x")));

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find(".ss-karaoke-error").TextContent.ShouldContain("Invalid chord chart"));
        page.Find("[data-testid=karaoke-banner]").HasAttribute("data-chords").ShouldBeFalse();
    }
}
