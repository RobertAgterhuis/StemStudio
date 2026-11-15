using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Saved-mix handling of <see cref="MixerState"/> (US4, SC-005).</summary>
public sealed partial class MixerState
{
    /// <summary>Restores every setting of a saved mix; stems without an entry get defaults. Clears the dirty flag.</summary>
    /// <param name="mix">The mix (must belong to this track).</param>
    public void ApplyMix(MixDto mix)
    {
        var settings = mix.Settings;
        foreach (var channel in Channels)
        {
            channel.Apply(settings.Channels.FirstOrDefault(c => c.Stem == channel.Stem));
        }

        MasterVolume = settings.MasterVolume;
        Metronome = settings.Metronome;
        Loop = settings.Loop;
        _loopStart = settings.Loop?.Start;
        _loopEnd = settings.Loop?.End;
        LoopError = null;
        MarkSaved(mix);
    }

    /// <summary>Records a successful save: the mix id and name, not dirty.</summary>
    /// <param name="mix">The saved mix.</param>
    public void MarkSaved(MixDto mix)
    {
        MixId = mix.Id;
        MixName = mix.Settings.Name;
        IsDirty = false;
    }

    /// <summary>Exports the current mixer as saveable settings.</summary>
    /// <param name="name">The mix name (trimmed).</param>
    /// <returns>The settings.</returns>
    public MixSettingsDto ToSettings(string name) =>
        new(name.Trim(), MasterVolume, Channels.Select(c => c.ToDto()).ToList(), Metronome, Loop);
}
