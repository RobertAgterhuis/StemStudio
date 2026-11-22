using StemStudio.Contracts;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Export;
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

    /// <summary>The key override (a name of <c>ContractLimits.KeyNames</c>), or empty for the estimate.</summary>
    public string KeyOverride { get; set; } = string.Empty;

    /// <summary>True when the chosen profile has a key (every profile but drums).</summary>
    public bool HasKey => Profile is not (null or ScoreProfileDto.Drums);

    /// <summary>The manual tempo; required when the beat grid is unreliable or absent.</summary>
    public TempoInput Tempo { get; } = new(options.GridStatus != GridStatusDto.Reliable, target.SuggestedTempo);

    /// <summary>Part of the source to notate, at least 2 s long; empty = all of it.</summary>
    public RangeInput Range { get; } = new(null, options.DurationSeconds, ContractLimits.MinScoreRangeSeconds);

    /// <summary>True when the chosen profile has a quality choice (piano).</summary>
    public bool HasQuality => Profile == ScoreProfileDto.Piano;

    /// <summary>The quality note of the chosen profile.</summary>
    public string? Limit => Options.Limits.FirstOrDefault(l => l.Profile == Profile)?.Text;

    /// <summary>The expected time of the chosen profile, e.g. "about 31 seconds".</summary>
    public string? Estimate => EstimateOf(Profile) is { } seconds ? TimeText.About(seconds) : null;

    /// <summary>The select label of a piano quality with its expected time: "Fast: general model (about 31 seconds)".</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>The label.</returns>
    public string QualityLabel(ScoreQualityDto quality)
    {
        var name = quality == ScoreQualityDto.Accurate ? "Accurate: piano model" : "Fast: general model";
        var seconds = Options.Estimates.FirstOrDefault(e => e.Profile == ScoreProfileDto.Piano && e.Quality == quality)?.Seconds;
        return seconds is { } s ? $"{name} ({TimeText.About(s)})" : name;
    }

    /// <summary>Chooses a profile and resets "simplified rhythm" to its default.</summary>
    /// <param name="profile">The profile, or null for none.</param>
    public void SetProfile(ScoreProfileDto? profile)
    {
        (Profile, Simplified) = (profile, profile == ScoreProfileDto.Vocals);
        if (profile is ScoreProfileDto.Guitar or ScoreProfileDto.Bass)
        {
            Tab?.UseProfile(profile.Value);
        }
    }

    /// <summary>Tablature options (016 US2), or null when the backend offers none for the source.</summary>
    public TabChoice? Tab { get; } = NewTab(options, target);

    /// <summary>True when the tablature options apply (guitar and bass profiles, FR-020).</summary>
    public bool ShowsTab => Tab is not null && Profile is ScoreProfileDto.Guitar or ScoreProfileDto.Bass;

    /// <summary>"Include chords" (016 FR-017, Advanced): the user's choice.</summary>
    public bool IncludeChords { get; set; }

    /// <summary>True for the profiles that take chord symbols above the top staff (vocals, guitar, keys, piano).</summary>
    public bool ChordsAllowed => Profile is ScoreProfileDto.Vocals or ScoreProfileDto.Guitar or ScoreProfileDto.Keys or ScoreProfileDto.Piano;

    /// <summary>True when the backend offers chords (ready chords and an allowed profile).</summary>
    public bool ChordsAvailable => ChordsAllowed && Options.Chords?.Available == true;

    /// <summary>Checks the choices.</summary>
    /// <returns>A readable problem, or null.</returns>
    public string? Validate() => Profile is null ? ProfileMessage : Tempo.Validate() ?? Range.Validate();

    /// <summary>The request (call <see cref="Validate"/> first).</summary>
    /// <returns>The request.</returns>
    public CreateScoreRequest Request() => new()
    {
        Source = target.Source,
        Profile = Profile,
        Quality = HasQuality ? Quality : null,
        Resolution = Simplified ? ScoreResolutionDto.Eighth : ScoreResolutionDto.Sixteenth,
        KeyOverride = HasKey && KeyOverride.Length > 0 ? KeyOverride : null,
        Tempo = Tempo.ToDto(),
        Range = Range.ToDto(),
        IncludeChords = ChordsAvailable && IncludeChords,
        Tablature = ShowsTab ? Tab!.ToDto() : null,
    };

    private static TabChoice? NewTab(ScoreOptionsDto options, ScoreTarget target)
    {
        if (options.Tablature is not { Available: true } tab)
        {
            return null;
        }

        var choice = new TabChoice(tab, target.Capo);
        choice.UseProfile(options.SuggestedProfile == ScoreProfileDto.Bass ? ScoreProfileDto.Bass : ScoreProfileDto.Guitar);
        return choice;
    }

    private int? EstimateOf(ScoreProfileDto? profile) =>
        Options.Estimates.FirstOrDefault(e => e.Profile == profile && (e.Quality is null || e.Quality == Quality))?.Seconds;
}
