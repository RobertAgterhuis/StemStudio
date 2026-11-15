using StemStudio.Web.Help;
using static StemStudio.Web.Tests.Help.HelpSamples;

namespace StemStudio.Web.Tests.Help;

public sealed class HelpRendererTests
{
    private static string Html(string body, params string[] otherIds)
    {
        var files = otherIds.Select(id => TopicFile(id)).Append(TopicFile("x", content: Topic("x", body: body)));
        return new HelpCatalog(files).Render("x").Html;
    }

    [Fact]
    public void RawHtml_IsShownAsText_AndNoScriptSurvives()
    {
        var html = Html("Hello <b>bold</b>\n\n<script>alert(1)</script>\n\n<div onclick=\"x()\">block</div>");

        html.ShouldNotContain("<script");
        html.ShouldNotContain("<b>");
        html.ShouldNotContain("<div");
        html.ShouldContain("&lt;b&gt;bold&lt;/b&gt;");
        html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void ExternalImages_AreDropped_LocalHelpImagesKept()
    {
        var html = Html("![remote](https://evil.example/x.png) ![local](/_content/help/mixer.png)");

        html.ShouldNotContain("evil.example");
        html.ShouldContain("""<img src="/_content/help/mixer.png" alt="local" />""");
        html.ShouldContain("remote");
    }

    [Fact]
    public void HelpLinks_BecomeInAppHelpLinks()
    {
        var html = Html("See [Pan](help:mixer-pan).", "mixer-pan");

        html.ShouldContain("""href="/help/mixer-pan" data-help-link="mixer-pan">Pan</a>""");
    }

    [Fact]
    public void RouteLinks_BecomeAppLinks()
    {
        Html("Go to [tracks](route:/tracks).").ShouldContain("""<a href="/tracks">tracks</a>""");
    }

    [Fact]
    public void HttpsLinks_OpenInANewTab_WithNoopener()
    {
        Html("[Docs](https://example.org/a)").ShouldContain("""<a href="https://example.org/a" target="_blank" rel="noopener">Docs</a>""");
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](http://example.org)")]
    [InlineData("[x](data:text/html,hi)")]
    public void OtherLinkSchemes_AreUnwrappedToText(string markdown)
    {
        var html = Html(markdown);

        html.ShouldNotContain("<a ");
        html.ShouldContain("x");
    }

    [Fact]
    public void Headings_GetTopicScopedIds_AndAreListed()
    {
        var rendered = new HelpCatalog(Files).Render("mixer-screen");

        rendered.Headings.ShouldBe([new HelpHeading("help-mixer-screen-channels", "Channels", 2), new HelpHeading("help-mixer-screen-transport", "Transport", 2)]);
        rendered.Html.ShouldContain("""<h2 id="help-mixer-screen-channels">Channels</h2>""");
    }

    [Fact]
    public void PipeTables_AreRendered()
    {
        Html("| Key | Action |\n|---|---|\n| L | Loop |").ShouldContain("<table>");
    }

    [Fact]
    public void Render_IsCached_AndUnknownIdThrows()
    {
        var catalog = new HelpCatalog(Files);

        catalog.Render("stem").ShouldBeSameAs(catalog.Render("stem"));
        catalog.Render("stem").Topic.Id.ShouldBe("stem");
        Should.Throw<KeyNotFoundException>(() => catalog.Render("nope"));
    }

    [Fact]
    public void HelpLinks_CanBeParsedBackFromAUri()
    {
        HelpLinks.TopicFromUri("http://localhost/help/mixer-pan").ShouldBe("mixer-pan");
        HelpLinks.TopicFromUri("http://localhost/tracks").ShouldBeNull();
        HelpLinks.TopicFromUri("http://localhost/help/").ShouldBeNull();
        HelpLinks.Href("stem").ShouldBe("/help/stem");
    }
}
