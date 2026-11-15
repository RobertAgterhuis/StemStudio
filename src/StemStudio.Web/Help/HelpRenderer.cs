using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace StemStudio.Web.Help;

/// <summary>
/// Renders topic Markdown to sanitised HTML (contracts/help-content.md 2, 4.7): CommonMark plus pipe tables,
/// raw HTML disabled (shown as text), heading ids scoped to the topic, <c>help:</c> and <c>route:</c> links
/// rewritten to in-app links, <c>https:</c> links opened in a new tab, every other scheme and every image
/// outside <c>/_content/help/</c> reduced to its text.
/// </summary>
public static class HelpRenderer
{
    private const string LocalImagePrefix = "/_content/help/";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .DisableHtml()
        .Build();

    /// <summary>Renders a topic.</summary>
    /// <param name="topic">The topic.</param>
    /// <returns>The rendered topic.</returns>
    public static RenderedTopic Render(HelpTopic topic)
    {
        var document = Markdown.Parse(topic.Markdown, Pipeline);
        var headings = ScopeHeadings(document, topic.Id);
        foreach (var link in document.Descendants<LinkInline>().ToList())
        {
            Rewrite(link);
        }

        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
        {
            autolink.ReplaceBy(new LiteralInline(autolink.Url));
        }

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        return new RenderedTopic(topic, writer.ToString(), headings);
    }

    private static List<HelpHeading> ScopeHeadings(MarkdownDocument document, string topicId)
    {
        var headings = new List<HelpHeading>();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var attributes = heading.GetAttributes();
            attributes.Id = $"help-{topicId}-{attributes.Id}";
            headings.Add(new HelpHeading(attributes.Id, Text(heading.Inline), heading.Level));
        }

        return headings;
    }

    private static string Text(ContainerInline? inline) =>
        string.Concat(inline?.Descendants<LiteralInline>().Select(l => l.Content.ToString()) ?? []);

    private static void Rewrite(LinkInline link)
    {
        var url = link.Url ?? string.Empty;
        if (link.IsImage)
        {
            KeepOrUnwrap(link, url.StartsWith(LocalImagePrefix, StringComparison.Ordinal));
            return;
        }

        switch (url)
        {
            case var help when help.StartsWith(HelpLinks.Scheme, StringComparison.Ordinal):
                link.Url = HelpLinks.Href(help[HelpLinks.Scheme.Length..]);
                link.GetAttributes().AddProperty("data-help-link", help[HelpLinks.Scheme.Length..]);
                break;
            case var route when route.StartsWith("route:", StringComparison.Ordinal):
                link.Url = route["route:".Length..];
                break;
            case var web when web.StartsWith("https://", StringComparison.Ordinal):
                link.GetAttributes().AddProperty("target", "_blank");
                link.GetAttributes().AddProperty("rel", "noopener");
                break;
            default:
                KeepOrUnwrap(link, keep: false);
                break;
        }
    }

    private static void KeepOrUnwrap(LinkInline link, bool keep)
    {
        if (keep)
        {
            return;
        }

        foreach (var child in link.ToList())
        {
            child.Remove();
            link.InsertBefore(child);
        }

        link.Remove();
    }
}
