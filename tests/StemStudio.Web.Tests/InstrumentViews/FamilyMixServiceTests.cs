using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Minus-one mixes per family (T057, data-model 5.3, FR-032, FR-060, DELTA 6).</summary>
public sealed class FamilyMixServiceTests
{
    private readonly MixerState _state = new(FamilyTrack());

    /// <summary>Drums (kick, snare, rest), vocals (lead, backing, rest), other (strings, synth, rest) and guitar (acoustic) split.</summary>
    internal static TrackDto FamilyTrack() => TestData.Track() with
    {
        Detail = TestData.Phase(DetailPhaseStatusDto.Completed, 100, "Completed") with
        {
            Parents =
            [
                TestData.Split(StemNames.Drums, "kick", "snare", "rest"),
                TestData.Split(StemNames.Vocals, "lead", "backing", "rest"),
                TestData.Split(StemNames.Other, "strings", "synth", "rest"),
                TestData.Split(StemNames.Guitar, "acoustic-guitar", "keys", "rest"),
            ],
        },
    };

    private string[] Muted() => _state.AllChannels.Where(c => c.Mute).Select(c => c.Stem).Order(StringComparer.Ordinal).ToArray();

    [Theory]
    [InlineData(InstrumentFamilyDto.Drums, new[] { "drums", "drums.kick", "drums.rest", "drums.snare" })]
    [InlineData(InstrumentFamilyDto.Vocals, new[] { "vocals", "vocals.backing", "vocals.lead", "vocals.rest" })]
    [InlineData(InstrumentFamilyDto.Bass, new[] { "bass" })]
    [InlineData(InstrumentFamilyDto.Guitar, new[] { "guitar", "guitar.acoustic-guitar", "guitar.rest" })]
    [InlineData(InstrumentFamilyDto.Keys, new[] { "guitar.keys", "other.synth", "piano" })]
    [InlineData(InstrumentFamilyDto.Orchestral, new[] { "other.rest", "other.strings" })]
    public void MinusOne_MutesEveryFamilyChannel_CollapsedOrNot(InstrumentFamilyDto family, string[] expected)
    {
        var result = FamilyMixService.MinusOne(_state, family);

        Muted().ShouldBe(expected);
        result.Select(c => c.Stem).ShouldBe(_state.AllChannels.Select(c => c.Stem));
        result.Where(c => c.Mute).Select(c => c.Stem).Order(StringComparer.Ordinal).ShouldBe(expected);
        _state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void MinusOne_UnmutesAndUnsolosOthers_KeepsVolumesAndPans()
    {
        _state.ToggleMute(StemNames.Bass);
        _state.ToggleSolo("vocals.lead");
        _state.ToggleSolo(StemNames.Drums);
        _state.SetVolume("drums.kick", 0.3);
        _state.SetPan(StemNames.Piano, -0.4);

        FamilyMixService.MinusOne(_state, InstrumentFamilyDto.Drums);

        _state.AllChannels.ShouldAllBe(c => !c.Solo);
        _state.Channel(StemNames.Bass).Mute.ShouldBeFalse();
        _state.Channel("drums.kick").Volume.ShouldBe(0.3);
        _state.Channel(StemNames.Piano).Pan.ShouldBe(-0.4);
    }

    [Fact]
    public void MinusOne_HoldsAfterExpandAndCollapse()
    {
        FamilyMixService.MinusOne(_state, InstrumentFamilyDto.Drums);

        _state.SetExpanded(StemNames.Drums, true);
        _state.AllChannels.Where(c => c.Stem.StartsWith("drums", StringComparison.Ordinal)).ShouldAllBe(c => !_state.IsAudible(c));
        _state.SetExpanded(StemNames.Drums, false);
        _state.IsAudible(_state.Channel(StemNames.Drums)).ShouldBeFalse();
    }

    [Fact]
    public void Orchestral_CountsTheOtherStem_WhileItIsNotSplit()
    {
        var state = new MixerState(TestData.Track());

        FamilyMixService.MinusOne(state, InstrumentFamilyDto.Orchestral);

        state.AllChannels.Where(c => c.Mute).Select(c => c.Stem).ShouldBe([StemNames.Other]);
    }

    [Fact]
    public void Capture_AndRestore_ReturnThePreviousFlags()
    {
        _state.ToggleMute(StemNames.Bass);
        _state.ToggleSolo("drums.snare");
        var before = FamilyMixService.Capture(_state);
        FamilyMixService.MinusOne(_state, InstrumentFamilyDto.Drums);

        var result = FamilyMixService.Restore(_state, before);

        _state.AllChannels.Where(c => c.Mute).Select(c => c.Stem).ShouldBe([StemNames.Bass]);
        _state.AllChannels.Where(c => c.Solo).Select(c => c.Stem).ShouldBe(["drums.snare"]);
        result.Count.ShouldBe(_state.AllChannels.Count());
    }

    [Fact]
    public void Restore_IgnoresChannelsTheTrackDoesNotHave()
    {
        var result = FamilyMixService.Restore(_state, [new ChannelFlags("drums.toms", true, false), new ChannelFlags(StemNames.Bass, true, false)]);

        _state.Channel(StemNames.Bass).Mute.ShouldBeTrue();
        result.ShouldNotContain(c => c.Stem == "drums.toms");
    }

    [Fact]
    public void Guide_UnmutesOnlyThePiece_AndClearsSolo()
    {
        FamilyMixService.MinusOne(_state, InstrumentFamilyDto.Drums);
        _state.ToggleSolo(StemNames.Piano);

        var result = FamilyMixService.Guide(_state, DrumPieceDto.Snare);

        Muted().ShouldBe(["drums", "drums.kick", "drums.rest"]);
        _state.AllChannels.ShouldAllBe(c => !c.Solo);
        result.ShouldContain(c => c.Stem == "drums.snare" && !c.Mute);
    }

    [Fact]
    public void Guide_ASecondPiece_ReplacesTheFirst()
    {
        FamilyMixService.MinusOne(_state, InstrumentFamilyDto.Drums);
        FamilyMixService.Guide(_state, DrumPieceDto.Kick);

        FamilyMixService.Guide(_state, DrumPieceDto.Snare);

        Muted().ShouldBe(["drums", "drums.kick", "drums.rest"]);
    }

    [Fact]
    public void Guide_WithoutDrumlessFirst_StillMutesTheOtherDrums()
    {
        FamilyMixService.Guide(_state, DrumPieceDto.Kick);

        Muted().ShouldBe(["drums", "drums.rest", "drums.snare"]);
    }

    [Fact]
    public void Guide_WithoutThePieceOnTheTrack_ChangesNothing()
    {
        FamilyMixService.MinusOne(_state, InstrumentFamilyDto.Drums);

        FamilyMixService.Guide(_state, DrumPieceDto.Toms).ShouldBeEmpty();
        Muted().Length.ShouldBe(4);
    }

    [Theory]
    [InlineData("vocals", InstrumentFamilyDto.Vocals)]
    [InlineData("drums", InstrumentFamilyDto.Drums)]
    [InlineData("bass", InstrumentFamilyDto.Bass)]
    [InlineData("guitar", InstrumentFamilyDto.Guitar)]
    [InlineData("piano", InstrumentFamilyDto.Keys)]
    [InlineData("other", InstrumentFamilyDto.Orchestral)]
    [InlineData("drums.percussion", InstrumentFamilyDto.Drums)]
    [InlineData("other.electric-guitar", InstrumentFamilyDto.Guitar)]
    [InlineData("piano.brass", InstrumentFamilyDto.Orchestral)]
    [InlineData("piano.woodwinds", InstrumentFamilyDto.Orchestral)]
    [InlineData("piano.rest", InstrumentFamilyDto.Keys)]
    public void FamilyOfChannel_FollowsTheDetailName_AndRestFollowsTheParent(string channel, InstrumentFamilyDto family) =>
        InstrumentFamilies.FamilyOfChannel(channel).ShouldBe(family);

    [Fact]
    public void FamilyOfChannel_UnknownName_IsNull() => InstrumentFamilies.FamilyOfChannel("take").ShouldBeNull();

    [Fact]
    public void Families_HaveIdsLabelsAndViews_InPickerOrder()
    {
        InstrumentFamilies.All.Select(f => (InstrumentFamilies.Id(f), InstrumentFamilies.Label(f), InstrumentFamilies.View(f))).ShouldBe(
        [
            ("vocals", "Vocals", InstrumentViewDto.Singing),
            ("guitar", "Guitar", InstrumentViewDto.Tablature),
            ("bass", "Bass", InstrumentViewDto.Tablature),
            ("keys", "Keys", InstrumentViewDto.Chords),
            ("drums", "Drums", InstrumentViewDto.DrumTrainer),
            ("orchestral", "Strings / brass / woodwinds", InstrumentViewDto.Part),
        ]);
        InstrumentFamilies.Parse("keys").ShouldBe(InstrumentFamilyDto.Keys);
        InstrumentFamilies.Parse("nope").ShouldBeNull();
        InstrumentFamilies.Parse(null).ShouldBeNull();
    }
}
