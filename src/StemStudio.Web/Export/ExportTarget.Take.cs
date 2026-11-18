using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Export;

/// <summary>Which take export the dialog prepares (ui-test-ids 1.14.0 <c>export-source-take-only</c>, <c>export-source-take-mix</c>).</summary>
public enum TakeExportPreset
{
    /// <summary>The take alone, raw and trimmed to the take (mode raw, channels <c>["take"]</c>, takeId).</summary>
    TakeOnly,

    /// <summary>The take with the mix as heard (mode mix with <c>mix.take</c>, compare translated into mutes).</summary>
    TakeMix,
}

/// <summary>The take of a take export (feature 015, FR-015).</summary>
/// <param name="TakeId">The take.</param>
/// <param name="Name">Its name (dialog text).</param>
/// <param name="Ready">False while it is being saved (<c>export-take-saving</c>, export disabled).</param>
/// <param name="Channel">The take strip settings as heard (compare already applied).</param>
public sealed record TakeExport(Guid TakeId, string Name, bool Ready, TakeChannelSettingsDto Channel);

/// <summary>Feature 015 part of the export target: the take, when the dialog was opened from a take row.</summary>
public sealed partial record ExportTarget
{
    /// <summary>The take to export, or null for every 007 export.</summary>
    public TakeExport? Take { get; init; }

    /// <summary>
    /// A mixer export from a take row: the take joins the channel list (<c>export-channel-take</c>) and "What I hear"; the compare
    /// mode is translated into explicit mutes (My take: the part's stems muted; Original: the take muted; data-model 2.7).
    /// </summary>
    /// <param name="mix">The mixer target (<c>MixerState.ToExportTarget</c>).</param>
    /// <param name="take">The take.</param>
    /// <param name="mutedParts">Stems to mute for "My take" (the take's part and its detail channels), or empty.</param>
    /// <returns>The target.</returns>
    public static ExportTarget ForTake(ExportTarget mix, TakeExport take, IReadOnlySet<string> mutedParts) => mix with
    {
        Take = take,
        Mix = mix.Mix is { } m ? m with { Channels = [.. m.Channels.Select(c => Muted(c, mutedParts))], Take = take.Channel } : null,
        Channels = [.. mix.Channels, new ExportChannel(TakeChannel, "Take")],
        Audible = take.Channel.Mute ? mix.Audible : [.. mix.Audible.Where(a => !mutedParts.Contains(a)), TakeChannel],
    };

    /// <summary>The export channel id of the take.</summary>
    public const string TakeChannel = "take";

    private static ChannelSettingsDto Muted(ChannelSettingsDto channel, IReadOnlySet<string> muted) =>
        muted.Contains(channel.Stem) ? channel with { Mute = true } : channel;
}
