using StemStudio.Web.Experience;

namespace StemStudio.Web.Help;

/// <summary>A guided tour loaded from <c>Help/tours/&lt;id&gt;.json</c> (contracts/help-content.md 3).</summary>
/// <param name="Id">Kebab-case id, equal to the file name.</param>
/// <param name="Version">Content version (&gt;= 1); a bump re-offers the tour.</param>
/// <param name="Title">Title, 1-40 characters.</param>
/// <param name="Route">Route template the tour is offered on, or <c>*</c> for any screen.</param>
/// <param name="Levels">Non-empty set of levels the offer is shown at.</param>
/// <param name="Steps">One to eight steps.</param>
public sealed record TourDefinition(
    string Id,
    int Version,
    string Title,
    string Route,
    IReadOnlyList<ExperienceLevel> Levels,
    IReadOnlyList<TourStep> Steps);
