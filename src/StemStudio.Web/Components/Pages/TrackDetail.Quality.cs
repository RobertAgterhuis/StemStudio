using StemStudio.Contracts.Insights;
using StemStudio.Web.Insights;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// The tip actions of the stem quality card (020 US2, FR-005): split further starts the 002 detail separation like the
/// detail card; open detail stem and mute open the mixer (Q2/Q3: the stem is muted in the live, unsaved mix).
/// </summary>
public sealed partial class TrackDetail : ITipActions
{
    /// <inheritdoc />
    public Task RunAsync(TipActionDto action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind == TipActionKindDto.SplitDetail)
        {
            return StartDetailAsync();
        }

        Navigation.NavigateTo(TipLinks.Mixer(Id, action));
        return Task.CompletedTask;
    }
}
