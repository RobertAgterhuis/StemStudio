using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Mixer;

/// <summary>
/// Hooks for feature 015 (play-along recording) in the mixer state: the take channel's saved settings and a solo that
/// lives outside the stem channels. The 015 lane owns what they mean; 014 only stores and gates them.
/// </summary>
public sealed partial class MixerState
{
    /// <summary>
    /// A soloed channel that is not a stem channel (015's take strip, engine 2.5.0 gate rule): while true every stem
    /// without solo is silent, exactly as when a stem is soloed.
    /// </summary>
    public bool ExternalSolo { get; set; }

    /// <summary>The take channel settings saved with the mix (<c>MixSettings.take</c>, backend 1.14.0), or null.</summary>
    public TakeChannelSettingsDto? Take { get; private set; }

    /// <summary>Changes the take settings (marks the mix as changed).</summary>
    /// <param name="take">The new settings, or null to drop them.</param>
    public void SetTake(TakeChannelSettingsDto? take)
    {
        Take = take;
        IsDirty = true;
    }
}
