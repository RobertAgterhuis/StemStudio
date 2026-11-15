using Bunit;
using NSubstitute;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Piano quality and the piano model credit (008 T054, FR-008, FR-014).</summary>
public sealed class ScorePianoTests : ScoreTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    private static ScoreTarget Piano => ScoreTarget.ForChannel(TestData.Track(), "piano");

    [Fact]
    public async Task Piano_OffersBothQualities_WithTheirEstimates()
    {
        var page = RenderDialog(ScoreData.Options("piano", ScoreProfileDto.Piano), Piano);

        var quality = page.Find("[data-testid=score-quality]");
        quality.GetAttribute("data-help-id").ShouldBe("sheet-music-piano");
        quality.QuerySelectorAll("option").Select(o => (o.GetAttribute("value"), o.TextContent))
            .ShouldBe([("accurate", "Accurate: piano model (about 9 minutes)"), ("fast", "Fast: general model (about 31 seconds)")]);
        quality.GetAttribute("value").ShouldBe("fast");
        page.Find("[data-testid=score-estimate]").TextContent.ShouldBe("about 31 seconds");

        await quality.ChangeAsync(new() { Value = "accurate" });
        page.Find("[data-testid=score-estimate]").TextContent.ShouldBe("about 9 minutes");
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Quality.ShouldBe(ScoreQualityDto.Accurate);
    }

    [Fact]
    public void GpuWorker_PreselectsAccurate()
    {
        var options = ScoreData.Options("piano", ScoreProfileDto.Piano) with { DefaultQuality = ScoreQualityDto.Accurate };

        RenderDialog(options, Piano).Find("[data-testid=score-quality]").GetAttribute("value").ShouldBe("accurate");
    }

    [Fact]
    public void OtherProfiles_HaveNoQuality() =>
        RenderDialog(ScoreData.Options()).FindAll("[data-testid=score-quality]").ShouldBeEmpty();

    [Fact]
    public async Task Beginner_GetsTheWorkersDefaultQuality()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner, ScoreData.Options("piano", ScoreProfileDto.Piano), Piano);
        page.FindAll("[data-testid=score-quality]").ShouldBeEmpty();

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Quality.ShouldBe(ScoreQualityDto.Fast);
    }

    [Fact]
    public void PianoTopic_ExplainsQualityAndTheCredit()
    {
        var topic = Catalog.Topic("sheet-music-piano").ShouldNotBeNull();

        Catalog.TopicForAnchor("sheet-music-piano")!.Id.ShouldBe("sheet-music-piano");
        (topic.Feature, topic.Reviewed).ShouldBe(("score.quality", "008"));
        topic.Markdown.ShouldContain("CC BY 4.0");
        topic.Markdown.ShouldContain("CPU");
    }
}

/// <summary>The piano model credit on library items (008 T054).</summary>
public sealed class ScorePianoLibraryTests : PageTestContext
{
    [Fact]
    public void PianoItem_ShowsTheModelCredit()
    {
        var facts = ScoreData.Facts(ScoreProfileDto.Piano) with
        {
            Engine = ScoreEngineDto.PianoCrnn,
            Quality = ScoreQualityDto.Accurate,
            Attribution = ScoreData.Attribution,
        };
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(ScoreData.Asset(facts: facts)));

        var page = Render<LibraryPage>();

        page.Find($"[data-testid=asset-attribution-{ScoreData.ScoreId}]").TextContent.ShouldBe(ScoreData.Attribution);
    }
}
