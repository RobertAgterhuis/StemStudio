using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Insights;
using StemStudio.Web.Api;
using StemStudio.Web.Insights;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Stem quality in the mixer (020): the rating badges on the channel strips (US1, the last ready report, read once) and
/// the tip links of the track page (US2, Q2/Q3): <c>?mute=</c> mutes a stem in the live, unsaved mix before the engine
/// starts; <c>?expand=</c> expands a split parent once the engine is ready.
/// </summary>
public sealed partial class Mixer
{
    private QualityReportDto? _quality;

    [Inject]
    private Microsoft.Extensions.Options.IOptions<StemStudioWebOptions> WebOptions { get; set; } = default!;

    /// <summary>Channel to mute on open (<c>?mute=</c>, the "Mute" tip).</summary>
    [SupplyParameterFromQuery(Name = "mute")]
    public string? MuteChannel { get; set; }

    /// <summary>Split parent to expand on open (<c>?expand=</c>, the "Open detail stem" tip).</summary>
    [SupplyParameterFromQuery(Name = "expand")]
    public string? ExpandParent { get; set; }

    private static void ApplyMuteLink(MixerState? state, string? channel)
    {
        if (state is not null && channel is not null && state.AllChannels.Any(c => c.Stem == channel && !c.Mute))
        {
            state.ToggleMute(channel);
        }
    }

    private Task ApplyExpandLinkAsync() => ExpandParent is { } parent ? ExpandForViewAsync(parent) : Task.CompletedTask;

    /// <summary>The rating of a channel, or null when the track has no report or the channel is not in it.</summary>
    /// <param name="channel">ChannelId.</param>
    /// <returns>The stem view or null.</returns>
    private StemQualityViewDto? QualityOf(string channel) => QualityLayout.Find(_quality, channel);

    // Only a track that ever had a report (Track.quality) is asked, and only while the report is switched on
    // (StemStudio:QualityReport:Enabled); a failing read just shows no badges.
    private async Task LoadQualityAsync()
    {
        if (!WebOptions.Value.QualityReport.Enabled || _track?.Quality is null)
        {
            return;
        }

        try
        {
            _quality = await Api.GetQualityReportAsync(Id);
        }
        catch (ApiException)
        {
            _quality = null;
        }
    }
}
