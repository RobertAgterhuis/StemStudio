using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>What a preset changed, to be sent to the engine exactly like manual changes.</summary>
/// <param name="Channels">The settings of every standard channel after the preset.</param>
/// <param name="Metronome">New metronome settings, or <see langword="null"/> when the preset leaves the click alone.</param>
public sealed record PresetResult(IReadOnlyList<ChannelSettingsDto> Channels, MetronomeSettingsDto? Metronome);

/// <summary>
/// A practice preset (data-model.md 5, FR-016). <see cref="Apply"/> only uses existing <see cref="MixerState"/>
/// operations (mute, solo, volume, pan, metronome), so the mix becomes dirty like a manual change.
/// </summary>
/// <param name="Id">Id as in ui-test-ids <c>preset-&lt;presetId&gt;</c>.</param>
/// <param name="Label">Visible label (design-spec.md 8).</param>
/// <param name="IsAvailable">True when the stems the preset needs exist.</param>
/// <param name="Apply">Changes the state and returns what the engine must receive.</param>
public sealed record PracticePreset(string Id, string Label, Func<MixerState, bool> IsAvailable, Func<MixerState, PresetResult> Apply);

/// <summary>The practice presets: play along per instrument, karaoke, focus on vocals and reset.</summary>
public static class PracticePresets
{
    /// <summary>Every preset in display order.</summary>
    public static IReadOnlyList<PracticePreset> All { get; } =
    [
        PlayAlong(StemNames.Drums, click: true),
        PlayAlong(StemNames.Bass),
        PlayAlong(StemNames.Guitar),
        PlayAlong(StemNames.Piano),
        new("karaoke", "Karaoke", s => Has(s, StemNames.Vocals), s => Single(s, StemNames.Vocals, mute: true)),
        new("focus-vocals", "Focus: vocals", s => Has(s, StemNames.Vocals), s => Single(s, StemNames.Vocals, mute: false)),
        new("reset", "Reset mix", _ => true, Reset),
    ];

    /// <summary>Gets a preset by id.</summary>
    /// <param name="id">The preset id.</param>
    /// <returns>The preset.</returns>
    /// <exception cref="ArgumentException">Unknown id.</exception>
    public static PracticePreset Get(string id) =>
        All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"Unknown preset '{id}'.", nameof(id));

    /// <summary>The presets whose stems exist on the track.</summary>
    /// <param name="state">The mixer.</param>
    /// <returns>The available presets in display order.</returns>
    public static IReadOnlyList<PracticePreset> Available(MixerState state) => All.Where(p => p.IsAvailable(state)).ToList();

    private static PracticePreset PlayAlong(string stem, bool click = false) =>
        new($"play-along-{stem}", $"Play along: {stem}", s => Has(s, stem), s => PlayAlongApply(s, stem, click));

    private static bool Has(MixerState state, string stem) => state.Channels.Any(c => c.Stem == stem);

    private static PresetResult PlayAlongApply(MixerState state, string stem, bool click)
    {
        foreach (var channel in state.Channels)
        {
            Set(state, channel, mute: channel.Stem == stem, solo: false);
        }

        var metronome = click ? state.SetMetronome(state.Metronome with { Enabled = true }) : null;
        return new PresetResult(Settle(state), metronome);
    }

    private static PresetResult Single(MixerState state, string stem, bool mute)
    {
        var channel = state.Channel(stem);
        Set(state, channel, mute: mute || channel.Mute, solo: !mute || channel.Solo);
        return new PresetResult(Settle(state), null);
    }

    private static PresetResult Reset(MixerState state)
    {
        foreach (var channel in state.Channels)
        {
            Set(state, channel, mute: false, solo: false);
            state.SetVolume(channel.Stem, 1);
            state.SetPan(channel.Stem, 0);
        }

        return new PresetResult(Settle(state), state.SetMetronome(state.Metronome with { Enabled = false }));
    }

    private static void Set(MixerState state, ChannelState channel, bool mute, bool solo)
    {
        if (channel.Mute != mute)
        {
            state.ToggleMute(channel.Stem);
        }

        if (channel.Solo != solo)
        {
            state.ToggleSolo(channel.Stem);
        }
    }

    // Re-applies each channel's own volume: an existing operation that marks the mix dirty and returns the
    // settings the engine needs, so a preset is indistinguishable from manual changes.
    private static List<ChannelSettingsDto> Settle(MixerState state) =>
        state.Channels.Select(c => state.SetVolume(c.Stem, c.Volume)).ToList();
}
