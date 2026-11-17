using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Tests.Export;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>The sheet music confirmation names the folder path and links to the song folder (022 T036, FR-012).</summary>
public sealed class ScoreSavedFolderTests : ScoreTestContext
{
    [Fact]
    public async Task Saved_ShowsThePath_AndOpensTheItemInItsSongFolder()
    {
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>())
            .Returns(ScoreData.Asset(AssetStatusDto.Queued) with { Placement = LibraryData.Placement() });
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        ExportSavedFolderTests.ShouldBeTheFolderPath(page.Find("[data-testid=score-saved] [data-testid=score-saved-folder]"));
        page.Find("[data-testid=score-saved-open]").GetAttribute("href").ShouldBe($"library/{LibraryData.RickSongId}#asset-{ScoreData.ScoreId}");
    }

    [Fact]
    public async Task WithoutPlacement_KeepsThe007Link()
    {
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        page.FindAll("[data-testid=score-saved-folder]").ShouldBeEmpty();
        page.Find("[data-testid=score-saved-open]").GetAttribute("href").ShouldBe($"library?asset={ScoreData.ScoreId}");
    }
}
