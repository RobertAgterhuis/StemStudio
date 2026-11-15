namespace StemStudio.Web.Help;

/// <summary>A topic rendered to sanitised HTML (Markdig with raw HTML disabled), cached after the first open.</summary>
/// <param name="Topic">The source topic.</param>
/// <param name="Html">Rendered HTML: no script, no raw HTML from the source, no external image.</param>
/// <param name="Headings">Headings of the body in document order (level 2 and lower).</param>
public sealed record RenderedTopic(HelpTopic Topic, string Html, IReadOnlyList<HelpHeading> Headings);
