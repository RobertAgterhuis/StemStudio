using StemStudio.Contracts.Scores;
using StemStudio.Web.Services;

namespace StemStudio.Web.Scores;

/// <summary>The choices of the sheet music dialog (feature 008, data-model 4.2) and the request they make.</summary>
/// <param name="options">The backend's options for the source.</param>
/// <param name="target">What is notated.</param>
public sealed class ScoreForm(ScoreOptionsDto options, ScoreTarget target)
{
    /// <summary>Message when no profile is chosen.</summary>
    public const string ProfileMessage = "Choose the instrument to notate.";

    /// <summary>The options the form was made from.</summary>
    public ScoreOptionsDto Options { get; } = options;

    /// <summary>The chosen profile; the suggestion at first.</summary>
    public ScoreProfileDto? Profile { get; private set; } = options.SuggestedProfile;

    /// <summary>"Simplified rhythm" (eighth notes); on by default for vocals, the profile default of data-model 2.1.</summary>
    public bool Simplified { get; set; } = options.SuggestedProfile == ScoreProfileDto.Vocals;

    /// <summary>Piano quality; the worker's default (accurate on a GPU, fast on the CPU) at first.</summary>
    public ScoreQualityDto Quality { get; set; } = options.DefaultQuality;

    /// <summary>True when the chosen profile has a quality choice (piano).</summary>
    public bool HasQuality => Profile == ScoreProfileDto.Piano;

    /// <summary>The quality note of the chosen profile.</summary>
    public string? Limit => Options.Limits.FirstOrDefault(l => l.Profile == Profile)?.Text;

    /// <summary>The expected time of the chosen profile, e.g. "about 31 seconds".</summary>
    public string? Estimate => EstimateOf(Profile) is { } seconds ? TimeText.About(seconds) : null;

    /// <summary>Chooses a profile and resets "simplified rhythm" to its default.</summary>
    /// <param name="profile">The profile, or null for none.</param>
    public void SetProfile(ScoreProfileDto? profile) => (Profile, Simplified) = (profile, profile == ScoreProfileDto.Vocals);

    /// <summary>Checks the choices.</summary>
    /// <returns>A readable problem, or null.</returns>
    public string? Validate() => Profile is null ? ProfileMessage : null;

    /// <summary>The request (call <see cref="Validate"/> first).</summary>
    /// <returns>The request.</returns>
    public CreateScoreRequest Request() => new()
    {
        Source = target.Source,
        Profile = Profile,
        Quality = HasQuality ? Quality : null,
        Resolution = Simplified ? ScoreResolutionDto.Eighth : ScoreResolutionDto.Sixteenth,
    };

    private int? EstimateOf(ScoreProfileDto? profile) =>
        Options.Estimates.FirstOrDefault(e => e.Profile == profile && (e.Quality is null || e.Quality == Quality))?.Seconds;
}
