using StemStudio.Contracts.Assets;
using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.Library;

/// <summary>Sheet music of a library item (feature 008, US5): the dialog, and the new item in the list.</summary>
public sealed partial class LibraryPage
{
    /// <summary>The open sheet music dialog's target, or null.</summary>
    private ScoreTarget? Scoring { get; set; }

    private void OpenScore(AssetDto asset) => Scoring = ScoreTarget.ForAsset(asset);

    private void CloseScore() => Scoring = null;

    /// <summary>Lists the new score item at once and polls it until it is ready.</summary>
    private async Task OnScoreSavedAsync(AssetDto asset)
    {
        await LoadAsync();
        StartPolling();
    }
}
