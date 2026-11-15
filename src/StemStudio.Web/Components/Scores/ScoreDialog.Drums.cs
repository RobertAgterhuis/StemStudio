using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Scores;

/// <summary>"Split the drums first" of the sheet music dialog (feature 008, US4, FR-009): starts the 002 detail phase.</summary>
public sealed partial class ScoreDialog
{
    /// <summary>True once the detail phase was queued from the dialog.</summary>
    private bool SplitStarted { get; set; }

    private async Task StartSplitAsync()
    {
        Busy = true;
        try
        {
            await Api.StartDetailAsync(Target.TrackId!.Value);
            (SplitStarted, Error) = (true, null);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }

        Busy = false;
    }
}
