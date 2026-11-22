using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.InstrumentViews;

/// <summary>The mute and solo flags of one channel, stored before a minus-one mix so it can be undone.</summary>
/// <param name="Stem">The engine channel id.</param>
/// <param name="Mute">Muted.</param>
/// <param name="Solo">Soloed.</param>
public sealed record ChannelFlags(string Stem, bool Mute, bool Solo);

/// <summary>
/// Minus-one mixes per family (data-model 5.3, FR-032, FR-060): only existing <see cref="MixerState"/> operations
/// are used, so a family mix marks the mixer dirty like a manual change. Every method returns the settings of the
/// channels the engine must receive (detail channels included: their flags are kept while hidden).
/// </summary>
public static class FamilyMixService
{
    /// <summary>Mutes every channel of the family (expanded or not), unmutes and unsolos every other; volumes and pans stay.</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="family">The family the user plays.</param>
    /// <returns>The settings of every channel.</returns>
    public static IReadOnlyList<ChannelSettingsDto> MinusOne(MixerState state, InstrumentFamilyDto family)
    {
        foreach (var channel in state.AllChannels)
        {
            Set(state, channel, mute: InstrumentFamilies.Contains(state, channel, family), solo: false);
        }

        return Settle(state, state.AllChannels);
    }

    /// <summary>The current mute and solo flags of every channel.</summary>
    /// <param name="state">The mixer.</param>
    /// <returns>The flags in channel order.</returns>
    public static IReadOnlyList<ChannelFlags> Capture(MixerState state) =>
        state.AllChannels.Select(c => new ChannelFlags(c.Stem, c.Mute, c.Solo)).ToList();

    /// <summary>Restores captured flags; channels the track does not have and empty entries (stored in the browser) are ignored.</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="flags">Flags from <see cref="Capture"/>.</param>
    /// <returns>The settings of the restored channels.</returns>
    public static IReadOnlyList<ChannelSettingsDto> Restore(MixerState state, IReadOnlyList<ChannelFlags> flags)
    {
        var known = state.AllChannels.ToDictionary(c => c.Stem, StringComparer.Ordinal);
        var restored = new List<ChannelState>();
        foreach (var flag in flags.Where(f => f?.Stem is not null && known.ContainsKey(f.Stem)))
        {
            var channel = known[flag.Stem];
            Set(state, channel, flag.Mute, flag.Solo);
            restored.Add(channel);
        }

        return Settle(state, restored);
    }

    /// <summary>
    /// The guide piece (FR-033, Advanced): the drumless mix (every drum channel muted, solo off) with only
    /// <c>drums.&lt;piece&gt;</c> unmuted, so a second piece replaces the first. Nothing changes when the track has no such
    /// detail channel.
    /// </summary>
    /// <param name="state">The mixer.</param>
    /// <param name="piece">The drum piece to keep audible.</param>
    /// <returns>The settings of every channel, or empty when the piece does not exist.</returns>
    public static IReadOnlyList<ChannelSettingsDto> Guide(MixerState state, DrumPieceDto piece)
    {
        var id = $"drums.{piece.ToString().ToLowerInvariant()}";
        if (!state.AllChannels.Any(c => c.Stem == id))
        {
            return [];
        }

        MinusOne(state, InstrumentFamilyDto.Drums);
        Set(state, state.AllChannels.First(c => c.Stem == id), mute: false, solo: false);
        return Settle(state, state.AllChannels);
    }

    /// <summary>Sets the mute and solo flags through the toggle operations (dirty like a manual change).</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="mute">The new mute flag.</param>
    /// <param name="solo">The new solo flag.</param>
    internal static void Set(MixerState state, ChannelState channel, bool mute, bool solo)
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

    // Re-applies each channel's own volume: an existing operation that marks the mix dirty and returns the engine payload.
    internal static List<ChannelSettingsDto> Settle(MixerState state, IEnumerable<ChannelState> channels) =>
        channels.Select(c => state.SetVolume(c.Stem, c.Volume)).ToList();
}
