using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.Pages;

/// <summary>Sheet music actions of the track detail page (feature 008, US1).</summary>
public sealed partial class TrackDetail
{
    /// <summary>The open sheet music dialog's target, or null.</summary>
    private ScoreTarget? Scoring { get; set; }

    private void OpenScore(string channel) => Scoring = ScoreTarget.ForChannel(Track!, channel) with { Capo = ScoreCapo };

    /// <summary>Closes the dialog and reloads the track: the dialog may have queued the drums split.</summary>
    private async Task CloseScoreAsync()
    {
        Scoring = null;
        await RefreshAsync(Stopping);
        StartPolling();
    }
}
