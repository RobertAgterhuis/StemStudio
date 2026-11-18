using System.Text.Json.Serialization;
using StemStudio.Contracts.ScoreFollow;

namespace StemStudio.Web.Audio;

/// <summary>Argument of <c>attachScoreFollower</c> (audio-engine 2.4.0 <c>ScoreViewMap</c>): the render subset of the backend score view.</summary>
/// <param name="Pages">Pages (the engine reads only their numbers).</param>
/// <param name="Bars">Bars with boxes in page fractions and times in track seconds.</param>
public sealed record ScoreViewMap(IReadOnlyList<ScoreViewPageDto> Pages, IReadOnlyList<ScoreBarDto> Bars)
{
    /// <summary>The map of a ready score view.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The map.</returns>
    public static ScoreViewMap Of(ScoreViewDto view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ScoreViewMap(view.Pages, view.Bars);
    }
}

/// <summary>Options of the score follower (audio-engine 2.4.0 <c>ScoreFollowOptions</c>).</summary>
/// <param name="Follow">Auto-scroll on/off (the highlight is always drawn).</param>
/// <param name="ReducedMotion">Jump instead of smooth scrolling; null = the browser setting.</param>
/// <param name="ScrollPauseMs">Pause after manual scrolling (0..60000); null = 4000.</param>
public sealed record ScoreFollowOptions(
    bool Follow,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ReducedMotion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ScrollPauseMs = null);
