using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Red stub (T045).</summary>
/// <param name="Channels">Channels.</param>
/// <param name="Metronome">Metronome.</param>
public sealed record PresetResult(IReadOnlyList<ChannelSettingsDto> Channels, MetronomeSettingsDto? Metronome);

/// <summary>Red stub (T045).</summary>
/// <param name="Id">Id.</param>
/// <param name="Label">Label.</param>
/// <param name="IsAvailable">Availability.</param>
/// <param name="Apply">Apply.</param>
public sealed record PracticePreset(string Id, string Label, Func<MixerState, bool> IsAvailable, Func<MixerState, PresetResult> Apply);

/// <summary>Red stub (T045).</summary>
public static class PracticePresets
{
    /// <summary>All.</summary>
    public static IReadOnlyList<PracticePreset> All { get; } = [];

    /// <summary>Get.</summary>
    /// <param name="id">Id.</param>
    /// <returns>Preset.</returns>
    public static PracticePreset Get(string id) => throw new NotImplementedException(id);

    /// <summary>Available.</summary>
    /// <param name="state">State.</param>
    /// <returns>Presets.</returns>
    public static IReadOnlyList<PracticePreset> Available(MixerState state) => All.Where(p => p.IsAvailable(state)).ToList();
}
