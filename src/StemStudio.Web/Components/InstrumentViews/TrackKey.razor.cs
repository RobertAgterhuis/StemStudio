using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>The key of the track summary; at Advanced the key regions with their start bars (FR-071).</summary>
public sealed partial class TrackKey
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The track (its key summary comes with it).</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    private IReadOnlyList<KeyViewRegionDto> Regions { get; set; } = [];

    private string Status => WireName.Of(Track.InstrumentViews?.Chords ?? AnalysisStatusDto.None);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (Track.Key is not null && (Experience ?? ExperienceContext.Default).IsVisible("key.details"))
        {
            try
            {
                Regions = (await Api.GetKeyAsync(Track.Id, 0, null)).Regions;
            }
            catch (ApiException)
            {
                Regions = [];
            }
        }
    }

    private static string KeyLine(TrackKeySummaryDto key) => KeyText.WithUncertainty(key.Display, key.Confidence);
}
