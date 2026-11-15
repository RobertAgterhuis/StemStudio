using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Tracks;

public sealed class BeatGridTests
{
    [Fact]
    public void Validate_AcceptsValidGrid()
    {
        TrackTests.ValidGrid().Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AcceptsFailedDetection()
    {
        var grid = TrackTests.ValidGrid() with { Bpm = 0, Beats = [], Downbeats = [], Confidence = 0, Reliable = false };

        grid.Validate().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1, 4, 0.5, "beatGrid.bpm")]
    [InlineData(400.1, 4, 0.5, "beatGrid.bpm")]
    [InlineData(120, 0, 0.5, "beatGrid.beatsPerBar")]
    [InlineData(120, 13, 0.5, "beatGrid.beatsPerBar")]
    [InlineData(120, 4, -0.1, "beatGrid.confidence")]
    [InlineData(120, 4, 1.1, "beatGrid.confidence")]
    public void Validate_RejectsOutOfRangeScalars(double bpm, int beatsPerBar, double confidence, string field)
    {
        var grid = TrackTests.ValidGrid() with { Bpm = bpm, BeatsPerBar = beatsPerBar, Confidence = confidence };

        grid.Validate().Select(e => e.Field).ShouldBe([field]);
    }

    [Theory]
    [InlineData(new[] { 1.0, 1.0 })]
    [InlineData(new[] { 2.0, 1.0 })]
    [InlineData(new[] { -0.5, 1.0 })]
    public void Validate_RequiresStrictlyAscendingNonNegativeBeats(double[] beats)
    {
        var grid = TrackTests.ValidGrid() with { Beats = beats, Downbeats = [] };

        grid.Validate().Single().Field.ShouldBe("beatGrid.beats");
    }

    [Fact]
    public void Validate_RequiresDownbeatsToBeSubsetOfBeats()
    {
        var grid = TrackTests.ValidGrid() with { Downbeats = [0.52, 0.6] };

        grid.Validate().Single().Field.ShouldBe("beatGrid.downbeats");
    }

    [Fact]
    public void BeatsPerBarDefaultsToFour()
    {
        new BeatGrid { Bpm = 1, Beats = [], Downbeats = [], Confidence = 0, Reliable = false }.BeatsPerBar.ShouldBe(4);
    }
}
