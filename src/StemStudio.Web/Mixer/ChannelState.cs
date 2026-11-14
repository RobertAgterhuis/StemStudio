using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Mutable mixer state of one stem's channel strip.</summary>
/// <param name="stem">The stem name.</param>
public sealed class ChannelState(string stem)
{
    /// <summary>The stem name (e.g. <c>vocals</c>).</summary>
    public string Stem { get; } = stem;

    /// <summary>Fader gain 0..1 (default 0.8).</summary>
    public double Volume { get; set; } = ContractLimits.DefaultChannelVolume;

    /// <summary>Pan -1..1 (default 0).</summary>
    public double Pan { get; set; }

    /// <summary>Muted.</summary>
    public bool Mute { get; set; }

    /// <summary>Soloed.</summary>
    public bool Solo { get; set; }

    /// <summary>The channel as contract DTO (also the engine payload).</summary>
    /// <returns>The settings.</returns>
    public ChannelSettingsDto ToDto() => new(Stem, Volume, Pan, Mute, Solo);

    /// <summary>Copies a saved channel's values.</summary>
    /// <param name="settings">The saved settings, or null to restore defaults.</param>
    public void Apply(ChannelSettingsDto? settings)
    {
        Volume = settings?.Volume ?? ContractLimits.DefaultChannelVolume;
        Pan = settings?.Pan ?? 0;
        Mute = settings?.Mute ?? false;
        Solo = settings?.Solo ?? false;
    }
}
