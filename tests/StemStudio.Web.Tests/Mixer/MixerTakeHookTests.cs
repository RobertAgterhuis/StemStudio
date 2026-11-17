using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Hooks for 015 in the mixer state (hook requests C and D): external solo and the take settings saved with the mix.</summary>
public sealed class MixerTakeHookTests
{
    private static readonly TakeChannelSettingsDto Take = new() { TakeId = Guid.NewGuid(), Volume = 0.8, Pan = -0.2, Mute = false, Solo = true };

    [Fact]
    public void ExternalSolo_SilencesEveryStemWithoutSolo()
    {
        var state = new MixerState(TestData.Track());
        var vocals = state.Channels[0];

        state.ExternalSolo = true;

        state.AnySolo.ShouldBeTrue();
        state.IsAudible(vocals).ShouldBeFalse();
        state.ToggleSolo(vocals.Stem);
        state.IsAudible(vocals).ShouldBeTrue();
        state.ExternalSolo = false;
        state.ToggleSolo(vocals.Stem);
        state.IsAudible(vocals).ShouldBeTrue();
    }

    [Fact]
    public void Take_IsSavedWithTheMix_AndRestoredOnLoad()
    {
        var state = new MixerState(TestData.Track());
        state.Take.ShouldBeNull();

        state.SetTake(Take);

        state.IsDirty.ShouldBeTrue();
        var settings = state.ToSettings("With take");
        settings.Take.ShouldBe(Take);
        var other = new MixerState(TestData.Track());
        other.ApplyMix(new MixDto(TestData.MixId, TestData.TrackId, settings, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        other.Take.ShouldBe(Take);
        other.IsDirty.ShouldBeFalse();
    }
}
