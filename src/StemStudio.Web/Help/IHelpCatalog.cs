namespace StemStudio.Web.Help;

/// <summary>
/// All help topics and tours, loaded once at start-up from the embedded resources (singleton,
/// contracts/help-content.md 1, data-model.md 4).
/// </summary>
public interface IHelpCatalog
{
    /// <summary>Every topic.</summary>
    IReadOnlyCollection<HelpTopic> Topics { get; }

    /// <summary>Distinct areas of the topics, in contract order.</summary>
    IReadOnlyList<string> Areas { get; }

    /// <summary>Every tour.</summary>
    IReadOnlyList<TourDefinition> Tours { get; }

    /// <summary>Finds a topic by id.</summary>
    /// <param name="id">Topic id.</param>
    /// <returns>The topic, or <see langword="null"/> when unknown.</returns>
    HelpTopic? Topic(string id);

    /// <summary>Finds the screen topic of a route.</summary>
    /// <param name="route">Route template, e.g. <c>/tracks/{id}/mixer</c>.</param>
    /// <returns>The screen topic, or <see langword="null"/> when none matches.</returns>
    HelpTopic? ScreenTopic(string route);

    /// <summary>Finds the topic that lists a marker in its anchors (literal ids before patterns).</summary>
    /// <param name="helpId">A <c>data-help-id</c> value.</param>
    /// <returns>The topic, or <see langword="null"/> when no anchor matches.</returns>
    HelpTopic? TopicForAnchor(string helpId);

    /// <summary>Renders a topic to sanitised HTML (cached).</summary>
    /// <param name="id">Topic id.</param>
    /// <returns>The rendered topic.</returns>
    /// <exception cref="KeyNotFoundException">The id is unknown.</exception>
    RenderedTopic Render(string id);
}
