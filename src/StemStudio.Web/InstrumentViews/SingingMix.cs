using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.InstrumentViews;

/// <summary>The lead vocal muted for singing feedback, with its flags before (FR-044).</summary>
/// <param name="Flags">The lead channel's flags before.</param>
/// <param name="Channel">The engine payload.</param>
public sealed record LeadMute(ChannelFlags Flags, ChannelSettingsDto Channel);

/// <summary>Mixer and tolerance rules of singing feedback (016 FR-043, FR-044, data-model 3.4).</summary>
public static class SingingMix
{
    /// <summary>Smallest tolerance.</summary>
    public const int MinTolerance = 10;

    /// <summary>Largest tolerance.</summary>
    public const int MaxTolerance = 200;

    private const string LeadDetail = "vocals.lead";

    /// <summary>
    /// Mutes the lead vocal detail channel when it is on the audible level (vocals split and expanded), else the vocals
    /// stem: a collapsed group plays its parent, so muting the hidden lead would leave the lead audible (review H1).
    /// </summary>
    /// <param name="state">The mixer.</param>
    /// <returns>What was changed.</returns>
    public static LeadMute MuteLead(MixerState state)
    {
        var lead = state.AllChannels.FirstOrDefault(c => c.Stem == LeadDetail && !state.IsHidden(c)) ?? state.AllChannels.First(c => c.Stem == StemNames.Vocals);
        var flags = new ChannelFlags(lead.Stem, lead.Mute, lead.Solo);
        FamilyMixService.Set(state, lead, mute: true, solo: lead.Solo);
        return new LeadMute(flags, FamilyMixService.Settle(state, [lead])[0]);
    }

    /// <summary>Restores the lead channel.</summary>
    /// <param name="state">The mixer.</param>
    /// <param name="flags">The flags of <see cref="MuteLead"/>.</param>
    /// <returns>The engine payload.</returns>
    public static IReadOnlyList<ChannelSettingsDto> Restore(MixerState state, ChannelFlags flags) => FamilyMixService.Restore(state, [flags]);

    /// <summary>The default tolerance of a level: 100 (Beginner), 50 (Advanced), 25 (Expert) cents.</summary>
    /// <param name="experience">The level.</param>
    /// <returns>Cents.</returns>
    public static int DefaultTolerance(ExperienceContext experience) =>
        experience.IsVisible("singing.raw") ? 25 : experience.IsVisible("singing.tolerance") ? 50 : 100;

    /// <summary>The tolerance in use: a stored value (clamped) from Advanced on, else the level default.</summary>
    /// <param name="experience">The level.</param>
    /// <param name="stored">The stored value, or null.</param>
    /// <returns>Cents.</returns>
    public static int Tolerance(ExperienceContext experience, int? stored) =>
        experience.IsVisible("singing.tolerance") && stored is { } cents ? Math.Clamp(cents, MinTolerance, MaxTolerance) : DefaultTolerance(experience);
}
