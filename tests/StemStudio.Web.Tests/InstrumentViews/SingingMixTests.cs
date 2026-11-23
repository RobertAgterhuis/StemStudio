using StemStudio.Contracts;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The lead mute of singing feedback on the audible level (review H1, FR-044).</summary>
public sealed class SingingMixTests
{
    private readonly MixerState _state = new(DrumlessTests.DrumTrack());

    private bool AnyVocalAudible() =>
        _state.AllChannels.Where(c => c.Stem == StemNames.Vocals || c.Stem.StartsWith("vocals.", StringComparison.Ordinal)).Any(_state.IsAudible);

    [Fact]
    public void MuteLead_OnCollapsedVocals_MutesTheAudibleParent_AndRestoreBringsItBack()
    {
        _state.IsExpanded(StemNames.Vocals).ShouldBeFalse();

        var mute = SingingMix.MuteLead(_state);

        AnyVocalAudible().ShouldBeFalse("the singer must not hear the lead through the collapsed parent");
        mute.Channel.Stem.ShouldBe(StemNames.Vocals);
        SingingMix.Restore(_state, mute.Flags);
        _state.IsAudible(_state.AllChannels.First(c => c.Stem == StemNames.Vocals)).ShouldBeTrue();
    }

    [Fact]
    public void MuteLead_OnExpandedVocals_MutesOnlyTheLeadDetail()
    {
        _state.SetExpanded(StemNames.Vocals, true);

        var mute = SingingMix.MuteLead(_state);

        mute.Channel.Stem.ShouldBe("vocals.lead");
        _state.IsAudible(_state.AllChannels.First(c => c.Stem == "vocals.lead")).ShouldBeFalse();
        _state.IsAudible(_state.AllChannels.First(c => c.Stem == "vocals.rest")).ShouldBeTrue();
    }
}
