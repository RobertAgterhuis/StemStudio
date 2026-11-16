using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Saved-mix handling of <see cref="MixerState"/> (US4, SC-005).</summary>
public sealed partial class MixerState
{
    /// <summary>
    /// Restores every setting of a saved mix; stems without an entry get defaults and parents not listed as expanded
    /// are collapsed (mixes saved before 1.2.0 collapse everything, FR-015); mixes saved before 1.11.0 get the default
    /// practice settings (012 FR-014). Clears the dirty flag.
    /// </summary>
    /// <param name="mix">The mix (must belong to this track).</param>
    public void ApplyMix(MixDto mix)
    {
        var settings = mix.Settings;
        ApplyChannels(settings);
        MasterVolume = settings.MasterVolume;
        Metronome = settings.Metronome;
        Loop = settings.Loop;
        _loopStart = settings.Loop?.Start;
        _loopEnd = settings.Loop?.End;
        LoopError = null;
        ApplyPractice(settings.Practice);
        MarkSaved(mix);
    }

    private void ApplyChannels(MixSettingsDto settings)
    {
        foreach (var channel in Channels)
        {
            channel.Apply(settings.Channels.FirstOrDefault(c => c.Stem == channel.Stem));
        }

        foreach (var group in Groups)
        {
            group.Apply(settings);
        }
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
        new(name.Trim(), MasterVolume, Channels.Select(c => c.ToDto()).ToList(), Metronome, Loop)
        {
            Expanded = Groups.Where(g => g.Expanded).Select(g => g.Parent).ToList(),
            DetailChannels = Groups.SelectMany(g => g.Stems).Select(s => s.ToDto()).ToList(),
            Practice = Practice,
        };
}
