using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Detail groups of <see cref="MixerState"/> (FR-012, FR-013, FR-015).</summary>
public sealed class MixerStateDetailTests
{
    private const string Snare = "drums.snare";
    private const string Kick = "drums.kick";
    private readonly MixerState _state = new(TestData.DetailTrack());

    [Fact]
    public void Groups_OnlyForSplitParents_InDisplayOrder_WithRestLast()
    {
        _state.Groups.Select(g => g.Parent).ShouldBe([StemNames.Vocals, StemNames.Drums]);
        _state.Group(StemNames.Vocals).Stems.Select(s => s.Name).ShouldBe(["lead", "backing", "rest"]);
        _state.Group(StemNames.Drums).Stems.Select(s => s.Id).ShouldBe([Kick, Snare, "drums.rest"]);
        _state.Groups.ShouldAllBe(g => !g.Expanded);
        _state.GroupOf(StemNames.Bass).ShouldBeNull();
        _state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void DetailChannels_HaveDefaults_AndKeysForTestIds()
    {
        var snare = _state.Group(StemNames.Drums).Stems[1];

        snare.Parent.ShouldBe(StemNames.Drums);
        snare.Name.ShouldBe("snare");
        snare.Channel.Stem.ShouldBe(Snare);
        snare.Channel.Key.ShouldBe("drums-snare");
        snare.Channel.Label.ShouldBe("snare");
        snare.Channel.Volume.ShouldBe(0.8);
        _state.Channel(StemNames.Drums).Key.ShouldBe("drums");
        _state.Channel(StemNames.Drums).Label.ShouldBe("drums");
        _state.Channel(Snare).ShouldBeSameAs(snare.Channel);
    }

    [Fact]
    public void NoGroups_WithoutCompletedSplit_OrForMissingParentStemsAndUnknownNames()
    {
        new MixerState(TestData.Track()).Groups.ShouldBeEmpty();
        var odd = TestData.Track(model: "htdemucs") with
        {
            Detail = TestData.Phase(DetailPhaseStatusDto.Completed, 100) with
            {
                Parents = [TestData.Split(StemNames.Guitar, "brass", "rest"), TestData.Split(StemNames.Vocals, "kick", "lead", "rest"), TestData.Split(StemNames.Other)],
            },
        };

        var state = new MixerState(odd);

        state.Groups.ShouldHaveSingleItem().Stems.Select(s => s.Name).ShouldBe(["lead", "rest"]);
    }

    [Fact]
    public void SetExpanded_TogglesGroup_MarksDirty_AndUnknownParentThrows()
    {
        _state.SetExpanded(StemNames.Drums, true);

        _state.IsExpanded(StemNames.Drums).ShouldBeTrue();
        _state.IsExpanded(StemNames.Vocals).ShouldBeFalse();
        _state.IsExpanded(StemNames.Bass).ShouldBeFalse();
        _state.IsDirty.ShouldBeTrue();
        Should.Throw<ArgumentException>(() => _state.SetExpanded(StemNames.Bass, true));
    }

    [Fact]
    public void RejectExpanded_Collapses_WithoutMarkingDirty()
    {
        _state.Group(StemNames.Drums).Expanded = true;

        _state.RejectExpanded(StemNames.Drums);

        _state.IsExpanded(StemNames.Drums).ShouldBeFalse();
        _state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void HiddenChannels_CollapsedDetailAndExpandedParent()
    {
        _state.IsHidden(_state.Channel(Snare)).ShouldBeTrue();
        _state.IsHidden(_state.Channel(StemNames.Drums)).ShouldBeFalse();

        _state.SetExpanded(StemNames.Drums, true);

        _state.IsHidden(_state.Channel(Snare)).ShouldBeFalse();
        _state.IsHidden(_state.Channel(StemNames.Drums)).ShouldBeTrue();
        _state.IsHidden(_state.Channel(StemNames.Bass)).ShouldBeFalse();
        _state.IsAudible(_state.Channel(StemNames.Drums)).ShouldBeFalse();
        _state.IsAudible(_state.Channel(Kick)).ShouldBeTrue();
    }

    [Fact]
    public void SoloOnDetailStem_SilencesEverythingElse()
    {
        _state.SetExpanded(StemNames.Drums, true);
        _state.ToggleSolo(Snare).ShouldBe(new ChannelSettingsDto(Snare, 0.8, 0, false, true));

        _state.AnySolo.ShouldBeTrue();
        _state.IsAudible(_state.Channel(Snare)).ShouldBeTrue();
        _state.IsAudible(_state.Channel(Kick)).ShouldBeFalse();
        _state.IsAudible(_state.Channel(StemNames.Vocals)).ShouldBeFalse();
    }

    [Fact]
    public void SoloOnCollapsedParent_KeepsItsDetailChannelsHidden()
    {
        _state.ToggleSolo(StemNames.Drums);

        _state.IsAudible(_state.Channel(StemNames.Drums)).ShouldBeTrue();
        _state.IsAudible(_state.Channel(StemNames.Bass)).ShouldBeFalse();
        _state.IsAudible(_state.Channel(Kick)).ShouldBeFalse();
    }

    [Fact]
    public void SoloAndMuteOfHiddenChannels_AreIgnored()
    {
        _state.ToggleSolo(Snare);
        _state.AnySolo.ShouldBeFalse();
        _state.IsAudible(_state.Channel(StemNames.Vocals)).ShouldBeTrue();

        _state.SetExpanded(StemNames.Drums, true);
        _state.ToggleSolo(Snare);
        _state.ToggleSolo(StemNames.Drums);
        _state.ToggleMute(StemNames.Drums);

        _state.AnySolo.ShouldBeFalse();
        _state.IsAudible(_state.Channel(Kick)).ShouldBeTrue();
        _state.Channel(StemNames.Drums).Solo.ShouldBeTrue();
    }

    [Fact]
    public void DetailSettings_AreKeptAcrossCollapse()
    {
        _state.SetExpanded(StemNames.Drums, true);
        _state.ToggleMute(Snare);
        _state.SetVolume(Kick, 0.3);

        _state.SetExpanded(StemNames.Drums, false);
        _state.SetExpanded(StemNames.Drums, true);

        _state.Channel(Snare).Mute.ShouldBeTrue();
        _state.Channel(Kick).Volume.ShouldBe(0.3);
    }

    [Fact]
    public void ToSettings_WritesExpandedAndEveryDetailChannel_InDisplayOrder()
    {
        _state.SetExpanded(StemNames.Drums, true);
        _state.ToggleMute(Snare);
        _state.SetPan("vocals.lead", -0.5);

        var settings = _state.ToSettings("No snare");

        settings.Expanded.ShouldBe([StemNames.Drums]);
        settings.DetailChannels.ShouldBe(
        [
            new DetailChannelSettingsDto(StemNames.Vocals, "lead", 0.8, -0.5, false, false),
            new DetailChannelSettingsDto(StemNames.Vocals, "backing", 0.8, 0, false, false),
            new DetailChannelSettingsDto(StemNames.Vocals, "rest", 0.8, 0, false, false),
            new DetailChannelSettingsDto(StemNames.Drums, "kick", 0.8, 0, false, false),
            new DetailChannelSettingsDto(StemNames.Drums, "snare", 0.8, 0, true, false),
            new DetailChannelSettingsDto(StemNames.Drums, "rest", 0.8, 0, false, false),
        ]);
        settings.Channels.Select(c => c.Stem).ShouldBe(StemNames.SixStem);
    }

    [Fact]
    public void ApplyMix_RestoresExpansionAndDetailSettings_IgnoringUnknownEntries()
    {
        var settings = TestData.Settings("No snare") with
        {
            Expanded = [StemNames.Drums, StemNames.Piano],
            DetailChannels = [new(StemNames.Drums, "snare", 0.5, 0.25, true, false), new(StemNames.Piano, "keys", 1, 0, true, true)],
        };
        _state.SetVolume(Kick, 0.1);

        _state.ApplyMix(TestData.Mix("No snare") with { Settings = settings });

        _state.IsExpanded(StemNames.Drums).ShouldBeTrue();
        _state.IsExpanded(StemNames.Vocals).ShouldBeFalse();
        _state.Channel(Snare).ToDto().ShouldBe(new ChannelSettingsDto(Snare, 0.5, 0.25, true, false));
        _state.Channel(Kick).Volume.ShouldBe(0.8);
        _state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void ApplyMix_WithoutDetailSettings_CollapsesEverything_AndRestoresDefaults()
    {
        _state.SetExpanded(StemNames.Drums, true);
        _state.SetExpanded(StemNames.Vocals, true);
        _state.ToggleMute(Snare);

        _state.ApplyMix(TestData.Mix());

        _state.Groups.ShouldAllBe(g => !g.Expanded);
        _state.Channel(Snare).Mute.ShouldBeFalse();
        _state.Channel(StemNames.Guitar).Mute.ShouldBeTrue();
        _state.ToSettings("x").Expanded.ShouldBeEmpty();
    }
}
