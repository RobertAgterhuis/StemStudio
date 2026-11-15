using StemStudio.Web.Preferences;

namespace StemStudio.Web.Experience;

/// <summary>Cascading level context. Red stub (T017).</summary>
/// <param name="Level">Level.</param>
/// <param name="Density">Density.</param>
public sealed record ExperienceContext(ExperienceLevel Level, DensityPreference Density)
{
    /// <summary>Default.</summary>
    public static ExperienceContext Default { get; } = new(ExperienceLevel.Beginner, DensityPreference.Comfortable);

    /// <summary>Visible.</summary>
    /// <param name="featureId">Id.</param>
    /// <returns>Visible.</returns>
    public bool IsVisible(string featureId) => throw new NotImplementedException(featureId);

    /// <summary>Is.</summary>
    /// <param name="level">Level.</param>
    /// <returns>Equal.</returns>
    public bool Is(ExperienceLevel level) => throw new NotImplementedException(level.ToString());
}
