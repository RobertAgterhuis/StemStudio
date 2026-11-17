using StemStudio.Contracts.Assets;
using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.Library;

/// <summary>Sheet music of a library item (feature 008, US5): the dialog, and the new item for the owner.</summary>
public sealed partial class AssetTable
{
    /// <summary>The open sheet music dialog's target, or null.</summary>
    private ScoreTarget? Scoring { get; set; }

    private void OpenScore(AssetDto asset) => Scoring = ScoreTarget.ForAsset(asset);

    private void CloseScore() => Scoring = null;

    private Task OnScoreSavedAsync(AssetDto asset) => OnReload.InvokeAsync();
}
