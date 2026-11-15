using StemStudio.Web.Experience;

namespace StemStudio.Web.Preferences;

/// <summary>
/// Immutable user preferences mirroring the stored record (data-model.md 1.1, schema version 1).
/// </summary>
/// <param name="Level">Chosen level, or <see langword="null"/> when not chosen yet.</param>
/// <param name="Theme">Theme choice.</param>
/// <param name="Density">Density choice.</param>
/// <param name="Motion">Motion choice.</param>
/// <param name="Help">Help progress.</param>
public sealed record UserPreferences(
    ExperienceLevel? Level,
    ThemePreference Theme,
    DensityPreference Density,
    MotionPreference Motion,
    HelpProgress Help)
{
    /// <summary>Defaults of data-model.md 1.1 (level not chosen, system theme, auto density, system motion).</summary>
    public static UserPreferences Default { get; } = new(
        null, ThemePreference.System, DensityPreference.Auto, MotionPreference.System, HelpProgress.Empty);

    /// <summary>The level the UI renders: <see cref="Level"/>, or Beginner when not chosen.</summary>
    public ExperienceLevel EffectiveLevel => Level ?? ExperienceLevel.Beginner;

    /// <summary>
    /// The density the UI renders, never <see cref="DensityPreference.Auto"/>: Auto resolves to
    /// Compact at Expert and Comfortable otherwise.
    /// </summary>
    public DensityPreference EffectiveDensity => Density switch
    {
        DensityPreference.Auto when EffectiveLevel == ExperienceLevel.Expert => DensityPreference.Compact,
        DensityPreference.Auto => DensityPreference.Comfortable,
        _ => Density,
    };
}
