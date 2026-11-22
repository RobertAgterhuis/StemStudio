using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>"Drumless" and the guide piece (T090, FR-032, FR-033, data-model 5.3).</summary>
public sealed class DrumlessTests
{
    private readonly MixerState _state = new(DrumTrack());

    /// <summary>Drums split into kick, snare, hi-hat and rest; vocals split into lead and rest.</summary>
    internal static TrackDto DrumTrack() => TestData.Track() with
    {
        Detail = TestData.Phase(DetailPhaseStatusDto.Completed, 100, "Completed") with
        {
            Parents =
            [
                TestData.Split(StemNames.Drums, "kick", "snare", "hihat", "rest"),
                TestData.Split(StemNames.Vocals, "lead", "rest"),
            ],
        },
    };

    private string[] Muted() => _state.AllChannels.Where(c => c.Mute).Select(c => c.Stem).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void Apply_MutesEveryDrumChannel_SwitchesTheMetronomeOn_AndAOneBarCountIn()
    {
        var change = Drumless.Apply(_state, tempoKnown: true);

        Muted().ShouldBe(["drums", "drums.hihat", "drums.kick", "drums.rest", "drums.snare"]);
        change.Channels.Count.ShouldBe(_state.AllChannels.Count());
        change.Metronome.Enabled.ShouldBeTrue();
        change.Practice!.CountIn.Bars.ShouldBe(1);
        _state.Metronome.Enabled.ShouldBeTrue();
        Drumless.IsApplied(_state).ShouldBeTrue();
    }

    [Fact]
    public void Apply_WithoutATempo_LeavesTheCountInAlone()
    {
        var change = Drumless.Apply(_state, tempoKnown: false);

        change.Practice.ShouldBeNull();
        _state.Practice.CountIn.Bars.ShouldBe(0);
    }

    [Fact]
    public void Restore_BringsBackTheFlags_TheMetronome_AndTheCountIn()
    {
        _state.ToggleMute(StemNames.Bass);
        _state.ToggleSolo("vocals.lead");
        var before = Drumless.Capture(_state);

        Drumless.Apply(_state, tempoKnown: true);
        var change = Drumless.Restore(_state, before);

        Muted().ShouldBe(["bass"]);
        _state.AllChannels.Single(c => c.Solo).Stem.ShouldBe("vocals.lead");
        change.Metronome.Enabled.ShouldBeFalse();
        change.Practice!.CountIn.Bars.ShouldBe(0);
        Drumless.IsApplied(_state).ShouldBeFalse();
    }

    [Fact]
    public void Guide_KeepsOnePieceAudible_AndSwitchingItMutesThePreviousOne()
    {
        Drumless.Apply(_state, tempoKnown: true);

        Drumless.Guide(_state, DrumPieceDto.Hihat);
        Muted().ShouldBe(["drums", "drums.kick", "drums.rest", "drums.snare"]);

        var change = Drumless.Guide(_state, DrumPieceDto.Snare);
        Muted().ShouldBe(["drums", "drums.hihat", "drums.kick", "drums.rest"]);
        change.Single(c => c.Stem == "drums.snare").Mute.ShouldBeFalse();

        Drumless.Guide(_state, null);
        Muted().ShouldBe(["drums", "drums.hihat", "drums.kick", "drums.rest", "drums.snare"]);
    }

    [Fact]
    public void GuidePieces_AreThePiecesWithADetailChannel()
    {
        Drumless.GuidePieces(_state).Order().ShouldBe([DrumPieceDto.Kick, DrumPieceDto.Snare, DrumPieceDto.Hihat]);
        Drumless.GuidePieces(new MixerState(TestData.Track())).ShouldBeEmpty();
    }

    [Fact]
    public void WithoutASplit_OnlyTheDrumsStemIsMuted()
    {
        var state = new MixerState(TestData.Track());

        Drumless.Apply(state, tempoKnown: true);

        state.AllChannels.Where(c => c.Mute).Select(c => c.Stem).ShouldBe(["drums"]);
        Drumless.IsApplied(state).ShouldBeTrue();
    }
}
