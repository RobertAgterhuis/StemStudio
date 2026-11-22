using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Final accessibility and help checks of feature 016 (T135, FR-061, FR-062, FR-064).</summary>
public sealed class InstrumentViewsPolishTests : MixerTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    public InstrumentViewsPolishTests()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(DrumlessTests.DrumTrack());
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns([]);
        Api.GetDrumHitsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.DrumHits());
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Api.GetVocalPitchAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch());
    }

    [Fact]
    public void EveryInstrumentsTopic_NamesItsLimits()
    {
        var topics = Catalog.Topics.Where(t => t.Area == "instruments").ToList();

        topics.Count.ShouldBe(11, "DELTA 5 lists eleven topics");
        topics.ShouldAllBe(t => t.Markdown.Contains("Limits", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(InstrumentViewDto.DrumTrainer)]
    [InlineData(InstrumentViewDto.Singing)]
    [InlineData(InstrumentViewDto.Chords)]
    [InlineData(InstrumentViewDto.Tablature)]
    [InlineData(InstrumentViewDto.Part)]
    public void EveryView_IsKeyboardReachable_OutsideTheDock_WithNamedControls(InstrumentViewDto view)
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());

        page.InvokeAsync(() => page.Instance.OpenView(view));

        var region = page.Find("[data-testid=instrument-view]");
        region.Closest(".ss-mixer-dock").ShouldBeNull();
        region.QuerySelectorAll("[tabindex]").ShouldAllBe(e => int.Parse(e.GetAttribute("tabindex")!, System.Globalization.CultureInfo.InvariantCulture) <= 0);
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(region);
    }
}
