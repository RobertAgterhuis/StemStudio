using StemStudio.Web.Preferences;

namespace StemStudio.Web.Experience;

/// <summary>
/// Cascading value of the app shell (data-model.md 2): the effective level and density. A new instance is
/// cascaded on every change, so subscribers re-render once.
/// </summary>
/// <param name="Level">Effective level.</param>
/// <param name="Density">Effective density (never Auto).</param>
public sealed record ExperienceContext(ExperienceLevel Level, DensityPreference Density)
{
    /// <summary>
    /// Used where no shell cascades a context (component tests, isolated renders): Advanced, the 001-003
    /// control set (ui-test-ids R7).
    /// </summary>
    public static ExperienceContext Default { get; } = new(ExperienceLevel.Advanced, DensityPreference.Comfortable);

    /// <summary>Whether a catalogue feature is shown at this level.</summary>
    /// <param name="featureId">Catalogue id.</param>
    /// <returns>True when visible.</returns>
    public bool IsVisible(string featureId) => FeatureCatalog.IsVisible(featureId, Level);

    /// <summary>Whether this is the given level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>True when equal.</returns>
    public bool Is(ExperienceLevel level) => Level == level;
}
