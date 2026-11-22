using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The song key next to the tempo (T120, FR-071..FR-074, FR-061; ui-test-ids track-key, mixer-key*).</summary>
public sealed class SongKeyTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    private static TrackKeySummaryDto Summary(double confidence = 0.81) =>
        new() { Tonic = 0, Mode = KeyModeDto.Major, Confidence = confidence, Display = "C major", Regions = 2 };

    public SongKeyTests() =>
        Api.GetKeyAsync(Track, Arg.Any<int>(), Arg.Any<WrittenInstrumentDto?>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Key());

    private IRenderedComponent<CascadingValue<ExperienceContext>> TrackKeyAt(ExperienceLevel level, TrackKeySummaryDto? key) =>
        RenderAtLevel<TrackKey>(level, p => p.Add(c => c.Track, TestData.Track() with { Key = key }));

    private static void MixerParameters(ComponentParameterCollectionBuilder<MixerKey> p, int transpose = 0, double position = 1, WrittenInstrumentDto? instrument = null) => p
        .Add(c => c.TrackId, Track)
        .Add(c => c.TransposeSemitones, transpose)
        .Add(c => c.Position, position)
        .Add(c => c.Instrument, instrument)
        .Add(c => c.ChordStatus, AnalysisStatusDto.Ready);

    [Fact]
    public void TrackPage_ShowsTheKey_WithItsData()
    {
        var key = TrackKeyAt(ExperienceLevel.Beginner, Summary()).Find("[data-testid=track-key]");

        key.TextContent.Trim().ShouldStartWith("C major");
        (key.GetAttribute("data-tonic"), key.GetAttribute("data-mode"), key.GetAttribute("data-uncertain")).ShouldBe(("0", "major", "false"));
        key.GetAttribute("data-help-id").ShouldBe("song-key");
    }

    [Fact]
    public void ALowConfidence_IsMarkedUncertain()
    {
        var key = TrackKeyAt(ExperienceLevel.Beginner, Summary(0.4)).Find("[data-testid=track-key]");

        key.GetAttribute("data-uncertain").ShouldBe("true");
        key.TextContent.ShouldContain("(uncertain)");
    }

    [Fact]
    public void WithoutReadyChords_TheKeyIsEmpty_WithTheStatus()
    {
        var track = TestData.Track() with { InstrumentViews = InstrumentViewsStatusDto.None with { Chords = AnalysisStatusDto.Running } };

        var key = Render<TrackKey>(p => p.Add(c => c.Track, track)).Find("[data-testid=track-key]");

        (key.TextContent.Trim(), key.GetAttribute("data-status")).ShouldBe((string.Empty, "running"));
    }

    [Fact]
    public void Advanced_TrackPage_ListsTheRegionsWithTheirBars()
    {
        var page = TrackKeyAt(ExperienceLevel.Advanced, Summary());

        page.WaitForAssertion(() => page.Find("[data-testid=track-key-region-1]").TextContent.ShouldBe("Bar 9: E major"));
        page.Find("[data-testid=track-key-regions]").TagName.ShouldBe("UL");
        Api.Received(1).GetKeyAsync(Track, 0, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Beginner_TrackPage_HasNoRegionList()
    {
        TrackKeyAt(ExperienceLevel.Beginner, Summary()).FindAll("[data-testid=track-key-regions]").ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_ShowsTheTransposedKey_AndTheCurrentRegion()
    {
        var key = Render<MixerKey>(p => MixerParameters(p, transpose: 2, position: 20));

        key.WaitForAssertion(() => key.Find("[data-testid=mixer-key]").TextContent.Trim().ShouldBe("D major (original C major, +2)"));
        key.Find("[data-testid=mixer-key]").GetAttribute("data-tonic").ShouldBe("2");
        var current = key.Find("[data-testid=mixer-key-current]");
        (current.TextContent.Trim(), current.GetAttribute("data-region-index")).ShouldBe(("Now: E major", "1"));
        Api.Received(1).GetKeyAsync(Track, 2, null, Arg.Any<CancellationToken>());
        key.FindAll("[data-testid=mixer-key-written]").ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_FetchesAgain_WhenTheTransposeChanges()
    {
        var key = Render<MixerKey>(p => MixerParameters(p));

        key.Render(p => MixerParameters(p, transpose: 3));

        key.WaitForAssertion(() => Api.Received(1).GetKeyAsync(Track, 3, null, Arg.Any<CancellationToken>()));
        key.Render(p => MixerParameters(p, transpose: 3, position: 30));
        Api.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(Api.GetKeyAsync)).ShouldBe(2);
    }

    [Fact]
    public void Mixer_ATransposingInstrument_ShowsTheWrittenKey()
    {
        Api.GetKeyAsync(Track, 0, WrittenInstrumentDto.AltoSaxEb, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Key() with { WrittenKey = "A major" });

        var key = Render<MixerKey>(p => MixerParameters(p, instrument: WrittenInstrumentDto.AltoSaxEb));

        key.WaitForAssertion(() => key.Find("[data-testid=mixer-key-written]").TextContent.Trim().ShouldBe("Your part: A major"));
    }

    [Fact]
    public void Advanced_Mixer_ShowsTheConfidence_AndTheAlternative()
    {
        Api.GetKeyAsync(Track, Arg.Any<int>(), Arg.Any<WrittenInstrumentDto?>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Key() with
        {
            Key = InstrumentViewsData.Key().Key! with { Alternative = new KeyAlternativeDto { Tonic = 9, Mode = KeyModeDto.Minor } },
        });

        var page = RenderAtLevel<MixerKey>(ExperienceLevel.Advanced, p => MixerParameters(p));

        page.WaitForAssertion(() => page.Find("[data-testid=mixer-key-confidence]").TextContent.Trim().ShouldBe("81 % (or A minor)"));
        RenderAtLevel<MixerKey>(ExperienceLevel.Beginner, p => MixerParameters(p)).FindAll("[data-testid=mixer-key-confidence]").ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_WithoutAKey_IsEmpty_WithTheStatus()
    {
        Api.GetKeyAsync(Track, Arg.Any<int>(), Arg.Any<WrittenInstrumentDto?>(), Arg.Any<CancellationToken>())
            .Returns(InstrumentViewsData.Key() with { Status = AnalysisStatusDto.Queued, Key = null, Display = null, Regions = [] });

        var key = Render<MixerKey>(p => MixerParameters(p));

        key.WaitForAssertion(() => key.Find("[data-testid=mixer-key]").GetAttribute("data-status").ShouldBe("queued"));
        key.FindAll("[data-testid=mixer-key-current]").ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_AnApiError_LeavesTheKeyEmpty()
    {
        Api.GetKeyAsync(Track, Arg.Any<int>(), Arg.Any<WrittenInstrumentDto?>(), Arg.Any<CancellationToken>())
            .Returns<KeyViewDto>(_ => throw new StemStudio.Web.Api.ApiException("boom"));

        Render<MixerKey>(p => MixerParameters(p)).Find("[data-testid=mixer-key]").TextContent.Trim().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1, KeyModeDto.Major, "Db major")]
    [InlineData(6, KeyModeDto.Major, "F# major")]
    [InlineData(1, KeyModeDto.Minor, "C# minor")]
    [InlineData(10, KeyModeDto.Minor, "Bb minor")]
    public void KeyNames_AreSpelledLikeKeySignatures(int tonic, KeyModeDto mode, string name) => KeyText.Name(tonic, mode).ShouldBe(name);
}
