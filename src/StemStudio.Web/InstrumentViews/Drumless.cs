using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.InstrumentViews;

/// <summary>What "Drumless" changed, stored for the restore (data-model 8 <c>stemstudio.drumless.previous.&lt;trackId&gt;</c>).</summary>
/// <param name="Flags">Mute and solo flags of every channel before.</param>
/// <param name="MetronomeEnabled">The metronome was on.</param>
/// <param name="CountInBars">The count-in bars before (0 = off).</param>
public sealed record DrumlessSnapshot(IReadOnlyList<ChannelFlags> Flags, bool MetronomeEnabled, int CountInBars);

/// <summary>The engine payload of a Drumless change.</summary>
/// <param name="Channels">Channel settings to send.</param>
/// <param name="Metronome">Metronome settings to send.</param>
/// <param name="Practice">Practice settings to send, or null when the count-in did not change.</param>
public sealed record DrumlessChange(IReadOnlyList<ChannelSettingsDto> Channels, MetronomeSettingsDto Metronome, PracticeSettingsDto? Practice);

/// <summary>
/// "Drumless" (016 US3, FR-032, FR-033, data-model 5.3): minus-one drums, the metronome on and a one-bar count-in (012);
/// the restore brings back the stored flags and click settings. The guide piece keeps one drum detail channel audible.
/// </summary>
public static class Drumless
{
    private static readonly DrumPieceDto[] Pieces = Enum.GetValues<DrumPieceDto>();

    /// <summary>The settings Drumless will change.</summary>
    /// <param name="state">The mixer.</param>
    /// <returns>The snapshot.</returns>
    public static DrumlessSnapshot Capture(MixerState state) =>
        new(FamilyMixService.Capture(state), state.Metronome.Enabled, state.Practice.CountIn.Bars);

    /// <summary>Applies Drumless.</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="tempoKnown">True when a tempo is known, so a count-in is possible.</param>
    /// <returns>The engine payload.</returns>
    public static DrumlessChange Apply(MixerState state, bool tempoKnown)
    {
        var channels = FamilyMixService.MinusOne(state, InstrumentFamilyDto.Drums);
        var metronome = state.SetMetronome(state.Metronome with { Enabled = true });
        return new DrumlessChange(channels, metronome, tempoKnown ? state.SetCountInBars(1) : null);
    }

    /// <summary>Restores the settings of a snapshot.</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="snapshot">The snapshot of <see cref="Capture"/>.</param>
    /// <returns>The engine payload.</returns>
    public static DrumlessChange Restore(MixerState state, DrumlessSnapshot snapshot)
    {
        var channels = FamilyMixService.Restore(state, snapshot.Flags);
        var metronome = state.SetMetronome(state.Metronome with { Enabled = snapshot.MetronomeEnabled });
        return new DrumlessChange(channels, metronome, state.SetCountInBars(snapshot.CountInBars));
    }

    /// <summary>Keeps one piece audible on top of the drumless mix, or none (FR-033).</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="piece">The guide piece, or null to mute every drum channel again.</param>
    /// <returns>The channel settings to send.</returns>
    public static IReadOnlyList<ChannelSettingsDto> Guide(MixerState state, DrumPieceDto? piece) =>
        piece is { } guide && FamilyMixService.Guide(state, guide) is { Count: > 0 } channels
            ? channels
            : FamilyMixService.MinusOne(state, InstrumentFamilyDto.Drums);

    /// <summary>True when every channel of the drum family is muted, or only the drums parent and one guide piece sound.</summary>
    /// <param name="state">The mixer.</param>
    /// <returns>Whether Drumless is in effect.</returns>
    public static bool IsApplied(MixerState state)
    {
        var drums = state.AllChannels.Where(c => InstrumentFamilies.Contains(state, c, InstrumentFamilyDto.Drums)).ToList();
        var open = drums.Where(c => !c.Mute).Select(c => c.Stem).ToList();

        // A guide piece (decision 3) leaves the drums parent and one piece unmuted; Drumless is still in effect.
        return open.Count == 0 || (open.Count == 2 && open.Contains(StemNames.Drums) && open.Exists(s => s.StartsWith(StemNames.Drums + ".", StringComparison.Ordinal)));
    }

    /// <summary>The pieces that have a drum detail channel on this track (possible guide pieces).</summary>
    /// <param name="state">The mixer.</param>
    /// <returns>The pieces.</returns>
    public static IReadOnlyList<DrumPieceDto> GuidePieces(MixerState state)
    {
        var stems = state.AllChannels.Select(c => c.Stem).ToHashSet(StringComparer.Ordinal);
        return [.. Pieces.Where(p => stems.Contains(ChannelOf(p)))];
    }

    /// <summary>The detail channel of a piece.</summary>
    /// <param name="piece">The piece.</param>
    /// <returns>E.g. <c>drums.hihat</c>.</returns>
    public static string ChannelOf(DrumPieceDto piece) => $"drums.{piece.ToString().ToLowerInvariant()}";
}
