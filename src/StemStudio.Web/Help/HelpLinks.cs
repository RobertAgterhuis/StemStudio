namespace StemStudio.Web.Help;

/// <summary>
/// In-app help links: topic Markdown writes <c>help:&lt;id&gt;</c>, rendered as <c>/help/&lt;id&gt;</c>. The shell
/// cancels navigation to such a URL and opens the topic in the help panel instead (no page has that route).
/// </summary>
public static class HelpLinks
{
    /// <summary>The Markdown link scheme.</summary>
    public const string Scheme = "help:";

    private const string Prefix = "/help/";

    /// <summary>The rendered link of a topic.</summary>
    /// <param name="topicId">The topic id.</param>
    /// <returns>For example <c>/help/mixer-pan</c>.</returns>
    public static string Href(string topicId) => Prefix + topicId;

    /// <summary>The topic of a help link URL.</summary>
    /// <param name="uri">An absolute or app-relative URL.</param>
    /// <returns>The topic id, or <see langword="null"/> when the URL is not a help link.</returns>
    public static string? TopicFromUri(string uri)
    {
        var path = Uri.TryCreate(uri, UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : uri;
        var id = path.StartsWith(Prefix, StringComparison.Ordinal) ? path[Prefix.Length..] : string.Empty;
        return TopicParser.KebabId().IsMatch(id) ? id : null;
    }
}
