using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.Pages;

/// <summary>Sheet music actions of the track detail page (feature 008, US1).</summary>
public sealed partial class TrackDetail
{
    /// <summary>The open sheet music dialog's target, or null.</summary>
    private ScoreTarget? Scoring { get; set; }

    private void OpenScore(string channel) => Scoring = ScoreTarget.ForChannel(Track!, channel);

    private void CloseScore() => Scoring = null;
}
