using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Audio;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Scoring units, the reference contour and the lead mute of singing feedback (T105, data-model 3.4, FR-044).</summary>
public sealed class SingingUnitsTests
{
    [Fact]
    public void SyncedLyrics_GiveOneUnitPerLine_WithTheOffset()
    {
        var units = SingingUnits.From(LyricsData.Found(offsetMs: 100), PracticeFlowData.Sections(), TestData.Grid(), 213);

        units.Kind.ShouldBe("Line");
        units.Units.ShouldBe([new SingingUnit(0, 11050, 13300), new SingingUnit(1, 14100, 16100)]);
    }

    [Fact]
    public void PlainLyrics_FallBackToSections()
    {
        var units = SingingUnits.From(LyricsData.Found(SyncLevelDto.Plain), PracticeFlowData.Sections(), TestData.Grid(), 213);

        units.Kind.ShouldBe("Section");
        units.Units.Count.ShouldBe(5);
        units.Units[1].ShouldBe(new SingingUnit(1, 8100, 24100));
    }

    [Fact]
    public void WithoutSections_FourBarBlocksOfTheGrid()
    {
        var grid = TestData.Grid() with { Downbeats = [0, 2, 4, 6, 8, 10, 12, 14, 16] };

        var units = SingingUnits.From(null, [], grid, 20);

        units.Kind.ShouldBe("Block");
        units.Units.ShouldBe([new SingingUnit(0, 0, 8000), new SingingUnit(1, 8000, 16000), new SingingUnit(2, 16000, 20000)]);
    }

    [Fact]
    public void WithoutAnything_TenSecondBlocks()
    {
        var units = SingingUnits.From(null, null, null, 25);

        units.Kind.ShouldBe("Part");
        units.Units.ShouldBe([new SingingUnit(0, 0, 10000), new SingingUnit(1, 10000, 20000), new SingingUnit(2, 20000, 25000)]);
    }

    [Fact]
    public void LinesWithoutTimesOrOutOfOrder_AreLeftOut()
    {
        var lyrics = LyricsData.Found() with
        {
            Lines =
            [
                new LyricsLineDto(5000, 6000, "b", []),
                new LyricsLineDto(1000, 2000, "a", []),
                new LyricsLineDto(null, 3000, "x", []),
                new LyricsLineDto(7000, 7000, "empty", []),
            ],
        };

        SingingUnits.From(lyrics, null, null, 10).Units.ShouldBe([new SingingUnit(0, 1000, 2000), new SingingUnit(1, 5000, 6000)]);
    }

    [Fact]
    public void Reference_ClampsFramesOutsideTheContract_ToUnvoiced()
    {
        var reference = SingingUnits.Reference(InstrumentViewsData.VocalPitch().Document!);

        reference.Frames.ShouldBe([0, 4500, 4510, 4700]);
        SingingUnits.Reference(InstrumentViewsData.VocalPitch().Document! with { Frames = [13000, 12700] }).Frames.ShouldBe([0, 12700]);
    }

    [Fact]
    public void Label_NamesTheUnitAndItsScore()
    {
        SingingUnits.Announce("Line", new SingingUnitScore(2, 87, 1)).ShouldBe("Line 3: 87 points");
        SingingUnits.Announce("Section", new SingingUnitScore(0, null, 1)).ShouldBe("Section 1: too little melody to score");
        new ScoredUnit("Line", new SingingUnitScore(0, 92, 1)).Text.ShouldBe("Line 1: 92");
        new ScoredUnit("Block", new SingingUnitScore(3, null, 2)).Text.ShouldBe("Block 4: no score");
    }

    [Fact]
    public void LeadMute_MutesTheLeadVocal_AndRestoresIt()
    {
        var state = new MixerState(DrumlessTests.DrumTrack());
        state.SetExpanded(StemNames.Vocals, true);
        var muted = SingingMix.MuteLead(state);

        muted.Flags.Stem.ShouldBe("vocals.lead");
        state.AllChannels.Single(c => c.Stem == "vocals.lead").Mute.ShouldBeTrue();
        SingingMix.Restore(state, muted.Flags).Single().Mute.ShouldBeFalse();

        var plain = new MixerState(TestData.Track());
        SingingMix.MuteLead(plain).Flags.Stem.ShouldBe(StemNames.Vocals);
    }
}
