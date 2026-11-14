using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Metronome handling of <see cref="MixerState"/> (FR-014).</summary>
public sealed partial class MixerState
{
    /// <summary>Replaces the metronome settings.</summary>
    /// <param name="settings">Validated settings.</param>
    /// <returns>The settings for the engine.</returns>
    public MetronomeSettingsDto SetMetronome(MetronomeSettingsDto settings)
    {
        Metronome = settings;
        IsDirty = true;
        return settings;
    }

    /// <summary>Restores the detected grid: no BPM override, offset 0, detected beats per bar (on/off and volume stay).</summary>
    /// <returns>The settings for the engine.</returns>
    public MetronomeSettingsDto ResetMetronome() =>
        SetMetronome(Metronome with { BpmOverride = null, OffsetMs = 0, BeatsPerBar = DetectedBeatsPerBar });
}
