using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Sections;

/// <summary>Boundary snapping (014 FR-003): downbeat when the grid is reliable, beat when unreliable, 0.1 s without a grid.</summary>
public sealed class SectionSnapperTests
{
    private static BeatGrid Grid(bool reliable) => new()
    {
        Bpm = 120,
        Beats = [0.1, 0.6, 1.1, 1.6, 2.1, 2.6, 3.1, 3.6, 4.1],
        Downbeats = [0.1, 2.1, 4.1],
        BeatsPerBar = 4,
        Confidence = reliable ? 0.9 : 0.3,
        Reliable = reliable,
    };

    [Fact]
    public void Mode_FollowsTheGrid()
    {
        SectionSnapper.ModeFor(Grid(true)).ShouldBe(SectionSnap.Downbeat);
        SectionSnapper.ModeFor(Grid(false)).ShouldBe(SectionSnap.Beat);
        SectionSnapper.ModeFor(null).ShouldBe(SectionSnap.Time);
        SectionSnapper.ModeFor(Grid(true) with { Beats = [], Downbeats = [] }).ShouldBe(SectionSnap.Time);
        SectionSnapper.ModeFor(Grid(true) with { Downbeats = [] }).ShouldBe(SectionSnap.Beat);
    }

    [Theory]
    [InlineData(1.0, 0.1)]
    [InlineData(1.2, 2.1)]
    [InlineData(9.0, 4.1)]
    [InlineData(-3.0, 0.1)]
    public void Reliable_SnapsToTheNearestDownbeat(double seconds, double expected) => SectionSnapper.Snap(seconds, Grid(true)).ShouldBe(expected);

    [Theory]
    [InlineData(0.8, 0.6)]
    [InlineData(0.9, 1.1)]
    [InlineData(4.0, 4.1)]
    public void Unreliable_SnapsToTheNearestBeat(double seconds, double expected) => SectionSnapper.Snap(seconds, Grid(false)).ShouldBe(expected);

    [Theory]
    [InlineData(12.34, 12.3)]
    [InlineData(12.35, 12.4)]
    [InlineData(-1, 0)]
    public void WithoutGrid_SnapsToATenthOfASecond(double seconds, double expected) => SectionSnapper.Snap(seconds, null).ShouldBe(expected, 1e-9);

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.1, 1)]
    [InlineData(2.0999999, 2)]
    [InlineData(3.0, 2)]
    [InlineData(10, 3)]
    public void BarAt_CountsDownbeatsUpToTheTime(double seconds, int bar) => SectionSnapper.BarAt(seconds, Grid(true)).ShouldBe(bar);

    [Fact]
    public void BarAt_WithoutDownbeats_IsNull()
    {
        SectionSnapper.BarAt(3, null).ShouldBeNull();
        SectionSnapper.BarAt(3, Grid(true) with { Downbeats = [] }).ShouldBeNull();
    }
}
