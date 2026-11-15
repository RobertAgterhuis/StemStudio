using Bunit;
using NSubstitute;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Accessibility and help of sheet music (008 T075, WCAG 2.2 AA).</summary>
public sealed class ScoreAccessibilityTests : ScoreTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Fact]
    public void EveryDialogControl_IsNamed_AndEverySelectHasALabel()
    {
        var page = RenderDialog(ScoreData.Options("piano", StemStudio.Contracts.Scores.ScoreProfileDto.Piano),
            StemStudio.Web.Scores.ScoreTarget.ForChannel(TestData.Track(), "piano"));

        foreach (var select in page.FindAll("[data-testid=score-dialog] select"))
        {
            page.Find($"label[for={select.Id}]").TextContent.ShouldNotBeNullOrWhiteSpace(select.OuterHtml);
        }

        foreach (var control in page.FindAll("[data-testid=score-dialog] input, [data-testid=score-dialog] button"))
        {
            var named = control.GetAttribute("aria-label") is not null || control.TextContent.Trim().Length > 0 || control.Closest("label") is not null;
            named.ShouldBeTrue(control.OuterHtml);
        }

        page.Find("[data-testid=score-dialog]").GetAttribute("aria-labelledby").ShouldBe("score-title");
    }

    [Fact]
    public void PdfPreview_HasATitle_AndAnOpenInANewTabAlternative()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(ScoreData.Asset()));

        var page = Render<LibraryPage>();

        var row = page.Find(".ss-score-row");
        row.QuerySelector($"[data-testid=asset-preview-{ScoreData.ScoreId}]")!.GetAttribute("title").ShouldStartWith("Sheet music preview");
        var open = row.QuerySelector(".ss-pdf-open")!;
        open.GetAttribute("href").ShouldBe($"{ArtworkData.PublicBase}/api/assets/{ScoreData.ScoreId}/files/pdf?inline=true");
        (open.GetAttribute("target"), open.GetAttribute("rel")).ShouldBe(("_blank", "noopener"));
        open.TextContent.ShouldContain("Open the PDF");
        row.QuerySelector($"[data-testid=asset-download-pdf-{ScoreData.ScoreId}]").ShouldNotBeNull();
    }

    [Fact]
    public void Warnings_AreSpelledOutUnderAHeading()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(ScoreData.Asset()));

        var page = Render<LibraryPage>();

        var list = page.Find($"[data-testid=asset-score-warnings-{ScoreData.ScoreId}]");
        page.Find($"#{list.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Warnings");
    }

    [Fact]
    public void LibraryHelp_CoversScoreItems()
    {
        var library = Catalog.Topic("library")!;

        library.Markdown.ShouldContain("sheet music");
        library.Related.ShouldContain("sheet-music");
        Catalog.Topic("sheet-music")!.Markdown.ShouldContain("Open the PDF");
    }
}
