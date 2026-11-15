using StemStudio.Contracts;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Practice presets as MixerState operations (T045, FR-016, data-model.md 5).</summary>
public sealed class PracticePresetsTests
{
    private readonly MixerState _state = new(TestData.Track());

    private PresetResult Apply(string id) => PracticePresets.Get(id).Apply(_state);

    [Fact]
    public void All_AreTheDocumentedPresets_InOrder() =>
        PracticePresets.All.Select(p => (p.Id, p.Label)).ShouldBe(
        [
            ("play-along-drums", "Play along: drums"), ("play-along-bass", "Play along: bass"), ("play-along-guitar", "Play along: guitar"),
            ("play-along-piano", "Play along: piano"), ("karaoke", "Karaoke"), ("focus-vocals", "Focus: vocals"), ("reset", "Reset mix"),
        ]);

    [Fact]
    public void PlayAlongDrums_MutesDrumsOnly_ClearsSolo_AndTurnsTheClickOn()
    {
        _state.ToggleMute(StemNames.Vocals);
        _state.ToggleSolo(StemNames.Bass);
        _state.SetVolume(StemNames.Guitar, 0.3);
        _state.SetMetronome(_state.Metronome with { Enabled = false });

        var result = Apply("play-along-drums");

        _state.Channels.Where(c => c.Mute).Select(c => c.Stem).ShouldBe([StemNames.Drums]);
        _state.Channels.ShouldAllBe(c => !c.Solo);
        _state.Channel(StemNames.Guitar).Volume.ShouldBe(0.3);
        _state.Metronome.Enabled.ShouldBeTrue();
        result.Metronome.ShouldBe(_state.Metronome);
        result.Channels.Select(c => c.Stem).ShouldBe(StemNames.SixStem);
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void PlayAlongBass_LeavesTheClickAlone()
    {
        _state.SetMetronome(_state.Metronome with { Enabled = false });

        var result = Apply("play-along-bass");

        _state.Channel(StemNames.Bass).Mute.ShouldBeTrue();
        _state.Metronome.Enabled.ShouldBeFalse();
        result.Metronome.ShouldBeNull();
    }

    [Fact]
    public void Karaoke_MutesVocals()
    {
        Apply("karaoke");

        _state.Channel(StemNames.Vocals).Mute.ShouldBeTrue();
        _state.Channels.Count(c => c.Mute).ShouldBe(1);
    }

    [Fact]
    public void Karaoke_KeepsAnAlreadyMutedVocal_Muted()
    {
        _state.ToggleMute(StemNames.Vocals);

        Apply("karaoke");

        _state.Channel(StemNames.Vocals).Mute.ShouldBeTrue();
    }

    [Fact]
    public void FocusVocals_SolosVocals()
    {
        Apply("focus-vocals");
        Apply("focus-vocals");

        _state.Channel(StemNames.Vocals).Solo.ShouldBeTrue();
    }

    [Fact]
    public void Reset_RestoresVolumesPanMuteSolo_AndTurnsTheClickOff()
    {
        _state.SetVolume(StemNames.Drums, 0.2);
        _state.SetPan(StemNames.Drums, -0.5);
        _state.ToggleMute(StemNames.Drums);
        _state.ToggleSolo(StemNames.Vocals);
        _state.SetMetronome(_state.Metronome with { Enabled = true });

        var result = Apply("reset");

        _state.Channels.ShouldAllBe(c => c.Volume == 1 && c.Pan == 0 && !c.Mute && !c.Solo);
        _state.Metronome.Enabled.ShouldBeFalse();
        result.Metronome.ShouldNotBeNull();
    }

    [Fact]
    public void Availability_DependsOnThePresentStems()
    {
        var fourStem = new MixerState(TestData.Track(model: "htdemucs"));

        PracticePresets.Available(fourStem).Select(p => p.Id)
            .ShouldBe(["play-along-drums", "play-along-bass", "karaoke", "focus-vocals", "reset"]);
        PracticePresets.Available(_state).Count.ShouldBe(7);
    }

    [Fact]
    public void UnknownPreset_Throws() => Should.Throw<ArgumentException>(() => PracticePresets.Get("nope"));
}
