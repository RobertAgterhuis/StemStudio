using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The hits of the current bar as text, the text alternative of the hit lanes (T090, FR-062, <c>drum-bar-text</c>).</summary>
public sealed class DrumBarTextTests
{
    // Beats 0.52, 1.05, 1.58, 2.11, 2.64; downbeats 0.52 and 2.64 (4/4).
    private static readonly Contracts.Tracks.BeatGridDto Grid = TestData.Grid();

    private static DrumHitDto Hit(int ms, DrumPieceDto piece, double strength = 0.6) => new() { TimeMs = ms, Piece = piece, Strength = strength };

    private static readonly DrumHitDto[] Hits =
    [
        Hit(500, DrumPieceDto.Kick, 0.9),
        Hit(520, DrumPieceDto.Kick, 0.9),
        Hit(790, DrumPieceDto.Hihat),
        Hit(1050, DrumPieceDto.Snare, 0.3),
        Hit(3000, DrumPieceDto.Hihat),
        Hit(5200, DrumPieceDto.Cymbals),
    ];

    [Fact]
    public void InABar_EachHitNamesItsBeat_AndLoudOrSoftHits()
    {
        var bar = DrumBarText.At(Hits, Grid, 1.2);

        bar.Heading.ShouldBe("Bar 1");
        bar.Hits.ShouldBe(["Kick on 1 (loud)", "Hi-hat on 1 and", "Snare on 2 (soft)"]);
    }

    [Fact]
    public void TheLastBar_IsAsLongAsThePreviousOne()
    {
        var bar = DrumBarText.At(Hits, Grid, 2.7);

        bar.Heading.ShouldBe("Bar 2");
        bar.Hits.ShouldBe(["Hi-hat on 1 and"]);
    }

    [Fact]
    public void BeforeTheFirstDownbeat_HitsHaveTheirTime()
    {
        var bar = DrumBarText.At(Hits, Grid, 0.2);

        bar.Heading.ShouldBe("Before bar 1");
        bar.Hits.ShouldBe(["Kick at 0.5 s (loud)"]);
    }

    [Fact]
    public void WithoutABeatGrid_TwoSecondWindowsAreUsed()
    {
        var bar = DrumBarText.At(Hits, null, 4.5);

        bar.Heading.ShouldBe("From 0:04 to 0:06");
        bar.Hits.ShouldBe(["Cymbals at 5.2 s"]);
    }

    [Fact]
    public void AnEmptyBar_HasNoHits_AndAGridWithoutDownbeatsCountsAsNone()
    {
        DrumBarText.At([], Grid, 1).Hits.ShouldBeEmpty();
        DrumBarText.At(Hits, Grid with { Downbeats = [] }, 0.6).Heading.ShouldBe("From 0:00 to 0:02");
    }

    [Fact]
    public void AHitJustBeforeTheNextBeat_CountsForThatBeat()
    {
        var bar = DrumBarText.At([Hit(1040, DrumPieceDto.Toms)], Grid, 1);

        bar.Hits.ShouldBe(["Toms on 2"]);
    }
}
