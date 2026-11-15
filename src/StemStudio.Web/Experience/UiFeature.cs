namespace StemStudio.Web.Experience;

/// <summary>
/// One entry of the feature catalogue (design-spec.md 5, data-model.md 2): a gated UI feature is
/// visible iff <c>Min &lt;= level &lt;= (Max ?? Expert)</c>.
/// </summary>
/// <param name="Id">Catalogue id, pattern <c>^[a-z]+(\.[a-z0-9-]+)+$</c>, e.g. <c>mixer.pan</c>.</param>
/// <param name="Min">Lowest level that sees the feature.</param>
/// <param name="Max">Highest level that sees the feature, or <see langword="null"/> for Expert.</param>
/// <param name="TopicId">Help topic explaining the feature (contracts/help-content.md 4.5).</param>
public sealed record UiFeature(string Id, ExperienceLevel Min, ExperienceLevel? Max, string TopicId);
