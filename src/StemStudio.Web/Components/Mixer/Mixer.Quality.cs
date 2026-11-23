using StemStudio.Contracts.Insights;
using StemStudio.Web.Api;
using StemStudio.Web.Insights;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Stem quality badges on the channel strips (020 US1, ui-test-ids quality-badge-*): the last ready report, read once.</summary>
public sealed partial class Mixer
{
    private QualityReportDto? _quality;

    /// <summary>The rating of a channel, or null when the track has no report or the channel is not in it.</summary>
    /// <param name="channel">ChannelId.</param>
    /// <returns>The stem view or null.</returns>
    private StemQualityViewDto? QualityOf(string channel) => QualityLayout.Find(_quality, channel);

    // Only a track that ever had a report (Track.quality) is asked; a failing read just shows no badges.
    private async Task LoadQualityAsync()
    {
        if (_track?.Quality is null)
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
