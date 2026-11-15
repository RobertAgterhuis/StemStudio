using System.Reflection;

namespace StemStudio.Web.Help;

/// <summary>A help source file. Red stub (T021).</summary>
/// <param name="Path">Path.</param>
/// <param name="Content">Content.</param>
public sealed record HelpFile(string Path, string Content);

/// <summary>Invalid help content. Red stub (T021).</summary>
/// <param name="message">Message.</param>
public sealed class HelpContentException(string message) : Exception(message);

/// <summary>Catalog. Red stub (T021).</summary>
/// <param name="files">Files.</param>
public sealed class HelpCatalog(IEnumerable<HelpFile> files) : IHelpCatalog
{
    /// <inheritdoc />
    public IReadOnlyCollection<HelpTopic> Topics { get; } = files.Any() ? throw new HelpContentException("stub") : [];

    /// <inheritdoc />
    public IReadOnlyList<string> Areas { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<TourDefinition> Tours { get; } = [];

    /// <summary>From assembly.</summary>
    /// <param name="assembly">Assembly.</param>
    /// <returns>Catalog.</returns>
    public static HelpCatalog FromAssembly(Assembly assembly) => new(EmbeddedFiles(assembly));

    /// <summary>Embedded.</summary>
    /// <param name="assembly">Assembly.</param>
    /// <returns>Files.</returns>
    public static IReadOnlyList<HelpFile> EmbeddedFiles(Assembly assembly) => [new(assembly.FullName!, string.Empty)];

    /// <summary>Path.</summary>
    /// <param name="resourceName">Name.</param>
    /// <returns>Path.</returns>
    public static string? ResourcePath(string resourceName) => resourceName;

    /// <inheritdoc />
    public HelpTopic? Topic(string id) => null;

    /// <inheritdoc />
    public HelpTopic? ScreenTopic(string route) => null;

    /// <inheritdoc />
    public HelpTopic? TopicForAnchor(string helpId) => null;

    /// <inheritdoc />
    public RenderedTopic Render(string id) => throw new KeyNotFoundException(id);
}

/// <summary>Search. Red stub (T021).</summary>
/// <param name="catalog">Catalog.</param>
public sealed class HelpSearch(IHelpCatalog catalog) : IHelpSearch
{
    /// <inheritdoc />
    public IReadOnlyList<HelpHit> Search(string query, int max = 10) => catalog.Topics.Count < 0 ? [] : throw new NotImplementedException(query);
}

/// <summary>Links. Red stub (T021).</summary>
public static class HelpLinks
{
    /// <summary>Topic.</summary>
    /// <param name="uri">Uri.</param>
    /// <returns>Id.</returns>
    public static string? TopicFromUri(string uri) => throw new NotImplementedException(uri);

    /// <summary>Href.</summary>
    /// <param name="topicId">Id.</param>
    /// <returns>Href.</returns>
    public static string Href(string topicId) => throw new NotImplementedException(topicId);
}
