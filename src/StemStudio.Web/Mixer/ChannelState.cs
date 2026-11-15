using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Mutable mixer state of one channel strip: a standard stem or a detail stem.</summary>
/// <param name="stem">The engine channel id: a stem name or <c>&lt;parent&gt;.&lt;detail&gt;</c> (engine contract 2.0.0).</param>
public sealed class ChannelState(string stem)
{
    /// <summary>The engine channel id (e.g. <c>vocals</c> or <c>drums.snare</c>).</summary>
    public string Stem { get; } = stem;

    /// <summary>The id used in <c>data-testid</c> and element ids (e.g. <c>drums-snare</c>, ui-test-ids 1.1.0).</summary>
    public string Key { get; } = stem.Replace('.', '-');

    /// <summary>The displayed name (e.g. <c>snare</c> for <c>drums.snare</c>).</summary>
    public string Label { get; } = stem[(stem.LastIndexOf('.') + 1)..];

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
