namespace StemStudio.Web.Experience;

/// <summary>Feature catalogue. Red stub (T017).</summary>
public static class FeatureCatalog
{
    /// <summary>All.</summary>
    public static IReadOnlyList<UiFeature> All { get; } = [];

    /// <summary>Get.</summary>
    /// <param name="id">Id.</param>
    /// <returns>Feature.</returns>
    public static UiFeature Get(string id) => throw new NotImplementedException(id);

    /// <summary>Visible.</summary>
    /// <param name="id">Id.</param>
    /// <param name="level">Level.</param>
    /// <returns>Visible.</returns>
    public static bool IsVisible(string id, ExperienceLevel level) => Get(id).Min <= level;
}
