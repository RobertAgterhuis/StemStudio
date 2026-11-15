using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StemStudio.Web.Tests.Shell;

/// <summary>The served document head (R-10, FR-009, SC-004): first paint before any stylesheet, cascade order, self-hosted font.</summary>
public sealed partial class AppHeadTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex(@"<(script|link)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadElement();

    private async Task<string> HeadAsync()
    {
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), Xunit.TestContext.Current.CancellationToken);
        var start = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        return html[start..html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase)];
    }

    private static int IndexOf(IReadOnlyList<string> elements, string fragment)
    {
        var index = elements.ToList().FindIndex(e => e.Contains(fragment, StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0, $"<head> has no element containing '{fragment}'");
        return index;
    }

    [Fact]
    public async Task FirstPaintScript_IsTheFirstScriptOrLink_AndSynchronous()
    {
        var elements = HeadElement().Matches(await HeadAsync()).Select(m => m.Value).ToList();

        elements[0].ShouldContain("js/first-paint");
        elements[0].ShouldNotContain("async");
        elements[0].ShouldNotContain("defer");
        elements[0].ShouldNotContain("type=\"module\"");
    }

    [Fact]
    public async Task Stylesheets_LoadInCascadeOrder_SoftwareBaseThenTokensMapBaseComponents()
    {
        var elements = HeadElement().Matches(await HeadAsync()).Select(m => m.Value).ToList();

        var order = new[] { "js/first-paint", "Radzen.Blazor/css/software", "css/tokens", "css/radzen-map", "css/base", "css/components/" }
            .Select(f => IndexOf(elements, f))
            .ToList();
        order.ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public async Task Archivo_IsPreloaded_WithCrossorigin()
    {
        var preload = HeadElement().Matches(await HeadAsync()).Select(m => m.Value).Single(e => e.Contains("rel=\"preload\"", StringComparison.Ordinal));

        preload.ShouldContain("fonts/archivo/");
        preload.ShouldContain("as=\"font\"");
        preload.ShouldContain("type=\"font/woff2\"");
        preload.ShouldContain("crossorigin");
    }

    [Fact]
    public async Task Head_HasNoExternalUrl_NoMaterialTheme_AndNoLegacyAppCss()
    {
        var head = await HeadAsync();

        head.ShouldNotContain("http://");
        head.ShouldNotContain("https://");
        head.ShouldNotContain("material");
        head.ShouldNotContain("app.css");
    }
}
