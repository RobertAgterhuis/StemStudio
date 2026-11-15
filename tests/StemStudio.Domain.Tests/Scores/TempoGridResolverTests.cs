using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Scores;

/// <summary>Feature 008 T032: the grid sent to the worker (data-model 4.3; FR-004, FR-007).</summary>
public sealed class TempoGridResolverTests
{
    private static readonly BeatGrid Reliable = new() { Bpm = 120, Beats = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0], Downbeats = [0.5, 2.5], BeatsPerBar = 4, Confidence = 0.9, Reliable = true };

    [Fact]
    public void AReliableGrid_IsSentAsItIs()
    {
        var grid = TempoGridResolver.Resolve(Reliable, null, 0).ShouldBeOfType<BeatGridTempo>();

        (grid.Bpm, grid.BeatsPerBar).ShouldBe((120, 4));
        grid.Beats.ShouldBe(Reliable.Beats);
        grid.Downbeats.ShouldBe(Reliable.Downbeats);
    }

    [Fact]
    public void ARange_ShiftsTheGridSoTheInputStartsAtZero()
    {
        var grid = TempoGridResolver.Resolve(Reliable, null, 1.2).ShouldBeOfType<BeatGridTempo>();

        grid.Beats.ShouldBe([-0.7, -0.2, 0.3, 0.8, 1.3, 1.8], tolerance: 1e-9);
        grid.Downbeats.ShouldBe([-0.7, 1.3], tolerance: 1e-9);
    }

    [Fact]
    public void AnAssetWithATrackRange_ShiftsByBothStarts()
    {
        var offset = TempoGridResolver.Offset(assetRangeStart: 30, scoreRangeStart: 5);

        var grid = TempoGridResolver.Resolve(Reliable, null, offset).ShouldBeOfType<BeatGridTempo>();

        (offset, grid.Beats[0]).ShouldBe((35, -34.5));
        TempoGridResolver.Offset(null, null).ShouldBe(0);
    }

    [Fact]
    public void AManualTempo_OverridesEveryGrid()
    {
        var grid = TempoGridResolver.Resolve(Reliable, new ManualTempo(96, 3, 0.4), 30).ShouldBeOfType<ManualTempoGrid>();

        grid.ShouldBe(new ManualTempoGrid(96, 3, 0.4));
        TempoGridResolver.Resolve(null, new ManualTempo(96, 3, 0.4), 0).ShouldBeOfType<ManualTempoGrid>();
    }

    [Fact]
    public void WithoutAReliableGridOrATempo_TheTempoIsRequired()
    {
        TempoGridResolver.Resolve(Reliable with { Reliable = false }, null, 0).ShouldBeNull();
        TempoGridResolver.Resolve(null, null, 0).ShouldBeNull();
    }

    [Fact]
    public void Status_TellsReliableUnreliableAndNone()
    {
        TempoGridResolver.StatusOf(Reliable).ShouldBe(GridStatus.Reliable);
        TempoGridResolver.StatusOf(Reliable with { Reliable = false }).ShouldBe(GridStatus.Unreliable);
        TempoGridResolver.StatusOf(null).ShouldBe(GridStatus.None);
        TempoGridResolver.StatusOf(Reliable with { Beats = [1.0] }).ShouldBe(GridStatus.None);
        TempoGridResolver.StatusOf(Reliable with { Downbeats = [] }).ShouldBe(GridStatus.None);
    }
}
