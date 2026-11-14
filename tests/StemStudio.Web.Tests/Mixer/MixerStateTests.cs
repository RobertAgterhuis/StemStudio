using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

public sealed class MixerStateTests
{
    private readonly MixerState _state = new(TestData.Track());

    [Fact]
    public void Defaults_FollowTheSpec()
    {
        _state.TrackId.ShouldBe(TestData.TrackId);
        _state.Duration.ShouldBe(213.04);
        _state.Channels.Select(c => c.Stem).ShouldBe(StemNames.SixStem);
        _state.Channels.ShouldAllBe(c => c.Volume == 0.8 && c.Pan == 0 && !c.Mute && !c.Solo);
        _state.MasterVolume.ShouldBe(1.0);
        _state.Metronome.ShouldBe(new MetronomeSettingsDto(true, 0.7, null, 0, 4));
        _state.Loop.ShouldBeNull();
        _state.IsDirty.ShouldBeFalse();
        _state.MixId.ShouldBeNull();
    }

    [Fact]
    public void Channels_FollowSixStemOrder_AndOnlyExistingStems()
    {
        var state = new MixerState(TestData.Track(model: "htdemucs") with { Stems = TestData.Stems("htdemucs").Reverse().ToList() });

        state.Channels.Select(c => c.Stem).ShouldBe(StemNames.FourStem);
    }

    [Fact]
    public void Metronome_StartsDisabled_WhenGridIsUnreliableOrMissing()
    {
        new MixerState(TestData.Track() with { BeatGrid = TestData.Grid(reliable: false, beatsPerBar: 3) }).Metronome
            .ShouldBe(new MetronomeSettingsDto(false, 0.7, null, 0, 3));
        var noGrid = new MixerState(TestData.Track() with { BeatGrid = null, DurationSeconds = null });
        noGrid.Metronome.ShouldBe(new MetronomeSettingsDto(false, 0.7, null, 0, 4));
        noGrid.Duration.ShouldBe(0);
    }

    [Fact]
    public void ChannelChanges_ClampMarkDirty_AndReturnEngineSettings()
    {
        _state.SetVolume(StemNames.Drums, 1.4).ShouldBe(new ChannelSettingsDto(StemNames.Drums, 1, 0, false, false));
        _state.IsDirty.ShouldBeTrue();
        _state.SetVolume(StemNames.Drums, -1).Volume.ShouldBe(0);
        _state.SetPan(StemNames.Drums, -3).Pan.ShouldBe(-1);
        _state.SetPan(StemNames.Drums, 0.25).Pan.ShouldBe(0.25);
        _state.ToggleMute(StemNames.Drums).Mute.ShouldBeTrue();
        _state.ToggleSolo(StemNames.Drums).Solo.ShouldBeTrue();
        _state.Channel(StemNames.Drums).ShouldBe(_state.Channels[1]);
    }

    [Fact]
    public void UnknownStem_Throws()
    {
        Should.Throw<ArgumentException>(() => _state.SetVolume("kazoo", 0.5));
    }

    [Fact]
    public void Audibility_MirrorsSoloAndMuteRules()
    {
        _state.Channels.ShouldAllBe(c => _state.IsAudible(c));

        _state.ToggleSolo(StemNames.Vocals);
        _state.ToggleSolo(StemNames.Bass);
        _state.ToggleMute(StemNames.Bass);

        _state.AnySolo.ShouldBeTrue();
        _state.IsAudible(_state.Channel(StemNames.Vocals)).ShouldBeTrue();
        _state.IsAudible(_state.Channel(StemNames.Bass)).ShouldBeFalse();
        _state.IsAudible(_state.Channel(StemNames.Drums)).ShouldBeFalse();
    }

    [Fact]
    public void Master_IsClamped_AndMarksDirty()
    {
        _state.SetMaster(2).ShouldBe(1);
        _state.SetMaster(0.4).ShouldBe(0.4);
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Duration_CanBeUpdatedFromTheEngine()
    {
        _state.Duration = 200;

        _state.Duration.ShouldBe(200);
        _state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Loop_ValidRegion_IsReturnedForTheEngine()
    {
        _state.SetLoopStart(10).ShouldBeNull();
        _state.LoopError.ShouldBeNull();

        _state.SetLoopEnd(20).ShouldBe(new LoopRegionDto(10, 20, false));
        _state.ToggleLoop().ShouldBe(new LoopRegionDto(10, 20, true));

        _state.LoopEnabled.ShouldBeTrue();
        _state.Loop.ShouldBe(new LoopRegionDto(10, 20, true));
        _state.IsDirty.ShouldBeTrue();
        _state.ToggleLoop().ShouldBe(new LoopRegionDto(10, 20, false));
        _state.LoopEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Loop_EndBeforeStart_IsRefused_WithReason()
    {
        _state.SetLoopStart(20);
        _state.SetLoopEnd(10).ShouldBeNull();

        _state.LoopError.ShouldBe(LoopValidator.EndBeforeStartMessage);
        _state.ToggleLoop().ShouldBeNull();
        _state.LoopEnabled.ShouldBeFalse();
        _state.Loop.ShouldBeNull();
    }

    [Fact]
    public void Loop_Toggle_WithoutMarkers_AsksForBoth()
    {
        _state.ToggleLoop().ShouldBeNull();

        _state.LoopError.ShouldBe(LoopValidator.MissingMarkersMessage);
    }

    [Fact]
    public void Loop_InvalidEditWhileEnabled_DisablesLooping()
    {
        _state.SetLoopStart(10);
        _state.SetLoopEnd(20);
        _state.ToggleLoop();

        _state.SetLoopEnd(300).ShouldBe(new LoopRegionDto(10, 20, false));

        _state.LoopEnabled.ShouldBeFalse();
        _state.LoopError!.ShouldContain("outside the track");
        _state.SetLoopEnd(30).ShouldBe(new LoopRegionDto(10, 30, false));
        _state.LoopError.ShouldBeNull();
    }

    [Fact]
    public void Loop_EngineRejection_DisablesAndShowsReason()
    {
        _state.SetLoopStart(10);
        _state.SetLoopEnd(20);
        _state.ToggleLoop();

        _state.RejectLoop("Invalid loop region: end > duration");

        _state.LoopEnabled.ShouldBeFalse();
        _state.LoopError.ShouldBe("Invalid loop region: end > duration");
    }

    [Theory]
    [InlineData(10, 20, 213, null)]
    [InlineData(0, 213, 213, null)]
    [InlineData(20, 20, 213, LoopValidator.EndBeforeStartMessage)]
    [InlineData(20, 10, 213, LoopValidator.EndBeforeStartMessage)]
    [InlineData(-1, 10, 213, "The loop is outside the track (0:00 - 3:33).")]
    [InlineData(10, 214, 213, "The loop is outside the track (0:00 - 3:33).")]
    public void LoopValidator_Messages(double start, double end, double duration, string? expected)
    {
        LoopValidator.Validate(start, end, duration).ShouldBe(expected);
    }

    [Fact]
    public void LoopValidator_UnknownDuration_OnlyChecksOrder()
    {
        LoopValidator.Validate(10, 5000, 0).ShouldBeNull();
    }
}
